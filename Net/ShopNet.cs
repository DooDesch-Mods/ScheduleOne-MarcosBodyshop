using System;
using System.Linq;
using System.Reflection;
using Il2CppFishNet;
using Il2CppScheduleOne.DevUtilities;
using Il2CppScheduleOne.PlayerScripts;
using Il2CppScheduleOne.Vehicles;
using GameQuestManager = Il2CppScheduleOne.Quests.QuestManager;
using EQuestState = Il2CppScheduleOne.Quests.EQuestState;

namespace MarcosBodyshop.Net
{
    /// <summary>
    /// The bodyshop's co-op channel. Messages ride two vanilla quest RPCs with a private "MRCO|" guid the vanilla
    /// handlers never see: client -> host on <c>SendQuestState</c> (a ServerRpc), host -> everyone on
    /// <c>ReceiveQuestState</c> (an ObserversRpc). The same carrier Chassis and RVRepairVan use.
    ///
    /// On a listen server the host also gets a client's message through the ReceiveQuestState relay, so both
    /// prefixes route by role: the host treats every message as a request, a client as state. Single player is a
    /// host with no clients; every send is handled locally.
    /// </summary>
    internal static class ShopNet
    {
        private const string Prefix = "MRCO|";

        internal static bool IsHost
        {
            get
            {
                try { return InstanceFinder.IsServer; }
                catch (Exception e) { Core.Log.Warning("[Net] could not read the server role: " + e.Message); return false; }
            }
        }

        /// <summary>A request to the host. On the host it is handled right here.</summary>
        internal static void ToHost(string op, params string[] args)
        {
            var m = new Msg(op, args);
            if (IsHost) { Dispatch(m, true); return; }
            try
            {
                var qm = NetworkSingleton<GameQuestManager>.Instance;
                if (qm == null) { Core.Log.Warning("[Net] no QuestManager - request " + m + " dropped."); return; }
                qm.SendQuestState(m.Encode(), EQuestState.Inactive);
                Core.Log.Msg("[Net] -> host " + m);
            }
            catch (Exception e) { Core.Log.Error("[Net] sending " + m + " to the host failed: " + e.Message); }
        }

        /// <summary>Host only: state for every client. The host handles it itself as well.</summary>
        internal static void ToAll(string op, params string[] args)
        {
            var m = new Msg(op, args);
            if (!IsHost) { Core.Log.Warning("[Net] a client tried to broadcast " + m + " - only the host does."); return; }
            Dispatch(m, false);
            try
            {
                var qm = NetworkSingleton<GameQuestManager>.Instance;
                if (qm == null) { Core.Log.Warning("[Net] no QuestManager - broadcast " + m + " reaches no client."); return; }
                qm.ReceiveQuestState(null, m.Encode(), EQuestState.Inactive);
            }
            catch (Exception e) { Core.Log.Error("[Net] broadcasting " + m + " failed: " + e.Message); }
        }

        private static void Dispatch(Msg m, bool request)
        {
            try
            {
                if (request) Sync.OnRequest(m);
                else Sync.OnState(m);
            }
            catch (Exception e) { Core.Log.Error("[Net] handling " + (request ? "request " : "state ") + m + " failed: " + e); }
        }

        internal static void Apply(HarmonyLib.Harmony h)
        {
            Patch(h, "RpcLogic___SendQuestState", nameof(SendPrefix));
            Patch(h, "RpcLogic___ReceiveQuestState", nameof(ReceivePrefix));
        }

        private static void Patch(HarmonyLib.Harmony h, string namePrefix, string prefix)
        {
            try
            {
                MethodInfo target = typeof(GameQuestManager)
                    .GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                    .FirstOrDefault(m => m.Name.StartsWith(namePrefix, StringComparison.Ordinal));
                if (target == null) { Core.Log.Error("[Net] " + namePrefix + " not found - the bodyshop works in single player only."); return; }
                h.Patch(target, prefix: new HarmonyLib.HarmonyMethod(typeof(ShopNet).GetMethod(prefix, BindingFlags.NonPublic | BindingFlags.Static)));
                Core.Log.Msg("[Net] carrier patched: " + target.Name + ".");
            }
            catch (Exception e) { Core.Log.Error("[Net] patching " + namePrefix + " failed - the bodyshop works in single player only: " + e.Message); }
        }

        // Host: RpcLogic___SendQuestState(string guid, EQuestState state).
        private static bool SendPrefix(string __0)
        {
            if (!Msg.TryDecode(__0, out Msg m)) return true;
            // On the sending client this is the local run of its own request; only the host acts on it.
            if (IsHost && m.IsRequest) Dispatch(m, true);
            return false;
        }

        // Everyone: RpcLogic___ReceiveQuestState(NetworkConnection conn, string guid, EQuestState state).
        private static bool ReceivePrefix(string __1)
        {
            if (!Msg.TryDecode(__1, out Msg m)) return true;
            // The host already handled its own broadcast in ToAll; what reaches it here is a relayed client request.
            if (IsHost)
            {
                if (m.IsRequest) Dispatch(m, true);
                return false;
            }
            if (!m.IsRequest) Dispatch(m, false);
            return false;
        }

        // ---- ids -------------------------------------------------------------------------------------------------

        internal static int IdOf(LandVehicle v)
        {
            if (v == null) return -1;
            var nob = v.NetworkObject;
            return nob != null ? nob.ObjectId : -1;
        }

        internal static LandVehicle VehicleById(int id)
        {
            if (id < 0) return null;
            var vm = NetworkSingleton<VehicleManager>.Instance;
            if (vm == null || vm.AllVehicles == null) return null;
            foreach (var v in vm.AllVehicles) if (v != null && IdOf(v) == id) return v;
            return null;
        }

        internal static string MyCode()
        {
            var local = Player.Local;
            return local != null ? local.PlayerCode ?? string.Empty : string.Empty;
        }

        internal static Player PlayerByCode(string code)
        {
            if (string.IsNullOrEmpty(code)) return null;
            var list = Player.PlayerList;
            if (list == null) return null;
            foreach (var p in list) if (p != null && p.PlayerCode == code) return p;
            return null;
        }

        internal static string NameOf(string code)
        {
            var p = PlayerByCode(code);
            return p != null ? p.PlayerName : "Another player";
        }

        /// <summary>One message: an op and its string arguments. Requests start with "req.", state does not.</summary>
        internal sealed class Msg
        {
            internal readonly string Op;
            internal readonly string[] Args;

            internal Msg(string op, string[] args) { Op = op; Args = args ?? new string[0]; }

            internal bool IsRequest => Op.StartsWith("req.", StringComparison.Ordinal);

            internal string Arg(int i) => i < Args.Length ? Args[i] : string.Empty;

            internal string Encode()
            {
                foreach (string a in Args)
                    if (a != null && a.IndexOf('|') >= 0) throw new ArgumentException("a bodyshop message argument may not contain '|': " + a);
                return Prefix + Op + (Args.Length > 0 ? "|" + string.Join("|", Args) : string.Empty);
            }

            internal static bool TryDecode(string s, out Msg m)
            {
                m = null;
                if (s == null || !s.StartsWith(Prefix, StringComparison.Ordinal)) return false;
                string[] parts = s.Substring(Prefix.Length).Split('|');
                m = new Msg(parts[0], parts.Skip(1).ToArray());
                return true;
            }

            public override string ToString() => Op + "(" + string.Join(", ", Args) + ")";
        }
    }
}
