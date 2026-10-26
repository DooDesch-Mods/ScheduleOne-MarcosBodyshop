using System;
using Chassis.Api;
using Il2CppScheduleOne.Vehicles;
using MarcosBodyshop.Api;
using UnityEngine;

namespace MarcosBodyshop.Shop
{
    /// <summary>What an entry costs and shows for a vehicle. The menu and the host ask the same functions.</summary>
    internal static class Prices
    {
        internal static bool Installed(BodyshopEntry e, LandVehicle v)
        {
            try { return e.Installed != null && e.Installed(v); }
            catch (Exception ex) { Core.Log.Error("[Shop] Installed of '" + e.Id + "' failed: " + ex.Message); return false; }
        }

        internal static bool Unlocked(BodyshopEntry e, LandVehicle v)
        {
            if (e.Rule != PriceRule.UnlockOnce) return false;
            try { return e.Unlocked != null && e.Unlocked(v); }
            catch (Exception ex) { Core.Log.Error("[Shop] Unlocked of '" + e.Id + "' failed: " + ex.Message); return false; }
        }

        internal static string BlockedOf(BodyshopEntry e, LandVehicle v)
        {
            try { return e.Blocked?.Invoke(v); }
            catch (Exception ex) { Core.Log.Error("[Shop] Blocked of '" + e.Id + "' failed - blocked: " + ex.Message); return "Not available."; }
        }

        /// <summary>The price of the next purchase: 0 when unlocked or when a toggle part is taken out.</summary>
        internal static int Of(BodyshopEntry e, LandVehicle v)
        {
            if (Unlocked(e, v)) return 0;
            if (e.Toggle && Installed(e, v) && e.Rule == PriceRule.EveryTime) return 0;
            return Math.Max(0, e.Price);
        }

        // ---- stat bars ------------------------------------------------------------------------------------------

        internal static readonly string[] StatNames = { "Speed", "Acceleration", "Braking", "Handling" };

        /// <summary>The four bars for driving factors, each 0..1, stock at 0.45.</summary>
        internal static float[] Bars(TuningStats s)
        {
            s = s ?? new TuningStats();
            float handling = s.Grip * Mathf.Sqrt(Mathf.Max(0.01f, s.Spring * s.Damper)) * Mathf.Sqrt(Mathf.Max(0.01f, s.Steer));
            // A lighter car pulls away quicker, as more torque would.
            float pull = s.Torque / Mathf.Max(0.1f, s.Mass);
            return new[] { Bar(s.Gearing, 1.5f), Bar(pull, 1.2f), Bar(s.Brake, 0.9f), Bar(handling, 1.2f) };
        }

        private static float Bar(float factor, float weight) => Mathf.Clamp(0.45f + (factor - 1f) * weight, 0.05f, 1f);

        /// <summary>The factors of the parts really on the car (not a preview), from Chassis' tuning keys.</summary>
        internal static TuningStats RealStats(LandVehicle v)
        {
            TuningStats all = null;
            foreach (var kv in ChassisApi.Tuning(v))
            {
                if (!kv.Key.StartsWith("part.", StringComparison.OrdinalIgnoreCase) || kv.Value.Length == 0) continue;
                var p = ChassisApi.TuningPartById(kv.Value);
                if (p?.Stats != null) all = TuningStats.Combine(all, p.Stats);
            }
            return all ?? new TuningStats();
        }

        /// <summary>The factors with one stats set taken out (the part a new one replaces).</summary>
        internal static TuningStats Without(TuningStats all, TuningStats part)
        {
            if (part == null) return all;
            float D(float a, float b) => Mathf.Abs(b) < 0.0001f ? a : a / b;
            return new TuningStats
            {
                Torque = D(all.Torque, part.Torque), Gearing = D(all.Gearing, part.Gearing), Brake = D(all.Brake, part.Brake),
                Grip = D(all.Grip, part.Grip), Steer = D(all.Steer, part.Steer), Mass = D(all.Mass, part.Mass),
                Spring = D(all.Spring, part.Spring), Damper = D(all.Damper, part.Damper), Downforce = D(all.Downforce, part.Downforce),
                EnginePitch = D(all.EnginePitch, part.EnginePitch), RideHeight = all.RideHeight - part.RideHeight,
            };
        }
    }
}
