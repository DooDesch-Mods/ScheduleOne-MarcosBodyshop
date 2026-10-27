using System;
using System.Collections.Generic;
using Chassis.Api;
using DooDesch.Localization;
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
            ("steering", "Steering", "steer", 850, "Steering upgrade."),
            ("weight", "Weight", "weight", 900, "Less weight, quicker off the line."),
            ("booster", "Nitro", "nitro", 950, null),
        };

        internal static void Register()
        {
            // Stages Chassis does not bring itself. No model and no fits: they go on every car.
            Stage("bodyshop_steering_1", "steering", "Quick Steering Rack", 750, new TuningStats { Steer = 1.15f });
            Stage("bodyshop_steering_2", "steering", "Race Steering Rack", 1750, new TuningStats { Steer = 1.3f });
            Stage("bodyshop_weight_1", "weight", "Lightweight Panels", 1000, new TuningStats { Mass = 0.9f });
            Stage("bodyshop_weight_2", "weight", "Carbon Body", 2500, new TuningStats { Mass = 0.8f });
            Nitro.Register();

            foreach (var s in Slots)
            {
                string slot = s.slot, blurb = s.blurb != null ? L10n.T(s.blurb) : null;
                BodyshopApi.RegisterCategory(new BodyshopCategory
                {
                    Id = slot, Label = L10n.T(s.label), Icon = s.icon, Order = s.order,
                    Camera = slot == "engine" || slot == "turbo" ? Shots.Front : slot == "transmission" || slot == "booster" ? Shots.Rear : slot == "weight" ? Shots.Side : Shots.Wheel,
                    AppliesTo = v => ChassisApi.TuningParts(v, slot).Count > 0,
                    Note = v => ChassisApi.TuningPartById(ChassisApi.InstalledPart(v, slot) ?? "")?.Label ?? L10n.T(Empty(slot) ? "None" : "Stock"),
                    Entries = v => Entries(v, slot, blurb),
                });
            }
        }

        private static IEnumerable<BodyshopEntry> Entries(LandVehicle v, string slot, string blurb)
        {
            yield return new BodyshopEntry
            {
                Id = "stock",
                Label = L10n.T(Empty(slot) ? "None" : "Stock"),
                Text = L10n.T("Back to stock. Free, no refund."),
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
                    Text = blurb ?? L10n.T("Hold {0} for nitro. The tank refills by itself.", KeyName(Prefs.NitroKey)),
                    Price = Prefs.Price(id, p.Price),
                    Stats = p.Stats ?? new TuningStats(),
                    Installed = x => string.Equals(ChassisApi.InstalledPart(x, slot), id, StringComparison.OrdinalIgnoreCase),
                    Preview = x => ChassisApi.PreviewPart(x, id),
                    EndPreview = x => ChassisApi.EndPreview(x, "part." + slot),
                    Apply = x => ChassisApi.InstallPart(x, id) ? null : "The part does not fit.",
                };
            }
        }

        /// <summary>Slots a car has nothing in by default.</summary>
        private static bool Empty(string slot) => slot == "turbo" || slot == "booster";

        private static void Stage(string id, string slot, string label, int price, TuningStats stats)
            => ChassisApi.RegisterTuningPart(new TuningPart { Id = id, Slot = slot, Label = L10n.T(label), Price = price, Stats = stats });

        internal static string KeyName(UnityEngine.KeyCode k)
        {
            switch (k)
            {
                case UnityEngine.KeyCode.LeftControl: return L10n.T("Left Ctrl");
                case UnityEngine.KeyCode.RightControl: return L10n.T("Right Ctrl");
                case UnityEngine.KeyCode.LeftShift: return L10n.T("Left Shift");
                case UnityEngine.KeyCode.LeftAlt: return L10n.T("Left Alt");
                default: return k.ToString();
            }
        }
    }
}
