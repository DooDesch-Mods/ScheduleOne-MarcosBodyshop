#if DEBUG
using System;
using System.Globalization;
using HarmonyLib;
using Il2CppScheduleOne.DevUtilities;
using Il2CppScheduleOne.PlayerScripts;
using MarcosBodyshop.Api;
using MarcosBodyshop.Shop;
using UnityEngine;

namespace MarcosBodyshop.Debug
{
    /// <summary>
    /// Dev console word 'bodyshop' (Debug builds only):
    /// <list type="bullet">
    /// <item><c>bodyshop open [garage]</c> / <c>close</c> - the menu, as Marco's dialogue opens it.</item>
    /// <item><c>bodyshop box</c> - the open vehicle's box and the renderers with the largest share of it.</item>
    /// <item><c>bodyshop bays [show|hide]</c> - every garage's bays and the vehicle in each; show/hide outlines every bay on the ground.</item>
    /// <item><c>bodyshop key up|down|enter|back</c> - a key to the page.</item>
    /// <item><c>bodyshop list cat</c>, <c>pick cat/entry</c>, <c>buy cat/entry</c> - the page's calls, without the page.</item>
    /// <item><c>bodyshop cam ex ey ez tx ty tz</c> - hold the camera there (to measure shots).</item>
    /// <item><c>bodyshop cash N</c> - add N to the local player's cash.</item>
    /// </list>
    /// </summary>
    internal static class Commands
    {
        private static void Say(string s) => Core.Log.Msg("[Console] " + s);

        internal static void Run(string[] p)
        {
            string sub = p.Length > 1 ? p[1].ToLowerInvariant() : "";
            string a = p.Length > 2 ? p[2] : "";
            switch (sub)
            {
                case "open":
                    Say(BodyshopApi.Open(a.Length > 0 ? a : BodyshopApi.MarcoGarage, out string why) ? "asked the host for the garage" : "not opened: " + why);
                    break;
                case "close": BodyshopApi.Close(); break;
                case "bays":
                    if (a == "show" || a == "hide") { Marking.ShowAll = a == "show"; Marking.Refresh(); Say("bay outlines " + (Marking.ShowAll ? "on" : "off")); }
                    Bays();
                    break;
                case "box": foreach (var line in Session.BoxReport()) Say(line); break;
                case "key": Session.Key(a.Length > 0 ? a : "down"); Say("key '" + a + "' sent"); break;
                case "list": Say("list: " + Session.List(a)); break;
                case "pick": Say("pick: " + Session.Pick(a)); break;
                case "buy": { string r = Session.Buy(a); Say("buy: " + (r.Length == 0 ? "sent to the host" : r)); break; }
                case "cam": Cam(p); break;
                case "cash":
                    if (!int.TryParse(a, NumberStyles.Integer, CultureInfo.InvariantCulture, out int n)) { Say("cash needs a whole number"); break; }
                    S1API.Money.Money.ChangeCashBalance(n, true, false);
                    Say("cash +" + n);
                    break;
                default:
                    Say("bodyshop open [garage] | close | bays [show|hide] | key up|down|enter|back | list cat | pick cat/entry | buy cat/entry | cam ex ey ez tx ty tz | cash N");
                    break;
            }
        }

        private static void Bays()
        {
            foreach (var g in Registry.Garages)
            {
                var v = Registry.VehicleIn(g.Id, out string why, out var bay);
                Say(g.Id + " (" + g.Name + "): " + (v != null ? v.VehicleName + " in bay '" + bay.Id + "' at " + v.transform.position.ToString("F1") : why));
                foreach (var b in g.Bays)
                {
                    var inside = b.FindVehicle != null ? b.FindVehicle() : Registry.InBox(b);
                    Say("  bay '" + b.Id + "': " + (inside != null ? inside.VehicleName + (inside.IsOccupied ? " (occupied)" : "") : "empty"));
                }
            }
        }

        private static void Cam(string[] p)
        {
            var f = new float[6];
            if (p.Length < 8) { Say("cam needs six numbers"); return; }
            for (int i = 0; i < 6; i++)
                if (!float.TryParse(p[i + 2], NumberStyles.Float, CultureInfo.InvariantCulture, out f[i])) { Say("cam: '" + p[i + 2] + "' is not a number"); return; }
            var pc = PlayerSingleton<PlayerCamera>.Instance;
            if (pc == null) { Say("cam: no player camera"); return; }
            var eye = new Vector3(f[0], f[1], f[2]);
            pc.OverrideTransform(eye, Quaternion.LookRotation(new Vector3(f[3], f[4], f[5]) - eye), 0f, false);
            Say("camera at " + eye.ToString("F2"));
        }
    }

    /// <summary>
    /// Both SubmitCommand overloads are patched, and the string body calls the list body, so one submission arrives
    /// twice; the frame + signature guard runs it once.
    /// </summary>
    internal static class ConsolePatch
    {
        private static int _lastFrame = -1;
        private static string _lastSignature = "";

        internal static bool TryHandle(string[] parts)
        {
            if (parts.Length == 0 || !parts[0].Equals("bodyshop", StringComparison.OrdinalIgnoreCase)) return false;
            string signature = string.Join(" ", parts);
            if (Time.frameCount == _lastFrame && signature == _lastSignature) return true;
            _lastFrame = Time.frameCount;
            _lastSignature = signature;
            try { Commands.Run(parts); }
            catch (Exception e) { Core.Log.Error("[Console] '" + signature + "' failed: " + e); }
            return true;
        }
    }

    [HarmonyPatch(typeof(Il2CppScheduleOne.Console), nameof(Il2CppScheduleOne.Console.SubmitCommand), new Type[] { typeof(string) })]
    internal static class Console_SubmitCommand_String_Patch
    {
        private static bool Prefix(string args)
        {
            if (string.IsNullOrWhiteSpace(args)) return true;
            return !ConsolePatch.TryHandle(args.Trim().Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries));
        }
    }

    [HarmonyPatch(typeof(Il2CppScheduleOne.Console), nameof(Il2CppScheduleOne.Console.SubmitCommand), new Type[] { typeof(Il2CppSystem.Collections.Generic.List<string>) })]
    internal static class Console_SubmitCommand_List_Patch
    {
        private static bool Prefix(Il2CppSystem.Collections.Generic.List<string> args)
        {
            if (args == null || args.Count == 0) return true;
            var parts = new string[args.Count];
            for (int i = 0; i < args.Count; i++) parts[i] = args[i];
            return !ConsolePatch.TryHandle(parts);
        }
    }
}
#endif
