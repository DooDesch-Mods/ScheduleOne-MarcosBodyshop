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
            Core.Log.Msg("[Menu] open: garage '" + garage + "', bay '" + bay.Id + "', " + v.VehicleName + " (net " + vehicleId + "), box " + LocalBox(v).size.ToString("F1") + ".");
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
            bool fixedShot = false;
            try
            {
                if (entry?.Camera != null) shot = entry.Camera(_vehicle);
                if (shot == null && _category?.Camera != null) shot = _category.Camera(_vehicle);
                if (shot == null && _bay?.Camera != null) { shot = _bay.Camera(_vehicle); fixedShot = shot != null; }
            }
            catch (Exception e) { Core.Log.Error("[Menu] a camera function failed - the camera stays: " + e.Message); return; }
            // A bay's own camera is placed for that bay; shots around the vehicle may land in a wall.
            var s = shot ?? DefaultShot(_vehicle);
            if (!fixedShot) s = FreeView(_vehicle, s);
            Cutaway.Aim(_vehicle, s.Eye);
            Vector3 look = s.Target - s.Eye;
            if (look.sqrMagnitude < 0.0001f) look = Vector3.forward;
            float fov = s.Fov > 1f ? s.Fov : 60f;
            var turn = Quaternion.LookRotation(look);
            turn = Quaternion.AngleAxis(-BesideMenu(fov), Vector3.up) * turn;
            pc.OverrideTransform(s.Eye, turn, instant ? 0f : CameraLerp, false);
            pc.OverrideFOV(fov, instant ? 0f : CameraLerp);
        }

        /// <summary>
        /// A shot whose eye stands behind a wall or inside a building (a vehicle parked with that side to it) goes to the
        /// vehicle's other side, mirrored, when that side is free. Whatever still stands in the view fades (<see cref="Cutaway"/>).
        /// </summary>
        // The menu covers the left of the screen (596 of 1920 px at 1080 lines, it scales with the height). The camera
        // turns left by this many degrees so the target sits in the middle of the free part.
        private static float BesideMenu(float fov)
        {
            if (Screen.width <= 0 || Screen.height <= 0) return 0f;
            // The menu's share of the width is also how far right of the screen centre the free part's centre lies,
            // in half-widths.
            float menu = Mathf.Clamp01(596f * Screen.height / 1080f / Screen.width);
            float halfTan = Mathf.Tan(fov * 0.5f * Mathf.Deg2Rad) * Screen.width / Screen.height;
            return Mathf.Atan(menu * halfTan) * Mathf.Rad2Deg;
        }

        internal static CameraShot FreeView(LandVehicle v, CameraShot s)
        {
            try
            {
                if (!Blocked(v, s.Target, s.Eye, out float free)) return s;
                var t = v.transform;
                Vector3 e = t.InverseTransformPoint(s.Eye), g = t.InverseTransformPoint(s.Target);
                var mirrored = new CameraShot(t.TransformPoint(new Vector3(-e.x, e.y, e.z)), t.TransformPoint(new Vector3(-g.x, g.y, g.z)), s.Fov);
                if (!Blocked(v, mirrored.Target, mirrored.Eye, out _))
                {
                    Core.Log.Msg("[Menu] camera view blocked " + free.ToString("F1", CultureInfo.InvariantCulture) + " m out - shown from the other side.");
                    return mirrored;
                }
                Core.Log.Msg("[Menu] camera view blocked on both sides - the shot stays, the blockers fade.");
                return s;
            }
            catch (Exception e) { Core.Log.Warning("[Menu] checking the camera's view failed - the shot stays as given: " + e.Message); return s; }
        }

        // Free distance from the target towards the eye; the vehicle itself and the local player do not count.
        private static bool Blocked(LandVehicle v, Vector3 from, Vector3 to, out float free)
        {
            Vector3 d = to - from;
            float len = d.magnitude;
            free = len;
            if (len < 0.01f) return false;
            var player = Player.Local != null ? Player.Local.transform : null;
            foreach (var hit in Physics.RaycastAll(from, d / len, len, ~0, QueryTriggerInteraction.Ignore))
            {
                var c = hit.collider;
                if (c == null || c.transform.IsChildOf(v.transform)) continue;
                if (player != null && c.transform.IsChildOf(player)) continue;
                if (hit.distance < free) free = hit.distance;
            }
            return free < len;
        }

        /// <summary>Front left, a little above, looking at the middle of the vehicle.</summary>
        internal static CameraShot DefaultShot(LandVehicle v)
        {
            var b = LocalBox(v);
            float r = Mathf.Max(b.extents.x, b.extents.z);
            Vector3 eye = b.center + new Vector3(-r * 1.2f, b.extents.y * 0.8f + 0.6f, r * 1.6f);
            return CameraShot.Around(v, eye, b.center, 60f);
        }

        /// <summary>World box of the vehicle, around <see cref="LocalBox"/>.</summary>
        internal static Bounds Bounds(LandVehicle v)
        {
            var b = LocalBox(v);
            var t = v.transform;
            var w = new Bounds(t.TransformPoint(b.center), Vector3.zero);
            for (int i = 0; i < 8; i++) w.Encapsulate(t.TransformPoint(b.center + Vector3.Scale(b.extents, Corner(i))));
            return w;
        }

        /// <summary>
        /// The vehicle's box in its own space (x right, y up, z forward). It comes from the vehicle's solid colliders:
        /// renderers also hold light cones, reflector strips and glow, whose meshes reach metres past the body. A vehicle
        /// without solid colliders takes its opaque meshes, minus static-batched ones (their mesh is the whole batch, at
        /// the place it was batched). World boxes are not used; they grow when the vehicle stands at an angle.
        /// </summary>
        internal static Bounds LocalBox(LandVehicle v)
        {
            var t = v.transform;
            var acc = new Box();
            foreach (var c in v.GetComponentsInChildren<Collider>())
            {
                if (c == null || !c.enabled || c.isTrigger) continue;
                var bc = c.TryCast<BoxCollider>();
                if (bc != null) { acc.Add(t, c.transform, new Bounds(bc.center, bc.size)); continue; }
                var mc = c.TryCast<MeshCollider>();
                if (mc != null && mc.sharedMesh != null) { acc.Add(t, c.transform, mc.sharedMesh.bounds); continue; }
                // Spheres, capsules and wheels: their world box, small enough that the angle does not matter.
                var wb = c.bounds;
                acc.Add(t, null, wb);
            }
            if (acc.Any) return acc.B;
            foreach (var r in v.GetComponentsInChildren<Renderer>())
            {
                if (r == null || !r.enabled || r.isPartOfStaticBatch) continue;
                var m = r.sharedMaterial;
                if (m != null && m.renderQueue >= (int)UnityEngine.Rendering.RenderQueue.AlphaTest + 50) continue;   // glass, beams, glow
                var mf = r.GetComponent<MeshFilter>();
                var mesh = mf != null ? mf.sharedMesh : null;
                if (mesh == null || mesh.bounds.size.sqrMagnitude < 0.0001f) continue;
                acc.Add(t, r.transform, mesh.bounds);
            }
            return acc.Any ? acc.B : new Bounds(Vector3.zero, Vector3.one * 2f);
        }

        // Collects the eight corners of local boxes in the vehicle's space; a null transform means a world box.
        private sealed class Box
        {
            internal Bounds B;
            internal bool Any;

            internal void Add(Transform vehicle, Transform space, Bounds local)
            {
                if (local.size.sqrMagnitude < 0.0001f) return;
                for (int i = 0; i < 8; i++)
                {
                    Vector3 corner = local.center + Vector3.Scale(local.extents, Corner(i));
                    Vector3 p = vehicle.InverseTransformPoint(space != null ? space.TransformPoint(corner) : corner);
                    if (!Any) { B = new Bounds(p, Vector3.zero); Any = true; }
                    else B.Encapsulate(p);
                }
            }
        }

        /// <summary>The open vehicle's box and its largest renderers, to find a mesh that makes the box too big.</summary>
        internal static List<string> BoxReport()
        {
            var lines = new List<string>();
            if (_vehicle == null) { lines.Add("box: no menu open"); return lines; }
            var t = _vehicle.transform;
            lines.Add("box: " + LocalBox(_vehicle).ToString("F1"));
            var sizes = new List<(float, string)>();
            foreach (var r in _vehicle.GetComponentsInChildren<Renderer>())
            {
                if (r == null || !r.enabled || r.isPartOfStaticBatch) continue;
                var mf = r.GetComponent<MeshFilter>();
                var mesh = mf != null ? mf.sharedMesh : null;
                if (mesh == null) continue;
                var mb = mesh.bounds;
                var b = new Bounds(t.InverseTransformPoint(r.transform.TransformPoint(mb.center)), Vector3.zero);
                for (int i = 0; i < 8; i++) b.Encapsulate(t.InverseTransformPoint(r.transform.TransformPoint(mb.center + Vector3.Scale(mb.extents, Corner(i)))));
                string path = r.name;
                for (var p = r.transform.parent; p != null && p != t; p = p.parent) path = p.name + "/" + path;
                sizes.Add((b.size.magnitude, path + " " + b.ToString("F1")));
            }
            sizes.Sort((x, y) => y.Item1.CompareTo(x.Item1));
            for (int i = 0; i < sizes.Count && i < 8; i++) lines.Add("  " + sizes[i].Item2);
            return lines;
        }

        private static Vector3 Corner(int i) => new Vector3((i & 1) == 0 ? -1f : 1f, (i & 2) == 0 ? -1f : 1f, (i & 4) == 0 ? -1f : 1f);

        // ---- per frame ------------------------------------------------------------------------------------------

        internal static void Tick()
        {
            if (!_open) return;
            Cutaway.Tick();
            if (_closeRequested) { Close("the player left the menu"); return; }
            if (_vehicle == null || ShopNet.VehicleById(_vehicleId) == null) { Close("the vehicle is gone"); return; }

            var kb = Keyboard.current;
            if (kb != null)
            {
                // W and S browse like the arrows, Space buys like Enter. Keys by position: physical W/S on every layout.
                if (kb.upArrowKey.wasPressedThisFrame || kb.wKey.wasPressedThisFrame) _surface?.Emit("key", "up");
                if (kb.downArrowKey.wasPressedThisFrame || kb.sKey.wasPressedThisFrame) _surface?.Emit("key", "down");
                if (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame) _surface?.Emit("key", "enter");
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
            Cutaway.Clear();
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
