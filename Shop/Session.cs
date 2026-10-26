using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using Chassis.Api;
using Il2CppScheduleOne;
using Il2CppScheduleOne.DevUtilities;
using Il2CppScheduleOne.PlayerScripts;
using Il2CppScheduleOne.State;
using Il2CppScheduleOne.Vehicles;
using MarcosBodyshop.Api;
using MarcosBodyshop.Net;
using MelonLoader;
using Sideload.Api;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace MarcosBodyshop.Shop
{
    /// <summary>
    /// The local player's garage menu: the game UI state (mouse free, movement locked), the held camera, the page on
    /// a full-screen overlay canvas, and the preview of the picked entry.
    ///
    /// Opening asks the host for the garage first (one player at a time). Buying charges the buyer and asks the host;
    /// the host's answer arrives in <see cref="OnSettled"/>.
    ///
    /// The page talks through <c>shop.*</c> calls: start (the categories), list (a category's entries), pick (preview
    /// and camera), buy, leave (back out of a category) and close. The host sends <c>key</c>, <c>refresh</c>,
    /// <c>flash</c> and <c>cash</c>.
    /// </summary>
    internal static class Session
    {
        private const string SurfaceId = "marcosbodyshop";
        private const string Bundle = "MarcosBodyshop.Assets.bodyshop";
        // The game's own overlay canvases sort above small numbers and hide the menu; Sideload's surface test uses 31000.
        private const int SortingOrder = 30500;
        private const float CameraLerp = 0.6f;

        private static string _pendingGarage;
        private static int _pendingVehicle = -1;

        private static bool _open;
        private static bool _opening;
        private static bool _closeRequested;
        private static string _garage;
        private static BodyshopBay _bay;
        private static LandVehicle _vehicle;
        private static int _vehicleId = -1;

        private static GameObject _canvas;
        private static GameObject _stateObject;
        private static MonoState _state;
        private static SurfaceHandle _surface;
        private static bool _exitRegistered;

        private static BodyshopCategory _category;
        private static BodyshopEntry _previewed;
        private static string _previewedCategory;
        private static bool _busy;
        private static int _shownCash = int.MinValue;
        private static float _nextCash;
        private static bool _refreshWanted;
        private static bool _hooked;

        internal static bool IsOpen => _open;
        internal static LandVehicle Vehicle => _open ? _vehicle : null;
        internal static string Garage => _garage;

        /// <summary>Sends a key to the page as the keyboard does (console and tests).</summary>
        internal static void Key(string key) => _surface?.Emit("key", key);

        // ---- opening ----------------------------------------------------------------------------------------------

        /// <summary>The local player asks to open a garage's menu.</summary>
        internal static bool Request(string garageId, out string reason)
        {
            if (_open || _opening) { reason = "The menu is already open."; return false; }
            var v = Registry.VehicleIn(garageId, out reason, out var bay);
            if (v == null) { Core.Log.Msg("[Menu] garage '" + garageId + "' not opened: " + reason + "."); return false; }
            int id = ShopNet.IdOf(v);
            if (id < 0) { reason = "The vehicle is not ready yet."; Core.Log.Warning("[Menu] " + v.VehicleName + " has no network id - not opened."); return false; }
            string me = ShopNet.MyCode();
            if (me.Length == 0) { reason = "The player is not ready yet."; Core.Log.Warning("[Menu] the local player does not exist yet - not opened."); return false; }
            _pendingGarage = garageId;
            _pendingVehicle = id;
            ShopNet.ToHost("req.open", garageId, me, id.ToString(CultureInfo.InvariantCulture));
            reason = null;
            return true;
        }

        /// <summary>The host's answer to <see cref="Request"/>.</summary>
        internal static void OnOpenAnswer(string garage, int vehicleId, bool ok, string reason)
        {
            if (_pendingGarage == null || !string.Equals(garage, _pendingGarage, StringComparison.OrdinalIgnoreCase) || vehicleId != _pendingVehicle)
            {
                Core.Log.Warning("[Menu] an answer for garage '" + garage + "' arrived that this player did not ask for - ignored.");
                return;
            }
            _pendingGarage = null;
            if (!ok)
            {
                Core.Log.Msg("[Menu] the host refused garage '" + garage + "': " + reason);
                Hud.Notify(reason);
                return;
            }
            var v = ShopNet.VehicleById(vehicleId);
            var again = Registry.VehicleIn(garage, out string why, out var bay);
            if (v == null || again == null || ShopNet.IdOf(again) != vehicleId)
            {
                Core.Log.Warning("[Menu] garage '" + garage + "' granted, but the vehicle left the bay (" + (why ?? "another vehicle is there") + ") - not opened.");
                ShopNet.ToHost("req.close", garage, ShopNet.MyCode());
                Hud.Notify(why ?? "The vehicle moved.");
                return;
            }
            MelonCoroutines.Start(OpenRoutine(garage, bay, v, vehicleId));
        }

        private static IEnumerator OpenRoutine(string garage, BodyshopBay bay, LandVehicle v, int vehicleId)
        {
            _opening = true;
            Effects.FadeOut(0.3f);
            yield return new WaitForSecondsRealtime(0.4f);
            try { OpenNow(garage, bay, v, vehicleId); }
            catch (Exception e)
            {
                Core.Log.Error("[Menu] opening garage '" + garage + "' failed: " + e);
                Close("the menu failed to open");
            }
            _opening = false;
            Effects.FadeIn(0.3f);
        }

        private static void OpenNow(string garage, BodyshopBay bay, LandVehicle v, int vehicleId)
        {
            var pc = PlayerSingleton<PlayerCamera>.Instance;
            if (pc == null) throw new InvalidOperationException("no player camera");
            if (!Surfaces.Available) throw new InvalidOperationException("Sideload with surfaces is not installed");
            if (SceneState.Current == null) throw new InvalidOperationException("no scene state");
            Hook();

            _garage = garage;
            _bay = bay;
            _vehicle = v;
            _vehicleId = vehicleId;
            _category = null;
            _previewed = null;
            _busy = false;

            if (bay.ParkAt != null)
            {
                Transform at = null;
                try { at = bay.ParkAt(v); }
                catch (Exception e) { Core.Log.Error("[Menu] ParkAt of bay '" + bay.Id + "' failed - the vehicle stays where it is: " + e.Message); }
                if (at != null) v.AlignTo(at, EParkingAlignment.RearToKerb, true);
            }

            // The game's own UI state: mouse free, movement and look locked, HUD, compass and inventory hidden.
            _stateObject = new GameObject("MarcosBodyshop_State");
            _state = _stateObject.AddComponent<MonoState>();
            _state.Properties = new StateProperties(
                StateProperties.EMouseState.Free, StateProperties.ECrosshairState.Hidden,
                StateProperties.EEquippingState.Disabled, StateProperties.EInventoryState.Disabled,
                StateProperties.EHUDState.Hidden, StateProperties.ECompassState.Hidden,
                StateProperties.EMovementState.Locked, StateProperties.ELookState.Locked,
                StateProperties.EBlurState.Disabled);
            _state.InitializeDefaultParent(SceneState.Current.InGame);
            _state.PushToDefaultParent();
            RegisterExit();

            _canvas = new GameObject("MarcosBodyshop_Menu");
            UnityEngine.Object.DontDestroyOnLoad(_canvas);
            var canvas = _canvas.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = SortingOrder;
            _canvas.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            _canvas.AddComponent<GraphicRaycaster>();
            var rect = new GameObject("page").AddComponent<RectTransform>();
            rect.SetParent(_canvas.transform, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;

            // A short side of 1080 css px: the page is written for a 1080p frame and scales with the screen.
            _surface = Surfaces.Mount(rect, SurfaceId, Bundle, 1080f);
            if (_surface == null) throw new InvalidOperationException("the surface did not mount");
            _surface.OnCall("shop.start", _ => Safe("start", () => Model.Root(_garage, _vehicle)))
                    .OnCall("shop.list", id => Safe("list", () => List(id)))
                    .OnCall("shop.pick", arg => Safe("pick", () => Pick(arg)))
                    .OnCall("shop.leave", _ => Safe("leave", () => Leave()))
                    .OnCall("shop.buy", arg => Safe("buy", () => Buy(arg)))
                    .OnCall("shop.close", _ => { _closeRequested = true; return "ok"; });

            _open = true;
            _closeRequested = false;
            _shownCash = int.MinValue;
            MoveCamera(null, true);
            Core.Log.Msg("[Menu] open: garage '" + garage + "', bay '" + bay.Id + "', " + v.VehicleName + " (net " + vehicleId + ").");
            BodyshopApi.RaiseMenu(v, true);
        }

        private static string Safe(string what, Func<string> f)
        {
            try { return f() ?? ""; }
            catch (Exception e) { Core.Log.Error("[Menu] page call '" + what + "' failed: " + e); return ""; }
        }

        private static void Hook()
        {
            if (_hooked) return;
            _hooked = true;
            ChassisApi.TuningChanged += (v, key) => { if (_open && v != null && ShopNet.IdOf(v) == _vehicleId) _refreshWanted = true; };
        }

        private static void RegisterExit()
        {
            if (_exitRegistered) return;
            try
            {
                GameInput.RegisterExitListener((Il2CppSystem.Action)OnExit, (Il2CppSystem.Func<bool>)(Func<bool>)(() => _open), 500, false);
                _exitRegistered = true;
            }
            catch (Exception e) { Core.Log.Error("[Menu] could not register the Escape listener - Escape will not go back: " + e.Message); }
        }

        private static void OnExit() => _surface?.Emit("key", "back");

        // ---- page calls -----------------------------------------------------------------------------------------

        internal static string List(string categoryId)
        {
            var c = Registry.Category(categoryId);
            if (c == null) { Core.Log.Warning("[Menu] the page asked for category '" + categoryId + "', which is not registered."); return ""; }
            if (_category != c)
            {
                LeaveCategory();
                _category = c;
                try { c.Entered?.Invoke(_vehicle); }
                catch (Exception e) { Core.Log.Error("[Menu] Entered of category '" + c.Id + "' failed: " + e.Message); }
                MoveCamera(null, false);
            }
            return Model.Entries(c, _vehicle);
        }

        internal static string Pick(string arg)
        {
            Split(arg, out string categoryId, out string entryId);
            var c = Registry.Category(categoryId);
            if (c == null || _vehicle == null) return "";
            if (entryId.Length == 0)
            {
                // A group row or nothing is highlighted: the vehicle shows what is really on it.
                if (_previewed == null) return "ok";
                EndPreview();
                MoveCamera(null, false);
                return "ok";
            }
            var e = Registry.Entry(c, _vehicle, entryId);
            if (e == null) { Core.Log.Warning("[Menu] pick: no entry '" + entryId + "' in '" + categoryId + "'."); return ""; }
            if (e == _previewed) return "ok";
            EndPreview();
            _previewed = e;
            _previewedCategory = c.Id;
            try { e.Preview?.Invoke(_vehicle); }
            catch (Exception ex) { Core.Log.Error("[Menu] Preview of '" + c.Id + "/" + e.Id + "' failed: " + ex.Message); }
            MoveCamera(e, false);
            return "ok";
        }

        private static string Leave()
        {
            LeaveCategory();
            MoveCamera(null, false);
            return "ok";
        }

        private static void LeaveCategory()
        {
            EndPreview();
            if (_category == null) return;
            var c = _category;
            _category = null;
            try { c.Left?.Invoke(_vehicle); }
            catch (Exception e) { Core.Log.Error("[Menu] Left of category '" + c.Id + "' failed: " + e.Message); }
        }

        private static void EndPreview()
        {
            if (_previewed == null) return;
            var e = _previewed;
            _previewed = null;
            try { e.EndPreview?.Invoke(_vehicle); }
            catch (Exception ex) { Core.Log.Error("[Menu] EndPreview of '" + _previewedCategory + "/" + e.Id + "' failed: " + ex.Message); }
        }

        /// <summary>Returns "" when the purchase went to the host, or the reason it did not.</summary>
        internal static string Buy(string arg)
        {
            if (_busy) return "One moment.";
            Split(arg, out string categoryId, out string entryId);
            var c = Registry.Category(categoryId);
            if (c == null || _vehicle == null) return "Not available.";
            string state = Registry.StateOf(c, _vehicle);
            if (state != null) return state.Length == 0 ? "Not available." : state;
            var e = Registry.Entry(c, _vehicle, entryId);
            if (e == null) return "Not available.";
            if (!e.Toggle && Prices.Installed(e, _vehicle)) return "Already installed.";
            string blocked = Prices.BlockedOf(e, _vehicle);
            if (blocked != null) return blocked;
            int price = Prices.Of(e, _vehicle);
            if (Payment.Cash() < price) return "Not enough cash.";
            int ticket = Payment.Charge(price, c.Id + "/" + e.Id + " on " + _vehicle.VehicleName);
            if (ticket == 0) return "Not enough cash.";
            _busy = true;
            ShopNet.ToHost("req.buy", _garage, _vehicleId.ToString(CultureInfo.InvariantCulture), c.Id, e.Id,
                           Payment.Ticket(ticket), price.ToString(CultureInfo.InvariantCulture), ShopNet.MyCode());
            return "";
        }

        /// <summary>The host's answer to a purchase of this player.</summary>
        internal static void OnSettled(bool kept, string garage, int vehicleId, string category, string entry, int amount, string reason)
        {
            _busy = false;
            if (!kept)
            {
                if (_open) _surface?.Emit("flash", reason.Length > 0 ? reason : "The garage refused.");
                _refreshWanted = true;
                return;
            }
            var v = ShopNet.VehicleById(vehicleId);
            var c = Registry.Category(category);
            BodyshopEntry e = c != null && v != null ? Registry.Entry(c, v, entry) : null;
            string label = e?.Label ?? entry;
            bool fitted = e == null || !e.Toggle || Prices.Installed(e, v);
            if (_open && _previewed != null && _previewed == e) _previewed = null;
            Effects.Workshop(v != null ? v.transform.position : Vector3.zero);
            if (_open) _surface?.Emit("flash", label + (e != null && e.Toggle ? (fitted ? " fitted." : " removed.") : " installed.") + (amount > 0 ? " -$" + amount.ToString("N0", CultureInfo.InvariantCulture) : ""));
            _refreshWanted = true;
            if (v != null) BodyshopApi.RaisePurchased(v, category, entry);
        }

        private static void Split(string arg, out string category, out string entry)
        {
            arg = arg ?? "";
            int bar = arg.IndexOf('/');
            category = bar < 0 ? arg : arg.Substring(0, bar);
            entry = bar < 0 ? "" : arg.Substring(bar + 1);
        }

        // ---- camera ---------------------------------------------------------------------------------------------

        private static void MoveCamera(BodyshopEntry entry, bool instant)
        {
            var pc = PlayerSingleton<PlayerCamera>.Instance;
            if (pc == null || _vehicle == null) return;
            CameraShot? shot = null;
            try
            {
                if (entry?.Camera != null) shot = entry.Camera(_vehicle);
                if (shot == null && _category?.Camera != null) shot = _category.Camera(_vehicle);
                if (shot == null && _bay?.Camera != null) shot = _bay.Camera(_vehicle);
            }
            catch (Exception e) { Core.Log.Error("[Menu] a camera function failed - the camera stays: " + e.Message); return; }
            var s = shot ?? DefaultShot(_vehicle);
            Vector3 look = s.Target - s.Eye;
            if (look.sqrMagnitude < 0.0001f) look = Vector3.forward;
            pc.OverrideTransform(s.Eye, Quaternion.LookRotation(look), instant ? 0f : CameraLerp, false);
            pc.OverrideFOV(s.Fov > 1f ? s.Fov : 60f, instant ? 0f : CameraLerp);
        }

        /// <summary>Front left, a little above, looking at the middle of the vehicle.</summary>
        internal static CameraShot DefaultShot(LandVehicle v)
        {
            var b = Bounds(v);
            float r = Mathf.Max(b.extents.x, b.extents.z);
            var t = v.transform;
            Vector3 eye = b.center + t.forward * (r * 1.6f) - t.right * (r * 1.2f) + Vector3.up * (b.extents.y * 0.8f + 0.6f);
            return new CameraShot(eye, b.center, 60f);
        }

        internal static Bounds Bounds(LandVehicle v)
        {
            var rs = v.GetComponentsInChildren<Renderer>();
            bool any = false;
            var b = new Bounds(v.transform.position, Vector3.one);
            foreach (var r in rs)
            {
                if (r == null || !r.enabled) continue;
                if (!any) { b = r.bounds; any = true; }
                else b.Encapsulate(r.bounds);
            }
            return b;
        }

        // ---- per frame ------------------------------------------------------------------------------------------

        internal static void Tick()
        {
            if (!_open) return;
            if (_closeRequested) { Close("the player left the menu"); return; }
            if (_vehicle == null || ShopNet.VehicleById(_vehicleId) == null) { Close("the vehicle is gone"); return; }

            var kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.upArrowKey.wasPressedThisFrame) _surface?.Emit("key", "up");
                if (kb.downArrowKey.wasPressedThisFrame) _surface?.Emit("key", "down");
                if (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame) _surface?.Emit("key", "enter");
            }

            if (_refreshWanted) { _refreshWanted = false; _surface?.Emit("refresh", ""); }
            if (Time.unscaledTime >= _nextCash)
            {
                _nextCash = Time.unscaledTime + 0.25f;
                int cash = Payment.Cash();
                if (cash != _shownCash) { _shownCash = cash; _surface?.Emit("cash", cash.ToString(CultureInfo.InvariantCulture)); }
            }
        }

        // ---- closing --------------------------------------------------------------------------------------------

        internal static void Close(string why)
        {
            bool was = _open;
            var v = _vehicle;
            string garage = _garage;
            if (was) LeaveCategory();
            _open = false;
            _closeRequested = false;
            _busy = false;

            try { if (Surfaces.IsMounted(SurfaceId)) Surfaces.Unmount(SurfaceId); }
            catch (Exception e) { Core.Log.Warning("[Menu] unmounting the page failed: " + e.Message); }
            if (_canvas != null) UnityEngine.Object.Destroy(_canvas);
            _canvas = null;
            _surface = null;

            var pc = PlayerSingleton<PlayerCamera>.Instance;
            if (pc != null && was)
            {
                try { pc.StopTransformOverride(0f, true, true); pc.StopFOVOverride(0f); }
                catch (Exception e) { Core.Log.Warning("[Menu] releasing the camera failed: " + e.Message); }
            }
            if (_state != null)
            {
                try { _state.PopFromDefaultParent(); }
                catch (Exception e) { Core.Log.Warning("[Menu] popping the UI state failed: " + e.Message); }
            }
            if (_stateObject != null) UnityEngine.Object.Destroy(_stateObject);
            _state = null;
            _stateObject = null;
            _vehicle = null;
            _bay = null;

            if (!was) return;
            if (garage != null) ShopNet.ToHost("req.close", garage, ShopNet.MyCode());
            Core.Log.Msg("[Menu] closed (" + why + ").");
            if (v != null) BodyshopApi.RaiseMenu(v, false);
        }

        internal static void SceneChanged(string scene)
        {
            // The game clears its exit listeners and the state stack on a scene change; the canvas is ours.
            _exitRegistered = false;
            _pendingGarage = null;
            if (_open) Close("scene '" + scene + "' loaded");
        }
    }
}
