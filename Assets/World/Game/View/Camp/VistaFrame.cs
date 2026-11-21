using System;
using System.Collections.Generic;
using UnityEngine;

namespace World.Game
{
    /// <summary>Where the opening vista looks from: a camera position, a bearing and a downward pitch (field of view <see cref="VistaFrame.Fov"/>).</summary>
    public struct VistaPose
    {
        public Vector3 Position;
        public float Yaw, Pitch;
        public bool Valid;
        public Quaternion Rotation => Quaternion.Euler(Pitch, Yaw, 0f);
        public override string ToString() => "pose at " + Position.ToString("0") + ", bearing " + Yaw.ToString("0") + ", pitch " + Pitch.ToString("0");
    }

    /// <summary>
    /// The owner's approved opening screen (Docs/design/OPENING_TARGET.md, 21 Nov 2026) as pure geometry: six blocks of a landscape screen,
    /// one per place (top row far away: temple, council, rhythm; bottom row near: duel, healer, cave), a 16:9 reference frame the vista
    /// camera composes for, and the projection between the two. Every screen shows the whole 16:9 reference frame centred in its safe area
    /// (wider or taller screens see a little more round it); the blocks always divide the safe area itself. Pure: no scene access.
    /// </summary>
    public static class VistaFrame
    {
        public const float Fov = 34f, RefAspect = 16f / 9f;
        /// <summary>The row split (fraction of the safe area's height from the top).</summary>
        public const float RowSplit = 0.44f;
        /// <summary>Column splits of the top (far) row and of the bottom (near) row (fractions of the safe area's width from the left).</summary>
        public const float TopA = 0.29f, TopB = 0.69f, BottomA = 0.32f, BottomB = 0.63f;

        /// <summary>The block of a place (fractions of the safe area, top-left origin).</summary>
        public static Rect Block(string zone)
        {
            switch (zone)
            {
                case WorldZones.HubTemple: return Rect.MinMaxRect(0f, 0f, TopA, RowSplit);
                case WorldZones.HubCouncil: return Rect.MinMaxRect(TopA, 0f, TopB, RowSplit);
                case WorldZones.HubRhythm: return Rect.MinMaxRect(TopB, 0f, 1f, RowSplit);
                case WorldZones.HubDuel: return Rect.MinMaxRect(0f, RowSplit, BottomA, 1f);
                case WorldZones.HubHealer: return Rect.MinMaxRect(BottomA, RowSplit, BottomB, 1f);
                case WorldZones.HubCave: return Rect.MinMaxRect(BottomB, RowSplit, 1f, 1f);
            }
            return Rect.zero;
        }

        /// <summary>The zone whose block holds a point of the safe area (fractions, top-left origin; points outside are clamped in).</summary>
        public static string ZoneAt(float u, float vTop)
        {
            u = Mathf.Clamp01(u); vTop = Mathf.Clamp01(vTop);
            if (vTop < RowSplit) return u < TopA ? WorldZones.HubTemple : u < TopB ? WorldZones.HubCouncil : WorldZones.HubRhythm;
            return u < BottomA ? WorldZones.HubDuel : u < BottomB ? WorldZones.HubHealer : WorldZones.HubCave;
        }

        public static readonly string[] Zones = { WorldZones.HubTemple, WorldZones.HubCouncil, WorldZones.HubRhythm, WorldZones.HubDuel, WorldZones.HubHealer, WorldZones.HubCave };

        /// <summary>Where the composition puts each place's centre in the 16:9 reference frame (top-left origin).</summary>
        public static Vector2 Target(string zone)
        {
            switch (zone)
            {
                case WorldZones.HubTemple: return new Vector2(0.15f, 0.26f);
                case WorldZones.HubCouncil: return new Vector2(0.49f, 0.24f);
                case WorldZones.HubRhythm: return new Vector2(0.84f, 0.24f);
                case WorldZones.HubDuel: return new Vector2(0.16f, 0.72f);
                case WorldZones.HubHealer: return new Vector2(0.475f, 0.72f);
                case WorldZones.HubCave: return new Vector2(0.80f, 0.72f);
            }
            return new Vector2(0.5f, 0.5f);
        }

        /// <summary>The player's start fire in the near foreground band.</summary>
        public static readonly Vector2 FireTarget = new Vector2(0.5f, 0.93f);

        // ------------------------------------------------------------------ projection (reference frame)

        /// <summary>A world point in the 16:9 reference frame: x = 0..1 left to right, y = 0..1 top to bottom, z = depth (metres, &lt;= 0 behind).</summary>
        public static Vector3 Ref(VistaPose pose, Vector3 world)
        {
            var local = Quaternion.Inverse(pose.Rotation) * (world - pose.Position);
            if (local.z <= 0.01f) return new Vector3(0.5f, 0.5f, local.z);
            float t = Mathf.Tan(Fov * 0.5f * Mathf.Deg2Rad);
            return new Vector3(0.5f + local.x / (local.z * t * RefAspect) * 0.5f, 0.5f - local.y / (local.z * t) * 0.5f, local.z);
        }

        /// <summary>The point at height <paramref name="y"/> seen at a reference-frame point (top-left origin), or null above the horizon.</summary>
        public static Vector3? Ground(VistaPose pose, float u, float vTop, float y = 0f)
        {
            float t = Mathf.Tan(Fov * 0.5f * Mathf.Deg2Rad);
            var dir = pose.Rotation * new Vector3((u - 0.5f) * 2f * t * RefAspect, (0.5f - vTop) * 2f * t, 1f);
            if (dir.y >= -1e-4f) return null;
            float s = (y - pose.Position.y) / dir.y;
            if (s <= 0f) return null;
            return pose.Position + dir * s;
        }

        /// <summary>
        /// The 16:9 reference frame on a screen (pixels, bottom-left origin): the largest 16:9 rectangle centred in the safe area
        /// <paramref name="safeGui"/> (GUI coordinates, top-left origin) that fits inside it.
        /// </summary>
        public static Rect RefRect(int w, int h, Rect safeGui)
        {
            var s = Rect.MinMaxRect(safeGui.xMin, h - safeGui.yMax, safeGui.xMax, h - safeGui.yMin);
            float rw = s.width, rh = s.width / RefAspect;
            if (rh > s.height) { rh = s.height; rw = s.height * RefAspect; }
            return new Rect(s.center.x - rw * 0.5f, s.center.y - rh * 0.5f, rw, rh);
        }

        /// <summary>A world point on a screen (pixels, bottom-left origin; z = depth).</summary>
        public static Vector3 Screen(VistaPose pose, Vector3 world, Rect refRect)
        {
            var r = Ref(pose, world);
            return new Vector3(refRect.x + r.x * refRect.width, refRect.y + (1f - r.y) * refRect.height, r.z);
        }

        /// <summary>The off-centre projection that shows the reference frame exactly in <paramref name="refRect"/> of a w x h screen.</summary>
        public static Matrix4x4 Projection(int w, int h, Rect refRect, float near, float far)
        {
            float t = Mathf.Tan(Fov * 0.5f * Mathf.Deg2Rad);
            float hw = near * t * RefAspect, hh = near * t; // half extents of the reference frame at the near plane
            float l = (0f - refRect.center.x) / (refRect.width * 0.5f) * hw, r = (w - refRect.center.x) / (refRect.width * 0.5f) * hw;
            float b = (0f - refRect.center.y) / (refRect.height * 0.5f) * hh, tp = (h - refRect.center.y) / (refRect.height * 0.5f) * hh;
            return Matrix4x4.Frustum(l, r, b, tp, near, far);
        }

        /// <summary>A plain symmetric projection (the walking view's) for blending.</summary>
        public static Matrix4x4 Plain(int w, int h, float fov, float near, float far) => Matrix4x4.Perspective(fov, (float)w / Mathf.Max(1, h), near, far);

        // ------------------------------------------------------------------ composing

        /// <summary>
        /// Finds the poses (best first) that put the two places that cannot move where the target wants them: the dance circle (its plot is part
        /// of the board) in the rhythm block and Mira's camp in the cave block; of the poses that manage it, the one whose ground under
        /// the other four targets lies on the hub (within <paramref name="hubRadius"/> of the hearth, on the island), with the council's
        /// ground at tower distance from the hearth and the far row as near as it can be. Deterministic.
        /// </summary>
        public static List<VistaPose> Compose(Vector3 dance, Vector3 mira, Vector3 hearth, float hubRadius, Func<Vector3, bool> onIsland, int keep = 30)
        {
            dance.y = 0; mira.y = 0; hearth.y = 0;
            var all = new List<KeyValuePair<float, VistaPose>>();
            var mt = Target(WorldZones.HubCave);
            var others = new[] { WorldZones.HubTemple, WorldZones.HubCouncil, WorldZones.HubDuel, WorldZones.HubHealer };
            for (int yi = 0; yi < 120; yi++)
            {
                float yaw = yi * 3f;
                for (float pitch = 28f; pitch <= 46f; pitch += 3f)
                    for (float hgt = 25f; hgt <= 70f; hgt += 5f)
                        foreach (float rx in new[] { 0.78f, 0.84f, 0.9f })
                            foreach (float ry in new[] { 0.18f, 0.26f, 0.34f })
                            {
                                var pose = new VistaPose { Position = new Vector3(0, hgt, 0), Yaw = yaw, Pitch = pitch };
                                var g = Ground(pose, rx, ry);
                                if (!g.HasValue) continue;
                                pose.Position = new Vector3(dance.x - g.Value.x, hgt, dance.z - g.Value.z);
                                var m = Ref(pose, mira);
                                if (m.z <= 1f || m.x < 0.67f || m.x > 0.95f || m.y < 0.56f || m.y > 0.88f) continue;
                                float score = Mathf.Abs(m.x - mt.x) * 20f + Mathf.Abs(m.y - mt.y) * 20f;
                                bool ok = true;
                                foreach (var z in others)
                                {
                                    var tz = Target(z);
                                    var q = Ground(pose, tz.x, tz.y);
                                    if (!q.HasValue) { ok = false; break; }
                                    float d = BoardSpace.Flat(q.Value - hearth);
                                    if (z == WorldZones.HubCouncil) { score += Mathf.Max(0f, d - 40f) * 2f + Mathf.Max(0f, 14f - d) * 2f; score += BoardSpace.Flat(q.Value - pose.Position) * 0.05f; }
                                    else score += Mathf.Max(0f, d - (hubRadius - 8f)) * 2f;
                                    if (onIsland != null && !onIsland(q.Value)) score += 25f;
                                }
                                if (!ok) continue;
                                var f = Ground(pose, FireTarget.x, FireTarget.y);
                                if (f.HasValue) score += Mathf.Max(0f, BoardSpace.Flat(f.Value - hearth) - hubRadius);
                                pose.Valid = true;
                                all.Add(new KeyValuePair<float, VistaPose>(score, pose));
                            }
            }
            all.Sort((a, b) => a.Key.CompareTo(b.Key));
            // the best first, one per bearing, pitch and height (the variants of one are near copies)
            var res = new List<VistaPose>();
            var seen = new HashSet<long>();
            foreach (var kv in all)
            {
                long key = (long)Mathf.RoundToInt(kv.Value.Yaw) * 100000 + Mathf.RoundToInt(kv.Value.Pitch) * 1000 + Mathf.RoundToInt(kv.Value.Position.y);
                if (!seen.Add(key)) continue;
                res.Add(kv.Value);
                if (res.Count >= keep) break;
            }
            return res;
        }

        /// <summary>
        /// Ground points whose view lies in a zone's block, nearest the zone's target first (a grid of offsets inside the block), at the
        /// height a place's centre is seen at (<paramref name="anchorHeight"/>).
        /// </summary>
        public static List<Vector3> Candidates(VistaPose pose, string zone, float anchorHeight)
        {
            var list = new List<KeyValuePair<float, Vector3>>();
            var t = Target(zone); var b = Block(zone);
            for (int i = -12; i <= 12; i++)
                for (int j = -9; j <= 9; j++)
                {
                    float u = t.x + i * 0.0125f, v = t.y + j * 0.0125f;
                    if (u < b.xMin + 0.07f || u > b.xMax - 0.06f || v < b.yMin + 0.05f || v > b.yMax - 0.08f) continue;
                    var g = Ground(pose, u, v, anchorHeight);
                    if (!g.HasValue) continue;
                    var p = g.Value; p.y = 0f;
                    list.Add(new KeyValuePair<float, Vector3>(i * i + j * j * 1.5f, p));
                }
            list.Sort((a, c) => a.Key.CompareTo(c.Key));
            var r = new List<Vector3>(list.Count);
            foreach (var kv in list) r.Add(kv.Value);
            return r;
        }
    }
}
