using System.Collections.Generic;
using UnityEngine;

namespace World.Game
{
    /// <summary>
    /// The opening vista's uneven ground (owner, 21 Nov 2026: "uneven ground, buildings, animations"): a rocky hillside with a lit cave
    /// mouth behind Mira's expedition camp, and grassy terraces rising behind the far side of the camp so the camp reads as a hollow
    /// among slopes from the lookout. Scenery only: the walkable hub ground stays as it was (the hill is a walk blocker). Original primitive art.
    /// </summary>
    /// <summary>Hides the hub's lookout tower while the council's own scene (with its tower) stands at the same site.</summary>
    public sealed class HideWhileCouncilStands : MonoBehaviour
    {
        private Renderer[] _r;
        private void Update()
        {
            if (_r == null) _r = GetComponentsInChildren<Renderer>(true);
            bool show = CouncilScene.Current == null;
            foreach (var r in _r) if (r != null && r.enabled != show) r.enabled = show;
        }
    }

    public static class VistaTerrain
    {
        public const string Owner = "vista-terrain";
        public static readonly Color Rock = new Color(0.42f, 0.40f, 0.38f), RockDark = new Color(0.30f, 0.29f, 0.28f),
            Earth = new Color(0.40f, 0.30f, 0.20f), Turf = new Color(0.22f, 0.40f, 0.18f), TurfLight = new Color(0.28f, 0.47f, 0.21f);

        public struct CaveHill { public Vector3 Centre, Mouth, Out; }

        /// <summary>
        /// The hill and its cave mouth just beyond Mira's camp as seen from the lookout (<paramref name="away"/>: the vista camera's
        /// level forward), its mouth turned towards the lookout; turned aside where that spot is off the island or too near another place.
        /// </summary>
        public static CaveHill BuildCaveHill(Transform root, Vector3 mira, Vector3 away, IReadOnlyList<VistaPlace> others)
        {
            mira.y = 0;
            var outDir = away; outDir.y = 0;
            outDir = outDir.sqrMagnitude > 1e-3f ? outDir.normalized : Vector3.forward;
            var board = WorldBootstrap.Instance != null ? WorldBootstrap.Instance.Sim.State.Board : null;
            Vector3 centre = mira + outDir * 16f;
            foreach (float turn in new[] { 0f, 30f, -30f, 60f, -60f, 90f, -90f })
            {
                var d = Quaternion.Euler(0, turn, 0) * outDir;
                var c = mira + d * 16f;
                if (board != null && (board.Topology.HexAt(c.x, c.z) < 0 || board.Topology.HexAt(c.x + d.x * 10f, c.z + d.z * 10f) < 0)) continue;
                bool crowded = false;
                foreach (var other in others) if (other.Zone != WorldZones.HubCave && BoardSpace.Flat(other.Centre - c) < other.Radius + 14f) crowded = true;
                if (crowded) continue;
                centre = c; outDir = d; break;
            }
            var right = new Vector3(outDir.z, 0, -outDir.x);
            var mb = new MeshBuilder();
            var rnd = new PropRandom(9187);
            // stacked, turned, tapering rings: a rough rocky hill about 10 m high
            float[] r = { 13f, 10.5f, 8f, 5f, 2.2f };
            float[] h = { 0f, 2.6f, 5.2f, 7.6f, 9.6f };
            for (int i = 0; i < r.Length - 1; i++)
            {
                var col = Color.Lerp(i % 2 == 0 ? Turf : TurfLight, Rock, i / 4f + 0.15f);
                mb.Cylinder(centre + Vector3.up * h[i], r[i], r[i + 1], h[i + 1] - h[i], 11 - i, col);
            }
            for (int i = 0; i < 26; i++)
            {
                float a = rnd.Range(0f, Mathf.PI * 2f), rr = rnd.Range(3f, 12f);
                var p = centre + new Vector3(Mathf.Sin(a), 0, Mathf.Cos(a)) * rr;
                float k = Mathf.InverseLerp(13f, 2f, rr);
                p.y = Mathf.Lerp(0f, 8.5f, k) - 0.4f;
                mb.Rock(p, rnd.Range(0.9f, 2.2f), Color.Lerp(Rock, RockDark, rnd.Next()), rnd.Next);
            }
            // the cave mouth: two rock jambs, a lintel boulder and the dark opening, set into the slope facing the camp
            var mouth = centre - outDir * 11.2f; mouth.y = 0f;
            var face = Quaternion.LookRotation(-outDir).eulerAngles.y;
            mb.Box(mouth + right * 2.5f + Vector3.up * 1.9f - outDir * 0.3f, new Vector3(1.6f, 3.8f, 2.4f), RockDark, face + 8f);
            mb.Box(mouth - right * 2.5f + Vector3.up * 1.7f - outDir * 0.3f, new Vector3(1.6f, 3.4f, 2.4f), RockDark, face - 6f);
            mb.Box(mouth + Vector3.up * 4.0f - outDir * 0.5f, new Vector3(6.8f, 1.3f, 2.8f), Rock, face + 3f);
            mb.Box(mouth + Vector3.up * 2.0f + outDir * 0.9f, new Vector3(3.6f, 4.2f, 2.6f), RockDark, face); // filler behind the opening
            for (int i = 0; i < 5; i++) mb.Rock(mouth + right * (i - 2) * 1.8f - outDir * 1.6f, 0.7f + 0.15f * i, RockDark, rnd.Next);
            var hill = mb.ToObject("Vista hill and cave", root, MaterialFactory.CreateVertexColored(Color.white, "Vista hill"));
            hill.layer = 2;
            // the opening itself: unlit black, so it reads as a hole at night
            var hole = new MeshBuilder();
            var o = mouth - outDir * 1.65f;
            hole.Quad(o - right * 1.7f, o - right * 1.7f + Vector3.up * 3.3f, o + right * 1.7f + Vector3.up * 3.3f, o + right * 1.7f, Color.black);
            hole.Quad(o + right * 1.7f, o + right * 1.7f + Vector3.up * 3.3f, o - right * 1.7f + Vector3.up * 3.3f, o - right * 1.7f, Color.black);
            var hg = hole.ToObject("Cave opening", root, MaterialFactory.CreateUnlit(new Color(0.02f, 0.02f, 0.03f), "Cave opening"));
            hg.layer = 2;
            // two torches at the mouth, and a cold blue gleam inside (the crystals)
            foreach (float s in new[] { -1f, 1f })
            {
                var t = mouth + right * s * 3.6f - outDir * 2.2f;
                var tb = new MeshBuilder();
                CampBuilder.Beam(tb, t, t + Vector3.up * 2.2f, 0.09f, CampBuilder.Wood);
                tb.Cone(t + Vector3.up * 2.2f, 0.22f, 0.5f, 6, new Color(1f, 0.6f, 0.2f));
                var tg = tb.ToObject("Cave torch", root, MaterialFactory.CreateVertexColored(Color.white, "Cave torch"));
                tg.layer = 2;
                HubArt.Glow(root, t + Vector3.up * 2.6f, 9f, 1.6f);
            }
            var gleam = HubArt.Glow(root, mouth - outDir * 0.6f + Vector3.up * 1.4f, 6f, 2.2f);
            gleam.color = new Color(0.45f, 0.75f, 1f);
            WalkGrid.AddStaticBlocker(centre, 11.5f, Owner);
            LandViews.AddClearing(centre.x, centre.z, 17f);
            return new CaveHill { Centre = centre, Mouth = mouth, Out = outDir };
        }

        /// <summary>
        /// Rolling ground round the far side of the camp from the lookout: grassy knolls and rock outcrops of different heights (only on
        /// the island and clear of every place), so the land behind the places rises and falls instead of lying flat. Trees are cleared
        /// round each knoll. Scenery only; each knoll is a walk blocker.
        /// </summary>
        public static void BuildBackdrop(Transform root, IReadOnlyList<VistaPlace> places, Vector3 focus, float yaw)
        {
            var board = WorldBootstrap.Instance != null ? WorldBootstrap.Instance.Sim.State.Board : null;
            if (board == null || places.Count == 0) return;
            float far = 0f;
            foreach (var p in places) far = Mathf.Max(far, BoardSpace.Flat(p.Centre - focus) + p.Radius);
            var mb = new MeshBuilder();
            var rnd = new PropRandom(5521);
            int made = 0;
            for (int i = 0; i < 40 && made < 14; i++)
            {
                float a = yaw + rnd.Range(-80f, 80f);
                float dist = far + rnd.Range(-6f, 26f);
                var c = focus + BoardSpace.DirOf(a) * dist;
                float r = rnd.Range(6f, 11f), h = rnd.Range(2.2f, 5.5f) * (dist > far + 10f ? 1.3f : 0.9f);
                if (!OnIsland(board, c) || !OnIsland(board, c + BoardSpace.DirOf(a) * r)) continue;
                bool clear = true;
                foreach (var p in places) if (BoardSpace.Flat(p.Centre - c) < p.Radius + r + 4f) clear = false;
                if (!clear) continue;
                Knoll(mb, c, r, h, rnd);
                WalkGrid.AddStaticBlocker(c, r * 0.8f, Owner);
                LandViews.AddClearing(c.x, c.z, r + 2f);
                made++;
            }
            if (mb.VertexCount == 0) return;
            var go = mb.ToObject("Vista knolls", root, MaterialFactory.CreateVertexColored(Color.white, "Vista knolls"));
            go.layer = 2;
        }

        /// <summary>A low grassy knoll: three turned, tapering rings and a few rocks.</summary>
        private static void Knoll(MeshBuilder mb, Vector3 c, float r, float h, PropRandom rnd)
        {
            float[] rr = { r, r * 0.72f, r * 0.42f, r * 0.12f };
            float[] hh = { 0f, h * 0.45f, h * 0.82f, h };
            for (int k = 0; k < 3; k++)
                mb.Cylinder(c + Vector3.up * (hh[k] - 0.05f), rr[k], rr[k + 1], hh[k + 1] - hh[k], 9 - k, Color.Lerp(Turf, TurfLight, rnd.Next() * 0.7f));
            int rocks = 1 + (int)(rnd.Next() * 3f);
            for (int k = 0; k < rocks; k++)
            {
                float a = rnd.Range(0f, Mathf.PI * 2f), d = rnd.Range(0.2f, 0.6f) * r;
                var p = c + new Vector3(Mathf.Sin(a), 0, Mathf.Cos(a)) * d; p.y = h * (1f - d / r) * 0.9f - 0.3f;
                mb.Rock(p, rnd.Range(0.6f, 1.4f), Color.Lerp(Rock, RockDark, rnd.Next()), rnd.Next);
            }
        }

        /// <summary>
        /// The council's lookout tower at its site, as the council builds it when it sits (four legs, braces, platform, ladder) with a hide
        /// roof, a pennant and a fire basket on top, and a small crowd waiting at its foot. Hidden while the council's own scene stands.
        /// </summary>
        public static void BuildHubTower(Transform root, CouncilDirector dir)
        {
            var site = dir.Site;
            var go = new GameObject("Vista lookout tower");
            go.transform.SetParent(root, false);
            var at = site.Position; at.y = PlayerWalker.GroundHeight(at);
            go.transform.SetPositionAndRotation(at, Quaternion.Euler(0, site.Yaw, 0));
            float H = CouncilScene.TowerH;
            var mb = new MeshBuilder();
            var wood = CampBuilder.Wood; var light = CampBuilder.WoodLight;
            foreach (var sx in new[] { -1f, 1f }) foreach (var sz in new[] { -1f, 1f }) mb.Box(new Vector3(sx * 1.05f, H * 0.5f, sz * 1.05f), new Vector3(0.28f, H, 0.28f), wood);
            for (int k = 1; k <= 2; k++)
            {
                float y = k * H / 3f;
                foreach (var sz in new[] { -1.05f, 1.05f }) mb.Box(new Vector3(0, y, sz), new Vector3(2.2f, 0.14f, 0.14f), light);
                foreach (var sx in new[] { -1.05f, 1.05f }) mb.Box(new Vector3(sx, y, 0), new Vector3(0.14f, 0.14f, 2.2f), light);
            }
            mb.Box(new Vector3(0, H + 0.05f, 0), new Vector3(2.5f, 0.2f, 2.5f), light);
            foreach (var sx in new[] { -1f, 1f }) foreach (var sz in new[] { -1f, 1f }) mb.Box(new Vector3(sx * 1.15f, H + 1.0f, sz * 1.15f), new Vector3(0.12f, 2.0f, 0.12f), wood);
            mb.Cone(new Vector3(0, H + 2.0f, 0), 1.9f, 1.4f, 4, CampBuilder.Hide);
            for (int k = 0; k < 11; k++) mb.Box(new Vector3(0.4f, 0.25f + k * 0.64f, -1.2f), new Vector3(0.6f, 0.08f, 0.1f), light);
            mb.Box(new Vector3(0.1f, H * 0.5f, -1.2f), new Vector3(0.08f, H, 0.08f), wood);
            mb.Box(new Vector3(0.7f, H * 0.5f, -1.2f), new Vector3(0.08f, H, 0.08f), wood);
            // pennant pole and pennant, and the fire basket at the platform's corner
            mb.Box(new Vector3(0, H + 3.8f, 0), new Vector3(0.07f, 1.8f, 0.07f), wood);
            mb.Tri(new Vector3(0, H + 4.6f, 0), new Vector3(0, H + 4.0f, 0), new Vector3(1.1f, H + 4.3f, 0), new Color(0.75f, 0.2f, 0.15f));
            mb.Tri(new Vector3(0, H + 4.0f, 0), new Vector3(0, H + 4.6f, 0), new Vector3(1.1f, H + 4.3f, 0), new Color(0.75f, 0.2f, 0.15f));
            mb.Cylinder(new Vector3(1.0f, H + 0.15f, 1.0f), 0.3f, 0.42f, 0.35f, 8, CampBuilder.Charcoal);
            mb.Cone(new Vector3(1.0f, H + 0.5f, 1.0f), 0.32f, 0.7f, 6, new Color(1f, 0.6f, 0.2f));
            var body = mb.ToObject("Tower", go.transform, MaterialFactory.CreateVertexColored(Color.white, "Vista tower"));
            body.transform.localPosition = Vector3.zero; body.transform.localRotation = Quaternion.identity;
            body.layer = 2;
            HubArt.Glow(go.transform, go.transform.TransformPoint(new Vector3(1.0f, H + 1.2f, 1.0f)), 14f, 1.8f);
            WalkGrid.AddSolid(site.Position, 2.0f, Owner);
            go.AddComponent<HideWhileCouncilStands>();
            // a few villagers waiting at the tower's foot
            var tun = new[] { new Color(0.55f, 0.35f, 0.25f), new Color(0.35f, 0.45f, 0.30f), new Color(0.50f, 0.42f, 0.30f), new Color(0.40f, 0.33f, 0.45f), new Color(0.60f, 0.50f, 0.35f) };
            for (int i = 0; i < 5; i++)
            {
                var p = site.ToWorld(new Vector3(-4.5f + i * 2.0f, 0f, 5.5f + (i % 2) * 1.4f));
                p.y = PlayerWalker.GroundHeight(p);
                var n = Npc.Spawn("council_crowd_" + i, "Villager", "Waits for the council at the lookout tower.", "", tun[i], p, BoardSpace.YawOf(site.Position - p));
                n.Doing = () => "Waiting to hear the council.";
            }
        }

        private static bool OnIsland(World.Sim.Board board, Vector3 p) => board.Topology.HexAt(p.x, p.z) >= 0;
    }
}
