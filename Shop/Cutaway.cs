using System;
using System.Collections.Generic;
using Il2CppScheduleOne.PlayerScripts;
using Il2CppScheduleOne.Vehicles;
using UnityEngine;
using UnityEngine.Rendering;

namespace MarcosBodyshop.Shop
{
    /// <summary>
    /// Fades what stands between the menu camera and the vehicle: a pole, a sign, the corner of a building. Each blocking
    /// renderer gets transparent copies of its materials (URP Lit, its colour and texture; text keeps its own shader and
    /// fades its face colour) and fades to a faint ghost; it fades back in when the camera moves on, and gets its own
    /// materials back when the menu closes. Without the URP Lit shader a blocker draws only its shadow.
    /// </summary>
    internal static class Cutaway
    {
        private const float Ghost = 0.08f;          // alpha of a faded blocker
        private const float FadeSeconds = 0.3f;
        private const float RayRadius = 0.35f;
        private const float GridStep = 1.5f;        // metres between two sight points on the vehicle

        private sealed class Faded
        {
            internal Renderer R;
            internal Material[] Original;
            internal Material[] Mine;
            internal string[] Prop;     // the colour property whose alpha fades, per material
            internal Color[] Base;      // that colour as the original had it
            internal ShadowCastingMode Shadows;
            internal float Alpha = 1f;
            internal bool Out;
        }

        private static readonly Dictionary<IntPtr, Faded> _faded = new Dictionary<IntPtr, Faded>();
        private static readonly List<IntPtr> _done = new List<IntPtr>();
        private static Shader _lit;
        private static bool _litMissing;

        /// <summary>The camera goes to <paramref name="eye"/>: fade what blocks the view of the vehicle, bring back the rest.</summary>
        internal static void Aim(LandVehicle v, Vector3 eye)
        {
            try
            {
                var watch = System.Diagnostics.Stopwatch.StartNew();
                var blockers = Blockers(v, eye);
                foreach (var f in _faded.Values) f.Out = false;
                int added = 0;
                foreach (var r in blockers)
                {
                    if (!_faded.TryGetValue(r.Pointer, out var f))
                    {
                        f = Make(r);
                        if (f == null) continue;
                        _faded[r.Pointer] = f;
                        added++;
                    }
                    f.Out = true;
                }
                if (blockers.Count > 0 || added > 0)
                    Core.Log.Msg("[Menu] camera view: " + blockers.Count + " blocker(s) faded" + (added > 0 ? ", " + added + " new" : "") + " (" + watch.ElapsedMilliseconds + " ms).");
            }
            catch (Exception e) { Core.Log.Warning("[Menu] fading what blocks the camera failed - it stays in view: " + e.Message); }
        }

        /// <summary>Per frame while the menu is open: move each blocker's alpha towards its target.</summary>
        internal static void Tick()
        {
            if (_faded.Count == 0) return;
            float step = Time.unscaledDeltaTime / FadeSeconds * (1f - Ghost);
            _done.Clear();
            foreach (var kv in _faded)
            {
                var f = kv.Value;
                if (f.R == null) { _done.Add(kv.Key); continue; }
                float target = f.Out ? Ghost : 1f;
                if (Mathf.Approximately(f.Alpha, target)) continue;
                f.Alpha = Mathf.MoveTowards(f.Alpha, target, step);
                if (!f.Out && f.Alpha >= 1f) { Restore(f); _done.Add(kv.Key); continue; }
                SetAlpha(f);
            }
            foreach (var k in _done) _faded.Remove(k);
        }

        /// <summary>Every blocker gets its own materials back at once.</summary>
        internal static void Clear()
        {
            int n = 0;
            foreach (var f in _faded.Values)
            {
                try { if (f.R != null) { Restore(f); n++; } }
                catch (Exception e) { Core.Log.Warning("[Menu] putting a faded object back failed: " + e.Message); }
            }
            _faded.Clear();
            if (n > 0) Core.Log.Msg("[Menu] " + n + " faded object(s) back in view.");
        }

        // ---- what blocks ----------------------------------------------------------------------------------------

        // Sight lines from the eye to a grid over the vehicle's box, about 1.5 m apart, so thin things (a fence, a pole,
        // a phone booth) in front of any part of it are found. A collider on a line names its object. Its renderers count
        // when their box is on the line (not the shelves inside a building); renderers of the object and its siblings
        // count when they sit inside the collider (the digits on a sign, the top bar of a fence whose collider is the
        // mesh beside it), or overlap it and are on the line.
        private static List<Renderer> Blockers(LandVehicle v, Vector3 eye)
        {
            var list = new List<Renderer>();
            var seen = new HashSet<IntPtr>();
            var hitColliders = new HashSet<IntPtr>();
            var t = v.transform;
            var box = Session.LocalBox(v);
            Vector3 c = box.center, h = box.extents;
            var xs = Steps(-0.8f * h.x, 0.8f * h.x);
            var ys = Steps(-0.6f * h.y, 0.8f * h.y);
            var zs = Steps(-0.85f * h.z, 0.85f * h.z);
            var points = new List<Vector3>(xs.Length * ys.Length * zs.Length);
            foreach (float x in xs) foreach (float y in ys) foreach (float z in zs)
                points.Add(t.TransformPoint(c + new Vector3(x, y, z)));
            var player = Player.Local != null ? Player.Local.transform : null;
            foreach (var p in points)
            {
                Vector3 d = p - eye;
                float len = d.magnitude;
                if (len < 0.5f) continue;
                var ray = new Ray(eye, d / len);
                var hits = new List<RaycastHit>(Physics.SphereCastAll(eye, RayRadius, d / len, len, ~0, QueryTriggerInteraction.Ignore));
                // A sphere cast misses a collider it starts in (an eye inside a building); the line back from the vehicle finds it.
                foreach (var hit in Physics.RaycastAll(p, -d / len, len, ~0, QueryTriggerInteraction.Ignore)) hits.Add(hit);
                foreach (var hit in hits)
                {
                    var col = hit.collider;
                    if (col == null || col.transform.IsChildOf(t)) continue;
                    if (player != null && col.transform.IsChildOf(player)) continue;
                    // A low line grazes the road; ground faces up and is never in the way.
                    if (hit.distance > 0f && hit.normal.y > 0.7f) continue;
                    var owner = col.transform;
                    foreach (var r in owner.GetComponentsInChildren<Renderer>())
                    {
                        if (r == null || !r.enabled || seen.Contains(r.Pointer) || r.transform.IsChildOf(t)) continue;
                        var b = r.bounds;
                        b.Expand(RayRadius * 2f);
                        if (!b.IntersectRay(ray, out float at) || at > len) continue;
                        seen.Add(r.Pointer);
                        list.Add(r);
                    }
                    if (!hitColliders.Add(col.Pointer)) continue;
                    var shell = col.bounds;
                    // A collider the size of a block (a foundation, a road) would take a whole district with it.
                    if (shell.size.x > 30f || shell.size.z > 30f) continue;
                    shell.Expand(0.5f);
                    // Colliders often sit in a group of their own ("Pay Phone/Colliders/Cube" beside "Pay Phone/pay phone/
                    // PayPhone_low"); climb until the family holds a mesh, at most three levels.
                    var family = owner.parent != null ? owner.parent : owner;
                    for (int up = 0; up < 3 && family.parent != null && family.GetComponentsInChildren<Renderer>().Length == 0; up++)
                        family = family.parent;
                    foreach (var r in family.GetComponentsInChildren<Renderer>())
                    {
                        if (r == null || !r.enabled || seen.Contains(r.Pointer) || r.transform.IsChildOf(t)) continue;
                        var b = r.bounds;
                        bool inside = shell.Contains(b.min) && shell.Contains(b.max);
                        if (!inside)
                        {
                            // A mesh larger than one of its colliders (the booth over its post) counts when it overlaps
                            // that collider and is on the line.
                            if (!b.Intersects(shell)) continue;
                            b.Expand(RayRadius * 2f);
                            if (!b.IntersectRay(ray, out float at) || at > len) continue;
                        }
                        seen.Add(r.Pointer);
                        list.Add(r);
                    }
                }
            }
            return list;
        }

        // From a to b in steps of at most GridStep, both ends included.
        private static float[] Steps(float a, float b)
        {
            int n = Mathf.Clamp(Mathf.CeilToInt((b - a) / GridStep), 1, 16);
            var s = new float[n + 1];
            for (int i = 0; i <= n; i++) s[i] = Mathf.Lerp(a, b, i / (float)n);
            return s;
        }

        // ---- fading ---------------------------------------------------------------------------------------------

        private static Faded Make(Renderer r)
        {
            var f = new Faded { R = r, Original = r.sharedMaterials, Shadows = r.shadowCastingMode };
            if (Lit() == null)
            {
                r.shadowCastingMode = ShadowCastingMode.ShadowsOnly;
                f.Alpha = Ghost;
                return f;
            }
            int n = f.Original.Length;
            f.Mine = new Material[n];
            f.Prop = new string[n];
            f.Base = new Color[n];
            for (int i = 0; i < n; i++)
            {
                var src = f.Original[i];
                if (src != null && src.HasProperty("_FaceColor"))
                {
                    // Text: a Lit copy would draw the font atlas as a block; its own shader is transparent already, and it
                    // blends premultiplied, so the whole colour fades, not only its alpha.
                    f.Mine[i] = new Material(src) { name = "MarcosBodyshop_Cutaway" };
                    f.Prop[i] = "_FaceColor";
                }
                else
                {
                    f.Mine[i] = Transparent(src);
                    f.Prop[i] = "_BaseColor";
                }
                f.Base[i] = f.Mine[i].GetColor(f.Prop[i]);
            }
            r.sharedMaterials = f.Mine;
            return f;
        }

        private static Shader Lit()
        {
            if (_lit != null || _litMissing) return _lit;
            _lit = Shader.Find("Universal Render Pipeline/Lit");
            if (_lit == null) { _litMissing = true; Core.Log.Warning("[Menu] no URP Lit shader - objects in front of the camera are hidden, only their shadow stays."); }
            return _lit;
        }

        // A see-through URP Lit copy that keeps the original's colour and texture.
        private static Material Transparent(Material src)
        {
            var m = new Material(Lit()) { name = "MarcosBodyshop_Cutaway" };
            if (src != null)
            {
                if (src.HasProperty("_BaseMap") && src.GetTexture("_BaseMap") != null) m.SetTexture("_BaseMap", src.GetTexture("_BaseMap"));
                else if (src.HasProperty("_MainTex") && src.GetTexture("_MainTex") != null) m.SetTexture("_BaseMap", src.GetTexture("_MainTex"));
                Color c = src.HasProperty("_BaseColor") ? src.GetColor("_BaseColor") : src.HasProperty("_Color") ? src.GetColor("_Color") : Color.white;
                m.SetColor("_BaseColor", c);
            }
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", 0f);
            m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_ZWrite", 0f);
            m.SetOverrideTag("RenderType", "Transparent");
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = (int)RenderQueue.Transparent;
            return m;
        }

        private static void SetAlpha(Faded f)
        {
            if (f.Mine == null) return;
            for (int i = 0; i < f.Mine.Length; i++)
            {
                var m = f.Mine[i];
                if (m == null) continue;
                Color c = f.Base[i];
                if (f.Prop[i] == "_FaceColor") c *= f.Alpha;
                else c.a *= f.Alpha;
                m.SetColor(f.Prop[i], c);
            }
        }

        private static void Restore(Faded f)
        {
            if (f.R == null) return;
            f.R.sharedMaterials = f.Original;
            f.R.shadowCastingMode = f.Shadows;
            if (f.Mine != null) foreach (var m in f.Mine) if (m != null) UnityEngine.Object.Destroy(m);
        }
    }
}
