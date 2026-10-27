using System;
using Chassis.Api;
using DooDesch.Localization;
using Il2CppScheduleOne.Vehicles;
using MarcosBodyshop.Api;
using MarcosBodyshop.Net;

namespace MarcosBodyshop.Shop
{
    /// <summary>The page's data: the categories of a vehicle, and the entries of one category, as JSON.</summary>
    internal static class Model
    {
        /// <summary>
        /// The page's own words. The page cannot reach L10n, so the root model carries them as an English-to-shown
        /// table; the page looks a word up by its English text and shows the English when the table lacks it.
        /// </summary>
        private static readonly string[] PageText =
        {
            "Browse", "Buy", "Back",
            "Nothing here", "more above", "more below",
            "Fitted", "Removed", "Free", "Installed",
            "Unlocked. Removing and refitting is free.",
            "Pay once to unlock. Removing and refitting is free after that.",
            "Not available.", "Already installed.", "Not enough money.", "The garage sent bad data.",
        };

        internal static string Root(string garageId, LandVehicle v)
        {
            var g = Registry.Garage(garageId);
            var j = new Json().Open('{');
            j.Prop("garage", g?.Name ?? "Bodyshop");
            j.Prop("vehicle", v != null ? v.VehicleName : "");
            j.Prop("cash", Payment.Balance());
            j.Key("text").Open('{');
            foreach (var en in PageText) j.Prop(en, L10n.T(en));
            j.Close('}');
            j.Key("items").Open('[');
            if (v != null)
                foreach (var s in Registry.CategoriesFor(garageId, v))
                {
                    var c = s.Category;
                    j.Open('{').Prop("id", c.Id).Prop("label", c.Label ?? c.Id).Prop("icon", "i-" + (c.Icon ?? "wrench"));
                    if (s.LockedReason != null) j.Prop("locked", s.LockedReason);
                    else j.Prop("note", NoteOf(c, v));
                    j.Close('}');
                }
            j.Close(']');
            return j.Close('}').ToString();
        }

        private static string NoteOf(BodyshopCategory c, LandVehicle v)
        {
            try
            {
                if (c.Note != null) return c.Note(v) ?? "";
                foreach (var e in Registry.EntriesOf(c, v)) if (!e.Toggle && Prices.Installed(e, v)) return e.Label;
                return "";
            }
            catch (Exception ex) { Core.Log.Error("[Menu] Note of category '" + c.Id + "' failed: " + ex.Message); return ""; }
        }

        internal static string Entries(BodyshopCategory c, LandVehicle v)
        {
            var entries = Registry.EntriesOf(c, v);
            TuningStats real = Prices.RealStats(v);
            TuningStats installed = null;
            foreach (var e in entries) if (!e.Toggle && e.Stats != null && Prices.Installed(e, v)) { installed = e.Stats; break; }
            TuningStats rest = Prices.Without(real, installed);
            float[] now = Prices.Bars(real);

            var j = new Json().Open('{');
            j.Prop("id", c.Id).Prop("title", c.Label ?? c.Id);
            j.Key("stats").Open('[');
            foreach (var n in Prices.StatNames) j.Val(n);
            j.Close(']');
            j.Key("items").Open('[');
            foreach (var e in entries)
            {
                j.Open('{').Prop("id", e.Id).Prop("label", e.Label ?? e.Id).Prop("price", Prices.Of(e, v));
                j.Prop("installed", Prices.Installed(e, v)).Prop("toggle", e.Toggle);
                if (e.Rule == PriceRule.UnlockOnce) j.Prop("unlocked", Prices.Unlocked(e, v));
                if (e.Group != null) j.Prop("group", e.Group);
                if (e.Swatch != null) j.Prop("swatch", e.Swatch);
                if (e.Text != null) j.Prop("text", e.Text);
                string blocked = Prices.BlockedOf(e, v);
                if (blocked != null) j.Prop("blocked", blocked);
                if (e.Stats != null)
                {
                    float[] then = Prices.Bars(TuningStats.Combine(rest, e.Stats));
                    j.Key("now").Open('['); foreach (float f in now) j.Val(f); j.Close(']');
                    j.Key("then").Open('['); foreach (float f in then) j.Val(f); j.Close(']');
                }
                j.Close('}');
            }
            j.Close(']');
            return j.Close('}').ToString();
        }
    }
}
