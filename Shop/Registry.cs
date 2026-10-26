using System;
using System.Collections.Generic;
using System.Linq;
using Il2CppScheduleOne.DevUtilities;
using Il2CppScheduleOne.Vehicles;
using MarcosBodyshop.Api;
using UnityEngine;

namespace MarcosBodyshop.Shop
{
    /// <summary>Garages, categories and gates from every mod, and the lookups the menu and the host share.</summary>
    internal static class Registry
    {
        private static readonly Dictionary<string, BodyshopGarage> _garages = new Dictionary<string, BodyshopGarage>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, BodyshopCategory> _categories = new Dictionary<string, BodyshopCategory>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, List<Func<LandVehicle, string>>> _gates = new Dictionary<string, List<Func<LandVehicle, string>>>(StringComparer.OrdinalIgnoreCase);

        internal static IEnumerable<BodyshopGarage> Garages => _garages.Values;

        internal static void AddGarage(BodyshopGarage g)
        {
            if (g == null || string.IsNullOrEmpty(g.Id)) { Core.Log.Warning("[Api] RegisterGarage: a garage needs an id - ignored."); return; }
            bool replaced = _garages.ContainsKey(g.Id);
            _garages[g.Id] = g;
            Core.Log.Msg("[Api] garage '" + g.Id + "' " + (replaced ? "replaced" : "registered") + " with " + g.Bays.Count + " bay(s).");
        }

        internal static BodyshopGarage Garage(string id) => id != null && _garages.TryGetValue(id, out var g) ? g : null;

        internal static bool AddBay(string garageId, BodyshopBay bay)
        {
            var g = Garage(garageId);
            if (g == null) { Core.Log.Warning("[Api] AddBay: no garage '" + garageId + "' - bay '" + bay?.Id + "' not added."); return false; }
            if (bay == null || string.IsNullOrEmpty(bay.Id)) { Core.Log.Warning("[Api] AddBay: a bay needs an id - ignored."); return false; }
            g.Bays.RemoveAll(b => string.Equals(b.Id, bay.Id, StringComparison.OrdinalIgnoreCase));
            g.Bays.Add(bay);
            Core.Log.Msg("[Api] bay '" + bay.Id + "' added to garage '" + garageId + "'.");
            Marking.Refresh();
            return true;
        }

        internal static void AddCategory(BodyshopCategory c)
        {
            if (c == null || string.IsNullOrEmpty(c.Id)) { Core.Log.Warning("[Api] RegisterCategory: a category needs an id - ignored."); return; }
            if (c.Entries == null) { Core.Log.Warning("[Api] RegisterCategory: category '" + c.Id + "' has no Entries - ignored."); return; }
            bool replaced = _categories.ContainsKey(c.Id);
            _categories[c.Id] = c;
            Core.Log.Msg("[Api] category '" + c.Id + "' " + (replaced ? "replaced." : "registered."));
        }

        internal static bool RemoveCategory(string id)
        {
            bool removed = id != null && _categories.Remove(id);
            Core.Log.Msg("[Api] category '" + id + "' " + (removed ? "removed." : "was not registered."));
            return removed;
        }

        internal static BodyshopCategory Category(string id) => id != null && _categories.TryGetValue(id, out var c) ? c : null;

        internal static void AddGate(string categoryId, Func<LandVehicle, string> rule)
        {
            if (string.IsNullOrEmpty(categoryId) || rule == null) { Core.Log.Warning("[Api] Gate: needs a category id and a rule - ignored."); return; }
            if (!_gates.TryGetValue(categoryId, out var list)) _gates[categoryId] = list = new List<Func<LandVehicle, string>>();
            list.Add(rule);
            Core.Log.Msg("[Api] gate added to category '" + categoryId + "' (" + list.Count + " in all).");
        }

        /// <summary>A category as the menu shows it for this vehicle.</summary>
        internal sealed class Shown
        {
            internal BodyshopCategory Category;
            internal string LockedReason;
        }

        /// <summary>The categories of this garage for the vehicle, in menu order, hidden ones left out.</summary>
        internal static List<Shown> CategoriesFor(string garageId, LandVehicle v)
        {
            var list = new List<Shown>();
            foreach (var c in _categories.Values.OrderBy(c => c.Order).ThenBy(c => c.Id, StringComparer.Ordinal))
            {
                if (c.Garage != null && !string.Equals(c.Garage, garageId, StringComparison.OrdinalIgnoreCase)) continue;
                string state = StateOf(c, v);
                if (state == "") continue;
                list.Add(new Shown { Category = c, LockedReason = state });
            }
            return list;
        }

        /// <summary>null: open; "": hidden; anything else: locked with that reason.</summary>
        internal static string StateOf(BodyshopCategory c, LandVehicle v)
        {
            try { if (c.AppliesTo != null && !c.AppliesTo(v)) return ""; }
            catch (Exception e) { Core.Log.Error("[Api] AppliesTo of category '" + c.Id + "' failed - hidden: " + e.Message); return ""; }
            if (_gates.TryGetValue(c.Id, out var gates))
                foreach (var g in gates)
                {
                    string r;
                    try { r = g(v); }
                    catch (Exception e) { Core.Log.Error("[Api] a gate of category '" + c.Id + "' failed - ignored: " + e.Message); continue; }
                    if (r != null) return r;
                }
            try { return c.Locked?.Invoke(v); }
            catch (Exception e) { Core.Log.Error("[Api] Locked of category '" + c.Id + "' failed - shown locked: " + e.Message); return "Not available."; }
        }

        internal static List<BodyshopEntry> EntriesOf(BodyshopCategory c, LandVehicle v)
        {
            var list = new List<BodyshopEntry>();
            try
            {
                var e = c.Entries(v);
                if (e != null) foreach (var x in e) if (x != null && !string.IsNullOrEmpty(x.Id)) list.Add(x);
            }
            catch (Exception ex) { Core.Log.Error("[Api] Entries of category '" + c.Id + "' failed - the category shows empty: " + ex); }
            return list;
        }

        internal static BodyshopEntry Entry(BodyshopCategory c, LandVehicle v, string entryId)
        {
            foreach (var e in EntriesOf(c, v)) if (string.Equals(e.Id, entryId, StringComparison.OrdinalIgnoreCase)) return e;
            return null;
        }

        // ---- bays ----------------------------------------------------------------------------------------------

        /// <summary>The first bay of the garage with a vehicle it accepts, or null with the reason.</summary>
        internal static LandVehicle VehicleIn(string garageId, out string reason, out BodyshopBay bay)
        {
            bay = null;
            var g = Garage(garageId);
            if (g == null) { reason = "There is no garage '" + garageId + "'."; return null; }
            LandVehicle occupied = null;
            foreach (var b in g.Bays)
            {
                LandVehicle v;
                try { v = b.FindVehicle != null ? b.FindVehicle() : InBox(b); }
                catch (Exception e) { Core.Log.Error("[Api] FindVehicle of bay '" + b.Id + "' failed: " + e.Message); continue; }
                if (v == null) continue;
                bool ok;
                try { ok = b.Accepts == null || b.Accepts(v); }
                catch (Exception e) { Core.Log.Error("[Api] Accepts of bay '" + b.Id + "' failed: " + e.Message); continue; }
                if (!ok) continue;
                if (v.IsOccupied) { occupied = v; continue; }
                bay = b;
                reason = null;
                return v;
            }
            reason = occupied != null ? "Someone is in the vehicle" : "Vehicle must be parked inside the shop";
            return null;
        }

        /// <summary>The nearest vehicle whose position lies inside the bay's box.</summary>
        internal static LandVehicle InBox(BodyshopBay b)
        {
            var vm = NetworkSingleton<VehicleManager>.Instance;
            if (vm == null || vm.AllVehicles == null) return null;
            var inv = Quaternion.Inverse(Quaternion.Euler(0f, b.Yaw, 0f));
            Vector3 half = b.Size * 0.5f;
            LandVehicle best = null;
            float bestD = float.MaxValue;
            foreach (var v in vm.AllVehicles)
            {
                if (v == null) continue;
                Vector3 local = inv * (v.transform.position - b.Center);
                if (Mathf.Abs(local.x) > half.x || Mathf.Abs(local.y) > half.y || Mathf.Abs(local.z) > half.z) continue;
                float d = local.sqrMagnitude;
                if (d < bestD) { bestD = d; best = v; }
            }
            return best;
        }

        internal static void Reset()
        {
            // Registrations stay across scenes; mods register once. Nothing per world is kept here.
        }
    }
}
