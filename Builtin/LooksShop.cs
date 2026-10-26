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
            ("Light Smoke", "0E1012CC"), ("Dark Smoke", "08090BE6"), ("Limo", "030304F8"),
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
            yield return LightsOnWhilePicked(Stock("Xenon Headlights", "xenon"));
            foreach (var l in Lights) yield return LightsOnWhilePicked(Key("Xenon Headlights", "xenon", l.name, l.hex, l.hex, "Coloured headlight glow.", Shots.Front));
            yield return Stock("Underglow", "neon");
            foreach (var l in Lights) yield return Neon(l.name, l.hex);
            foreach (var s in Sides) yield return NeonSides(s.name, s.sides, s.text);
            yield return Stock("Window Tint", "tint");
            foreach (var t in Tints) yield return Key("Window Tint", "tint", t.name, "#" + t.rgba.Substring(0, 6), t.rgba, "Darker glass.", Shots.Side, swatchHex: "3A3D42");
            yield return Stock("Rim Colour", "rims");
            foreach (var r in Rims) yield return Key("Rim Colour", "rims", r.name, r.hex, r.hex + "," + TuningFinish.Metal, "Painted rims.", Shots.Wheel);
            yield return Stock("Tyre Smoke", "smoke");
            foreach (var l in Lights) yield return Key("Tyre Smoke", "smoke", l.name, l.hex, l.hex, "Coloured drift smoke.", Shots.Wheel);
        }

        /// <summary>The headlights' state before a xenon preview switched them on; null while no preview holds them.</summary>
        private static bool? _lightsWere;

        /// <summary>A xenon entry switches the headlights on while it is picked, so the colour shows, and back after.</summary>
        private static BodyshopEntry LightsOnWhilePicked(BodyshopEntry e)
        {
            var preview = e.Preview;
            var end = e.EndPreview;
            e.Preview = x => { preview?.Invoke(x); HeadlightsOn(x); };
            e.EndPreview = x => { end?.Invoke(x); HeadlightsBack(x); };
            return e;
        }

        private static void HeadlightsOn(LandVehicle v)
        {
            var lights = v != null ? v.lights : null;
            if (lights == null) { Core.Log.Warning("[Looks] the vehicle has no headlights - the xenon preview stays dark."); return; }
            try
            {
                if (_lightsWere == null) _lightsWere = lights.HeadlightsOn;
                if (!lights.HeadlightsOn) lights.HeadlightsOn = true;
            }
            catch (Exception e) { Core.Log.Warning("[Looks] switching the headlights on for the preview failed: " + e.Message); }
        }

        private static void HeadlightsBack(LandVehicle v)
        {
            if (_lightsWere == null) return;
            bool was = _lightsWere.Value;
            _lightsWere = null;
            var lights = v != null ? v.lights : null;
            if (lights == null) { Core.Log.Warning("[Looks] the vehicle is gone - its headlights stay as the preview left them."); return; }
            try { if (lights.HeadlightsOn != was) lights.HeadlightsOn = was; }
            catch (Exception e) { Core.Log.Warning("[Looks] switching the headlights back after the preview failed: " + e.Message); }
        }

        private static readonly (string name, string sides, string text)[] Sides =
        {
            ("All Four Sides", "fblr", "Neon all around the body."),
            ("Sides Only", "lr", "Neon along both sides."),
            ("Front and Back", "fb", "Neon under the front and the back."),
        };

        /// <summary>The colour of the underglow value "RRGGBB,sides", or null.</summary>
        private static string NeonColour(string value) => string.IsNullOrEmpty(value) ? null : value.Split(',')[0];

        /// <summary>The sides of the underglow value, "fblr" when it names none.</summary>
        private static string NeonSidesOf(string value)
        {
            if (string.IsNullOrEmpty(value)) return "fblr";
            int c = value.IndexOf(',');
            return c >= 0 && c + 1 < value.Length ? value.Substring(c + 1) : "fblr";
        }

        /// <summary>An underglow colour; it keeps the sides the car has.</summary>
        private static BodyshopEntry Neon(string name, string hex)
        {
            string With(LandVehicle x) => hex + "," + NeonSidesOf(ChassisApi.GetTuning(x, "neon"));
            return new BodyshopEntry
            {
                Id = "neon_" + name.Replace(" ", "").ToLowerInvariant(),
                Group = "Underglow",
                Label = name,
                Text = "Neon under the body.",
                Price = Prefs.Price("neon", 250),
                Swatch = "#" + hex,
                Installed = x => string.Equals(NeonColour(ChassisApi.GetTuning(x, "neon")), hex, StringComparison.OrdinalIgnoreCase),
                Preview = x => ChassisApi.PreviewTuning(x, "neon", With(x)),
                EndPreview = x => ChassisApi.EndPreview(x, "neon"),
                Apply = x => ChassisApi.SetTuning(x, "neon", With(x)) ? null : "The garage could not fit it.",
                Camera = Shots.Side,
            };
        }

        /// <summary>Where the underglow shines; it keeps the colour. Needs underglow first.</summary>
        private static BodyshopEntry NeonSides(string name, string sides, string text)
        {
            string With(LandVehicle x) => NeonColour(ChassisApi.GetTuning(x, "neon")) + "," + sides;
            return new BodyshopEntry
            {
                Id = "neonsides_" + sides,
                Group = "Underglow Sides",
                Label = name,
                Text = text,
                Price = Prefs.Price("neon_sides", 125),
                Installed = x => ChassisApi.GetTuning(x, "neon") != null && NeonSidesOf(ChassisApi.GetTuning(x, "neon")) == sides,
                Blocked = x => ChassisApi.GetTuning(x, "neon") == null ? "Fit underglow first." : null,
                Preview = x => { if (ChassisApi.GetTuning(x, "neon") != null) ChassisApi.PreviewTuning(x, "neon", With(x)); },
                EndPreview = x => ChassisApi.EndPreview(x, "neon"),
                Apply = x => ChassisApi.GetTuning(x, "neon") == null ? "Fit underglow first." : ChassisApi.SetTuning(x, "neon", With(x)) ? null : "The garage could not do it.",
                Camera = Shots.Side,
            };
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
