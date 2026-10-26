using System;
using System.Collections;
using System.Collections.Generic;
using Il2CppScheduleOne.Audio;
using Il2CppScheduleOne.Core.Audio;
using Il2CppScheduleOne.DevUtilities;
using MelonLoader;
using UnityEngine;

namespace MarcosBodyshop.Shop
{
    /// <summary>
    /// The workshop's sounds: an air wrench when a part goes in (CC0, embedded as <c>fit.pcm</c>), the game's own
    /// respray sound for paint and tint, and the game's UK drill track from a radio in Marco's hall while the menu is
    /// open. The game's clips and the radio's model are looked up in what the scene has loaded; nothing of the game
    /// ships with the mod.
    ///
    /// The game scales every source by the player's volume settings itself (<c>AudioSourceController</c>), not in the
    /// mixer, so each source here does the same: music by the music volume, the rest by the effects volume.
    /// </summary>
    internal static class Sounds
    {
        private const string SprayClip = "spray three times";
        private const string MusicClip = "goat-uk-drill-music-20930";
        private const string RadioModel = "SM_Item_Radio_01";
        private const float MusicVolume = 0.35f, FadeSeconds = 0.8f, FitVolume = 0.9f, HornVolume = 0.8f;
        private const float RadioNear = 3f, RadioFar = 28f;

        /// <summary>Where the radio stands: on Marco's desk, left of the keyboard, its front towards the hall.</summary>
        internal static Vector3 RadioAt = new Vector3(8.72f, 1.07f, -8.72f);
        internal static float RadioYaw = 195f;

        // Known places of the vanilla radio, cheap to find by path; the full search is the fallback.
        private static readonly string[] RadioPaths =
        {
            "@Properties/RV/RV/SM_Item_Radio_01",
            "Map/Hyland Point/Region_Northtown/Pawn shop/SM_Item_Radio_01",
            "@Managers/@Intro/Container/RV/SM_Item_Radio_01",
        };

        private static readonly Dictionary<string, AudioClip> _game = new Dictionary<string, AudioClip>(StringComparer.Ordinal);
        private static readonly HashSet<string> _missing = new HashSet<string>(StringComparer.Ordinal);
        private static AudioClip _fit;
        private static bool _fitTried;
        private static GameObject _radio;
        private static AudioSource _music;
        private static AudioSource _horn;
        private static float _level, _target;
        private static object _musicLoop;

        /// <summary>The player's volume for this kind of sound, master included. 1 before the game's audio exists.</summary>
        internal static float Setting(EAudioType type)
        {
            try
            {
                if (Singleton<AudioManager>.InstanceExists) return Singleton<AudioManager>.Instance.GetVolume(type, true);
            }
            catch (Exception e) { Core.Log.Warning("[Sound] reading the " + type + " volume failed - full volume: " + e.Message); }
            return 1f;
        }

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
                AudioSource.PlayClipAtPoint(clip, cam != null ? cam.transform.position : at, FitVolume * Setting(EAudioType.FX));
                Core.Log.Msg("[Sound] " + category + "/" + entry + ": played '" + clip.name + "'.");
            }
            catch (Exception e) { Core.Log.Warning("[Sound] the install sound '" + clip.name + "' failed: " + e.Message); }
        }

        /// <summary>
        /// A horn in the menu's preview. One source for all of them: picking the next horn cuts the one still sounding.
        /// </summary>
        internal static void Horn(AudioClip clip, Vector3 at)
        {
            if (clip == null) return;
            if (_horn == null)
            {
                var go = new GameObject("bodyshop_horn_preview");
                UnityEngine.Object.DontDestroyOnLoad(go);
                _horn = go.AddComponent<AudioSource>();
                _horn.playOnAwake = false;
                _horn.spatialBlend = 1f;
                _horn.rolloffMode = AudioRolloffMode.Logarithmic;
                _horn.minDistance = 3f;
                _horn.maxDistance = 40f;
                Mixer(_horn, false);
            }
            if (_horn.isPlaying) Core.Log.Msg("[Sound] horn preview '" + (_horn.clip != null ? _horn.clip.name : "?") + "' cut for '" + clip.name + "'.");
            _horn.Stop();
            _horn.transform.position = at;
            _horn.clip = clip;
            _horn.volume = HornVolume * Setting(EAudioType.FX);
            _horn.Play();
        }

        /// <summary>The world is in: the radio goes on its shelf, so it stands there before anyone opens the menu.</summary>
        internal static void WorldLoaded()
        {
            try { Radio(); }
            catch (Exception e) { Core.Log.Error("[Sound] placing the workshop radio failed - the music plays from the menu's spot: " + e); }
        }

        /// <summary>The menu opened: the drill track fades in from the radio in the hall.</summary>
        internal static void MusicOn()
        {
            var clip = Game(MusicClip);
            if (clip == null) return;
            try
            {
                if (_music == null)
                {
                    var host = Radio();
                    _music = host.AddComponent<AudioSource>();
                    _music.loop = true;
                    _music.playOnAwake = false;
                    _music.spatialBlend = 1f;
                    _music.rolloffMode = AudioRolloffMode.Logarithmic;
                    _music.minDistance = RadioNear;
                    _music.maxDistance = RadioFar;
                    _music.dopplerLevel = 0f;
                    Mixer(_music, true);
                }
                _music.clip = clip;
                _target = MusicVolume;
                if (!_music.isPlaying) { _level = 0f; _music.volume = 0f; _music.Play(); }
                if (_musicLoop == null) _musicLoop = MelonCoroutines.Start(MusicLoop());
                Core.Log.Msg("[Sound] workshop radio on ('" + clip.name + "', music volume " + Setting(EAudioType.Music).ToString("F2") + ").");
            }
            catch (Exception e) { Core.Log.Warning("[Sound] the workshop radio failed to start: " + e.Message); }
        }

        /// <summary>The menu closed: the radio fades out and stops.</summary>
        internal static void MusicOff()
        {
            if (_music == null || !_music.isPlaying) return;
            _target = 0f;
            Core.Log.Msg("[Sound] workshop radio off.");
        }

        /// <summary>
        /// While the radio plays: fades towards the target and follows the music volume setting, so moving the slider
        /// in the settings applies at once.
        /// </summary>
        private static IEnumerator MusicLoop()
        {
            while (_music != null && _music.isPlaying)
            {
                _level = Mathf.MoveTowards(_level, _target, Time.unscaledDeltaTime * MusicVolume / FadeSeconds);
                _music.volume = _level * Setting(EAudioType.Music);
                if (_target <= 0f && _level <= 0f) { _music.Stop(); break; }
                yield return null;
            }
            _musicLoop = null;
        }

        /// <summary>Routes a source into the game's music or game mixer channel, where the game's own sounds of that kind play.</summary>
        internal static void Mixer(AudioSource src, bool music)
        {
            var am = Singleton<AudioManager>.InstanceExists ? Singleton<AudioManager>.Instance : null;
            var group = am == null ? null : music ? am.MusicMixer : am.MainGameMixer;
            if (group != null) src.outputAudioMixerGroup = group;
            else Core.Log.Warning("[Sound] no " + (music ? "music" : "game") + " channel of the game for '" + src.gameObject.name + "'.");
        }

        /// <summary>
        /// The radio in Marco's hall: a copy of the game's radio model (mesh and materials only), or an empty object at
        /// the same spot when the game has none loaded.
        /// </summary>
        private static GameObject Radio()
        {
            if (_radio != null) return _radio;
            _radio = new GameObject("bodyshop_radio");
            _radio.transform.SetPositionAndRotation(RadioAt, Quaternion.Euler(0f, RadioYaw, 0f));
            var model = RadioSource();
            if (model == null)
            {
                Core.Log.Warning("[Sound] the game's radio model '" + RadioModel + "' is not loaded - the music plays from the spot without a radio.");
                return _radio;
            }
            _radio.transform.localScale = model.transform.lossyScale;
            _radio.AddComponent<MeshFilter>().sharedMesh = model.GetComponent<MeshFilter>().sharedMesh;
            var r = _radio.AddComponent<MeshRenderer>();
            r.sharedMaterials = model.GetComponent<MeshRenderer>().sharedMaterials;
            Core.Log.Msg("[Sound] workshop radio placed at " + RadioAt + " (model from '" + model.name + "').");
            return _radio;
        }

        /// <summary>Moves the radio, and the music with it, to a new spot until the game restarts.</summary>
        internal static string Place(Vector3 at, float yaw)
        {
            RadioAt = at;
            RadioYaw = yaw;
            Radio().transform.SetPositionAndRotation(at, Quaternion.Euler(0f, yaw, 0f));
            return "radio at " + at.ToString("F2") + ", yaw " + yaw.ToString("F0");
        }

        private static GameObject RadioSource()
        {
            foreach (var path in RadioPaths)
            {
                var go = GameObject.Find(path);
                if (Usable(go)) return go;
            }
            try
            {
                foreach (var mf in Resources.FindObjectsOfTypeAll<MeshFilter>())
                    if (mf != null && mf.gameObject.name == RadioModel && Usable(mf.gameObject)) return mf.gameObject;
            }
            catch (Exception e) { Core.Log.Warning("[Sound] searching the game's radio model failed: " + e.Message); }
            return null;
        }

        private static bool Usable(GameObject go)
            => go != null && go.GetComponent<MeshFilter>()?.sharedMesh != null && go.GetComponent<MeshRenderer>() != null;

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
            if (_musicLoop != null) MelonCoroutines.Stop(_musicLoop);
            _musicLoop = null;
            if (_radio != null) UnityEngine.Object.Destroy(_radio);
            if (_horn != null) UnityEngine.Object.Destroy(_horn.gameObject);
            _radio = null;
            _music = null;
            _horn = null;
            _level = _target = 0f;
        }
    }
}
