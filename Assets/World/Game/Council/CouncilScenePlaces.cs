using UnityEngine;
using World.Sim;
using World.Sim.Encounters.Council;

namespace World.Game
{
    /// <summary>The eleven dispute places and the lookout tower, built from small primitives at normal size. Original shapes.</summary>
    public sealed partial class CouncilScene
    {
        static readonly Color Wood = new Color(0.45f, 0.30f, 0.16f), LWood = new Color(0.62f, 0.45f, 0.26f), DWood = new Color(0.30f, 0.19f, 0.10f);
        static readonly Color Stone = new Color(0.52f, 0.52f, 0.54f), DStone = new Color(0.38f, 0.38f, 0.40f), Water = new Color(0.20f, 0.40f, 0.58f);
        static readonly Color Straw = new Color(0.78f, 0.66f, 0.38f), Leaf = new Color(0.30f, 0.52f, 0.24f), Clay = new Color(0.70f, 0.38f, 0.24f);
        static readonly Color Earth = new Color(0.42f, 0.32f, 0.20f), Sack = new Color(0.80f, 0.70f, 0.48f);

        void Solid(Vector3 world, float r)
        {
            _solids.Add(new System.Collections.Generic.KeyValuePair<Vector3, float>(new Vector3(world.x, 0, world.z), r));
            WalkGrid.AddSolid(new Vector3(world.x, 0, world.z), r, "council");
        }

        static Vector3 V(float x, float y, float z) => new Vector3(x, y, z);

        void BuildPlaces(SettlementCouncil sc)
        {
            for (int t = 2; t <= 12; t++)
            {
                int i = t - 2;
                var c = Site.Loc[i]; float gnd = GroundAt(c); c.y = gnd;
                var fwd = c - Site.Position; fwd.y = 0; fwd = fwd.sqrMagnitude < 1e-3f ? Vector3.forward : fwd.normalized;
                var go = new GameObject("Place " + t); go.transform.SetParent(Root, false);
                go.transform.position = c; go.transform.rotation = Quaternion.Euler(0, YawTo(Vector3.zero, fwd), 0);
                var pl = new Place { Track = t, DisputeId = sc != null && sc.Disputes.Count > i ? sc.Disputes[i].DisputeId : null, C = c, Fwd = fwd, Right = new Vector3(fwd.z, 0, -fwd.x), R = Site.Radius[i], Root = go.transform, Ground = gnd };
                _places[i] = pl;
                var pr = go.transform;
                // trodden ground under the gathering
                Cyl(pr, "Ground", V(0, 0.015f, 0), V(pl.R * 2.2f, 0.015f, pl.R * 2.2f), new Color(0.37f, 0.29f, 0.19f));
                // the supporters' stepped block (left), the catspaw's clear patch with a low plinth (right)
                {
                    int cols = Place.ColsFor(CouncilRules.Length(t)), rows = Mathf.CeilToInt(CouncilRules.Length(t) / (float)cols);
                    float bx = -(0.6f * pl.R + 1.8f), bz = -0.1f * pl.R;
                    for (int rw = 1; rw < rows; rw++)
                        Cube(pr, "Step", V(bx, Place.StepH * rw * 0.5f, bz + 0.8f * rw), V(cols * 0.72f + 0.9f, Place.StepH * rw, 0.85f), Earth * 1.05f);
                    float ox = 0.6f * pl.R + 4.8f;
                    Cyl(pr, "Patch", V(ox - 1.2f, 0.035f, bz), V(7.4f, 0.015f, 7.4f), new Color(0.52f, 0.44f, 0.30f));
                    Cyl(pr, "Plinth", V(ox, 0.1f, bz), V(1.3f, 0.1f, 1.3f), new Color(0.46f, 0.40f, 0.34f));
                }
                switch (t)
                {
                    case 2: Crossing(pl); break;
                    case 3: Boundary(pl); break;
                    case 4: Forest(pl); break;
                    case 5: Workyard(pl); break;
                    case 6: Healer(pl); break;
                    case 7: Storehouse(pl); break;
                    case 8: Quarry(pl); break;
                    case 9: MillStone(pl); break;
                    case 10: Spring(pl); break;
                    case 11: Track(pl); break;
                    default: KeeperPost(pl); break;
                }
                // a fire basket at the back of each gathering so it reads at night
                var tp = c + fwd * (pl.R * 0.95f) - pl.Right * (pl.R * 0.3f);
                var tl = WorldLights.Torch(pr, tp, false, true, "Place fire " + t);
                tl.Intensity = 1.6f; tl.Range = 13f; tl.Priority = 2;
            }
        }

        void BuildTower()
        {
            var tw = new GameObject("Lookout tower").transform; tw.SetParent(Root, false);
            tw.position = new Vector3(Site.Position.x, TowerGround, Site.Position.z); tw.rotation = Quaternion.Euler(0, Site.Yaw, 0);
            foreach (var sx in new[] { -1f, 1f }) foreach (var sz in new[] { -1f, 1f })
                Cube(tw, "Leg", V(sx * 1.05f, TowerH * 0.5f, sz * 1.05f), V(0.28f, TowerH, 0.28f), Wood);
            for (int k = 1; k <= 2; k++)
            {
                float y = k * TowerH / 3f;
                foreach (var sz in new[] { -1.05f, 1.05f }) Cube(tw, "Brace", V(0, y, sz), V(2.2f, 0.14f, 0.14f), LWood);
                foreach (var sx in new[] { -1.05f, 1.05f }) Cube(tw, "Brace", V(sx, y, 0), V(0.14f, 0.14f, 2.2f), LWood);
            }
            Cube(tw, "Platform", V(0, TowerH + 0.05f, 0), V(2.5f, 0.2f, 2.5f), LWood);
            for (int k = 0; k < 11; k++) Cube(tw, "Rung", V(0.4f, 0.25f + k * 0.64f, -1.2f), V(0.6f, 0.08f, 0.1f), LWood);
            Cube(tw, "Ladder side", V(0.1f, TowerH * 0.5f, -1.2f), V(0.08f, TowerH, 0.08f), Wood);
            Cube(tw, "Ladder side", V(0.7f, TowerH * 0.5f, -1.2f), V(0.08f, TowerH, 0.08f), Wood);
            Solid(tw.position, 2.0f);
            var tl = WorldLights.Torch(tw, tw.position + tw.rotation * V(-2.2f, 0, -2.2f), false, true, "Tower fire");
            tl.Intensity = 1.8f; tl.Range = 14f; tl.Priority = 3;
        }

        // ---- the eleven places; local frame: x right, z away from the tower, objective at the origin ----

        void Crossing(Place pl)
        {
            var pr = pl.Root;
            Cyl(pr, "Pool", V(0, 0.03f, 7.2f), V(8.4f, 0.02f, 8.4f), Water);
            Cube(pr, "Landing", V(0, 0.12f, 3.6f), V(1.1f, 0.12f, 3.6f), LWood);
            foreach (var s in new[] { -1f, 1f }) Cyl(pr, "Pile", V(s * 0.6f, 0.4f, 5.2f), V(0.18f, 0.4f, 0.18f), DWood);
            for (int k = 0; k < 5; k++) Cube(pr, "Stone", V(-3.5f + k * 1.6f, 0.12f, 2.9f + Mathf.Sin(k * 2f) * 0.3f), V(0.7f, 0.22f, 0.55f), Stone, V(0, k * 40f, 0));
            Cube(pr, "Paddle shaft", V(0, 0.75f, 0), V(0.07f, 1.5f, 0.07f), LWood, V(0, 0, 8));
            Cube(pr, "Paddle blade", V(0.08f, 1.45f, 0), V(0.3f, 0.5f, 0.05f), Wood, V(0, 0, 8));
            Solid(pl.C, 0.5f); Solid(pl.C + pl.Fwd * 7.2f, 4.0f);
        }

        void Boundary(Place pl)
        {
            var pr = pl.Root; float x = pl.R + 0.8f;
            foreach (var s in new[] { -1f, 1f }) Cube(pr, "Stake", V(s * x, 0.6f, 2.4f), V(0.16f, 1.2f, 0.16f), Wood);
            Cube(pr, "Rope", V(0, 0.95f, 2.4f), V(2 * x, 0.05f, 0.05f), Straw);
            Cube(pr, "Rope low", V(0, 0.55f, 2.4f), V(2 * x, 0.05f, 0.05f), Straw);
            for (int k = 0; k < 3; k++) Cube(pr, "Cairn", V(0, 0.18f + k * 0.34f, 0), V(0.9f - k * 0.22f, 0.34f, 0.8f - k * 0.2f), Stone, V(0, k * 25f, 0));
            Solid(pl.C, 0.7f);
        }

        void Forest(Place pl)
        {
            var pr = pl.Root;
            for (int k = 0; k < 8; k++)
            {
                float x = -pl.R * 1.6f + k * (pl.R * 3.2f / 7f), z = pl.R + 1.5f + (k % 3) * 1.2f;
                Cyl(pr, "Trunk", V(x, 1.4f, z), V(0.5f, 1.4f, 0.5f), DWood);
                Ball(pr, "Crown", V(x, 3.4f, z), V(2.8f, 2.6f, 2.8f), Leaf * (0.8f + 0.1f * (k % 3)));
                Solid(pl.C + pl.Right * x + pl.Fwd * z, 0.5f);
            }
            Cyl(pr, "Sapling", V(0, 0.45f, 0), V(0.08f, 0.45f, 0.08f), Wood);
            Ball(pr, "Sapling top", V(0, 1.0f, 0), V(0.6f, 0.5f, 0.6f), Leaf * 1.2f);
            foreach (var s in new[] { -0.5f, 0.5f }) Cube(pr, "Fence", V(s, 0.3f, 0.3f), V(0.06f, 0.6f, 0.06f), Wood);
            Solid(pl.C, 0.45f);
        }

        void Workyard(Place pl)
        {
            var pr = pl.Root;
            for (int k = 0; k < 6; k++)
                Cyl(pr, "Log", V(-2.2f + (k % 3) * 0.55f, 0.25f + (k / 3) * 0.45f, pl.R + 1.5f), V(0.5f, 1.0f, 0.5f), Wood).transform.localRotation = Quaternion.Euler(0, 0, 90f);
            foreach (var s in new[] { -0.6f, 0.6f }) Cube(pr, "Horse leg", V(2.4f + s, 0.4f, pl.R + 1.0f), V(0.12f, 0.8f, 0.12f), DWood);
            Cube(pr, "Horse beam", V(2.4f, 0.85f, pl.R + 1.0f), V(1.6f, 0.12f, 0.16f), LWood);
            Cube(pr, "Plank", V(-3.8f, 0.55f, 1.2f), V(0.12f, 1.1f, 1.8f), LWood, V(0, 0, 14));
            Cyl(pr, "Split log", V(0, 0.45f, 0), V(0.5f, 0.45f, 0.5f), Wood);
            Cube(pr, "Wedge", V(0, 1.0f, 0), V(0.14f, 0.3f, 0.5f), LWood, V(0, 0, 20));
            Solid(pl.C, 0.5f); Solid(pl.C + pl.Fwd * (pl.R + 1.5f) - pl.Right * 1.6f, 1.3f); Solid(pl.C + pl.Fwd * (pl.R + 1f) + pl.Right * 2.4f, 1f);
        }

        void Healer(Place pl)
        {
            var pr = pl.Root; float z = pl.R + 1.4f;
            foreach (var s in new[] { -1.8f, 1.8f }) Cube(pr, "Post", V(s, 1.1f, z), V(0.16f, 2.2f, 0.16f), Wood);
            foreach (var s in new[] { -1.8f, 1.8f }) Cube(pr, "Back post", V(s, 0.7f, z + 1.8f), V(0.16f, 1.4f, 0.16f), Wood);
            Cube(pr, "Roof", V(0, 1.75f, z + 0.9f), V(4.2f, 0.12f, 2.6f), Straw, V(-24, 0, 0));
            Cube(pr, "Bench", V(0, 0.3f, z + 1.0f), V(3.0f, 0.12f, 0.6f), LWood);
            for (int k = 0; k < 5; k++) Cyl(pr, "Herb bundle", V(-1.3f + k * 0.65f, 1.5f, z), V(0.16f, 0.25f, 0.16f), Leaf);
            for (int k = 0; k < 3; k++) { Ball(pr, "Pot", V(-0.5f + k * 0.5f, 0.26f, 0.0f), V(0.4f, 0.46f, 0.4f), Clay); }
            Solid(pl.C, 0.7f); Solid(pl.C + pl.Fwd * (z + 0.9f), 2.4f);
        }

        void Storehouse(Place pl)
        {
            var pr = pl.Root;
            for (int k = 0; k < 4; k++) Cyl(pr, "Basket", V(-2.6f + k * 1.0f, 0.3f, pl.R + 1.0f), V(0.9f, 0.3f, 0.9f), Straw);
            for (int k = 0; k < 4; k++) Ball(pr, "Sack", V(2.2f + (k % 2) * 0.7f, 0.3f + (k / 2) * 0.4f, pl.R + 0.8f), V(0.7f, 0.65f, 0.6f), Sack);
            Cyl(pr, "Big basket", V(0, 0.38f, 0), V(1.1f, 0.38f, 1.1f), Straw * 0.9f);
            Cube(pr, "Mat", V(0, 0.03f, -1.8f), V(2.6f, 0.03f, 1.2f), Straw * 0.85f);
            Solid(pl.C, 0.9f);
        }

        void Quarry(Place pl)
        {
            var pr = pl.Root; float z = pl.R + 1.6f;
            for (int row = 0; row < 3; row++) for (int k = 0; k < 7 - row; k++)
                Cube(pr, "Face block", V(-3.6f + k * 1.2f + row * 0.6f, 0.45f + row * 0.9f, z + row * 0.4f), V(1.15f, 0.9f, 1.2f), Stone * (0.85f + 0.1f * ((k + row) % 3)), V(0, (k * 13 + row * 7) % 20 - 10, 0));
            for (int k = 0; k < 5; k++) Cube(pr, "Rubble", V(-2f + k * 1.1f, 0.15f, z - 1.2f + Mathf.Sin(k * 3f) * 0.4f), V(0.5f, 0.3f, 0.4f), DStone, V(0, k * 33f, 0));
            Cube(pr, "Pick shaft", V(0, 0.6f, 0), V(0.07f, 1.2f, 0.07f), Wood, V(0, 0, 14));
            Cube(pr, "Pick head", V(0.18f, 1.18f, 0), V(0.5f, 0.1f, 0.1f), DStone, V(0, 0, 14));
            Solid(pl.C, 0.45f); Solid(pl.C + pl.Fwd * (z + 0.5f), 4.2f);
        }

        void MillStone(Place pl)
        {
            var pr = pl.Root;
            Cyl(pr, "Base", V(0, 0.25f, 0), V(1.6f, 0.25f, 1.6f), DStone);
            Cyl(pr, "Millstone", V(0, 0.62f, 0), V(1.4f, 0.12f, 1.4f), Stone);
            Cube(pr, "Handle", V(0.75f, 1.0f, 0), V(1.3f, 0.07f, 0.07f), LWood, V(0, 0, 18));
            for (int k = 0; k < 4; k++) Ball(pr, "Sack", V(-2.0f + k * 0.8f, 0.3f, pl.R + 0.8f), V(0.7f, 0.62f, 0.6f), Sack);
            Cube(pr, "Scale beam", V(2.4f, 0.9f, 1.4f), V(1.2f, 0.06f, 0.06f), Wood);
            Cube(pr, "Scale post", V(2.4f, 0.5f, 1.4f), V(0.08f, 1.0f, 0.08f), Wood);
            Solid(pl.C, 0.95f);
        }

        void Spring(Place pl)
        {
            var pr = pl.Root;
            Cyl(pr, "Spring water", V(0, 0.2f, 0), V(1.7f, 0.04f, 1.7f), Water);
            for (int k = 0; k < 10; k++)
            {
                float a = k * 36f * Mathf.Deg2Rad;
                Cube(pr, "Ring stone", V(Mathf.Cos(a) * 1.0f, 0.2f, Mathf.Sin(a) * 1.0f), V(0.45f, 0.4f, 0.4f), Stone * (0.85f + 0.12f * (k % 3)), V(0, -k * 36f, 0));
            }
            Cube(pr, "Trough", V(1.9f, 0.25f, 0.2f), V(0.5f, 0.3f, 1.4f), Wood);
            Cyl(pr, "Bucket", V(-1.8f, 0.2f, -0.2f), V(0.4f, 0.2f, 0.4f), Wood);
            Cube(pr, "Chalk board", V(-2.5f, 0.6f, 1.5f), V(0.9f, 1.0f, 0.08f), DWood);
            Solid(pl.C, 1.25f);
        }

        void Track(Place pl)
        {
            var pr = pl.Root;
            Cube(pr, "Track", V(pl.R * 0.2f, 0.025f, pl.R + 5f), V(2.2f, 0.03f, 14f), Earth * 0.85f);
            Cyl(pr, "Log roller", V(0, 0.4f, 0), V(0.8f, 0.4f, 0.8f), Wood).transform.localRotation = Quaternion.Euler(90, 20, 0);
            Cube(pr, "Sign post", V(-pl.R - 0.6f, 0.9f, pl.R * 0.6f), V(0.14f, 1.8f, 0.14f), Wood);
            Cube(pr, "Sign arm", V(-pl.R - 0.2f, 1.5f, pl.R * 0.6f), V(0.8f, 0.2f, 0.06f), LWood);
            for (int k = 0; k < 3; k++) Cube(pr, "Cairn", V(pl.R + 0.5f, 0.15f + k * 0.28f, pl.R), V(0.7f - k * 0.18f, 0.28f, 0.6f - k * 0.15f), Stone);
            Solid(pl.C, 0.5f);
        }

        void KeeperPost(Place pl)
        {
            var pr = pl.Root;
            Cube(pr, "Keeper post", V(0, 1.5f, 0), V(0.3f, 3.0f, 0.3f), DWood);
            for (int k = 0; k < 6; k++) Cube(pr, "Notch band", V(0, 0.8f + k * 0.35f, -0.16f), V(0.34f, 0.06f, 0.04f), LWood);
            Cube(pr, "Keeper seat", V(1.4f, 0.22f, 0.4f), V(1.2f, 0.44f, 0.6f), Stone);
            Cube(pr, "Tally stick", V(-0.6f, 0.6f, 0.0f), V(0.06f, 1.2f, 0.06f), LWood, V(0, 0, 12));
            Solid(pl.C, 0.4f);
        }
    }
}
