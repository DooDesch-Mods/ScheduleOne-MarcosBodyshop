using System;
using System.Collections.Generic;
using MelonLoader;
using UnityEngine;

[assembly: MelonInfo(typeof(MarcosBodyshop.Core), "Marco's Bodyshop", DooDesch.ModVersion.Current, "DooDesch", "https://github.com/DooDesch-Mods/ScheduleOne-MarcosBodyshop")]
[assembly: MelonGame("TVGS", "Schedule I")]
[assembly: MelonAdditionalDependencies("Chassis")]

namespace MarcosBodyshop
{
    /// <summary>
    /// MelonLoader entry point. Marco's repaint becomes a tuning garage for every car: paint, lights and looks, and
    /// the Chassis performance parts, in a menu on a Sideload surface. Other mods add to it through BodyshopApi.
    /// </summary>
    public sealed class Core : MelonMod
    {
        public static MelonLogger.Instance Log { get; private set; }

        private bool _inWorld;

        public override void OnInitializeMelon()
        {
            Log = LoggerInstance;
            Config.Prefs.Init();
            Localization.German.Register();   // before any L10n.T use: the built-in categories take their labels at registration
            Net.ShopNet.Apply(HarmonyInstance);
            Builtin.MarcoGarage.Register();
            Builtin.PaintShop.Register();
            Builtin.LooksShop.Register();
            Builtin.HornShop.Register();
            Builtin.PerformanceShop.Register();

            // No PatchAll here: MelonLoader applies the [HarmonyPatch] classes of a MelonMod itself.
            int patched = 0;
            foreach (var _ in HarmonyInstance.GetPatchedMethods()) patched++;
            Log.Msg("[Core] loaded; Harmony patches on " + patched + " method(s).");
        }

        public override void OnUpdate()
        {
            Later.Tick();
            if (!_inWorld) return;
            Shop.Session.Tick();
            Net.Sync.Tick();
            Builtin.Nitro.Tick();
            Builtin.MarcoGarage.Tick();
        }

        public override void OnLateUpdate()
        {
            if (!_inWorld) return;
            Builtin.Flames.LateTick();
            // After the flames, so a car's sound sources find the exhaust the flames were just built at.
            Builtin.NitroSound.Tick();
        }

        public override void OnSceneWasLoaded(int buildIndex, string sceneName)
        {
            bool world = sceneName == "Main";
            Shop.Session.SceneChanged(sceneName);
            if (world == _inWorld) return;
            _inWorld = world;
            if (world)
            {
                Shop.Marking.WorldLoaded();
                Shop.Sounds.WorldLoaded();
                Log.Msg("[Core] world loaded.");
                return;
            }
            Shop.Marking.WorldLeft();
            Builtin.MarcoGarage.WorldLeft();
            Net.Sync.Reset();
            Net.Payment.Reset();
            Builtin.Nitro.Reset();
            Shop.Sounds.Reset();
            Log.Msg("[Core] left the world (" + sceneName + ").");
        }
    }

    /// <summary>Runs an action a few frames later, on the main thread.</summary>
    internal static class Later
    {
        private static readonly List<(int frame, Action action)> _queue = new List<(int, Action)>();

        internal static void Frames(int frames, Action action) => _queue.Add((Time.frameCount + Math.Max(1, frames), action));

        internal static void Tick()
        {
            if (_queue.Count == 0) return;
            int now = Time.frameCount;
            for (int i = _queue.Count - 1; i >= 0; i--)
            {
                if (_queue[i].frame > now) continue;
                var a = _queue[i].action;
                _queue.RemoveAt(i);
                try { a(); }
                catch (Exception e) { Core.Log.Error("[Core] a delayed action failed: " + e); }
            }
        }
    }
}
