using System;
using System.Collections;
using System.Collections.Generic;
using Il2CppScheduleOne.Audio;
using Il2CppScheduleOne.DevUtilities;
using MelonLoader;
using UnityEngine;

namespace MarcosBodyshop.Shop
{
    /// <summary>
    /// The workshop's sounds: an air wrench when a part goes in (CC0, embedded as <c>fit.pcm</c>), the game's own
    /// respray sound for paint and tint, and the game's UK drill track while the menu is open. The game's clips are
    /// looked up by name in what the scene has loaded; nothing of the game ships with the mod.
    /// </summary>
    internal static class Sounds
    {
        private const string SprayClip = "spray three times";
        private const string MusicClip = "goat-uk-drill-music-20930";
        private const float MusicVolume = 0.5f, FadeSeconds = 0.8f;

        private static readonly Dictionary<string, AudioClip> _game = new Dictionary<string, AudioClip>(StringComparer.Ordinal);
        private static readonly HashSet<string> _missing = new HashSet<string>(StringComparer.Ordinal);
        private static AudioClip _fit;
        private static bool _fitTried;
        private static AudioSource _music;
        private static object _fade;

        /// <summary>The sound of one finished purchase: spray for paint and tint, the air wrench for everything else.</summary>
        internal static void Fitted(string category, string entry, Vector3 at)
        {
            bool spray = (category ?? "").IndexOf("paint", StringComparison.OrdinalIgnoreCase) >= 0
                         || (entry ?? "").StartsWith("tint", StringComparison.OrdinalIgnoreCase);
            var clip = spray ? Game(SprayClip) : Fit();
            if (clip == null && spray) clip = Fit();
            if (clip == null) { Core.Log.Warning("[Sound] no install sound for " + category + "/" + entry + " - the purchase stays silent."); return; }
            try
            {
                var cam = Camera.main;
                AudioSource.PlayClipAtPoint(clip, cam != null ? cam.transform.position : at, 0.9f);
                Core.Log.Msg("[Sound] " + category + "/" + entry + ": played '" + clip.name + "'.");
            }
            catch (Exception e) { Core.Log.Warning("[Sound] the install sound '" + clip.name + "' failed: " + e.Message); }
        }

        /// <summary>The menu opened: the drill track fades in on the game's music channel.</summary>
        internal static void MusicOn()
        {
            var clip = Game(MusicClip);
            if (clip == null) return;
            try
            {
                if (_music == null)
                {
                    var go = new GameObject("bodyshop_music");
                    UnityEngine.Object.DontDestroyOnLoad(go);
                    _music = go.AddComponent<AudioSource>();
                    _music.loop = true;
                    _music.spatialBlend = 0f;
                    _music.playOnAwake = false;
                    var am = Singleton<AudioManager>.Instance;
                    if (am != null && am.MusicMixer != null) _music.outputAudioMixerGroup = am.MusicMixer;
                    else Core.Log.Warning("[Sound] no music channel of the game - the workshop music ignores the music volume setting.");
                }
                _music.clip = clip;
                _music.volume = 0f;
                if (!_music.isPlaying) _music.Play();
                Fade(MusicVolume, false);
                Core.Log.Msg("[Sound] workshop music on ('" + clip.name + "').");
            }
            catch (Exception e) { Core.Log.Warning("[Sound] the workshop music failed to start: " + e.Message); }
        }

        /// <summary>The menu closed: the music fades out and stops.</summary>
        internal static void MusicOff()
        {
            if (_music == null || !_music.isPlaying) return;
            Fade(0f, true);
            Core.Log.Msg("[Sound] workshop music off.");
        }

        private static void Fade(float to, bool stop)
        {
            if (_fade != null) MelonCoroutines.Stop(_fade);
            _fade = MelonCoroutines.Start(FadeRoutine(to, stop));
        }

        private static IEnumerator FadeRoutine(float to, bool stop)
        {
            var src = _music;
            if (src == null) yield break;
            float from = src.volume, t = 0f;
            while (t < FadeSeconds && src != null)
            {
                t += Time.unscaledDeltaTime;
                src.volume = Mathf.Lerp(from, to, t / FadeSeconds);
                yield return null;
            }
            if (src == null) yield break;
            src.volume = to;
            if (stop) src.Stop();
            _fade = null;
        }

        private static AudioClip Fit()
        {
            if (_fit != null || _fitTried) return _fit;
            _fitTried = true;
            try
            {
                _fit = Builtin.HornShop.Pcm("bodyshop_fit", "fit");
                if (_fit != null) _fit.hideFlags |= HideFlags.DontUnloadUnusedAsset;
            }
            catch (Exception e) { Core.Log.Error("[Sound] making the install sound failed - parts go in silently: " + e); }
            return _fit;
        }

        /// <summary>A clip of the game by name, from what is loaded. Logged once when the game has none by that name.</summary>
        private static AudioClip Game(string name)
        {
            if (_game.TryGetValue(name, out var c) && c != null) return c;
            try
            {
                foreach (var clip in Resources.FindObjectsOfTypeAll<AudioClip>())
                    if (clip != null && clip.name == name) { _game[name] = clip; return clip; }
            }
            catch (Exception e) { Core.Log.Warning("[Sound] searching the game's clips for '" + name + "' failed: " + e.Message); return null; }
            if (_missing.Add(name)) Core.Log.Warning("[Sound] the game has no clip '" + name + "' loaded - that sound is skipped.");
            return null;
        }

        internal static void Reset()
        {
            _game.Clear();
            _missing.Clear();
            if (_music != null) UnityEngine.Object.Destroy(_music.gameObject);
            _music = null;
            _fade = null;
        }
    }
}
