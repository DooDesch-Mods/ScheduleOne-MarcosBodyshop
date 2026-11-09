using System;
using System.Collections.Generic;
using Il2CppScheduleOne.Vehicles;
using MarcosBodyshop.Shop;
using UnityEngine;

namespace MarcosBodyshop.Builtin
{
    /// <summary>
    /// The roof load of the trunk tiers, made from the game's own props: a layer of bin bags (tier 1), a block of boxes
    /// (tier 2), and a mattress with an armchair, a box and a bag on top (tier 3), each held by straps. The pieces sit on the highest flat stretch of the roof, found by
    /// casting down onto the car's own colliders, so a pickup carries them on its cab.
    /// </summary>
    internal static class RoofLoad
    {
        private static readonly string[] BagMeshes = { "Garbage bag", "GarbageBag", "Trash bag", "Bag_LOD0" };
        private static readonly string[] BoxMeshes = { "Box", "Box_0", "Box_1", "Cardboard Box", "CardboardBox" };
        // "name@part" takes a mesh only from an object whose path holds that part: two meshes are called "Bed", and the
        // mattress is the one inside the RV.
        private static readonly string[] MattressMeshes = { "Bed@Interior", "Mattress", "Hippie_Bed_LOD0" };
        private static readonly string[] ArmchairMeshes = { "Armchair" };
        private static readonly string[] StrapSource = { "Tire" };
        private static readonly Color StrapColour = new Color(0.1f, 0.3f, 0.85f);
        private static Material _strap;

        private sealed class Prop { internal Mesh Mesh; internal Material[] Materials; internal string Name; internal Quaternion Rot = Quaternion.identity; internal Vector3 Scale = Vector3.one; }
        private static readonly Dictionary<string, Prop> _props = new Dictionary<string, Prop>();
        private static readonly HashSet<string> _missingLogged = new HashSet<string>();

        internal static GameObject Build(LandVehicle v, int tier)
        {
            var root = new GameObject("MarcoTrunk_" + tier);
            try
            {
                Roof(v, out Vector3 top, out float length, out float width);
                // Everything lines up with the car in a grid of two across, held down by straps across the load.
                float w = width * 0.9f, half = w * 0.5f;
                switch (tier)
                {
                    case 1:
                    {
                        // Bin bags lying flat, pressed against each other.
                        int n = Mathf.Clamp(Mathf.RoundToInt(length / 0.55f), 2, 4);
                        float cell = length / n, h = Mathf.Min(0.34f, half * 0.7f);
                        var bag = Find("bag", BagMeshes);
                        for (int i = 0; i < n; i++)
                            for (int s = 0; s < 2; s++)
                                Fit(root, bag, top + new Vector3((s - 0.5f) * half, 0f, (i + 0.5f - n * 0.5f) * cell),
                                    new Vector3(half * 1.04f, h, cell * 1.04f), Jitter(i * 2 + s, 6f));
                        // The bags are round, so the strap runs over their tops and slants down to the roof's edges.
                        Straps(root, top, length, half * 0.55f, half + 0.02f, h * 0.9f);
                        break;
                    }
                    case 2:
                    {
                        // A block of boxes, two layers high.
                        int n = Mathf.Clamp(Mathf.RoundToInt(length / half), 2, 4);
                        float cell = length / n, h = Mathf.Min(0.4f, half * 0.85f);
                        var box = Find("box", BoxMeshes);
                        for (int layer = 0; layer < 2; layer++)
                            for (int i = 0; i < n; i++)
                                for (int s = 0; s < 2; s++)
                                    Fit(root, box, top + new Vector3((s - 0.5f) * half, layer * h, (i + 0.5f - n * 0.5f) * cell),
                                        new Vector3(half * 0.98f, h, cell * 0.98f), Jitter(layer * 8 + i * 2 + s, 2f));
                        Straps(root, top, length, half, half, 2f * h);
                        break;
                    }
                    default:
                    {
                        // A mattress over the whole roof with an armchair on its back half, facing backwards, and a box
                        // and a bin bag at the front. Straps hold the front pieces and both ends of the mattress.
                        float ml = length * 1.2f, mw = w * 1.1f, mh = Mathf.Clamp(mw * 0.2f, 0.22f, 0.28f);
                        float m = Fit(root, Find("mattress", MattressMeshes), top, new Vector3(mw, mh, ml), 0f, lengthAlongZ: true) != null ? mh : 0f;
                        Vector3 on = top + new Vector3(0f, m, 0f);
                        var chair = Find("armchair", ArmchairMeshes);
                        if (chair != null)
                        {
                            var cs = Size(chair);
                            float k = Mathf.Min(w * 0.65f / Mathf.Max(0.01f, cs.x), length * 0.45f / Mathf.Max(0.01f, cs.z));
                            Fit(root, chair, on + new Vector3(0f, 0f, -0.22f * length), cs * k, 180f);
                        }
                        float front = 0.24f * length, side = Mathf.Min(half * 0.8f, length * 0.34f);
                        Fit(root, Find("box", BoxMeshes), on + new Vector3(-0.5f * half, 0f, front), new Vector3(side, side, side), Jitter(1, 3f));
                        Fit(root, Find("bag", BagMeshes), on + new Vector3(0.5f * half, 0f, front), new Vector3(side, side * 0.8f, side), Jitter(2, 6f));
                        float reach = 0.5f * half + 0.5f * side;
                        Strap(root, on + new Vector3(0f, 0f, front), reach, reach, side, 0f);
                        Strap(root, top + new Vector3(0f, 0f, 0.55f * length), mw * 0.5f, mw * 0.5f, m, 0f);
                        Strap(root, top + new Vector3(0f, 0f, -0.55f * length), mw * 0.5f, mw * 0.5f, m, 0f);
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

        /// <summary>
        /// Puts a prop with the middle of its bottom on <paramref name="at"/>, stretched to exactly <paramref name="size"/>
        /// (before the turn by <paramref name="yaw"/>). With <paramref name="lengthAlongZ"/> a prop that is longer across
        /// than along is turned a quarter first, so its long side runs along the car.
        /// </summary>
        private static GameObject Fit(GameObject root, Prop p, Vector3 at, Vector3 size, float yaw, bool lengthAlongZ = false)
        {
            if (p == null) return null;
            if (lengthAlongZ) p = LaidFlat(p);
            var go = Piece(root, p, out var b);
            Vector3 s = b.size;
            if (lengthAlongZ && s.x > s.z) { yaw += 90f; size = new Vector3(size.z, size.y, size.x); }
            var k = new Vector3(size.x / Mathf.Max(0.01f, s.x), size.y / Mathf.Max(0.01f, s.y), size.z / Mathf.Max(0.01f, s.z));
            var rot = Quaternion.Euler(0f, yaw, 0f);
            go.transform.localRotation = rot;
            go.transform.localScale = k;
            Vector3 c = rot * Vector3.Scale(b.center, k);
            go.transform.localPosition = at - new Vector3(c.x, c.y - size.y * 0.5f, c.z);
            return go;
        }

        /// <summary>
        /// The prop tipped so its thinnest side is up. The RV mattress stands upright in its own space; the bed frame
        /// above it lays it down.
        /// </summary>
        private static Prop LaidFlat(Prop p)
        {
            Vector3 s = Size(p);
            Quaternion tilt;
            if (s.y <= s.x && s.y <= s.z) return p;
            tilt = s.z <= s.x ? Quaternion.Euler(-90f, 0f, 0f) : Quaternion.Euler(0f, 0f, 90f);
            return new Prop { Mesh = p.Mesh, Materials = p.Materials, Name = p.Name, Rot = tilt * p.Rot, Scale = p.Scale };
        }

        /// <summary>The size of a prop with the scale and tilt of the object it came from.</summary>
        private static Vector3 Size(Prop p) => Abs(Matrix4x4.TRS(Vector3.zero, p.Rot, p.Scale).MultiplyVector(p.Mesh.bounds.size));

        /// <summary>A small fixed turn per piece, so a grid of the same prop does not look copied.</summary>
        private static float Jitter(int i, float max) => max * Mathf.Sin(i * 2.399f + 0.7f);

        /// <summary>Two straps across a load on the roof, a quarter of the length in from each end.</summary>
        private static void Straps(GameObject root, Vector3 top, float length, float over, float edge, float height)
        {
            Strap(root, top + new Vector3(0f, 0f, 0.25f * length), over, edge, height, 0.05f);
            Strap(root, top + new Vector3(0f, 0f, -0.25f * length), over, edge, height, 0.05f);
        }

        /// <summary>
        /// A strap across the car through <paramref name="at"/> (the middle of the surface the load stands on): flat over
        /// the load between ±<paramref name="over"/> at <paramref name="height"/>, then down both sides to ±<paramref name="edge"/>,
        /// <paramref name="below"/> under the surface so it meets a curved roof.
        /// </summary>
        private static void Strap(GameObject root, Vector3 at, float over, float edge, float height, float below)
        {
            var mat = StrapMaterial();
            if (mat == null) return;
            const float Thick = 0.012f;
            float y = at.y + height + Thick * 0.5f;
            Bar(root, mat, new Vector3(at.x - over, y, at.z), new Vector3(at.x + over, y, at.z));
            foreach (float sx in new[] { -1f, 1f })
                Bar(root, mat, new Vector3(at.x + sx * over, y, at.z), new Vector3(at.x + sx * (edge + Thick * 0.5f), at.y - below, at.z));
        }

        /// <summary>One flat piece of strap from <paramref name="a"/> to <paramref name="b"/>, its face towards the outside.</summary>
        private static void Bar(GameObject root, Material mat, Vector3 a, Vector3 b)
        {
            const float Wide = 0.05f, Thick = 0.012f;
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "Trunk_strap";
            var col = go.GetComponent<Collider>();
            if (col != null) UnityEngine.Object.DestroyImmediate(col);
            go.transform.SetParent(root.transform, false);
            Vector3 d = b - a;
            go.transform.localPosition = (a + b) * 0.5f;
            go.transform.localRotation = Quaternion.FromToRotation(Vector3.right, d.normalized);
            go.transform.localScale = new Vector3(d.magnitude + Thick, Thick, Wide);
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
        }

        /// <summary>The strap's look: a tyre's material without its texture, in tie-down blue.</summary>
        private static Material StrapMaterial()
        {
            if (_strap != null) return _strap;
            var p = Find("strap", StrapSource);
            if (p == null) return null;
            _strap = new Material(p.Materials[0]) { name = "Trunk_strap" };
            foreach (string tex in new[] { "_BaseMap", "_MainTex", "_BumpMap", "_MetallicGlossMap", "_OcclusionMap" })
                if (_strap.HasProperty(tex)) _strap.SetTexture(tex, null);
            if (_strap.HasProperty("_BaseColor")) _strap.SetColor("_BaseColor", StrapColour);
            if (_strap.HasProperty("_Color")) _strap.SetColor("_Color", StrapColour);
            return _strap;
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
            foreach (string entry in names)
            {
                int at = entry.IndexOf('@');
                string name = at < 0 ? entry : entry.Substring(0, at), part = at < 0 ? null : entry.Substring(at + 1);
                foreach (var f in filters)
                {
                    var mesh = f != null ? f.sharedMesh : null;
                    if (mesh == null || !string.Equals(mesh.name, name, StringComparison.OrdinalIgnoreCase)) continue;
                    if (part != null && Path(f.transform).IndexOf(part, StringComparison.OrdinalIgnoreCase) < 0) continue;
                    var r = f.GetComponent<MeshRenderer>();
                    if (r == null || r.sharedMaterials == null || r.sharedMaterials.Length == 0) continue;
                    var prop = new Prop { Mesh = mesh, Materials = r.sharedMaterials, Name = "Trunk_" + what, Rot = NoYaw(f.transform.localRotation), Scale = f.transform.localScale };
                    _props[what] = prop;
                    Core.Log.Msg("[Trunk] " + what + " prop: mesh '" + mesh.name + "' from '" + f.name + "', " + mesh.bounds.size.ToString("F2")
                        + ", local scale " + prop.Scale.ToString("F2") + ", rotation " + prop.Rot.eulerAngles.ToString("F0") + ".");
                    return prop;
                }
            }
            if (_missingLogged.Add(what)) Core.Log.Warning("[Trunk] no " + what + " prop found (meshes " + string.Join(", ", names) + ") - the roof load goes without it.");
            return null;
        }

#if DEBUG
        private static readonly List<GameObject> _shown = new List<GameObject>();

        /// <summary>Every loaded mesh with that name, one line per distinct mesh: index, size with the source's scale, source path.</summary>
        internal static List<string> ListMeshes(string name, out List<MeshFilter> found)
        {
            found = new List<MeshFilter>();
            var lines = new List<string>();
            var seen = new HashSet<int>();
            foreach (var f in Resources.FindObjectsOfTypeAll<MeshFilter>())
            {
                var mesh = f != null ? f.sharedMesh : null;
                if (mesh == null || !string.Equals(mesh.name, name, StringComparison.OrdinalIgnoreCase) || !seen.Add(mesh.GetInstanceID())) continue;
                if (f.GetComponent<MeshRenderer>() == null) continue;
                var size = Abs(Matrix4x4.TRS(Vector3.zero, NoYaw(f.transform.localRotation), f.transform.localScale).MultiplyVector(mesh.bounds.size));
                lines.Add(found.Count + ": " + size.ToString("F2") + " from " + Path(f.transform));
                found.Add(f);
            }
            return lines;
        }

        /// <summary>Puts a copy of mesh <paramref name="name"/> (the <paramref name="index"/>th distinct one) on the ground at <paramref name="at"/>.</summary>
        internal static string Show(string name, int index, Vector3 at, float yaw)
        {
            ListMeshes(name, out var found);
            if (index < 0 || index >= found.Count) return "no mesh '" + name + "' #" + index + " (" + found.Count + " loaded)";
            var f = found[index];
            var p = new Prop { Mesh = f.sharedMesh, Materials = f.GetComponent<MeshRenderer>().sharedMaterials, Name = "PropShow_" + name,
                Rot = NoYaw(f.transform.localRotation), Scale = f.transform.localScale };
            var root = new GameObject("PropShow");
            var go = Piece(root, p, out var b);
            go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            Vector3 c = go.transform.localRotation * b.center;
            go.transform.localPosition = new Vector3(-c.x, b.extents.y - c.y, -c.z);
            root.transform.position = at;
            _shown.Add(root);
            return name + " #" + index + " " + b.size.ToString("F2") + " at " + at.ToString("F1");
        }

        internal static string ClearShown()
        {
            int n = _shown.Count;
            foreach (var g in _shown) if (g != null) UnityEngine.Object.Destroy(g);
            _shown.Clear();
            return n + " shown prop(s) removed";
        }
#endif

        private static string Path(Transform t)
        {
            string s = t.name;
            for (var p = t.parent; p != null; p = p.parent) s = p.name + "/" + s;
            return s;
        }

        /// <summary>The prop meshes the roof load uses and where they came from, for the debug console.</summary>
        internal static string Report()
        {
            var parts = new List<string>();
            foreach (var w in new[] { ("bag", BagMeshes), ("box", BoxMeshes), ("mattress", MattressMeshes), ("armchair", ArmchairMeshes), ("strap", StrapSource) })
            {
                var p = Find(w.Item1, w.Item2);
                parts.Add(w.Item1 + ": " + (p != null ? p.Mesh.name + " " + Size(p).ToString("F2") : "missing"));
            }
            return string.Join(" | ", parts);
        }
    }
}
