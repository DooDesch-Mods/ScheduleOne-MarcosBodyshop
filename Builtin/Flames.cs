using System;
using System.Collections.Generic;
using Il2CppScheduleOne.Vehicles;
using MarcosBodyshop.Net;
using UnityEngine;

namespace MarcosBodyshop.Builtin
{
    /// <summary>
    /// The nitro flame at each exhaust of a car (the game's exhaust smoke emitters, <see cref="VehicleFX.exhaustFX"/>):
    /// a blue additive cone that fades to its tip, a white core and a blue light, flickering while the nitro burns.
    /// Every machine builds its own; <c>nitro vehicle 1|0</c> from the host switches them.
    /// </summary>
    internal static class Flames
    {
        private const string FlameName = "bodyshop_flame";
        private const float Length = 0.75f, Radius = 0.07f;
        private const float CoreLength = 0.32f, CoreRadius = 0.035f;

        private static readonly HashSet<int> _on = new HashSet<int>();
        private static readonly Dictionary<int, List<Transform>> _built = new Dictionary<int, List<Transform>>();
        private static Mesh _cone;
        private static Material _outer, _core;
        private static bool _materialsTried;

        internal static void Reset()
        {
            _on.Clear();
            _built.Clear();
        }

        /// <summary>Everyone: the host says a car's nitro went on or off.</summary>
        internal static void OnNitro(int vehicle, bool on)
        {
            bool changed = on ? _on.Add(vehicle) : _on.Remove(vehicle);
            if (changed) Core.Log.Msg("[Flames] net " + vehicle + " nitro flames " + (on ? "on" : "off") + ".");
        }

        internal static void LateTick()
        {
            if (_on.Count == 0 && _built.Count == 0) return;
            foreach (int id in new List<int>(_on))
            {
                var lv = ShopNet.VehicleById(id);
                // A driver who left the game cannot send the off any more.
                if (lv == null || !lv.IsOccupied) { _on.Remove(id); Core.Log.Msg("[Flames] net " + id + " " + (lv == null ? "is gone" : "has no driver") + " - flames off."); continue; }
                if (!_built.ContainsKey(id)) _built[id] = Build(lv, id);
            }
            foreach (var kv in new List<KeyValuePair<int, List<Transform>>>(_built))
            {
                bool on = _on.Contains(kv.Key);
                bool alive = false;
                foreach (var f in kv.Value)
                {
                    if (f == null) continue;
                    alive = true;
                    if (f.gameObject.activeSelf != on) f.gameObject.SetActive(on);
                    if (!on) continue;
                    float len = UnityEngine.Random.Range(0.75f, 1.25f), wide = UnityEngine.Random.Range(0.9f, 1.1f);
                    // Sizes are metres: the scale of the car root is divided out.
                    float unit = f.parent != null && f.parent.lossyScale.x > 0.0001f ? 1f / f.parent.lossyScale.x : 1f;
                    f.localScale = new Vector3(wide, wide, len) * unit;
                    var l = f.GetComponentInChildren<Light>();
                    if (l != null) l.intensity = UnityEngine.Random.Range(2.5f, 5f);
                }
                if (!alive) _built.Remove(kv.Key);
            }
        }

        internal static string Describe() => "flames on " + _on.Count + " car(s), built on " + _built.Count;

        /// <summary>One flame per exhaust; one at the rear underside when the car has no exhaust emitters.</summary>
        private static List<Transform> Build(LandVehicle lv, int id)
        {
            var list = new List<Transform>();
            if (!Materials()) return list;
            var fx = lv.GetComponentInChildren<VehicleFX>(true);
            if (fx != null && fx.exhaustFX != null)
                foreach (var ps in fx.exhaustFX)
                    if (ps != null) list.Add(Flame(lv, lv.transform, lv.transform.InverseTransformPoint(ps.transform.position)));
            if (list.Count == 0)
            {
                var b = Shop.Session.LocalBox(lv);
                var at = lv.transform.TransformPoint(new Vector3(b.center.x + b.extents.x * 0.5f, b.min.y + 0.25f, b.min.z));
                list.Add(Flame(lv, lv.transform, lv.transform.InverseTransformPoint(at)));
                Core.Log.Warning("[Flames] net " + id + " (" + lv.VehicleName + ") has no exhaust emitters - one flame at the rear underside.");
            }
            else Core.Log.Msg("[Flames] net " + id + " (" + lv.VehicleName + "): " + list.Count + " flame(s) at the exhaust (car scale " + lv.transform.lossyScale.x.ToString("0.###") + ").");
            return list;
        }

        /// <summary>
        /// A flame under the car root at a local point. Not under the exhaust emitter: some cars scale their emitters up,
        /// which blew the cone up to tens of metres around the camera.
        /// </summary>
        private static Transform Flame(LandVehicle lv, Transform parent, Vector3 local)
        {
            var flame = new GameObject(FlameName).transform;
            flame.SetParent(parent, false);
            flame.localPosition = local;
            // Out of the back of the car, whichever way the emitter faces.
            flame.rotation = Quaternion.LookRotation(-lv.transform.forward, lv.transform.up);
            Cone(flame, "outer", _outer, Length, Radius);
            Cone(flame, "core", _core, CoreLength, CoreRadius);
            var light = new GameObject("light").AddComponent<Light>();
            light.transform.SetParent(flame, false);
            light.transform.localPosition = new Vector3(0f, 0f, 0.25f);
            light.type = LightType.Point;
            light.color = new Color(0.35f, 0.55f, 1f);
            light.range = 4f;
            light.intensity = 0f;
            light.shadows = LightShadows.None;
            flame.gameObject.SetActive(false);
            return flame;
        }

        private static void Cone(Transform parent, string name, Material mat, float length, float radius)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localScale = new Vector3(radius, radius, length);
            go.AddComponent<MeshFilter>().sharedMesh = ConeMesh();
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
        }

        /// <summary>A unit cone along +Z: radius 1 at z 0, the tip at z 1; white at the base, black (nothing, additive) at the tip.</summary>
        private static Mesh ConeMesh()
        {
            if (_cone != null) return _cone;
            const int n = 16;
            var v = new Vector3[n + 2];
            var c = new Color[n + 2];
            var tris = new int[n * 6];
            for (int i = 0; i < n; i++)
            {
                float a = i * Mathf.PI * 2f / n;
                v[i] = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f);
                c[i] = Color.white;
            }
            v[n] = new Vector3(0f, 0f, 1f); c[n] = Color.black;
            v[n + 1] = Vector3.zero; c[n + 1] = Color.white;
            for (int i = 0; i < n; i++)
            {
                int j = (i + 1) % n;
                tris[i * 6 + 0] = i; tris[i * 6 + 1] = n; tris[i * 6 + 2] = j;
                tris[i * 6 + 3] = j; tris[i * 6 + 4] = n + 1; tris[i * 6 + 5] = i;
            }
            _cone = new Mesh { name = "bodyshop_flame_cone" };
            _cone.vertices = v;
            _cone.colors = c;
            _cone.triangles = tris;
            _cone.RecalculateBounds();
            _cone.hideFlags |= HideFlags.DontUnloadUnusedAsset;
            return _cone;
        }

        /// <summary>
        /// Additive, unlit, both sides, no depth write. URP's particle shader takes the cone's vertex colours, so the flame
        /// fades out to its tip; URP/Unlit is the fallback and draws an even cone.
        /// </summary>
        private static bool Materials()
        {
            if (_outer != null && _core != null) return true;
            if (_materialsTried) return false;
            _materialsTried = true;
            string used = "Universal Render Pipeline/Particles/Unlit";
            var shader = Shader.Find(used);
            if (shader == null) { used = "Universal Render Pipeline/Unlit"; shader = Shader.Find(used); }
            if (shader == null) { Core.Log.Error("[Flames] no URP unlit shader found - nitro shows no flames."); return false; }
            _outer = Additive(shader, new Color(0.25f, 0.45f, 1.6f));
            _core = Additive(shader, new Color(1.1f, 1.2f, 1.6f));
            Core.Log.Msg("[Flames] flame materials use '" + used + "'.");
            return true;
        }

        private static Material Additive(Shader shader, Color colour)
        {
            var m = new Material(shader) { name = "bodyshop_flame" };
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", 2f); // additive
            m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.One);
            m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
            m.SetFloat("_ZWrite", 0f);
            m.SetFloat("_Cull", 0f);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = 3000;
            m.SetColor("_BaseColor", colour);
            m.hideFlags |= HideFlags.DontUnloadUnusedAsset;
            return m;
        }
    }
}
