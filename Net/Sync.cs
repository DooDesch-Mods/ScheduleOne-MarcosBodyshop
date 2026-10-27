using System;
using System.Collections.Generic;
using System.Globalization;
using DooDesch.Localization;
using MarcosBodyshop.Api;
using MarcosBodyshop.Shop;
using UnityEngine;

namespace MarcosBodyshop.Net
{
    /// <summary>
    /// What the host decides and what everyone hears: the garage lock (one player per garage) and purchases (the
    /// buyer paid, the host applies, the buyer hears whether the money is kept).
    ///
    /// Requests: <c>req.open garage player vehicle</c>, <c>req.close garage player</c>,
    /// <c>req.buy garage vehicle category entry ticket paid player</c>, <c>req.nitro vehicle 1|0</c>.
    /// State: <c>open garage player vehicle ok reason</c>, <c>lock garage holder</c>,
    /// <c>settle ticket kept garage vehicle category entry reason</c>, <c>nitro vehicle 1|0</c>.
    /// </summary>
    internal static class Sync
    {
        // Host: who uses which garage.
        private static readonly Dictionary<string, string> _holders = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private static float _nextCheck;

        internal static void OnRequest(ShopNet.Msg m)
        {
            switch (m.Op)
            {
                case "req.open": HostOpen(m.Arg(0), m.Arg(1), I(m.Arg(2))); break;
                case "req.close": HostClose(m.Arg(0), m.Arg(1), "closed the menu"); break;
                case "req.buy": HostBuy(m.Arg(0), I(m.Arg(1)), m.Arg(2), m.Arg(3), I(m.Arg(4)), I(m.Arg(5)), m.Arg(6)); break;
                case "req.nitro": Builtin.Nitro.HostNitro(I(m.Arg(0)), m.Arg(1) == "1"); break;
                default: Core.Log.Warning("[Net] unknown request " + m + " - ignored."); break;
            }
        }

        internal static void OnState(ShopNet.Msg m)
        {
            switch (m.Op)
            {
                case "open":
                    if (m.Arg(1) != ShopNet.MyCode()) return;
                    Session.OnOpenAnswer(m.Arg(0), I(m.Arg(2)), m.Arg(3) == "1", m.Arg(4));
                    break;
                case "lock":
                    Core.Log.Msg("[Net] garage '" + m.Arg(0) + "' is " + (m.Arg(1).Length == 0 ? "free." : "used by " + ShopNet.NameOf(m.Arg(1)) + "."));
                    break;
                case "settle":
                    Payment.Settle(I(m.Arg(0)), m.Arg(1) == "1", m.Arg(2), I(m.Arg(3)), m.Arg(4), m.Arg(5), m.Arg(6));
                    break;
                case "nitro": Builtin.Flames.OnNitro(I(m.Arg(0)), m.Arg(1) == "1"); break;
                default: Core.Log.Warning("[Net] unknown state " + m + " - ignored."); break;
            }
        }

        // ---- host: the lock ---------------------------------------------------------------------------------------

        private static void HostOpen(string garage, string player, int vehicleId)
        {
            if (Registry.Garage(garage) == null) { Answer(garage, player, vehicleId, false, "There is no garage here."); return; }
            if (_holders.TryGetValue(garage, out string holder) && holder != player && ShopNet.PlayerByCode(holder) != null)
            {
                Core.Log.Msg("[Net] " + ShopNet.NameOf(player) + " asked for garage '" + garage + "', which " + ShopNet.NameOf(holder) + " is using - refused.");
                Answer(garage, player, vehicleId, false, ShopNet.NameOf(holder) + IsUsing);
                return;
            }
            _holders[garage] = player;
            Core.Log.Msg("[Net] garage '" + garage + "' given to " + ShopNet.NameOf(player) + ".");
            ShopNet.ToAll("lock", garage, player);
            Answer(garage, player, vehicleId, true, "");
        }

        private static void Answer(string garage, string player, int vehicleId, bool ok, string reason)
            => ShopNet.ToAll("open", garage, player, vehicleId.ToString(CultureInfo.InvariantCulture), ok ? "1" : "0", reason);

        private static void HostClose(string garage, string player, string why)
        {
            if (!_holders.TryGetValue(garage, out string holder) || holder != player) return;
            _holders.Remove(garage);
            Core.Log.Msg("[Net] garage '" + garage + "' free again (" + ShopNet.NameOf(player) + " " + why + ").");
            ShopNet.ToAll("lock", garage, "");
        }

        /// <summary>Host, twice a second: a holder who left the game frees the garage.</summary>
        internal static void Tick()
        {
            if (_holders.Count == 0 || Time.unscaledTime < _nextCheck || !ShopNet.IsHost) return;
            _nextCheck = Time.unscaledTime + 0.5f;
            foreach (var kv in new List<KeyValuePair<string, string>>(_holders))
                if (ShopNet.PlayerByCode(kv.Value) == null) HostClose(kv.Key, kv.Value, "left the game");
        }

        // ---- host: purchases --------------------------------------------------------------------------------------

        private static void HostBuy(string garage, int vehicleId, string categoryId, string entryId, int ticket, int paid, string player)
        {
            string why = HostTryBuy(garage, vehicleId, categoryId, entryId, paid, player, out var v);
            string g = garage, cat = categoryId, ent = entryId;
            ShopNet.ToAll("settle", ticket.ToString(CultureInfo.InvariantCulture), why == null ? "1" : "0", g,
                          vehicleId.ToString(CultureInfo.InvariantCulture), cat, ent, Clean(why ?? ""));
            if (why == null) Core.Log.Msg("[Shop] " + ShopNet.NameOf(player) + " bought " + cat + "/" + ent + " for " + paid + " on net " + vehicleId + ".");
            else Core.Log.Warning("[Shop] " + ShopNet.NameOf(player) + ": " + cat + "/" + ent + " on net " + vehicleId + " refused, " + why);
        }

        private static string HostTryBuy(string garage, int vehicleId, string categoryId, string entryId, int paid, string player, out Il2CppScheduleOne.Vehicles.LandVehicle v)
        {
            v = ShopNet.VehicleById(vehicleId);
            if (v == null) return "The vehicle is gone.";
            if (!_holders.TryGetValue(garage, out string holder) || holder != player) return "The garage is not yours right now.";
            var c = Registry.Category(categoryId);
            if (c == null) return "This category is not installed on the host.";
            string state = Registry.StateOf(c, v);
            if (state == "") return "This category does not fit the vehicle.";
            if (state != null) return state;
            var e = Registry.Entry(c, v, entryId);
            if (e == null) return "This part is not available on the host.";
            int price = Prices.Of(e, v);
            if (paid < price) return PriceChanged + price.ToString(CultureInfo.InvariantCulture) + ".";
            string blocked = Prices.BlockedOf(e, v);
            if (blocked != null) return blocked;
            if (e.Apply == null) return "This part cannot be fitted.";
            try { return e.Apply(v); }
            catch (Exception ex) { Core.Log.Error("[Shop] Apply of " + categoryId + "/" + entryId + " failed: " + ex); return "The garage could not fit it."; }
        }

        private static string Clean(string s) => s.Replace('|', '/');

        private const string PriceChanged = "The price changed to ";
        private const string IsUsing = " is using the garage.";

        /// <summary>
        /// A refusal the host sent, in the receiver's language. The host answers in English (its own refusals and the
        /// built-in entries' Apply reasons), so every player reads it in their own language; the two lines with a value
        /// in them are taken apart first. A reason a mod wrote in its own words matches no key and stays as it came.
        /// </summary>
        internal static string Heard(string reason)
        {
            if (string.IsNullOrEmpty(reason)) return reason ?? "";
            if (reason.StartsWith(PriceChanged, StringComparison.Ordinal) && reason.EndsWith(".", StringComparison.Ordinal))
                return L10n.T("The price changed to {0}.", reason.Substring(PriceChanged.Length, reason.Length - PriceChanged.Length - 1));
            if (reason.EndsWith(IsUsing, StringComparison.Ordinal) && reason.Length > IsUsing.Length)
                return L10n.T("{0} is using the garage.", reason.Substring(0, reason.Length - IsUsing.Length));
            return L10n.T(reason);
        }

        private static int I(string s) => int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out int n) ? n : -1;

        internal static void Reset()
        {
            _holders.Clear();
            _nextCheck = 0f;
        }
    }
}
