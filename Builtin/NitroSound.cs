using System;
using System.Collections.Generic;
using Il2CppScheduleOne.Audio;
using Il2CppScheduleOne.Core.Audio;
using MarcosBodyshop.Net;
using UnityEngine;

namespace MarcosBodyshop.Builtin
{
    /// <summary>
    /// The nitro's sound, on every machine and on the same host on/off as the flames (<see cref="Flames.OnNitro"/>):
    /// on plays the engage burst once and fades the boost loop in, off fades the loop out and plays the release hiss.
    /// The three sounds are synthesised by <c>Assets/sounds/nitro.py</c>. Each car gets one pair of 3D sources at its
    /// exhaust, one for the loop and one for the bursts, scaled every frame by the player's effects volume.
    /// </summary>
    internal static class NitroSound
    {
        private const string HostName = "bodyshop_nitro_sound";
        private const float LoopVolume = 0.55f, EngageVolume = 0.9f, ReleaseVolume = 0.8f;
        /// <summary>Seconds the loop takes to come in and to die away.</summary>
        private const float FadeIn = 0.15f, FadeOut = 0.2f;
        private const float Near = 4f, Far = 60f;

        private sealed class Car
        {
            internal GameObject Host;
            internal AudioSource Loop, Shot;
            /// <summary>The host was made; a missing host after that means the car and its sources are gone.</summary>
            internal bool Built;
            internal bool On, Engage, Release;
            internal float Level;
        }

        private static readonly Dictionary<int, Car> _cars = new Dictionary<int, Car>();
        private static AudioClip _engage, _loop, _release;
        private static bool _clipsTried, _clipsOk;

        /// <summary>Everyone: the car's nitro went on or off. The sources follow in <see cref="Tick"/>.</summary>
        internal static void Set(int vehicle, bool on)
        {
            if (!Clips()) return;
            if (!_cars.TryGetValue(vehicle, out var c))
            {
                if (!on) return;
                c = _cars[vehicle] = new Car();
            }
            if (c.On == on) return;
            c.On = on;
            c.Engage = on;
            // A car whose sources were never made has made no sound, so it has nothing to release.
            c.Release = !on && c.Built;
            Core.Log.Msg("[NitroSound] sound " + (on ? "on" : "off") + " for net " + vehicle + ".");
        }

        internal static void Tick()
        {
            if (_cars.Count == 0) return;
            float fx = Shop.Sounds.Setting(EAudioType.FX);
            float dt = Time.deltaTime;
            foreach (int id in new List<int>(_cars.Keys))
            {
                var c = _cars[id];
                if (c.Host == null)
                {
                    if (c.Built) { _cars.Remove(id); Core.Log.Msg("[NitroSound] net " + id + " is gone - its nitro sound went with it."); continue; }
                    if (!c.On) { _cars.Remove(id); continue; }
                    var lv = ShopNet.VehicleById(id);
                    if (lv == null) { _cars.Remove(id); Core.Log.Warning("[NitroSound] net " + id + " is not here - no nitro sound."); continue; }
                    try { Build(c, lv.transform, id); }
                    catch (Exception e)
                    {
                        _cars.Remove(id);
                        if (c.Host != null) UnityEngine.Object.Destroy(c.Host);
                        Core.Log.Error("[NitroSound] making the sources for net " + id + " failed - its nitro stays silent: " + e);
                        continue;
                    }
                }
                try
                {
                    c.Shot.volume = fx;
                    if (c.Engage)
                    {
                        c.Engage = false;
                        c.Shot.PlayOneShot(_engage, EngageVolume);
                        if (!c.Loop.isPlaying) c.Loop.Play();
                    }
                    if (c.Release)
                    {
                        c.Release = false;
                        c.Shot.PlayOneShot(_release, ReleaseVolume);
                    }
                    c.Level = Mathf.MoveTowards(c.Level, c.On ? 1f : 0f, dt / (c.On ? FadeIn : FadeOut));
                    c.Loop.volume = c.Level * LoopVolume * fx;
                    if (!c.On && c.Level <= 0f && c.Loop.isPlaying) c.Loop.Stop();
                }
                catch (Exception e)
                {
                    _cars.Remove(id);
                    if (c.Host != null) UnityEngine.Object.Destroy(c.Host);
                    Core.Log.Error("[NitroSound] playing the nitro sound of net " + id + " failed - its sources are removed: " + e);
                }
            }
        }

        /// <summary>The source pair under the car root, at the exhaust when the flames found one, else at the root.</summary>
        private static void Build(Car c, Transform car, int id)
        {
            c.Host = new GameObject(HostName);
            c.Built = true;
            c.Host.transform.SetParent(car, false);
            bool exhaust = Flames.TryExhaust(id, out var local);
            c.Host.transform.localPosition = exhaust ? local : Vector3.zero;
            c.Loop = Source(c.Host);
            c.Loop.clip = _loop;
            c.Loop.loop = true;
            c.Loop.volume = 0f;
            c.Shot = Source(c.Host);
            Core.Log.Msg("[NitroSound] net " + id + ": sources made at the " + (exhaust ? "exhaust" : "car root") + ".");
        }

        private static AudioSource Source(GameObject host)
        {
            var src = host.AddComponent<AudioSource>();
            src.playOnAwake = false;
            src.spatialBlend = 1f;
            src.rolloffMode = AudioRolloffMode.Logarithmic;
            src.minDistance = Near;
            src.maxDistance = Far;
            src.dopplerLevel = 0f;
            Shop.Sounds.Mixer(src, false);
            return src;
        }

        /// <summary>The three clips, made once per game start. False when one is missing; then nitro stays silent.</summary>
        private static bool Clips()
        {
            if (_clipsTried) return _clipsOk;
            _clipsTried = true;
            try
            {
                _engage = Clip("bodyshop_nitro_engage", "nitro-engage");
                _loop = Clip("bodyshop_nitro_loop", "nitro-loop");
                _release = Clip("bodyshop_nitro_release", "nitro-release");
                _clipsOk = _engage != null && _loop != null && _release != null;
                if (_clipsOk) Core.Log.Msg("[NitroSound] nitro sounds loaded.");
                else Core.Log.Error("[NitroSound] a nitro sound is missing - nitro stays silent.");
            }
            catch (Exception e) { Core.Log.Error("[NitroSound] making the nitro sounds failed - nitro stays silent: " + e); }
            return _clipsOk;
        }

        private static AudioClip Clip(string name, string file)
        {
            var clip = HornShop.Pcm(name, file);
            if (clip != null) clip.hideFlags |= HideFlags.DontUnloadUnusedAsset;
            return clip;
        }

        internal static string Describe()
        {
            int on = 0;
            foreach (var c in _cars.Values) if (c.On) on++;
            return "nitro sound on " + on + " of " + _cars.Count + " tracked car(s)";
        }

        /// <summary>Leaving the world: every car's sources go. The clips stay loaded for the next world.</summary>
        internal static void Reset()
        {
            foreach (var c in _cars.Values)
                if (c.Host != null) UnityEngine.Object.Destroy(c.Host);
            _cars.Clear();
        }
    }
}
