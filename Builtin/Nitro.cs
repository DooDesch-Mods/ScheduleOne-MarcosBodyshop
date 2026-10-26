using System;
using System.Collections.Generic;
using System.Globalization;
using Chassis.Api;
using HarmonyLib;
using Il2CppScheduleOne;
using Il2CppScheduleOne.PlayerScripts;
using Il2CppScheduleOne.Vehicles;
using MarcosBodyshop.Config;
using MarcosBodyshop.Net;
using UnityEngine;

namespace MarcosBodyshop.Builtin
{
    /// <summary>
    /// The Nitrous Kit (booster slot): while the driver holds the nitro key and drives forward, the car pulls harder and
    /// runs faster by <see cref="Prefs.NitroBoost"/>, from a tank that empties in <see cref="Prefs.NitroSeconds"/> and
    /// refills by itself. Blue flames burn and the nitro hisses at the car's exhaust on every machine
    /// (<see cref="Flames"/>, <see cref="NitroSound"/>); the driver's on/off goes through the
    /// host (<c>req.nitro vehicle 1|0</c> -> <c>nitro vehicle 1|0</c>). The wheelchair of Rollin keeps its own nitro.
    /// </summary>
    internal static class Nitro
    {
        internal const string PartId = "bodyshop_nitro";
        private const string Wheelchair = "rollin_wheelchair";
        /// <summary>How fast the boost comes in and fades, in factor per second.</summary>
        private const float FactorRate = 3f;
        /// <summary>Seconds after the key is let go before the tank starts to refill.</summary>
        private const float RechargeDelay = 1.5f;
        /// <summary>
        /// Extra forward acceleration at full boost, in m/s^2 per 1.0 of factor above 1. A car's tyres are at their grip
        /// limit when it pulls away, so more torque alone only spins them; this push is what the driver feels.
        /// </summary>
        private const float PushPerFactor = 10f;

        private static int _car = -1;
        private static float _factor = 1f;
        private static bool _on;
        private static float _sinceNitro = RechargeDelay;
        private static int _shownStep = -1;
        private static readonly Dictionary<int, float> _tank = new Dictionary<int, float>();
        /// <summary>Physics steps the boost drove the car since the last burn started.</summary>
        private static int _boostSteps;

        /// <summary>Nitro held by the console command until this time, without key or throttle.</summary>
        internal static float ForceUntil = -1f;

        internal static void Register()
        {
            ChassisApi.RegisterTuningPart(new TuningPart { Id = PartId, Slot = "booster", Label = "Nitrous Kit", Price = 5000 });
            Hint("Nitro");
        }

        private static void Hint(string label) =>
            ChassisApi.AddKeyHint("bodyshop.nitro", label, c => c.Driver && Fitted(c.Vehicle), () => Prefs.NitroKey);

        internal static bool Fitted(LandVehicle lv) =>
            lv != null && !string.Equals(ChassisApi.CodeOf(lv), Wheelchair, StringComparison.OrdinalIgnoreCase)
            && string.Equals(ChassisApi.InstalledPart(lv, "booster"), PartId, StringComparison.OrdinalIgnoreCase);

        internal static void Reset()
        {
            _car = -1;
            _factor = 1f;
            _on = false;
            _sinceNitro = RechargeDelay;
            _shownStep = -1;
            _tank.Clear();
            ForceUntil = -1f;
            Flames.Reset();
        }

        /// <summary>The car the local player drives, or null.</summary>
        private static LandVehicle Driven()
        {
            var nob = Player.Local != null ? Player.Local.CurrentVehicle : null;
            if (nob == null) return null;
            var lv = nob.GetComponent<LandVehicle>();
            return lv != null && lv.LocalPlayerIsDriver ? lv : null;
        }

        internal static void Tick()
        {
            var lv = Driven();
            int id = lv != null && Fitted(lv) ? ShopNet.IdOf(lv) : -1;
            if (id != _car)
            {
                if (_on && _car >= 0) Send(_car, false);
                _on = false;
                _factor = 1f;
                _shownStep = -1;
                if (id >= 0) Core.Log.Msg("[Nitro] driving net " + id + " with the Nitrous Kit, tank " + Pct(Tank(id)) + ", key " + Prefs.NitroKey + ".");
                else if (_car >= 0) Core.Log.Msg("[Nitro] no longer driving net " + _car + ".");
                _car = id;
            }
            if (id < 0) return;

            float dt = Time.deltaTime;
            bool forced = Time.time < ForceUntil;
            bool input = !GameInput.IsTyping && !Shop.Session.IsOpen;
            bool held = (input && Input.GetKey(Prefs.NitroKey) && GameInput.VehicleDriveAxis > 0.01f) || forced;
            float tank = Tank(id);
            bool want = held && tank > 0f;
            if (want)
            {
                tank = Mathf.Max(0f, tank - dt / Prefs.NitroSeconds);
                _sinceNitro = 0f;
            }
            else
            {
                _sinceNitro += dt;
                if (_sinceNitro >= RechargeDelay && tank < 1f) tank = Mathf.Min(1f, tank + dt / Prefs.NitroRecharge);
            }
            _tank[id] = tank;
            if (want != _on)
            {
                _on = want;
                if (want) _boostSteps = 0;
                Send(id, want);
                Core.Log.Msg("[Nitro] net " + id + " nitro " + (want ? "on" : "off") + ", tank " + Pct(tank) + (!want && tank <= 0f ? " (empty)" : "")
                    + (want ? "" : ", boosted " + _boostSteps + " physics steps") + ".");
            }
            _factor = Mathf.MoveTowards(_factor, _on ? Prefs.NitroBoost : 1f, dt * FactorRate);

            int step = Mathf.RoundToInt(tank * 10f);
            if (step != _shownStep) { _shownStep = step; Hint("Nitro " + (step * 10) + "%"); }
        }

        private static void Send(int id, bool on) => ShopNet.ToHost("req.nitro", id.ToString(CultureInfo.InvariantCulture), on ? "1" : "0");

        /// <summary>Host: a driver switched nitro; every machine shows the flames.</summary>
        internal static void HostNitro(int vehicle, bool on)
        {
            var lv = ShopNet.VehicleById(vehicle);
            if (lv == null) { Core.Log.Warning("[Nitro] nitro for net " + vehicle + ", which is not here - dropped."); return; }
            if (on && !Fitted(lv)) { Core.Log.Warning("[Nitro] nitro for net " + vehicle + ", which has no Nitrous Kit - dropped."); return; }
            ShopNet.ToAll("nitro", vehicle.ToString(CultureInfo.InvariantCulture), on ? "1" : "0");
        }

        internal static string Describe()
        {
            return "nitro: " + (_car < 0 ? "not driving a car with the Nitrous Kit" : "net " + _car + " x" + F(_factor) + (_on ? ", burning" : "") + ", boosted " + _boostSteps + " steps, tank " + Pct(Tank(_car)))
                + "; key " + Prefs.NitroKey + ", boost x" + F(Prefs.NitroBoost) + ", " + F(Prefs.NitroSeconds) + " s, refill " + F(Prefs.NitroRecharge) + " s; " + Flames.Describe();
        }

        private static float Tank(int id) => _tank.TryGetValue(id, out float t) ? t : 1f;
        private static string F(float v) => v.ToString("0.##", CultureInfo.InvariantCulture);
        private static string Pct(float t) => Mathf.RoundToInt(t * 100f) + "%";

        /// <summary>
        /// Recomputes the drive torque vanilla just set, with the curve stretched by the factor: pull = throttle x factor
        /// x curve(speed / factor), and pushes the body forward while a driven wheel touches the ground and the stretched
        /// curve still pulls. Only forward; reversing and braking stay the car's own.
        /// </summary>
        [HarmonyPatch(typeof(LandVehicle), "ApplyThrottle")]
        private static class ThrottlePatch
        {
            private static bool _failed;

            private static void Postfix(LandVehicle __instance)
            {
                if (_failed || _car < 0 || Mathf.Abs(_factor - 1f) < 0.001f || !__instance.LocalPlayerIsDriver) return;
                try
                {
                    if (ShopNet.IdOf(__instance) != _car) return;
                    float thr = __instance.currentThrottle;
                    if (thr <= 0f || __instance.HandbrakeApplied) return;
                    float speed = __instance.Speed_Kmh;
                    // Vanilla only drives in this case; otherwise it brakes, and the brake stays its own.
                    if (speed < -4f) return;
                    float at = Mathf.Abs(speed) / _factor;
                    float torque = thr * _factor * __instance.motorTorque.Evaluate(at) * __instance.diffGearing / 2f;
                    bool grounded = false;
                    foreach (var wc in __instance.driveWheels)
                    {
                        if (wc == null) continue;
                        wc.motorTorque = torque;
                        grounded |= wc.isGrounded;
                    }
                    if (grounded && torque > 0f && __instance.Rb != null)
                        __instance.Rb.AddForce(__instance.transform.forward * (thr * (_factor - 1f) * PushPerFactor), ForceMode.Acceleration);
                    _boostSteps++;
                }
                catch (Exception e)
                {
                    _failed = true;
                    Core.Log.Error("[Nitro] scaling the drive torque failed - nitro gives no boost from now on: " + e);
                }
            }
        }
    }
}
