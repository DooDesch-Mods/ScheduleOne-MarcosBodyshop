using System;
using System.Collections;
using Il2CppScheduleOne.Building;
using Il2CppScheduleOne.DevUtilities;
using Il2CppScheduleOne.ItemFramework;
using Il2CppScheduleOne.Money;
using Il2CppScheduleOne.UI;
using MelonLoader;
using UnityEngine;

namespace MarcosBodyshop.Shop
{
    /// <summary>The fade around opening and buying, the workshop sound, and the game's notification line.</summary>
    internal static class Effects
    {
        internal static void FadeOut(float seconds)
        {
            try { Singleton<BlackOverlay>.Instance?.Open(seconds, null); }
            catch (Exception e) { Core.Log.Warning("[Fx] the fade to black failed - no fade: " + e.Message); }
        }

        internal static void FadeIn(float seconds)
        {
            try { Singleton<BlackOverlay>.Instance?.Close(seconds, null); }
            catch (Exception e) { Core.Log.Warning("[Fx] the fade back failed: " + e.Message); }
        }

        /// <summary>After a purchase: a short fade with the clank of the game's metal build sound and the cash sound.</summary>
        internal static void Workshop(Vector3 at) => MelonCoroutines.Start(WorkshopRoutine(at));

        private static IEnumerator WorkshopRoutine(Vector3 at)
        {
            FadeOut(0.2f);
            yield return new WaitForSecondsRealtime(0.2f);
            Clank(at);
            yield return new WaitForSecondsRealtime(0.35f);
            Clank(at);
            Cash();
            yield return new WaitForSecondsRealtime(0.35f);
            FadeIn(0.35f);
        }

        private static void Clank(Vector3 at)
        {
            try
            {
                var bm = NetworkSingleton<BuildManager>.Instance;
                if (bm == null) { Core.Log.Warning("[Fx] no BuildManager - no workshop sound."); return; }
                var cam = Camera.main;
                bm.PlayBuildSound(BuildableItemDefinition.EBuildSoundType.Metal, cam != null ? cam.transform.position : at);
            }
            catch (Exception e) { Core.Log.Warning("[Fx] the workshop sound failed: " + e.Message); }
        }

        private static void Cash()
        {
            try { NetworkSingleton<MoneyManager>.Instance?.PlayCashSound(); }
            catch (Exception e) { Core.Log.Warning("[Fx] the cash sound failed: " + e.Message); }
        }
    }

    /// <summary>A line in the game's notification list, for answers that come without a dialogue.</summary>
    internal static class Hud
    {
        internal static void Notify(string text)
        {
            try
            {
                var nm = Singleton<NotificationsManager>.Instance;
                if (nm == null) { Core.Log.Warning("[Hud] no notification list - '" + text + "' not shown."); return; }
                nm.SendNotification("Marco's Bodyshop", text, null, 5f, true);
                Core.Log.Msg("[Hud] notified: " + text);
            }
            catch (Exception e) { Core.Log.Warning("[Hud] the notification '" + text + "' failed: " + e.Message); }
        }
    }
}
