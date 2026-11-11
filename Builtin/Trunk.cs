using System;
using System.Collections.Generic;
using Chassis.Api;
using DooDesch.Localization;
using HarmonyLib;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes;
using Il2CppScheduleOne.DevUtilities;
using Il2CppScheduleOne.ItemFramework;
using Il2CppScheduleOne.Persistence.Datas;
using Il2CppScheduleOne.Storage;
using Il2CppScheduleOne.Vehicles;
using MarcosBodyshop.Api;
using MarcosBodyshop.Config;
using MarcosBodyshop.Shop;
using UnityEngine;

namespace MarcosBodyshop.Builtin
{
    /// <summary>
    /// Trunk: three tiers of extra trunk slots, one at a time in the Chassis slot "trunk". Each tier adds five slots to
    /// the car's own count, up to the 20 the game's storage window can show; a tier that would add nothing on a car
    /// is not offered there. The load rides on the roof, built from the game's own props.
    /// </summary>
    internal static class Trunk
    {
        internal const string Slot = "trunk";
        internal const int Cap = 20, PerTier = 5;

        private static readonly (string id, string label, int price)[] Tiers =
        {
            ("bodyshop_trunk_1", "Spare Tyres", 1250),
            ("bodyshop_trunk_2", "Box Stack", 2500),
            ("bodyshop_trunk_3", "Moving Day", 4375),
        };

        /// <summary>The game's own trunk size per vehicle code.</summary>
        private static readonly Dictionary<string, int> Stock = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["shitbox"] = 5, ["veeper"] = 16, ["bruiser"] = 5, ["dinkler"] = 8, ["hounddog"] = 5, ["cheetah"] = 4, ["hotbox"] = 5,
        };

        internal static void Register()
        {
            for (int t = 1; t <= Tiers.Length; t++)
            {
                int tier = t;
                var part = new TuningPart
                {
                    Id = Tiers[t - 1].id, Slot = Slot, Label = L10n.T(Tiers[t - 1].label), Price = Tiers[t - 1].price,
                    Build = v => RoofLoad.Build(v, tier),
                };
                foreach (var kv in Stock)
                    if (Target(kv.Value, tier) > Target(kv.Value, tier - 1)) part.Fits[kv.Key] = new PartFit();
                ChassisApi.RegisterTuningPart(part);
            }
            ChassisApi.TuningChanged += (v, key) =>
            {
                // A trunk part bought or removed is the player's choice and wins over another mod's size; "*" is the
                // whole tuning arriving (load, late join) and respects it.
                if (string.Equals(key, "part." + Slot, StringComparison.OrdinalIgnoreCase)) Sync(v, "tuning " + key, force: true);
                else if (key == "*") Sync(v, "tuning " + key);
            };

            BodyshopApi.RegisterCategory(new BodyshopCategory
            {
                Id = Slot, Label = L10n.T("Trunk"), Icon = "trunk", Order = 975,
                Camera = Shots.Roof,
                AppliesTo = v => StockOf(v) > 0 && ChassisApi.TuningParts(v, Slot).Count > 0,
                Note = v => L10n.T("{0} slots", SlotsOf(v)),
                Entries = Entries,
            });
        }

        private static IEnumerable<BodyshopEntry> Entries(LandVehicle v)
        {
            yield return new BodyshopEntry
            {
                Id = "stock",
                Label = L10n.T("Stock"),
                Text = L10n.T("{0} slots.", StockOf(v)) + " " + L10n.T("Back to stock. Free, no refund."),
                Price = 0,
                Installed = x => TierOf(x) == 0,
                Blocked = x => Tr(EmptyBeyond(x, StockOf(x))),
                Preview = x => ChassisApi.PreviewTuning(x, "part." + Slot, ""),
                EndPreview = x => ChassisApi.EndPreview(x, "part." + Slot),
                Apply = x => EmptyBeyond(x, StockOf(x)) ?? (ChassisApi.RemovePart(x, Slot) ? null : "The garage could not do it."),
            };
            foreach (var p in ChassisApi.TuningParts(v, Slot))
            {
                string id = p.Id;
                int tier = TierOfId(id);
                if (tier == 0) continue;
                yield return new BodyshopEntry
                {
                    Id = id,
                    Label = p.Label ?? id,
                    Text = L10n.T("{0} slots.", Target(StockOf(v), tier)),
                    Price = Prefs.Price(id, p.Price),
                    Installed = x => string.Equals(ChassisApi.InstalledPart(x, Slot), id, StringComparison.OrdinalIgnoreCase),
                    Blocked = x => Tr(EmptyBeyond(x, Target(StockOf(x), tier))),
                    Preview = x => ChassisApi.PreviewPart(x, id),
                    EndPreview = x => ChassisApi.EndPreview(x, "part." + Slot),
                    Apply = x => EmptyBeyond(x, Target(StockOf(x), tier)) ?? (ChassisApi.InstallPart(x, id) ? null : "The part does not fit."),
                };
            }
        }

        // ---- slot counts -------------------------------------------------------------------------------------------

        private static int Target(int stock, int tier) => Math.Min(Cap, stock + PerTier * Math.Max(0, tier));

        private static int TierOfId(string id)
        {
            for (int i = 0; i < Tiers.Length; i++) if (string.Equals(Tiers[i].id, id, StringComparison.OrdinalIgnoreCase)) return i + 1;
            return 0;
        }

        internal static int TierOf(LandVehicle v) => TierOfId(ChassisApi.InstalledPart(v, Slot));

        /// <summary>The game's trunk size of this car, or 0 for a car without a known trunk.</summary>
        internal static int StockOf(LandVehicle v)
        {
            if (v == null) return 0;
            return Stock.TryGetValue(ChassisApi.CodeOf(v) ?? "", out int n) ? n : 0;
        }

        internal static int SlotsOf(LandVehicle v)
        {
            var s = v != null ? v.Storage : null;
            return s != null && s.ItemSlots != null ? s.ItemSlots.Count : 0;
        }

        private static string Tr(string s) => s == null ? null : L10n.T(s);

        /// <summary>
        /// Null when every slot from <paramref name="keep"/> on is empty; else the reason the trunk cannot shrink, in
        /// English: the host sends it and the buyer translates it.
        /// </summary>
        private static string EmptyBeyond(LandVehicle v, int keep)
        {
            var s = v != null ? v.Storage : null;
            if (s == null || s.ItemSlots == null) return null;
            for (int i = keep; i < s.ItemSlots.Count; i++)
            {
                var slot = s.ItemSlots[i];
                if (slot != null && slot.ItemInstance != null && slot.Quantity > 0) return "Empty the extra trunk space first.";
            }
            return null;
        }

        /// <summary>The slot count Bodyshop last gave a car, and the tier it was for, by instance id.</summary>
        private static readonly Dictionary<int, (int slots, int tier)> Applied = new Dictionary<int, (int, int)>();

        /// <summary>Cars whose trunk size another mod set; Bodyshop leaves them until the player changes the tier.</summary>
        private static readonly HashSet<int> Foreign = new HashSet<int>();

        private static int Key(LandVehicle v) => v.GetInstanceID();

        /// <summary>
        /// Brings the car's trunk to the size its installed tier asks for. Without <paramref name="force"/> a size that
        /// Bodyshop did not set stays: another mod that sizes trunks would otherwise be undone every check, and both
        /// would resize back and forth.
        /// </summary>
        internal static void Sync(LandVehicle v, string why, bool force = false)
        {
            int stock = StockOf(v);
            if (stock == 0) return;
            int key = Key(v), tier = TierOf(v), had = SlotsOf(v);
            if (force)
            {
                if (Foreign.Remove(key)) Core.Log.Msg("[Trunk] " + Describe(v) + ": the trunk tier changed - Bodyshop sizes this trunk again.");
            }
            else
            {
                if (Foreign.Contains(key)) return;
                bool known = Applied.TryGetValue(key, out var mine);
                if (known && had != mine.slots)
                {
                    Foreign.Add(key);
                    Core.Log.Msg("[Trunk] " + Describe(v) + ": another mod set the trunk to " + had + " slot(s) (Bodyshop set " + mine.slots + ") - Bodyshop leaves it (" + why + ").");
                    return;
                }
                if (!known && tier == 0)
                {
                    // No tier and never resized by Bodyshop: the size is the game's or another mod's.
                    if (had != stock)
                    {
                        Foreign.Add(key);
                        Core.Log.Msg("[Trunk] " + Describe(v) + ": " + had + " slot(s) without a Bodyshop tier (stock " + stock + ") - Bodyshop leaves it (" + why + ").");
                    }
                    return;
                }
            }
            int want = Target(stock, tier);
            if (had == want) { Applied[key] = (want, tier); return; }
            int now = Resize(v, want, out string note);
            Applied[key] = (now, tier);
            if (now == had) Core.Log.Warning("[Trunk] " + Describe(v) + ": stays at " + had + " slot(s), wanted " + want + " (" + why + ")" + (note != null ? " - " + note : "."));
            else Core.Log.Msg("[Trunk] " + Describe(v) + ": " + had + " -> " + now + " slot(s) (" + why + ")" + (note != null ? " - " + note : "."));
        }

        /// <summary>Keeps the record in step when Bodyshop itself grows a trunk outside <see cref="Sync"/>.</summary>
        internal static void NoteGrown(LandVehicle v, int now)
        {
            if (v == null) return;
            int key = Key(v);
            if (Applied.TryGetValue(key, out var mine)) Applied[key] = (now, mine.tier);
        }

        internal static bool IsForeign(LandVehicle v) => v != null && Foreign.Contains(Key(v));

        /// <summary>
        /// Grows or shrinks the trunk to <paramref name="want"/> slots, never above <see cref="Cap"/>. Shrinking removes
        /// only empty slots from the end, so nothing in the trunk is lost. Returns the new count.
        /// </summary>
        internal static int Resize(LandVehicle v, int want, out string note)
        {
            note = null;
            var s = v != null ? v.Storage : null;
            if (s == null || s.ItemSlots == null) { note = "the vehicle has no trunk"; return 0; }
            want = Mathf.Clamp(want, 0, Cap);
            var slots = s.ItemSlots;
            bool changed = false;
            try
            {
                var owner = s.Cast<IItemSlotOwner>();
                while (slots.Count < want)
                {
                    var slot = new ItemSlot(s.SlotsAreFilterable);
                    var storage = s;
                    slot.onItemDataChanged = DelegateSupport.ConvertDelegate<Il2CppSystem.Action>(new Action(() => storage.ContentsChanged()));
                    slot.SetSlotOwner(owner);
                    changed = true;
                }
                while (slots.Count > want)
                {
                    var last = slots[slots.Count - 1];
                    if (last != null && last.ItemInstance != null && last.Quantity > 0) { note = "slot " + slots.Count + " still holds " + last.ItemInstance.Name; break; }
                    slots.RemoveAt(slots.Count - 1);
                    changed = true;
                }
            }
            catch (Exception e) { Core.Log.Error("[Trunk] resizing " + Describe(v) + " failed: " + e); note = "resize failed"; }
            if (changed)
            {
                s.SlotCount = slots.Count;
                s.DisplayRowCount = Math.Max(1, (slots.Count + 9) / 10);
                try { new ItemSlotSiblingSet(slots); }
                catch (Exception e) { Core.Log.Warning("[Trunk] linking the slots of " + Describe(v) + " failed, stacking between them may not work: " + e.Message); }
            }
            return slots.Count;
        }

        /// <summary>Grows the trunk of the storage to hold slot <paramref name="index"/>, when the storage is a vehicle's.</summary>
        internal static void EnsureIndex(StorageEntity s, int index, string why)
        {
            if (s == null || s.ItemSlots == null || index < s.ItemSlots.Count) return;
            if (index >= Cap) { Core.Log.Warning("[Trunk] " + why + " for slot " + index + " of " + s.name + ", beyond the " + Cap + " the game can show - left to the game."); return; }
            var v = s.GetComponentInParent<LandVehicle>();
            if (v == null || v.Storage == null || v.Storage.Pointer != s.Pointer) return;
            int now = Resize(v, index + 1, out string note);
            NoteGrown(v, now);
            Core.Log.Msg("[Trunk] " + Describe(v) + ": grew to " + now + " slot(s) for " + why + " on slot " + index + (note != null ? " - " + note : "."));
        }

        private static float _nextCheck;

        /// <summary>
        /// Every two seconds: cars whose tuning arrived without an event (load, late join) get their size. A car whose
        /// size and tier still match what Bodyshop gave it is skipped.
        /// </summary>
        internal static void Tick()
        {
            if (Time.time < _nextCheck) return;
            _nextCheck = Time.time + 2f;
            var vm = NetworkSingleton<VehicleManager>.Instance;
            if (vm == null || vm.AllVehicles == null) return;
            foreach (var v in vm.AllVehicles)
            {
                if (v == null || StockOf(v) == 0) continue;
                int key = Key(v);
                if (Foreign.Contains(key)) continue;
                if (Applied.TryGetValue(key, out var mine) && mine.slots == SlotsOf(v) && mine.tier == TierOf(v)) continue;
                int tier = TierOf(v);
                if (SlotsOf(v) != Target(StockOf(v), tier) || Applied.ContainsKey(key)) Sync(v, "check");
                else if (tier > 0) Applied[key] = (SlotsOf(v), tier);
            }
        }

        internal static string Describe(LandVehicle v) => v == null ? "no vehicle" : v.VehicleName + " (" + ChassisApi.CodeOf(v) + ")";

        internal static string Report()
        {
            var vm = NetworkSingleton<VehicleManager>.Instance;
            if (vm == null || vm.AllVehicles == null) return "no vehicle manager";
            var lines = new List<string>();
            foreach (var v in vm.AllVehicles)
                if (v != null) lines.Add(ReportOf(v));
            return lines.Count == 0 ? "no vehicles" : string.Join(" | ", lines);
        }

        /// <summary>One car's tier, slot count, wanted count, rows and the indices of the filled slots.</summary>
        internal static string ReportOf(LandVehicle v)
        {
            if (v == null) return "no vehicle";
            int stock = StockOf(v), tier = TierOf(v);
            var filled = new List<string>();
            var s = v.Storage;
            if (s != null && s.ItemSlots != null)
                for (int i = 0; i < s.ItemSlots.Count; i++)
                    if (s.ItemSlots[i] != null && s.ItemSlots[i].ItemInstance != null) filled.Add(i.ToString());
            return Describe(v) + ": tier " + tier + ", " + SlotsOf(v) + " slot(s), wants " + (stock > 0 ? Target(stock, tier).ToString() : "-")
                + ", rows " + (s != null ? s.DisplayRowCount : 0) + ", filled [" + string.Join(",", filled) + "]"
                + (IsForeign(v) ? ", size left to another mod" : "");
        }
    }

    /// <summary>
    /// The save holds one entry per trunk slot, empty ones included. Before the game loads a car's contents, the trunk
    /// grows to the saved count, so items in extra slots are not dropped while the tuning is still on its way.
    /// </summary>
    [HarmonyPatch(typeof(LandVehicle), nameof(LandVehicle.Load))]
    internal static class TrunkLoadPatch
    {
        private static void Prefix(LandVehicle __instance, VehicleData data)
        {
            try
            {
                var items = data != null && data.VehicleContents != null ? data.VehicleContents.Items : null;
                if (items == null || __instance.Storage == null || __instance.Storage.ItemSlots == null) return;
                int saved = items.Length, have = __instance.Storage.ItemSlots.Count;
                if (saved <= have) return;
                int now = Trunk.Resize(__instance, Math.Min(saved, Trunk.Cap), out string note);
                Trunk.NoteGrown(__instance, now);
                Core.Log.Msg("[Trunk] " + Trunk.Describe(__instance) + ": the save holds " + saved + " slot(s), trunk grown " + have + " -> " + now + (note != null ? " - " + note : "."));
            }
            catch (Exception e) { Core.Log.Error("[Trunk] growing the trunk before load failed, items in extra slots may be lost: " + e); }
        }
    }

    /// <summary>
    /// The host sends trunk contents by slot index. A client whose tuning has not arrived yet would drop an index past
    /// its own count, so the trunk grows first.
    /// </summary>
    [HarmonyPatch]
    internal static class TrunkRpcPatches
    {
        [HarmonyPatch(typeof(StorageEntity), "RpcLogic___SetStoredInstance_Internal_2652194801")]
        [HarmonyPrefix]
        private static void Stored(StorageEntity __instance, int __1) => Grow(__instance, __1, "an item");

        [HarmonyPatch(typeof(StorageEntity), "RpcLogic___SetItemSlotQuantity_Internal_1692629761")]
        [HarmonyPrefix]
        private static void Quantity(StorageEntity __instance, int __0) => Grow(__instance, __0, "a quantity");

        [HarmonyPatch(typeof(StorageEntity), "RpcLogic___SetSlotLocked_Internal_3170825843")]
        [HarmonyPrefix]
        private static void Locked(StorageEntity __instance, int __1) => Grow(__instance, __1, "a lock");

        [HarmonyPatch(typeof(StorageEntity), "RpcLogic___SetSlotFilter_Internal_527532783")]
        [HarmonyPrefix]
        private static void Filter(StorageEntity __instance, int __1) => Grow(__instance, __1, "a filter");

        private static void Grow(StorageEntity s, int index, string what)
        {
            try { Trunk.EnsureIndex(s, index, what); }
            catch (Exception e) { Core.Log.Error("[Trunk] growing a trunk for " + what + " on slot " + index + " failed: " + e); }
        }
    }
}
