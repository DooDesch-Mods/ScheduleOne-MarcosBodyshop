using System;
using System.Collections.Generic;
using MelonLoader;

namespace MarcosBodyshop.Config
{
    /// <summary>
    /// Prices of the built-in categories, in UserData/MelonPreferences.cfg under [MarcosBodyshop]. The host's prices
    /// decide: a buyer who paid less than the host's price gets the money back.
    /// </summary>
    internal static class Prefs
    {
        private static MelonPreferences_Category _cat;
        private static readonly Dictionary<string, MelonPreferences_Entry<int>> _prices = new Dictionary<string, MelonPreferences_Entry<int>>(StringComparer.OrdinalIgnoreCase);

        // id -> (default, description). Part ids are the Chassis built-in part ids.
        private static readonly (string id, int price, string what)[] Defaults =
        {
            ("paint_factory", 100, "A factory colour (the game's own repaint)."),
            ("paint_metallic", 250, "A metallic colour."),
            ("paint_matte", 250, "A matte colour."),
            ("paint_chrome", 375, "A chrome finish."),
            ("xenon", 375, "Xenon headlights in a colour."),
            ("neon", 375, "Underglow in a colour."),
            ("tint", 250, "Window tint."),
            ("rims", 250, "Rim colour."),
            ("smoke", 125, "Tyre smoke colour."),
            ("engine_1", 500, "Engine Tune 1."), ("engine_2", 1250, "Engine Tune 2."), ("engine_3", 2500, "Engine Tune 3."),
            ("brakes_1", 500, "Street Brakes."), ("brakes_2", 1250, "Sport Brakes."), ("brakes_3", 2500, "Race Brakes."),
            ("transmission_1", 500, "Street Transmission."), ("transmission_2", 1250, "Sport Transmission."), ("transmission_3", 2500, "Race Transmission."),
            ("suspension_1", 500, "Lowered Suspension."), ("suspension_2", 1000, "Street Suspension."),
            ("suspension_3", 1750, "Sport Suspension."), ("suspension_4", 2500, "Competition Suspension."),
            ("turbo", 3750, "Turbo Tuning."),
            ("tyres_drift", 750, "Drift Tyres."),
        };

        internal static void Init()
        {
            _cat = MelonPreferences.CreateCategory("MarcosBodyshop", "Marco's Bodyshop");
            foreach (var d in Defaults)
                _prices[d.id] = _cat.CreateEntry("Price_" + d.id, d.price, "Price " + d.id, d.what + " Whole dollars.");
            Core.Log.Msg("[Config] " + _prices.Count + " prices loaded.");
        }

        /// <summary>The configured price of a built-in id, or <paramref name="fallback"/> for ids without one.</summary>
        internal static int Price(string id, int fallback)
        {
            if (id != null && _prices.TryGetValue(id, out var e)) return Math.Max(0, e.Value);
            return Math.Max(0, fallback);
        }
    }
}
