using System;
using System.Collections.Generic;
using Chassis.Api;
using Il2CppScheduleOne.Vehicles;
using MarcosBodyshop.Api;
using MarcosBodyshop.Config;
using UnityEngine;

namespace MarcosBodyshop.Builtin
{
    /// <summary>Lights &amp; Looks: xenon headlights, underglow, window tint, rim colour and tyre smoke, all Chassis keys.</summary>
    internal static class LooksShop
    {
        private static readonly (string name, string hex)[] Lights =
        {
            ("White", "F2F4FF"), ("Blue", "3D7BFF"), ("Ice Blue", "9FD8FF"), ("Yellow", "FFD23D"), ("Orange", "FF8A2A"),
            ("Red", "FF3030"), ("Pink", "FF5FB7"), ("Purple", "9B4DFF"), ("Green", "46E06A"),
        };

        private static readonly (string name, string hex)[] Rims =
        {
            ("Black", "16181B"), ("Silver", "B8BDC4"), ("Gold", "D9B44A"), ("White", "ECEDEF"), ("Red", "C8312B"), ("Blue", "22408A"),
        };

        private static readonly (string name, string rgba)[] Tints =
        {
            ("Light Smoke", "10121466"), ("Dark Smoke", "0A0B0DA8"), ("Limo", "050506E0"),
        };

        internal static void Register()
        {
            BodyshopApi.RegisterCategory(new BodyshopCategory
            {
                Id = "looks", Label = "Lights & Looks", Icon = "looks", Order = 200,
                Camera = Shots.Front,
                Note = v => { int n = 0; foreach (var k in new[] { "xenon", "neon", "tint", "rims", "smoke" }) if (ChassisApi.GetTuning(v, k) != null) n++; return n == 0 ? "Stock" : n + " fitted"; },
                Entries = Entries,
            });
        }

        private static IEnumerable<BodyshopEntry> Entries(LandVehicle v)
        {
            yield return Stock("Xenon Headlights", "xenon");
            foreach (var l in Lights) yield return Key("Xenon Headlights", "xenon", l.name, l.hex, l.hex, "Coloured headlight glow.", Shots.Front);
            yield return Stock("Underglow", "neon");
            foreach (var l in Lights) yield return Key("Underglow", "neon", l.name, l.hex, l.hex + ",fblr", "Neon under the body, all four sides.", Shots.Side);
            yield return Stock("Window Tint", "tint");
            foreach (var t in Tints) yield return Key("Window Tint", "tint", t.name, "#" + t.rgba.Substring(0, 6), t.rgba, "Darker glass.", Shots.Side, swatchHex: "3A3D42");
            yield return Stock("Rim Colour", "rims");
            foreach (var r in Rims) yield return Key("Rim Colour", "rims", r.name, r.hex, r.hex + "," + TuningFinish.Metal, "Painted rims.", Shots.Wheel);
            yield return Stock("Tyre Smoke", "smoke");
            foreach (var l in Lights) yield return Key("Tyre Smoke", "smoke", l.name, l.hex, l.hex, "Coloured drift smoke.", Shots.Wheel);
        }

        private static BodyshopEntry Key(string group, string key, string name, string hex, string value, string text,
                                         Func<LandVehicle, CameraShot?> camera, string swatchHex = null)
        {
            return new BodyshopEntry
            {
                Id = key + "_" + name.Replace(" ", "").ToLowerInvariant(),
                Group = group,
                Label = name,
                Text = text,
                Price = Prefs.Price(key, 250),
                Swatch = "#" + (swatchHex ?? hex.TrimStart('#')),
                Installed = x => string.Equals(ChassisApi.GetTuning(x, key), value, StringComparison.OrdinalIgnoreCase),
                Preview = x => ChassisApi.PreviewTuning(x, key, value),
                EndPreview = x => ChassisApi.EndPreview(x, key),
                Apply = x => ChassisApi.SetTuning(x, key, value) ? null : "The garage could not fit it.",
                Camera = camera,
            };
        }

        private static BodyshopEntry Stock(string group, string key) => new BodyshopEntry
        {
            Id = key + "_stock",
            Group = group,
            Label = "Stock",
            Text = "Back to stock. Free, no refund.",
            Price = 0,
            Installed = x => ChassisApi.GetTuning(x, key) == null,
            Preview = x => ChassisApi.PreviewTuning(x, key, ""),
            EndPreview = x => ChassisApi.EndPreview(x, key),
            Apply = x => ChassisApi.SetTuning(x, key, "") ? null : "The garage could not do it.",
        };
    }
}
