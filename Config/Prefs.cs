using System;
using System.Collections.Generic;
using MelonLoader;
using UnityEngine;

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
            ("neon_sides", 125, "Underglow on other sides (front and back, or the sides only)."),
            ("horn", 150, "A horn."),
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
            ("bodyshop_steering_1", 750, "Quick Steering Rack."), ("bodyshop_steering_2", 1750, "Race Steering Rack."),
            ("bodyshop_weight_1", 1000, "Lightweight Panels."), ("bodyshop_weight_2", 2500, "Carbon Body."),
            ("bodyshop_nitro", 5000, "Nitrous Kit."),
        };

        private static MelonPreferences_Entry<KeyCode> _nitroKey;
        private static MelonPreferences_Entry<float> _nitroBoost, _nitroSeconds, _nitroRecharge;

        /// <summary>The key the driver holds for nitro, on this machine.</summary>
        internal static KeyCode NitroKey => _nitroKey?.Value ?? KeyCode.LeftControl;
        /// <summary>Pull and top speed while nitro burns, as a factor of the car's own (1.1 to 3).</summary>
        internal static float NitroBoost => Mathf.Clamp(_nitroBoost?.Value ?? 1.5f, 1.1f, 3f);
        /// <summary>Seconds a full tank lasts.</summary>
        internal static float NitroSeconds => Mathf.Max(0.5f, _nitroSeconds?.Value ?? 8f);
        /// <summary>Seconds an empty tank needs to fill up again.</summary>
        internal static float NitroRecharge => Mathf.Max(1f, _nitroRecharge?.Value ?? 30f);

        internal static void Init()
        {
            _cat = MelonPreferences.CreateCategory("MarcosBodyshop", "Marco's Bodyshop");
            foreach (var d in Defaults)
                _prices[d.id] = _cat.CreateEntry("Price_" + d.id, d.price, "Price " + d.id, d.what + " Whole dollars.");
            _nitroKey = _cat.CreateEntry("NitroKey", KeyCode.LeftControl, "Nitro key", "Hold it while you drive a car with the Nitrous Kit.");
            _nitroBoost = _cat.CreateEntry("NitroBoost", 1.5f, "Nitro boost", "Pull and top speed while nitro burns, as a factor of the car's own (1.1 to 3).");
            _nitroSeconds = _cat.CreateEntry("NitroSeconds", 8f, "Nitro duration", "Seconds a full nitro tank lasts.");
            _nitroRecharge = _cat.CreateEntry("NitroRechargeSeconds", 30f, "Nitro recharge", "Seconds an empty nitro tank needs to fill up again.");
            Core.Log.Msg("[Config] " + _prices.Count + " prices loaded; nitro on " + NitroKey + ", x" + NitroBoost.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)
                + " for " + NitroSeconds.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture) + " s, refill " + NitroRecharge.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture) + " s.");
        }

        /// <summary>The configured price of a built-in id, or <paramref name="fallback"/> for ids without one.</summary>
        internal static int Price(string id, int fallback)
        {
            if (id != null && _prices.TryGetValue(id, out var e)) return Math.Max(0, e.Value);
            return Math.Max(0, fallback);
        }
    }
}
