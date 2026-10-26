using System;
using System.Collections.Generic;
using MarcosBodyshop.Api;
using UnityEngine;

namespace MarcosBodyshop.Shop
{
    /// <summary>
    /// The painted outline of each bay with <see cref="BodyshopBay.Marked"/>, or of every bay while <see cref="ShowAll"/>
    /// is on (the debug console): four flat strips on the ground, no colliders. Built when the world is loaded and
    /// rebuilt when a bay is added.
    /// </summary>
    internal static class Marking
    {
        private const float Width = 0.15f;
        private static readonly List<GameObject> _lines = new List<GameObject>();
        private static bool _inWorld;
        private static Material _material;

        /// <summary>Outline every bay with a box, marked or not.</summary>
        internal static bool ShowAll;

        internal static void WorldLoaded() { _inWorld = true; Refresh(); }

        internal static void WorldLeft()
        {
            _inWorld = false;
            Clear();
        }

        internal static void Refresh()
        {
            if (!_inWorld) return;
            Clear();
            int n = 0;
            foreach (var g in Registry.Garages)
                foreach (var b in g.Bays)
                {
                    // A bay that finds its vehicle itself (the vanilla hall) has no box to outline.
                    if (!b.Marked && (!ShowAll || b.FindVehicle != null)) continue;
                    try { Build(b); n++; }
                    catch (Exception e) { Core.Log.Error("[Bay] marking bay '" + b.Id + "' of '" + g.Id + "' failed: " + e.Message); }
                }
            Core.Log.Msg("[Bay] " + n + " bay(s) marked on the ground" + (ShowAll ? " (every bay, debug)" : "") + ".");
        }

        private static void Clear()
        {
            foreach (var go in _lines) if (go != null) UnityEngine.Object.Destroy(go);
            _lines.Clear();
        }

        private static void Build(BodyshopBay b)
        {
            var rot = Quaternion.Euler(0f, b.Yaw, 0f);
            float hx = b.Size.x * 0.5f, hz = b.Size.z * 0.5f;
            Strip(b, rot, new Vector3(-hx, 0f, 0f), new Vector3(Width, 0.01f, b.Size.z));
            Strip(b, rot, new Vector3(hx, 0f, 0f), new Vector3(Width, 0.01f, b.Size.z));
            Strip(b, rot, new Vector3(0f, 0f, -hz), new Vector3(b.Size.x, 0.01f, Width));
            Strip(b, rot, new Vector3(0f, 0f, hz), new Vector3(b.Size.x, 0.01f, Width));
        }

        private static void Strip(BodyshopBay b, Quaternion rot, Vector3 local, Vector3 size)
        {
            Vector3 at = b.Center + rot * local;
            // Find the ground under the strip; the bay's centre height is only the box, not the floor.
            if (Physics.Raycast(at + Vector3.up * 3f, Vector3.down, out RaycastHit hit, 8f, ~0, QueryTriggerInteraction.Ignore))
                at.y = hit.point.y + 0.012f;
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "MarcosBodyshop_BayLine_" + b.Id;
            var col = go.GetComponent<Collider>();
            if (col != null) UnityEngine.Object.Destroy(col);
            go.transform.SetPositionAndRotation(at, rot);
            go.transform.localScale = size;
            var r = go.GetComponent<Renderer>();
            if (r != null)
            {
                r.sharedMaterial = MaterialOf(r.sharedMaterial);
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            _lines.Add(go);
        }

        private static Material MaterialOf(Material fallback)
        {
            if (_material != null) return _material;
            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) { Core.Log.Warning("[Bay] no URP unlit shader - the bay lines use the default material."); return fallback; }
            _material = new Material(shader) { name = "MarcosBodyshop_BayLine" };
            _material.SetColor("_BaseColor", new Color(0.93f, 0.76f, 0.2f, 1f));
            return _material;
        }
    }
}
