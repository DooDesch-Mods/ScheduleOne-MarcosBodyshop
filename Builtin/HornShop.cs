using System;
using System.Collections.Generic;
using System.IO;
using Chassis.Api;
using Il2CppScheduleOne.Vehicles;
using MarcosBodyshop.Api;
using MarcosBodyshop.Config;
using UnityEngine;

namespace MarcosBodyshop.Builtin
{
    /// <summary>
    /// The Horn category: six horns for the Chassis "horn" key, which the driver sounds with G. Each is a CC0 recording
    /// (THIRD-PARTY-NOTICES.md), cut to one honk and embedded as 16-bit mono PCM at 22050 Hz. Picking a horn in the menu
    /// plays it once.
    /// </summary>
    internal static class HornShop
    {
        private const int Rate = 22050;

        private static readonly (string Name, string Label, string File)[] All =
        {
            ("bodyshop_clown", "Clown Horn", "clown"),
            ("bodyshop_duck", "Rubber Duck", "duck"),
            ("bodyshop_train", "Freight Train", "train"),
            ("bodyshop_fanfare", "Fanfare", "fanfare"),
            ("bodyshop_cucaracha", "La Cucaracha", "cucaracha"),
            ("bodyshop_bicycle", "Bicycle Horn", "bicycle"),
        };

        private static readonly Dictionary<string, AudioClip> _clips = new Dictionary<string, AudioClip>(StringComparer.OrdinalIgnoreCase);

        internal static void Register()
        {
            foreach (var h in All)
            {
                try
                {
                    var clip = Pcm(h.Name, h.File);
                    if (clip == null) continue;
                    clip.hideFlags |= HideFlags.DontUnloadUnusedAsset;
                    _clips[h.Name] = clip;
                    ChassisApi.RegisterSound(h.Name, clip);
                }
                catch (Exception e) { Core.Log.Error("[Horn] making '" + h.Name + "' failed - that horn is missing from the menu: " + e); }
            }
            Core.Log.Msg("[Horn] " + _clips.Count + " of " + All.Length + " horns registered with Chassis.");

            BodyshopApi.RegisterCategory(new BodyshopCategory
            {
                Id = "horn", Label = "Horn", Icon = "horn", Order = 250,
                Camera = Shots.Front,
                Note = v => { string h = ChassisApi.GetTuning(v, "horn"); if (h == null) return "Stock"; foreach (var x in All) if (x.Name == h) return x.Label; return "Custom"; },
                Entries = Entries,
            });
        }

        private static IEnumerable<BodyshopEntry> Entries(LandVehicle v)
        {
            yield return new BodyshopEntry
            {
                Id = "stock",
                Label = "Stock",
                Text = "Back to stock. Free, no refund.",
                Price = 0,
                Installed = x => ChassisApi.GetTuning(x, "horn") == null,
                Preview = x => ChassisApi.PreviewTuning(x, "horn", ""),
                EndPreview = x => ChassisApi.EndPreview(x, "horn"),
                Apply = x => ChassisApi.SetTuning(x, "horn", "") ? null : "The garage could not do it.",
            };
            foreach (var h in All)
            {
                if (!_clips.ContainsKey(h.Name)) continue;
                string name = h.Name;
                yield return new BodyshopEntry
                {
                    Id = name,
                    Label = h.Label,
                    Text = "Hold G to honk.",
                    Price = Prefs.Price("horn", 150),
                    Installed = x => string.Equals(ChassisApi.GetTuning(x, "horn"), name, StringComparison.OrdinalIgnoreCase),
                    Preview = x => { ChassisApi.PreviewTuning(x, "horn", name); Play(x, name); },
                    EndPreview = x => ChassisApi.EndPreview(x, "horn"),
                    Apply = x => ChassisApi.SetTuning(x, "horn", name) ? null : "The garage could not fit it.",
                };
            }
        }

        /// <summary>The horn once at the front of the car, on this machine only; a horn still sounding stops.</summary>
        private static void Play(LandVehicle v, string name)
        {
            if (!_clips.TryGetValue(name, out var clip) || clip == null) { Core.Log.Warning("[Horn] no clip '" + name + "' to play in the preview."); return; }
            try
            {
                Shop.Sounds.Horn(clip, v.transform.TransformPoint(new Vector3(0f, 0.6f, 1.8f)));
                Core.Log.Msg("[Horn] preview of '" + name + "' played.");
            }
            catch (Exception e) { Core.Log.Warning("[Horn] the preview of '" + name + "' could not play: " + e.Message); }
        }

        // ---- clips -------------------------------------------------------------------------------------------------

        /// <summary>16-bit mono PCM embedded as <c>MarcosBodyshop.Assets.sounds.&lt;file&gt;.pcm</c>.</summary>
        internal static AudioClip Pcm(string name, string file)
        {
            string resource = "MarcosBodyshop.Assets.sounds." + file + ".pcm";
            byte[] bytes;
            using (Stream s = typeof(HornShop).Assembly.GetManifestResourceStream(resource))
            {
                if (s == null) { Core.Log.Error("[Sound] embedded sound '" + resource + "' is missing - '" + name + "' stays silent."); return null; }
                using (var ms = new MemoryStream()) { s.CopyTo(ms); bytes = ms.ToArray(); }
            }
            int n = bytes.Length / 2;
            var data = new float[n];
            for (int i = 0; i < n; i++) data[i] = BitConverter.ToInt16(bytes, i * 2) / 32768f;
            var clip = AudioClip.Create(name, n, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
