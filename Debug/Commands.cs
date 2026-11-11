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
    /// <item><c>bodyshop cash N</c> - add N to the bank balance.</item>
    /// <item><c>bodyshop radio x y z yaw</c> - move the workshop radio (to find its spot).</item>
    /// <item><c>bodyshop park x z yaw</c> - where the yard puts a vehicle on the next open (to find the spot).</item>
    /// <item><c>bodyshop trunk</c> - every car's trunk tier and slot count, and the props of the roof load.</item>
    /// <item><c>bodyshop trunk put N [item]</c> / <c>take N</c> - fill or empty slot N of the trunk of the car in the hall.</item>
    /// <item><c>bodyshop trunk foreign N</c> - size the trunk of the car in the hall like another mod would.</item>
    /// <item><c>bodyshop nitro [seconds]</c> - burn nitro in the driven car without the key; no number shows the state.</item>
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
                case "radio":
                {
                    var f = new float[4];
                    if (p.Length < 6) { Say("radio needs x y z yaw"); break; }
                    bool ok = true;
                    for (int i = 0; i < 4; i++) ok &= float.TryParse(p[i + 2], NumberStyles.Float, CultureInfo.InvariantCulture, out f[i]);
                    Say(ok ? Sounds.Place(new Vector3(f[0], f[1], f[2]), f[3]) : "radio: four numbers needed");
                    break;
                }
                case "park":
                {
                    var f = new float[3];
                    if (p.Length < 5) { Say("park needs x z yaw; now " + Builtin.MarcoGarage.YardParkSpot.ToString("F2") + " yaw " + Builtin.MarcoGarage.YardParkYaw.ToString("F0")); break; }
                    bool ok = true;
                    for (int i = 0; i < 3; i++) ok &= float.TryParse(p[i + 2], NumberStyles.Float, CultureInfo.InvariantCulture, out f[i]);
                    if (!ok) { Say("park: three numbers needed"); break; }
                    Builtin.MarcoGarage.YardParkSpot = new Vector2(f[0], f[1]);
                    Builtin.MarcoGarage.YardParkYaw = f[2];
                    Say("yard spot (" + f[0].ToString("F2") + ", " + f[1].ToString("F2") + ") yaw " + f[2].ToString("F0") + " - takes effect on the next open");
                    break;
                }
                case "cash":
                    if (!int.TryParse(a, NumberStyles.Integer, CultureInfo.InvariantCulture, out int n)) { Say("cash needs a whole number"); break; }
                    S1API.Money.Money.CreateOnlineTransaction("Bodyshop debug", n, 1f, string.Empty);
                    Say("bank balance +" + n);
                    break;
                case "nitro":
                    if (a.Length > 0 && float.TryParse(a, NumberStyles.Float, CultureInfo.InvariantCulture, out float secs)) { Builtin.Nitro.ForceUntil = Time.time + secs; Say("nitro held for " + a + " s"); }
                    Say(Builtin.Nitro.Describe());
                    break;
                case "prop": Prop(p); break;
                case "trunk":
                    if (a == "put" || a == "take") { Say("trunk " + a + ": " + TrunkSlot(a, p)); break; }
                    if (a == "move") { Say("trunk move: " + MoveHallCar(p)); break; }
                    if (a == "foreign") { Say("trunk foreign: " + ForeignSize(p)); break; }
                    Say("trunk: " + Builtin.Trunk.Report());
                    {
                        var hall = Registry.VehicleIn(BodyshopApi.MarcoGarage, out string hallWhy, out _);
                        Say("trunk hall: " + (hall != null ? Builtin.Trunk.ReportOf(hall) : "no vehicle in the hall: " + hallWhy));
                    }
                    Say("trunk props: " + Builtin.RoofLoad.Report());
                    break;
                default:
                    Say("bodyshop trunk [put N|take N|foreign N] | nitro [seconds] | open [garage] | close | bays [show|hide] | key up|down|enter|back | list cat | pick cat/entry | buy cat/entry | cam ex ey ez tx ty tz | radio x y z yaw | park x z yaw | cash N");
                    break;
            }
        }

        /// <summary>
        /// <c>bodyshop trunk foreign N</c> sizes the trunk of the car in Marco's hall to N slots the way another mod
        /// would: straight on the storage, without Bodyshop's record.
        /// </summary>
        private static string ForeignSize(string[] p)
        {
            var v = Registry.VehicleIn(BodyshopApi.MarcoGarage, out string why, out _);
            if (v == null) return "no vehicle in the hall: " + why;
            if (p.Length < 4 || !int.TryParse(p[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out int n)) return "needs a slot count";
            int now = Builtin.Trunk.Resize(v, n, out string note);
            return Builtin.Trunk.Describe(v) + " set to " + now + " slot(s)" + (note != null ? " - " + note : "");
        }

        /// <summary>
        /// <c>bodyshop trunk put N [item]</c> stores one item in slot N of the trunk of the car in Marco's hall;
        /// <c>bodyshop trunk take N</c> empties that slot.
        /// </summary>
        private static string TrunkSlot(string op, string[] p)
        {
            var v = Registry.VehicleIn(BodyshopApi.MarcoGarage, out string why, out _);
            if (v == null) return "no vehicle in the hall: " + why;
            var s = v.Storage;
            if (s == null || s.ItemSlots == null) return v.VehicleName + " has no trunk";
            if (p.Length < 4 || !int.TryParse(p[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out int i) || i < 0 || i >= s.ItemSlots.Count)
                return "needs a slot number from 0 to " + (s.ItemSlots.Count - 1);
            var slot = s.ItemSlots[i];
            if (op == "take") { slot.ClearStoredInstance(); return "slot " + i + " of " + v.VehicleName + " emptied"; }
            string id = p.Length > 4 ? p[4] : "ogkush";
            var def = Il2CppScheduleOne.Registry.GetItem(id);
            if (def == null) return "no item '" + id + "'";
            slot.SetStoredItem(def.GetDefaultInstance(1));
            return "slot " + i + " of " + v.VehicleName + " holds " + id;
        }

        /// <summary>
        /// <c>bodyshop prop list Mesh_Name</c> lists every loaded mesh of that name; <c>bodyshop prop show Mesh_Name index x y z [yaw]</c>
        /// stands a copy on the ground there; <c>bodyshop prop clear</c> removes the copies. Mesh names with spaces use '_' for
        /// the space only when no mesh has the name with the underscore.
        /// </summary>
        private static void Prop(string[] p)
        {
            string op = p.Length > 2 ? p[2].ToLowerInvariant() : "";
            if (op == "clear") { Say("prop: " + Builtin.RoofLoad.ClearShown()); return; }
            if (p.Length < 4) { Say("prop list Mesh_Name | show Mesh_Name index x y z [yaw] | clear"); return; }
            string name = p[3];
            if (Builtin.RoofLoad.ListMeshes(name, out _).Count == 0 && name.Contains("_")) name = name.Replace('_', ' ');
            if (op == "list")
            {
                var lines = Builtin.RoofLoad.ListMeshes(name, out _);
                Say("prop list '" + name + "': " + (lines.Count == 0 ? "none loaded" : string.Join(" | ", lines)));
                return;
            }
            if (op != "show" || p.Length < 8) { Say("prop show Mesh_Name index x y z [yaw]"); return; }
            var f = new float[5];
            bool ok = int.TryParse(p[4], NumberStyles.Integer, CultureInfo.InvariantCulture, out int index);
            for (int i = 0; i < 4 && ok; i++)
                ok = i == 3 && p.Length < 9 ? true : float.TryParse(p[5 + i], NumberStyles.Float, CultureInfo.InvariantCulture, out f[i]);
            if (!ok) { Say("prop show: index and numbers needed"); return; }
            Say("prop show: " + Builtin.RoofLoad.Show(name, index, new Vector3(f[0], f[1], f[2]), f[3]));
        }

        /// <summary><c>bodyshop trunk move x y z</c> puts the car in Marco's hall at that spot, so the next car can come in.</summary>
        private static string MoveHallCar(string[] p)
        {
            var v = Registry.VehicleIn(BodyshopApi.MarcoGarage, out string why, out _);
            if (v == null) return "no vehicle in the hall: " + why;
            var f = new float[3];
            if (p.Length < 6) return "needs x y z";
            for (int i = 0; i < 3; i++)
                if (!float.TryParse(p[i + 3], NumberStyles.Float, CultureInfo.InvariantCulture, out f[i])) return "'" + p[i + 3] + "' is not a number";
            var rb = v.GetComponent<Rigidbody>();
            // Kinematic, so it stays where it is put: a car that falls below the map is put back by the game.
            if (rb != null) { rb.velocity = Vector3.zero; rb.angularVelocity = Vector3.zero; rb.isKinematic = true; }
            v.transform.position = new Vector3(f[0], f[1], f[2]);
            if (rb != null) rb.position = v.transform.position;
            return v.VehicleName + " moved to " + v.transform.position.ToString("F1");
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
