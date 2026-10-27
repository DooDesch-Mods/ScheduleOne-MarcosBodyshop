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

        // id -> (default, description, the default of 1.0.x). Part ids are the Chassis built-in part ids. The performance
        // parts cost 25 % more than in 1.0.x; a price still at its old default moves to the new one.
        private static readonly (string id, int price, string what, int was)[] Defaults =
        {
            ("paint_factory", 100, "A factory colour (the game's own repaint).", 100),
            ("paint_metallic", 250, "A metallic colour.", 250),
            ("paint_matte", 250, "A matte colour.", 250),
            ("paint_chrome", 375, "A chrome finish.", 375),
            ("xenon", 375, "Xenon headlights in a colour.", 375),
            ("neon", 375, "Underglow in a colour.", 375),
            ("neon_sides", 125, "Underglow on other sides (front and back, or the sides only).", 125),
            ("horn", 150, "A horn.", 150),
            ("tint", 250, "Window tint.", 250),
            ("rims", 250, "Rim colour.", 250),
            ("smoke", 125, "Tyre smoke colour.", 125),
            ("engine_1", 625, "Engine Tune 1.", 500), ("engine_2", 1575, "Engine Tune 2.", 1250), ("engine_3", 3125, "Engine Tune 3.", 2500),
            ("brakes_1", 625, "Street Brakes.", 500), ("brakes_2", 1575, "Sport Brakes.", 1250), ("brakes_3", 3125, "Race Brakes.", 2500),
            ("transmission_1", 625, "Street Transmission.", 500), ("transmission_2", 1575, "Sport Transmission.", 1250), ("transmission_3", 3125, "Race Transmission.", 2500),
            ("suspension_1", 625, "Lowered Suspension.", 500), ("suspension_2", 1250, "Street Suspension.", 1000),
            ("suspension_3", 2200, "Sport Suspension.", 1750), ("suspension_4", 3125, "Competition Suspension.", 2500),
            ("turbo", 4700, "Turbo Tuning.", 3750),
            ("tyres_drift", 950, "Drift Tyres.", 750),
            ("bodyshop_steering_1", 950, "Quick Steering Rack.", 750), ("bodyshop_steering_2", 2200, "Race Steering Rack.", 1750),
            ("bodyshop_weight_1", 1250, "Lightweight Panels.", 1000), ("bodyshop_weight_2", 3125, "Carbon Body.", 2500),
            ("bodyshop_nitro", 6250, "Nitrous Kit.", 5000),
        };

        /// <summary>Seconds an empty nitro tank needed to refill in 1.0.x.</summary>
        private const float OldNitroRecharge = 30f;
        internal const float DefaultNitroRecharge = 90f;

        private static MelonPreferences_Entry<KeyCode> _nitroKey;
        private static MelonPreferences_Entry<float> _nitroBoost, _nitroSeconds, _nitroRecharge;

        /// <summary>The key the driver holds for nitro, on this machine.</summary>
        internal static KeyCode NitroKey => _nitroKey?.Value ?? KeyCode.LeftControl;
        /// <summary>Pull and top speed while nitro burns, as a factor of the car's own (1.1 to 3).</summary>
        internal static float NitroBoost => Mathf.Clamp(_nitroBoost?.Value ?? 1.5f, 1.1f, 3f);
        /// <summary>Seconds a full tank lasts.</summary>
        internal static float NitroSeconds => Mathf.Max(0.5f, _nitroSeconds?.Value ?? 8f);
        /// <summary>Seconds an empty tank needs to fill up again.</summary>
        internal static float NitroRecharge => Mathf.Max(1f, _nitroRecharge?.Value ?? DefaultNitroRecharge);

        internal static void Init()
        {
            _cat = MelonPreferences.CreateCategory("MarcosBodyshop", "Marco's Bodyshop");
            int moved = 0;
            foreach (var d in Defaults)
            {
                var e = _cat.CreateEntry("Price_" + d.id, d.price, "Price " + d.id, d.what + " Whole dollars.");
                if (e.Value == d.was && d.was != d.price) { e.Value = d.price; moved++; }
                _prices[d.id] = e;
            }
            if (moved > 0) Core.Log.Msg("[Config] " + moved + " price(s) still at the 1.0.x default moved to the new default (performance parts +25 %).");
            _nitroKey = _cat.CreateEntry("NitroKey", KeyCode.LeftControl, "Nitro key", "Hold it while you drive a car with the Nitrous Kit.");
            _nitroBoost = _cat.CreateEntry("NitroBoost", 1.5f, "Nitro boost", "Pull and top speed while nitro burns, as a factor of the car's own (1.1 to 3).");
            _nitroSeconds = _cat.CreateEntry("NitroSeconds", 8f, "Nitro duration", "Seconds a full nitro tank lasts.");
            _nitroRecharge = _cat.CreateEntry("NitroRechargeSeconds", DefaultNitroRecharge, "Nitro recharge", "Seconds an empty nitro tank needs to fill up again.");
            if (Mathf.Approximately(_nitroRecharge.Value, OldNitroRecharge))
            {
                _nitroRecharge.Value = DefaultNitroRecharge;
                Core.Log.Msg("[Config] NitroRechargeSeconds moved from the 1.0.x default " + OldNitroRecharge + " to " + DefaultNitroRecharge + ".");
            }
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
