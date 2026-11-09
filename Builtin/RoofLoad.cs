using System;
using System.Collections.Generic;
using Il2CppScheduleOne.Vehicles;
using MarcosBodyshop.Shop;
using UnityEngine;

namespace MarcosBodyshop.Builtin
{
    /// <summary>
    /// The roof load of the trunk tiers, made from the game's own props: bin bags (tier 1), boxes and bags (tier 2), and
    /// a mattress with boxes and bags on top (tier 3). The pieces sit on the highest flat stretch of the roof, found by
    /// casting down onto the car's own colliders, so a pickup carries them on its cab.
    /// </summary>
    internal static class RoofLoad
    {
        private static readonly string[] BagMeshes = { "Garbage bag", "GarbageBag", "Trash bag", "Bag_LOD0" };
        private static readonly string[] BoxMeshes = { "Box", "Box_0", "Box_1", "Cardboard Box", "CardboardBox" };
        private static readonly string[] MattressMeshes = { "Mattress", "Hippie_Bed_LOD0" };

        private sealed class Prop { internal Mesh Mesh; internal Material[] Materials; internal string Name; internal Quaternion Rot = Quaternion.identity; internal Vector3 Scale = Vector3.one; }
        private static readonly Dictionary<string, Prop> _props = new Dictionary<string, Prop>();
        private static readonly HashSet<string> _missingLogged = new HashSet<string>();

        internal static GameObject Build(LandVehicle v, int tier)
        {
            var root = new GameObject("MarcoTrunk_" + tier);
            try
            {
                Roof(v, out Vector3 top, out float length, out float width);
                // Fixed offsets per piece, in parts of the roof's length and width; heights stack on what lies below.
                switch (tier)
                {
                    case 1:
                        Bag(root, top + new Vector3(-0.2f * width, 0f, 0.25f * length), width * 0.42f, 15f);
                        Bag(root, top + new Vector3(0.22f * width, 0f, 0.05f * length), width * 0.45f, -30f);
                        Bag(root, top + new Vector3(-0.05f * width, 0f, -0.25f * length), width * 0.4f, 70f);
                        break;
                    case 2:
                    {
                        float h1 = Box(root, top + new Vector3(-0.18f * width, 0f, 0.15f * length), width * 0.5f, 5f);
                        Box(root, top + new Vector3(0.2f * width, 0f, -0.15f * length), width * 0.45f, -12f);
                        Box(root, top + new Vector3(-0.15f * width, h1, 0.15f * length), width * 0.38f, 25f);
                        Bag(root, top + new Vector3(0.2f * width, 0f, 0.3f * length), width * 0.38f, 40f);
                        Bag(root, top + new Vector3(-0.2f * width, 0f, -0.32f * length), width * 0.38f, -20f);
                        break;
                    }
                    default:
                    {
                        // The mattress is longer than the flat roof and hangs over its ends a little.
                        float m = Mattress(root, top, length * 1.3f, width * 1.15f);
                        Vector3 on = top + new Vector3(0f, m, 0f);
                        float h1 = Box(root, on + new Vector3(-0.15f * width, 0f, 0.18f * length), width * 0.45f, -8f);
                        Box(root, on + new Vector3(0.18f * width, 0f, -0.2f * length), width * 0.42f, 18f);
                        Box(root, on + new Vector3(-0.12f * width, h1, 0.18f * length), width * 0.34f, 30f);
                        Bag(root, on + new Vector3(0.22f * width, 0f, 0.25f * length), width * 0.36f, -35f);
                        Bag(root, on + new Vector3(-0.2f * width, 0f, -0.3f * length), width * 0.36f, 60f);
                        break;
                    }
                }
            }
            catch (Exception e) { Core.Log.Error("[Trunk] building the roof load of tier " + tier + " on " + Trunk.Describe(v) + " failed: " + e); }
            return root;
        }

        // ---- the roof ------------------------------------------------------------------------------------------------

        /// <summary>
        /// The middle of the highest flat stretch of the roof, in the car's space, with its usable length and width.
        /// Columns down the car's centre line hit its colliders; the stretch is every column within 12 cm of the top.
        /// </summary>
        private static void Roof(LandVehicle v, out Vector3 top, out float length, out float width)
        {
            var box = Session.LocalBox(v);
            Vector3 c = box.center, h = box.extents;
            top = new Vector3(c.x, c.y + h.y, c.z);
            length = h.z * 2f * 0.5f;
            width = h.x * 2f * 0.8f;

            var cols = new List<Collider>();
            foreach (var col in v.GetComponentsInChildren<Collider>())
                if (col != null && col.enabled && !col.isTrigger) cols.Add(col);
            if (cols.Count == 0) { Core.Log.Warning("[Trunk] " + Trunk.Describe(v) + " has no solid collider - the load sits on the top of its box."); return; }

            const int Steps = 24;
            var heights = new float?[Steps + 1];
            float best = float.MinValue;
            var t = v.transform;
            for (int i = 0; i <= Steps; i++)
            {
                float z = c.z - h.z + 2f * h.z * i / Steps;
                Vector3 from = t.TransformPoint(new Vector3(c.x, c.y + h.y + 1f, z));
                var ray = new Ray(from, -t.up);
                float nearest = float.MaxValue;
                foreach (var col in cols)
                    if (col.Raycast(ray, out RaycastHit hit, h.y * 2f + 2f) && hit.distance < nearest) nearest = hit.distance;
                if (nearest == float.MaxValue) continue;
                float y = t.InverseTransformPoint(from + (-t.up) * nearest).y;
                heights[i] = y;
                if (y > best) best = y;
            }
            if (best == float.MinValue) { Core.Log.Warning("[Trunk] no ray hit the roof of " + Trunk.Describe(v) + " - the load sits on the top of its box."); return; }

            // The longest run of columns near the top.
            int runStart = -1, bestStart = 0, bestLen = 0;
            for (int i = 0; i <= Steps + 1; i++)
            {
                bool high = i <= Steps && heights[i].HasValue && heights[i].Value >= best - 0.12f;
                if (high && runStart < 0) runStart = i;
                if (!high && runStart >= 0)
                {
                    if (i - runStart > bestLen) { bestLen = i - runStart; bestStart = runStart; }
                    runStart = -1;
                }
            }
            float step = 2f * h.z / Steps;
            float z0 = c.z - h.z + step * bestStart, z1 = c.z - h.z + step * (bestStart + bestLen - 1);
            top = new Vector3(c.x, best, (z0 + z1) * 0.5f);
            length = Mathf.Max(0.6f, (z1 - z0) * 0.9f);
            Core.Log.Msg("[Trunk] roof of " + Trunk.Describe(v) + ": top " + best.ToString("F2") + " m, flat " + (z1 - z0).ToString("F2") + " m of " + (2f * h.z).ToString("F2") + " m, width " + width.ToString("F2") + " m.");
        }

        // ---- pieces --------------------------------------------------------------------------------------------------

        private static float Bag(GameObject root, Vector3 at, float size, float yaw) => Place(root, "bag", BagMeshes, at, size, yaw, flat: false);
        private static float Box(GameObject root, Vector3 at, float size, float yaw) => Place(root, "box", BoxMeshes, at, size, yaw, flat: false);

        /// <summary>The mattress lies flat over the whole roof; returns its thickness.</summary>
        private static float Mattress(GameObject root, Vector3 at, float length, float width)
        {
            var p = Find("mattress", MattressMeshes);
            if (p == null) return 0f;
            var go = Piece(root, p, out var bounds);
            // Lay it flat: its thinnest axis up, its longest along the car.
            Vector3 s = bounds.size;
            Quaternion rot = Quaternion.identity;
            if (s.z <= s.x && s.z <= s.y) rot = Quaternion.Euler(-90f, 0f, 0f);
            else if (s.x <= s.y && s.x <= s.z) rot = Quaternion.Euler(0f, 0f, 90f);
            Vector3 laid = Abs(rot * s);
            if (laid.x > laid.z) { rot = Quaternion.Euler(0f, 90f, 0f) * rot; laid = Abs(rot * s); }
            // Stretched to the given footprint; the thickness follows the mean stretch, kept between 15 and 25 cm.
            float sx = width / Mathf.Max(0.01f, laid.x), sz = length / Mathf.Max(0.01f, laid.z);
            float thick = Mathf.Clamp(laid.y * (sx + sz) * 0.5f, 0.15f, 0.25f);
            Vector3 laidScale = new Vector3(sx, thick / Mathf.Max(0.01f, laid.y), sz);
            // The rotation only swaps axes, so the stretch along the laid axes maps back onto the mesh's own axes.
            Vector3 local = Abs(Quaternion.Inverse(rot) * laidScale);
            go.transform.localRotation = rot;
            go.transform.localScale = local;
            Vector3 centre = rot * Vector3.Scale(bounds.center, local);
            float bottom = centre.y - thick * 0.5f;
            go.transform.localPosition = at - new Vector3(centre.x, bottom, centre.z);
            return thick;
        }

        /// <summary>Puts one prop with its bottom on <paramref name="at"/>, scaled so its widest side is <paramref name="size"/>. Returns its height.</summary>
        private static float Place(GameObject root, string what, string[] names, Vector3 at, float size, float yaw, bool flat)
        {
            var p = Find(what, names);
            if (p == null) return 0f;
            var go = Piece(root, p, out var bounds);
            Vector3 s = bounds.size;
            float k = size / Mathf.Max(0.01f, Mathf.Max(s.x, s.z));
            var rot = Quaternion.Euler(0f, yaw, 0f);
            go.transform.localRotation = rot;
            go.transform.localScale = Vector3.one * k;
            Vector3 centre = rot * bounds.center * k;
            go.transform.localPosition = at - new Vector3(centre.x, centre.y - s.y * 0.5f * k, centre.z);
            return s.y * k;
        }

        private static GameObject Piece(GameObject root, Prop p, out Bounds bounds)
        {
            var go = new GameObject(p.Name);
            go.transform.SetParent(root.transform, false);
            // The mesh keeps the local rotation and scale of the object it came from: some props (the RV bed) carry
            // their size there, not in the mesh. The returned bounds are in the space of the outer object.
            var inner = new GameObject("Mesh");
            inner.transform.SetParent(go.transform, false);
            inner.transform.localRotation = p.Rot;
            inner.transform.localScale = p.Scale;
            inner.AddComponent<MeshFilter>().sharedMesh = p.Mesh;
            var r = inner.AddComponent<MeshRenderer>();
            r.sharedMaterials = p.Materials;
            var m = Matrix4x4.TRS(Vector3.zero, p.Rot, p.Scale);
            var b = p.Mesh.bounds;
            bounds = new Bounds(m.MultiplyPoint3x4(b.center), Vector3.zero);
            for (int i = 0; i < 8; i++)
                bounds.Encapsulate(m.MultiplyPoint3x4(b.center + Vector3.Scale(b.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1))));
            return go;
        }

        /// <summary>The source's tilt without its turn: the turn only fitted the prop to its old spot.</summary>
        private static Quaternion NoYaw(Quaternion q) { var e = q.eulerAngles; return Quaternion.Euler(e.x, 0f, e.z); }

        private static Vector3 Abs(Vector3 v) => new Vector3(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));

        /// <summary>The first prop of the game whose mesh has one of the names, with the materials its renderer uses.</summary>
        private static Prop Find(string what, string[] names)
        {
            if (_props.TryGetValue(what, out var cached) && cached.Mesh != null) return cached;
            var filters = Resources.FindObjectsOfTypeAll<MeshFilter>();
            foreach (string name in names)
                foreach (var f in filters)
                {
                    var mesh = f != null ? f.sharedMesh : null;
                    if (mesh == null || !string.Equals(mesh.name, name, StringComparison.OrdinalIgnoreCase)) continue;
                    var r = f.GetComponent<MeshRenderer>();
                    if (r == null || r.sharedMaterials == null || r.sharedMaterials.Length == 0) continue;
                    var prop = new Prop { Mesh = mesh, Materials = r.sharedMaterials, Name = "Trunk_" + what, Rot = NoYaw(f.transform.localRotation), Scale = f.transform.localScale };
                    _props[what] = prop;
                    Core.Log.Msg("[Trunk] " + what + " prop: mesh '" + mesh.name + "' from '" + f.name + "', " + mesh.bounds.size.ToString("F2")
                        + ", local scale " + prop.Scale.ToString("F2") + ", rotation " + prop.Rot.eulerAngles.ToString("F0") + ".");
                    return prop;
                }
            if (_missingLogged.Add(what)) Core.Log.Warning("[Trunk] no " + what + " prop found (meshes " + string.Join(", ", names) + ") - the roof load goes without it.");
            return null;
        }

        /// <summary>The prop meshes the roof load uses and where they came from, for the debug console.</summary>
        internal static string Report()
        {
            var parts = new List<string>();
            foreach (var w in new[] { ("bag", BagMeshes), ("box", BoxMeshes), ("mattress", MattressMeshes) })
            {
                var p = Find(w.Item1, w.Item2);
                parts.Add(w.Item1 + ": " + (p != null ? p.Mesh.name + " " + Abs(Matrix4x4.TRS(Vector3.zero, p.Rot, p.Scale).MultiplyVector(p.Mesh.bounds.size)).ToString("F2") : "missing"));
            }
            return string.Join(" | ", parts);
        }
    }
}
