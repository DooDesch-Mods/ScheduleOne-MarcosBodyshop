using System;
using System.Collections.Generic;
using System.Globalization;
using MarcosBodyshop.Shop;

namespace MarcosBodyshop.Net
{
    /// <summary>
    /// The buyer pays from the bank balance, as vanilla Marco's repaint does (<c>MoneyManager.CreateOnlineTransaction</c>).
    /// The buyer charges itself before it sends the purchase, and the host answers with <c>settle</c> - kept when it
    /// applied the part, refunded to the balance when it refused. The ticket is a random id the buyer keeps until the answer arrives; every peer hears
    /// every answer, only the ticket holder acts on it.
    /// </summary>
    internal static class Payment
    {
        private static readonly Dictionary<int, int> _pending = new Dictionary<int, int>();
        private static readonly Random _rng = new Random();

        private const string Transaction = "Marco's Bodyshop";

        /// <summary>The bank balance in whole dollars.</summary>
        internal static int Balance()
        {
            try { return (int)Math.Floor(S1API.Money.Money.GetOnlineBalance()); }
            catch (Exception e) { Core.Log.Error("[Pay] reading the bank balance failed: " + e.Message); return 0; }
        }

        /// <summary>Takes the price from the bank balance and opens a ticket; 0 when the balance is short (nothing taken).</summary>
        internal static int Charge(int amount, string what)
        {
            if (amount < 0) amount = 0;
            if (Balance() < amount) { Core.Log.Msg("[Pay] " + what + " refused - short of $" + amount + "."); return 0; }
            int ticket;
            do { ticket = _rng.Next(1, int.MaxValue); } while (_pending.ContainsKey(ticket));
            if (amount > 0)
            {
                try { S1API.Money.Money.CreateOnlineTransaction(Transaction, -amount, 1f, what); }
                catch (Exception e) { Core.Log.Error("[Pay] taking $" + amount + " for " + what + " failed - not bought: " + e.Message); return 0; }
            }
            _pending[ticket] = amount;
            Core.Log.Msg("[Pay] " + what + ": paid $" + amount + " from the bank balance (ticket " + ticket + ").");
            return ticket;
        }

        /// <summary>The host's answer. Ignored unless this player holds the ticket.</summary>
        internal static void Settle(int ticket, bool kept, string garage, int vehicleId, string category, string entry, string reason)
        {
            if (!_pending.TryGetValue(ticket, out int amount)) return;
            _pending.Remove(ticket);
            if (!kept)
            {
                if (amount > 0)
                {
                    try { S1API.Money.Money.CreateOnlineTransaction(Transaction + " refund", amount, 1f, string.Empty); }
                    catch (Exception e) { Core.Log.Error("[Pay] refunding $" + amount + " (ticket " + ticket + ") failed: " + e.Message); }
                }
                Core.Log.Warning("[Pay] host refused ticket " + ticket + " (" + reason + ") - refunded $" + amount + ".");
            }
            else Core.Log.Msg("[Pay] host confirmed ticket " + ticket + ".");
            Session.OnSettled(kept, garage, vehicleId, category, entry, amount, reason);
        }

        /// <summary>Scene change: a ticket still open never got its answer. The money stays spent, and the log says so.</summary>
        internal static void Reset()
        {
            if (_pending.Count > 0)
                Core.Log.Warning("[Pay] " + _pending.Count + " purchase(s) never answered by the host before the scene changed.");
            _pending.Clear();
        }

        internal static string Ticket(int t) => t.ToString(CultureInfo.InvariantCulture);
    }
}
