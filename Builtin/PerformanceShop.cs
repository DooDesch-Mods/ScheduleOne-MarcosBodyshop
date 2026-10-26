using System;
using System.Collections.Generic;
using Chassis.Api;
using Il2CppScheduleOne.Vehicles;
using MarcosBodyshop.Api;
using MarcosBodyshop.Config;

namespace MarcosBodyshop.Builtin
{
    /// <summary>
    /// One category per Chassis performance slot. The entries are every registered part that fits the vehicle, the
    /// Chassis built-ins and parts of other mods alike; built-ins cost the configured price, others their own.
    /// </summary>
    internal static class PerformanceShop
    {
        private static readonly (string slot, string label, string icon, int order, string blurb)[] Slots =
        {
            ("engine", "Engine", "engine", 300, "Engine upgrade."),
            ("brakes", "Brakes", "brakes", 400, "Brake upgrade."),
            ("transmission", "Transmission", "gears", 500, "Transmission upgrade."),
            ("suspension", "Suspension", "susp", 600, "Suspension upgrade."),
            ("turbo", "Turbo", "turbo", 700, "Turbo upgrade."),
            ("tyres", "Tyres", "tyres", 800, "Tyre upgrade."),
        };

        internal static void Register()
        {
            foreach (var s in Slots)
            {
                string slot = s.slot, blurb = s.blurb;
                BodyshopApi.RegisterCategory(new BodyshopCategory
                {
                    Id = slot, Label = s.label, Icon = s.icon, Order = s.order,
                    Camera = slot == "engine" || slot == "turbo" ? Shots.Front : slot == "transmission" ? Shots.Rear : Shots.Wheel,
                    AppliesTo = v => ChassisApi.TuningParts(v, slot).Count > 0,
                    Note = v => ChassisApi.TuningPartById(ChassisApi.InstalledPart(v, slot) ?? "")?.Label ?? (slot == "turbo" ? "None" : "Stock"),
                    Entries = v => Entries(v, slot, blurb),
                });
            }
        }

        private static IEnumerable<BodyshopEntry> Entries(LandVehicle v, string slot, string blurb)
        {
            yield return new BodyshopEntry
            {
                Id = "stock",
                Label = slot == "turbo" ? "None" : "Stock",
                Text = "Back to stock. Free, no refund.",
                Price = 0,
                Stats = new TuningStats(),
                Installed = x => ChassisApi.InstalledPart(x, slot) == null,
                Preview = x => ChassisApi.PreviewTuning(x, "part." + slot, ""),
                EndPreview = x => ChassisApi.EndPreview(x, "part." + slot),
                Apply = x => ChassisApi.RemovePart(x, slot) ? null : "The garage could not do it.",
            };
            var parts = ChassisApi.TuningParts(v, slot);
            parts.Sort((a, b) => Prefs.Price(a.Id, a.Price).CompareTo(Prefs.Price(b.Id, b.Price)));
            foreach (var p in parts)
            {
                string id = p.Id;
                yield return new BodyshopEntry
                {
                    Id = id,
                    Label = p.Label ?? id,
                    Text = blurb,
                    Price = Prefs.Price(id, p.Price),
                    Stats = p.Stats ?? new TuningStats(),
                    Installed = x => string.Equals(ChassisApi.InstalledPart(x, slot), id, StringComparison.OrdinalIgnoreCase),
                    Preview = x => ChassisApi.PreviewPart(x, id),
                    EndPreview = x => ChassisApi.EndPreview(x, "part." + slot),
                    Apply = x => ChassisApi.InstallPart(x, id) ? null : "The part does not fit.",
                };
            }
        }
    }
}
