#if DEBUG
using System;
using System.Globalization;
using Il2CppScheduleOne;
using Il2CppScheduleOne.DevUtilities;
using Il2CppScheduleOne.PlayerScripts;
using Il2CppScheduleOne.State;
using Sideload.Api;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace MarcosBodyshop.Mock
{
    /// <summary>
    /// The garage menu mockup on a Sideload surface, held the way the real garage will hold it: a game UI state with
    /// the mouse free and movement locked, the camera overridden, the page on a full-screen overlay canvas of its own.
    ///
    /// It also answers the phase-1 question - do mouse and keys reach the page while the camera is overridden - by
    /// logging every step a key or a pointer takes: the mod reading the key, the page receiving it, the page seeing a
    /// hover or a click. "Never arrived" and "arrived and did nothing" are different lines.
    ///
    /// <list type="bullet">
    /// <item><c>bodyshop open [car|rv] [pick...]</c> - push the state, hold the camera, mount the page.</item>
    /// <item><c>bodyshop cam ex ey ez tx ty tz</c> - camera eye and target for the next open (or now, when open).</item>
    /// <item><c>bodyshop show car|rv [pick...]</c> - jump the page to a menu path.</item>
    /// <item><c>bodyshop key up|down|enter|back</c> - the same key event the keyboard sends, for screenshots.</item>
    /// <item><c>bodyshop probe</c> - cursor, camera, movement and pointer state now.</item>
    /// <item><c>bodyshop close</c>.</item>
    /// </list>
    /// </summary>
    internal static class GarageMock
    {
        private const string SurfaceId = "marcosbodyshop";
        private const string Bundle = "MarcosBodyshop.Assets.bodyshop";

        private static GameObject _canvas;
        private static GameObject _stateObject;
        private static MonoState _state;
        private static SurfaceHandle _surface;
        private static bool _open;
        private static bool _closeRequested;
        private static bool _exitRegistered;
        private static bool _hasPose;
        private static Vector3 _eye, _target;
        private static Vector3 _playerAtOpen;
        private static float _nextProbe;
        // The game's own overlay canvases sort above small numbers and hide the menu; Sideload's surface test uses 31000.
        private static int _sortingOrder = 30500;
        // What the page should show. Kept, because an emit before the page's script has run reaches nobody; the
        // page asks for it once on start.
        private static string _lastShow = "car";

        internal static void SceneChanged(string scene)
        {
            // The game clears its exit listeners and the state stack on a scene change; the canvas is ours.
            _exitRegistered = false;
            if (_open) { Core.Log.Msg("[Mock] scene '" + scene + "' loaded while open - tearing the mockup down."); Close(); }
        }

        internal static void Command(string[] parts)
        {
            string sub = parts.Length > 1 ? parts[1].ToLowerInvariant() : "";
            string[] rest = parts.Length > 2 ? parts[2..] : Array.Empty<string>();
            switch (sub)
            {
                case "open": Open(rest); break;
                case "close": Close(); break;
                case "show": Show(rest); break;
                case "cam": Cam(rest); break;
                case "key":
                    if (!_open) { Core.Log.Warning("[Mock] key: the mockup is not open."); return; }
                    string k = rest.Length > 0 ? rest[0].ToLowerInvariant() : "down";
                    _surface?.Emit("key", k);
                    Core.Log.Msg("[Mock] key '" + k + "' sent from the console.");
                    break;
                case "probe": Probe("console"); break;
                case "order": Order(rest); break;
                default:
                    Core.Log.Msg("[Mock] bodyshop open [car|rv] [pick...] | cam ex ey ez tx ty tz | show car|rv [pick...] | key up|down|enter|back | order N | probe | close");
                    break;
            }
        }

        private static void Order(string[] a)
        {
            if (a.Length < 1 || !int.TryParse(a[0], out int n)) { Core.Log.Warning("[Mock] order needs a whole number."); return; }
            _sortingOrder = n;
            var canvas = _canvas != null ? _canvas.GetComponent<Canvas>() : null;
            if (canvas != null) canvas.sortingOrder = n;
            Core.Log.Msg("[Mock] menu canvas sorting order " + n + (canvas != null ? " (applied to the open menu)." : " (used on the next open)."));
        }

        private static void Cam(string[] a)
        {
            var f = new float[6];
            if (a.Length < 6) { Core.Log.Warning("[Mock] cam needs six numbers: eye x y z, target x y z."); return; }
            for (int i = 0; i < 6; i++)
                if (!float.TryParse(a[i], NumberStyles.Float, CultureInfo.InvariantCulture, out f[i]))
                { Core.Log.Warning("[Mock] cam: '" + a[i] + "' is not a number."); return; }
            _eye = new Vector3(f[0], f[1], f[2]);
            _target = new Vector3(f[3], f[4], f[5]);
            _hasPose = true;
            Core.Log.Msg("[Mock] camera pose set: eye " + _eye.ToString("F2") + ", target " + _target.ToString("F2") + ".");
            if (_open) HoldCamera();
        }

        private static void Open(string[] showArgs)
        {
            if (_open) { Show(showArgs); return; }

            var pc = PlayerSingleton<PlayerCamera>.Instance;
            if (pc == null) { Core.Log.Warning("[Mock] open: no player camera - load a save first."); return; }
            if (!Surfaces.Available) { Core.Log.Error("[Mock] open: Sideload with surfaces is not installed."); return; }
            if (SceneState.Current == null) { Core.Log.Warning("[Mock] open: no scene state - load a save first."); return; }

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

            if (!_hasPose)
            {
                _eye = pc.transform.position;
                _target = _eye + pc.transform.forward * 10f;
            }
            HoldCamera();
            RegisterExit();

            _canvas = new GameObject("MarcosBodyshop_Menu");
            var canvas = _canvas.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = _sortingOrder;
            _canvas.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            _canvas.AddComponent<GraphicRaycaster>();

            var rect = new GameObject("page").AddComponent<RectTransform>();
            rect.SetParent(_canvas.transform, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;

            // A short side of 1080 css px: the page is written for a 1080p frame and scales with the screen.
            _surface = Surfaces.Mount(rect, SurfaceId, Bundle, 1080f);
            if (_surface == null) { Core.Log.Error("[Mock] open: the surface did not mount."); Close(); return; }

            _surface.OnCall("mock.seen", k => { Core.Log.Msg("[input] page received key '" + k + "'."); return "ok"; })
                    .OnCall("mock.hover", l => { Core.Log.Msg("[input] page saw the pointer on '" + l + "'."); return "ok"; })
                    .OnCall("mock.click", l => { Core.Log.Msg("[input] page saw a click on '" + l + "'."); return "ok"; })
                    .OnCall("mock.bought", l => { Core.Log.Msg("[Mock] page bought '" + l + "'."); return "ok"; })
                    .OnCall("mock.start", _ => { Core.Log.Msg("[Mock] page started, showing '" + _lastShow + "'."); return _lastShow; })
                    .OnCall("mock.close", _ => { _closeRequested = true; Core.Log.Msg("[Mock] page asked to close."); return "ok"; });

            _open = true;
            _closeRequested = false;
            _playerAtOpen = Player.Local != null ? Player.Local.transform.position : Vector3.zero;
            _nextProbe = Time.unscaledTime + 1f;
            Core.Log.Msg("[Mock] open at " + Screen.width + "x" + Screen.height + ", camera held at " + _eye.ToString("F2") + ".");
            Show(showArgs);
        }

        private static void HoldCamera()
        {
            var pc = PlayerSingleton<PlayerCamera>.Instance;
            if (pc == null) { Core.Log.Warning("[Mock] camera: no player camera."); return; }
            Vector3 look = _target - _eye;
            if (look.sqrMagnitude < 0.0001f) look = Vector3.forward;
            pc.OverrideTransform(_eye, Quaternion.LookRotation(look), 0f, false);
            pc.OverrideFOV(60f, 0f);
        }

        private static void Show(string[] a)
        {
            if (!_open) { Core.Log.Warning("[Mock] show: the mockup is not open."); return; }
            string arg = a.Length == 0 ? "car" : string.Join(" ", a);
            _lastShow = arg;
            _surface.Emit("show", arg);
            Core.Log.Msg("[Mock] asked the page to show '" + arg + "'.");
        }

        private static void RegisterExit()
        {
            if (_exitRegistered) return;
            try
            {
                GameInput.RegisterExitListener((Il2CppSystem.Action)OnExit, (Il2CppSystem.Func<bool>)(Func<bool>)(() => _open), 500, false);
                _exitRegistered = true;
            }
            catch (Exception e) { Core.Log.Error("[Mock] could not register the Escape listener: " + e.Message); }
        }

        private static void OnExit()
        {
            Core.Log.Msg("[input] mod saw the game's exit action (Escape / right-click).");
            _surface?.Emit("key", "back");
        }

        internal static void Close()
        {
            bool wasOpen = _open;
            _open = false;
            _closeRequested = false;

            try { if (Surfaces.IsMounted(SurfaceId)) Surfaces.Unmount(SurfaceId); }
            catch (Exception e) { Core.Log.Warning("[Mock] unmount: " + e.Message); }
            if (_canvas != null) UnityEngine.Object.Destroy(_canvas);
            _canvas = null;
            _surface = null;

            var pc = PlayerSingleton<PlayerCamera>.Instance;
            if (pc != null)
            {
                try { pc.StopTransformOverride(0f, true, true); pc.StopFOVOverride(0f); }
                catch (Exception e) { Core.Log.Warning("[Mock] camera release: " + e.Message); }
            }

            if (_state != null)
            {
                try { _state.PopFromDefaultParent(); }
                catch (Exception e) { Core.Log.Warning("[Mock] popping the UI state: " + e.Message); }
            }
            if (_stateObject != null) UnityEngine.Object.Destroy(_stateObject);
            _state = null;
            _stateObject = null;

            Core.Log.Msg(wasOpen ? "[Mock] closed." : "[Mock] close: was not open.");
        }

        internal static void Tick()
        {
            if (!_open) return;
            if (_closeRequested) { Close(); return; }

            var kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.upArrowKey.wasPressedThisFrame) Relay("up", "ArrowUp");
                if (kb.downArrowKey.wasPressedThisFrame) Relay("down", "ArrowDown");
                if (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame) Relay("enter", "Enter");
            }

            var mouse = Mouse.current;
            if (mouse != null && mouse.leftButton.wasPressedThisFrame)
                Core.Log.Msg("[input] mod saw a left click at " + mouse.position.ReadValue().ToString("F0")
                             + ", over UI: " + (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) + ".");

            if (Time.unscaledTime >= _nextProbe) { _nextProbe = Time.unscaledTime + 5f; Probe("timer"); }
        }

        private static void Relay(string key, string name)
        {
            Core.Log.Msg("[input] mod read " + name + " at frame " + Time.frameCount + "; player moved "
                         + Moved().ToString("F3", CultureInfo.InvariantCulture) + " m since open.");
            _surface?.Emit("key", key);
        }

        private static float Moved() =>
            Player.Local != null ? Vector3.Distance(Player.Local.transform.position, _playerAtOpen) : -1f;

        private static void Probe(string why)
        {
            var pc = PlayerSingleton<PlayerCamera>.Instance;
            var pm = PlayerSingleton<PlayerMovement>.Instance;
            var mouse = Mouse.current;
            Core.Log.Msg("[probe:" + why + "] cursor " + Cursor.lockState + (Cursor.visible ? " visible" : " hidden")
                         + ", camera overridden " + (pc != null && pc.transformOverriden)
                         + " at " + (pc != null ? pc.transform.position.ToString("F2") : "-")
                         + ", can move " + (pm != null && pm.CanMove)
                         + ", player moved " + Moved().ToString("F3", CultureInfo.InvariantCulture) + " m"
                         + ", pointer " + (mouse != null ? mouse.position.ReadValue().ToString("F0") : "-")
                         + " over UI " + (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
                         + ", top state is ours " + (_state != null && SceneState.ActiveState != null
                                                     && SceneState.ActiveState.Pointer == _state.Pointer) + ".");
        }
    }
}
#endif
