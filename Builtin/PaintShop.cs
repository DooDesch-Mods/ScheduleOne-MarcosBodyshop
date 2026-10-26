using System;
using System.Collections.Generic;
using Chassis.Api;
using Il2CppScheduleOne.DevUtilities;
using Il2CppScheduleOne.Vehicles;
using Il2CppScheduleOne.Vehicles.Modification;
using MarcosBodyshop.Api;
using MarcosBodyshop.Config;
using UnityEngine;

namespace MarcosBodyshop.Builtin
{
    /// <summary>
    /// The Paint category: the game's factory colours (the vanilla repaint, $100) and Chassis paint in metallic,
    /// matte and chrome. A factory colour clears the custom paint, as vanilla's repaint does. The Second Paint category
    /// offers the same Chassis paints as "paint2", for a vehicle whose model names second paint slots (stripes, trim).
    /// </summary>
    internal static class PaintShop
    {
        private static readonly (string name, string hex)[] Palette =
        {
            ("Black", "16181B"), ("Graphite", "3C4046"), ("Silver", "9A9FA6"), ("White", "ECEDEF"),
            ("Yellow", "E8C23A"), ("Orange", "E57D2A"), ("Red", "C8312B"), ("Wine", "6E1E22"),
            ("Pink", "E07FA8"), ("Purple", "6B3FA0"), ("Navy", "1F2A4A"), ("Blue", "22408A"),
            ("Sky Blue", "7FB4E3"), ("Teal", "2E9A9E"), ("Lime", "8ED36B"), ("Forest", "2E5A34"),
        };

        private static readonly (string name, string hex)[] Chrome =
        {
            ("Chrome", "D8DCE0"), ("Gold Chrome", "D9B44A"), ("Rose Chrome", "D99A8E"), ("Black Chrome", "2A2D31"),
        };

        internal static void Register()
        {
            BodyshopApi.RegisterCategory(new BodyshopCategory
            {
                Id = "paint", Label = "Paint", Icon = "paint", Order = 100,
                Camera = Shots.Side,
                Note = NoteOf,
                Entries = Entries,
            });
            BodyshopApi.RegisterCategory(new BodyshopCategory
            {
                Id = "paint2", Label = "Second Paint", Icon = "paint", Order = 110,
                AppliesTo = ChassisApi.HasSecondPaint,
                Camera = Shots.Side,
                Note = Note2Of,
                Entries = Entries2,
            });
        }

        private static string Note2Of(LandVehicle v)
        {
            if (ChassisApi.GetTuning(v, "paint2") == null) return "Stock";
            foreach (var e in Entries2(v)) if (e.Installed(v)) return e.Label;
            return "Custom";
        }

        private static IEnumerable<BodyshopEntry> Entries2(LandVehicle v)
        {
            foreach (var p in Palette) yield return Custom("Metallic", p.name, p.hex, TuningFinish.Metallic, "paint_metallic", true);
            yield return Stock("Metallic", true);
            foreach (var p in Palette) yield return Custom("Matte", p.name, p.hex, TuningFinish.Matte, "paint_matte", true);
            yield return Stock("Matte", true);
            foreach (var p in Chrome) yield return Custom("Chrome", p.name, p.hex, TuningFinish.Chrome, "paint_chrome", true);
            yield return Stock("Chrome", true);
        }

        private static string NoteOf(LandVehicle v)
        {
            string custom = ChassisApi.GetTuning(v, "paint");
            if (custom != null)
            {
                foreach (var e in Entries(v)) if (e.Group != "Factory" && e.Installed(v)) return e.Label;
                return "Custom";
            }
            return ChassisApi.HasOwnPaintTargets(v) ? "Stock" : FactoryName(v.OwnedColor);
        }

        private static IEnumerable<BodyshopEntry> Entries(LandVehicle v)
        {
            // A body of a mod's own takes no game colour: the game paints only the vanilla body.
            if (!ChassisApi.HasOwnPaintTargets(v)) foreach (var e in Factory()) yield return e;
            foreach (var p in Palette) yield return Custom("Metallic", p.name, p.hex, TuningFinish.Metallic, "paint_metallic");
            yield return Stock("Metallic");
            foreach (var p in Palette) yield return Custom("Matte", p.name, p.hex, TuningFinish.Matte, "paint_matte");
            yield return Stock("Matte");
            foreach (var p in Chrome) yield return Custom("Chrome", p.name, p.hex, TuningFinish.Chrome, "paint_chrome");
            yield return Stock("Chrome");
        }

        // ---- factory colours --------------------------------------------------------------------------------------

        private static IEnumerable<BodyshopEntry> Factory()
        {
            var lib = Singleton<VehicleColors>.Instance?.colorLibrary;
            if (lib == null) { Core.Log.Warning("[Paint] the game's colour library is not loaded - no factory colours."); yield break; }
            for (int i = 0; i < lib.Count; i++)
            {
                var d = lib[i];
                if (d == null || d.color == EVehicleColor.Custom) continue;
                EVehicleColor col = d.color;
                Color32 ui = d.UIColor;
                yield return new BodyshopEntry
                {
                    Id = "factory_" + col.ToString().ToLowerInvariant(),
                    Group = "Factory",
                    Label = d.colorName,
                    Text = "Factory paint.",
                    Price = Prefs.Price("paint_factory", 100),
                    Swatch = "#" + ui.r.ToString("X2") + ui.g.ToString("X2") + ui.b.ToString("X2"),
                    Installed = x => x.OwnedColor == col && ChassisApi.GetTuning(x, "paint") == null,
                    Preview = x => PreviewFactory(x, col),
                    EndPreview = EndFactory,
                    Apply = x => { x.SendOwnedColor(col); return null; },
                };
            }
        }

        private static string FactoryName(EVehicleColor c)
        {
            try { return Singleton<VehicleColors>.Instance?.GetColorName(c) ?? c.ToString(); }
            catch (Exception e) { Core.Log.Warning("[Paint] the name of colour " + c + " failed: " + e.Message); return c.ToString(); }
        }

        /// <summary>
        /// Shows a factory colour: the custom paint is hidden by a Chassis preview first, and the colour goes on two
        /// frames later, after Chassis has put the factory paint back.
        /// </summary>
        private static void PreviewFactory(LandVehicle v, EVehicleColor col)
        {
            if (ChassisApi.GetTuning(v, "paint") != null) ChassisApi.PreviewTuning(v, "paint", "");
            Later.Frames(2, () => { if (v != null) v.ApplyColor(col); });
        }

        private static void EndFactory(LandVehicle v)
        {
            ChassisApi.EndPreview(v, "paint");
            v.ApplyOwnedColor();
        }

        // ---- Chassis paint ----------------------------------------------------------------------------------------

        private static BodyshopEntry Custom(string group, string name, string hex, string finish, string priceId, bool second = false)
        {
            string key = second ? "paint2" : "paint";
            string value = hex + "," + finish;
            ColorUtility.TryParseHtmlString("#" + hex, out Color colour);
            return new BodyshopEntry
            {
                Id = group.ToLowerInvariant() + "_" + name.Replace(" ", "").ToLowerInvariant(),
                Group = group,
                Label = name,
                Text = group + " paint.",
                Price = Prefs.Price(priceId, 250),
                Swatch = "#" + hex,
                Installed = x => string.Equals(ChassisApi.GetTuning(x, key), value, StringComparison.OrdinalIgnoreCase),
                Preview = x => ChassisApi.PreviewTuning(x, key, value),
                EndPreview = x => ChassisApi.EndPreview(x, key),
                Apply = x => ChassisApi.SetPaint(x, colour, finish, second) ? null : "The paint was refused.",
            };
        }

        private static BodyshopEntry Stock(string group, bool second = false)
        {
            string key = second ? "paint2" : "paint";
            return new BodyshopEntry
            {
                Id = group.ToLowerInvariant() + "_stock",
                Group = group,
                Label = "Factory Paint",
                Text = "Back to the factory colour. Free, no refund.",
                Price = 0,
                Installed = x => ChassisApi.GetTuning(x, key) == null,
                Preview = x => ChassisApi.PreviewTuning(x, key, ""),
                EndPreview = x => ChassisApi.EndPreview(x, key),
                Apply = x => ChassisApi.ClearPaint(x, second) ? null : "The paint was refused.",
            };
        }
    }
}
