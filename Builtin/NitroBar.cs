using System;
using Il2CppScheduleOne.DevUtilities;
using Il2CppScheduleOne.PlayerScripts;
using Il2CppScheduleOne.UI;
using UnityEngine;
using UnityEngine.UI;

namespace MarcosBodyshop.Builtin
{
    /// <summary>
    /// The nitro tank in the game's own stamina bar, the one the skateboard push drains: while the local player drives a
    /// car with the Nitrous Kit, the bar shows the tank. It shows while the tank burns or fills and fades out as vanilla
    /// does (1.5 s after the last change, over 0.5 s, <c>StaminaBar.StaminaShowTime</c>/<c>StaminaFadeTime</c>). The
    /// vanilla fill is white at 31 % alpha; for the tank it turns blue. Out of the car the bar goes back to the player's
    /// stamina and its own colour.
    /// </summary>
    internal static class NitroBar
    {
        private const float ShowTime = 1.5f, FadeTime = 0.5f;
        private static readonly Color Blue = new Color(0.3f, 0.62f, 1f, 0.85f);

        private static Image[] _fills;
        private static Color[] _fillColours;

        private static StaminaBar _bar;
        private static bool _driving, _missingLogged;
        private static float _shown = -1f, _changedAt;

        internal static void Reset()
        {
            _bar = null;
            _fills = null;
            _fillColours = null;
            _driving = false;
            _missingLogged = false;
            _shown = -1f;
        }

        /// <summary>Every frame while the local player drives a car with the kit; <paramref name="tank"/> is 0..1.</summary>
        internal static void Show(float tank)
        {
            var bar = Bar();
            if (bar == null) return;
            try
            {
                if (!_driving)
                {
                    _driving = true;
                    _changedAt = -ShowTime - FadeTime;
                    int tinted = Tint(bar);
                    Core.Log.Msg("[Nitro] the stamina bar shows the nitro tank (" + tinted + " fill(s) blue).");
                }
                if (Mathf.Abs(tank - _shown) > 0.0005f)
                {
                    _shown = tank;
                    _changedAt = Time.time;
                    foreach (var s in bar.Sliders) if (s != null) s.value = tank;
                }
                float since = Time.time - _changedAt;
                bar.Group.alpha = since < ShowTime ? 1f : Mathf.Clamp01(1f - (since - ShowTime) / FadeTime);
            }
            catch (Exception e)
            {
                Core.Log.Error("[Nitro] showing the tank in the stamina bar failed - the bar is left alone: " + e.Message);
                _bar = null;
                _missingLogged = true;
            }
        }

        /// <summary>The local player no longer drives a car with the kit: the bar shows stamina again and fades.</summary>
        internal static void Hide()
        {
            if (!_driving) return;
            _driving = false;
            _shown = -1f;
            var bar = _bar;
            if (bar == null) return;
            try
            {
                float stamina = PlayerSingleton<PlayerMovement>.InstanceExists
                    ? PlayerSingleton<PlayerMovement>.Instance.CurrentStaminaReserve / PlayerMovement.StaminaReserveMax : 1f;
                foreach (var s in bar.Sliders) if (s != null) s.value = stamina;
                bar.Group.alpha = 0f;
                if (_fills != null)
                    for (int i = 0; i < _fills.Length; i++) if (_fills[i] != null) _fills[i].color = _fillColours[i];
                Core.Log.Msg("[Nitro] the stamina bar shows stamina again.");
            }
            catch (Exception e) { Core.Log.Warning("[Nitro] handing the stamina bar back failed: " + e.Message); }
        }

        /// <summary>Turns the sliders' fill images blue and keeps their own colours for <see cref="Hide"/>.</summary>
        private static int Tint(StaminaBar bar)
        {
            if (_fills == null)
            {
                var sliders = bar.Sliders;
                _fills = new Image[sliders.Length];
                _fillColours = new Color[sliders.Length];
                for (int i = 0; i < sliders.Length; i++)
                {
                    var fill = sliders[i] != null && sliders[i].fillRect != null ? sliders[i].fillRect.GetComponent<Image>() : null;
                    _fills[i] = fill;
                    if (fill != null) _fillColours[i] = fill.color;
                    else Core.Log.Warning("[Nitro] stamina slider " + i + " has no fill image - that half stays white.");
                }
            }
            int n = 0;
            foreach (var fill in _fills) if (fill != null) { fill.color = Blue; n++; }
            return n;
        }

        private static StaminaBar Bar()
        {
            if (_bar != null) return _bar;
            if (_missingLogged) return null;
            _bar = UnityEngine.Object.FindObjectOfType<StaminaBar>(true);
            if (_bar == null || _bar.Sliders == null || _bar.Group == null)
            {
                _missingLogged = true;
                _bar = null;
                Core.Log.Warning("[Nitro] no stamina bar in the HUD - the nitro tank is not shown.");
            }
            return _bar;
        }
    }
}
