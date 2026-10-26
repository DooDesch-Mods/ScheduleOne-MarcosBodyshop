using System;
using System.Reflection;
using HarmonyLib;
using Il2CppScheduleOne.DevUtilities;
using Il2CppScheduleOne.Dialogue;
using Il2CppScheduleOne.NPCs;
using Il2CppScheduleOne.NPCs.CharacterClasses;
using Il2CppScheduleOne.UI;
using Il2CppScheduleOne.Vehicles;
using MarcosBodyshop.Api;
using MarcosBodyshop.Shop;
using UnityEngine;
using UnityEngine.Events;

namespace MarcosBodyshop.Builtin
{
    /// <summary>
    /// Marco's garage at the gas station. The hall bay is vanilla's: the vehicle the shop's detector sees, parked on
    /// the repaint spot, seen from the repaint camera. The yard bay in front of the hall is for vehicles too big for
    /// the hall (the hall's entrance is 3.6 x 4.0 m): the lot in front of it, without markings, seen from the vehicle's default shot.
    ///
    /// Marco's "I'd like to repaint my vehicle" becomes "Modify my vehicle" and opens the bodyshop instead of the
    /// vanilla paint menu. Its check (vehicle parked, nobody inside) now looks at both bays.
    /// </summary>
    internal static class MarcoGarage
    {
        private const string VanillaChoice = "I'd like to repaint my vehicle";
        private const string ChoiceText = "Modify my vehicle";

        // The lot in front of the hall, measured in game: from the hall's wall over the parking stalls to the
        // pavement, and from the hall's corner to the pumps. A vehicle whose centre stands on it is in the bay.
        internal static readonly Vector3 YardCenter = new Vector3(10.05f, 1.6f, -18.7f);
        internal const float YardYaw = 75f;
        internal static readonly Vector3 YardSize = new Vector3(9.5f, 5f, 16f);

        private static Marco _marco;
        private static bool _choiceReplaced;
        private static float _nextTry;

        internal static void Register()
        {
            var g = new BodyshopGarage { Id = BodyshopApi.MarcoGarage, Name = "Marco's Bodyshop" };
            g.Bays.Add(new BodyshopBay
            {
                Id = "hall",
                FindVehicle = () => Marco()?.VehicleDetector?.closestVehicle,
                ParkAt = _ => ModMenu()?.VehiclePosition,
                Camera = HallShot,
            });
            g.Bays.Add(new BodyshopBay
            {
                Id = "yard",
                Center = YardCenter, Size = YardSize, Yaw = YardYaw,
            });
            BodyshopApi.RegisterGarage(g);
            BodyshopApi.Purchased += (v, cat, entry) =>
            {
                if (Session.Garage != BodyshopApi.MarcoGarage) return;
                try { ((NPC)Marco())?.DialogueHandler?.ShowWorldspaceDialogue_5s("Thanks buddy"); }
                catch (Exception e) { Core.Log.Warning("[Marco] his thank-you line failed: " + e.Message); }
            };
        }

        private static CameraShot HallShot(LandVehicle v)
        {
            var cam = ModMenu()?.CameraPosition;
            if (cam == null) return Session.DefaultShot(v);
            return new CameraShot(cam.position, Session.Bounds(v).center, 60f);
        }

        private static VehicleModMenu ModMenu() => Singleton<VehicleModMenu>.InstanceExists ? Singleton<VehicleModMenu>.Instance : null;

        internal static Marco Marco()
        {
            if (_marco != null) return _marco;
            var all = UnityEngine.Object.FindObjectsOfType<Marco>();
            _marco = all != null && all.Length > 0 ? all[0] : null;
            return _marco;
        }

        internal static void WorldLeft()
        {
            _marco = null;
            _choiceReplaced = false;
            _nextTry = 0f;
        }

        /// <summary>Every two seconds until done: Marco spawns late, his dialogue choices exist from Awake.</summary>
        internal static void Tick()
        {
            if (_choiceReplaced || Time.unscaledTime < _nextTry) return;
            _nextTry = Time.unscaledTime + 2f;
            var m = Marco();
            if (m == null) return;
            try
            {
                var dc = ((NPC)m).DialogueHandler?.GetComponent<DialogueController>();
                if (dc == null) { Core.Log.Warning("[Marco] Marco has no dialogue controller yet."); return; }
                var choices = dc.Choices;
                for (int i = 0; i < choices.Count; i++)
                {
                    var c = choices[i];
                    if (c == null || (c.ChoiceText != VanillaChoice && c.ChoiceText != ChoiceText)) continue;
                    c.ChoiceText = ChoiceText;
                    c.onChoosen = new UnityEvent();
                    c.onChoosen.AddListener((Action)OnChosen);
                    _choiceReplaced = true;
                    Core.Log.Msg("[Marco] repaint choice replaced by '" + ChoiceText + "'.");
                    return;
                }
                Core.Log.Error("[Marco] Marco has no '" + VanillaChoice + "' choice - the bodyshop cannot be opened from his dialogue.");
                _choiceReplaced = true;
            }
            catch (Exception e) { Core.Log.Error("[Marco] replacing the repaint choice failed: " + e); }
        }

        private static void OnChosen()
        {
            if (!BodyshopApi.Open(BodyshopApi.MarcoGarage, out string reason))
            {
                Core.Log.Msg("[Marco] the bodyshop did not open: " + reason);
                try { ((NPC)Marco())?.DialogueHandler?.ShowWorldspaceDialogue_5s(reason); }
                catch (Exception e) { Core.Log.Warning("[Marco] his answer line failed: " + e.Message); }
            }
        }

        /// <summary>
        /// Marco's own check of the choice. Vanilla wants a vehicle in the hall; the bodyshop also takes the yard.
        /// The method is called through a delegate, so IL2CPP cannot inline it into the dialogue.
        /// </summary>
        [HarmonyPatch(typeof(Marco), "RepaintVehicleValid")]
        internal static class ValidPatch
        {
            private static bool Prefix(ref string __0, ref bool __result)
            {
                try
                {
                    var v = Registry.VehicleIn(BodyshopApi.MarcoGarage, out string reason, out _);
                    __result = v != null;
                    __0 = reason ?? string.Empty;
                    return false;
                }
                catch (Exception e)
                {
                    Core.Log.Error("[Marco] the repaint check failed - vanilla decides: " + e.Message);
                    return true;
                }
            }
        }
    }
}
