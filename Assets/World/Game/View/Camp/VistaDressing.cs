using System.Collections.Generic;
using UnityEngine;

namespace World.Game
{
    /// <summary>
    /// A small group of hub villagers who talk among themselves: every few seconds one of them gestures, nods, points at another or
    /// claps, so a place reads as busy from the opening vista. Animation only; never writes state.
    /// </summary>
    public sealed class VistaChatter : MonoBehaviour
    {
        public readonly List<Npc> People = new List<Npc>();
        public string[] Moves = { ActorKeys.Gesture, ActorKeys.Nod, ActorKeys.Gesture, ActorKeys.Surprise };
        public float MinGap = 1.2f, MaxGap = 3.2f;
        public int Seed = 1;
        private PropRandom _rnd;
        private float _next;
        private int _pointer = -1; private float _pointUntil;

        private void Update()
        {
            if (People.Count == 0) return;
            if (_rnd == null) { _rnd = new PropRandom(Seed); _next = Time.time + _rnd.Range(0.2f, MaxGap); }
            if (_pointer >= 0 && Time.time > _pointUntil) { var p = People[_pointer]; if (p != null) p.PointAt(null); _pointer = -1; }
            if (Time.time < _next) return;
            _next = Time.time + _rnd.Range(MinGap, MaxGap);
            int i = Mathf.Min(People.Count - 1, (int)(_rnd.Next() * People.Count));
            var n = People[i];
            if (n == null || n.Actor == null || n.IsWalking) return;
            if (People.Count > 1 && _rnd.Next() < 0.35f && _pointer < 0)
            {
                var other = People[(i + 1 + (int)(_rnd.Next() * (People.Count - 1))) % People.Count];
                if (other != null) { n.PointAt(other.transform.position + Vector3.up * 1.4f); _pointer = i; _pointUntil = Time.time + 1.4f; return; }
            }
            n.Actor.PlayOneShot(Moves[(int)(_rnd.Next() * Moves.Length) % Moves.Length]);
        }
    }

    /// <summary>
    /// Cheap original props and people that make each place read from the opening vista (owner's approved target, 21 Nov 2026): the
    /// council's longhouse with its chief and arguing clusters, a trader and a farmer at the temple, a coughing patient at the lodge,
    /// a scout with a lantern at the cave mouth, and the start fire in the near foreground. Primitive vertex-coloured art; the
    /// buildings are walk blockers. Reads nothing but the layout; never writes state.
    /// </summary>
    public static class VistaDressing
    {
        public const string Owner = "vista-dressing";
        private static readonly Color Thatch = new Color(0.62f, 0.50f, 0.27f), ThatchDark = new Color(0.47f, 0.37f, 0.19f), Daub = new Color(0.55f, 0.44f, 0.32f);

        private static Npc Person(string id, string name, string role, string doing, Color tunic, Vector3 at, Vector3 face)
        {
            at.y = PlayerWalker.GroundHeight(at);
            var n = Npc.Spawn(id, name, role, "", tunic, at, BoardSpace.YawOf(face - at));
            n.Doing = () => doing;
            return n;
        }

        private static VistaChatter Chatter(Transform root, string name, int seed, params Npc[] people)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root, false);
            var c = go.AddComponent<VistaChatter>();
            c.Seed = seed;
            c.People.AddRange(people);
            return c;
        }

        /// <summary>
        /// The biggest building of the camp: a long timber-and-thatch house behind the council's tower (away from the fire), its door
        /// towards the tower, a fire basket each side of the door, the chief in the doorway and two knots of villagers arguing outside.
        /// </summary>
        public static void BuildCouncilHall(Transform root, CouncilSite site)
        {
            if (site == null || !site.HasHall) return;
            var toFire = site.Position - site.Hall; toFire.y = 0; toFire.Normalize();
            var f = new HubFrame(site.Hall, BoardSpace.YawOf(toFire)); // local +z: the front, towards the tower and the fire
            float L = CouncilSite.HallHalfLength, W = CouncilSite.HallHalfWidth;
            const float wallH = 3.0f, ridge = 8.2f;
            var mb = new MeshBuilder();
            // walls: wattle and daub between timber posts, a dark doorway in the long front wall
            mb.Box(new Vector3(0, wallH * 0.5f, -W + 0.2f), new Vector3(L * 2f, wallH, 0.4f), Daub);
            mb.Box(new Vector3(-L + 0.2f, wallH * 0.5f, 0), new Vector3(0.4f, wallH, W * 2f), Daub);
            mb.Box(new Vector3(L - 0.2f, wallH * 0.5f, 0), new Vector3(0.4f, wallH, W * 2f), Daub);
            mb.Box(new Vector3(-(L + 1.6f) * 0.5f - 0.0f, wallH * 0.5f, W - 0.2f), new Vector3(L - 1.6f, wallH, 0.4f), Daub);
            mb.Box(new Vector3((L + 1.6f) * 0.5f, wallH * 0.5f, W - 0.2f), new Vector3(L - 1.6f, wallH, 0.4f), Daub);
            mb.Box(new Vector3(0, 1.25f, W - 0.25f), new Vector3(3.0f, 2.5f, 0.3f), CampBuilder.Charcoal);
            mb.Box(new Vector3(0, 2.75f, W - 0.1f), new Vector3(3.6f, 0.5f, 0.5f), CampBuilder.Wood);
            for (int i = -4; i <= 4; i++)
            {
                float x = i * L / 4.3f;
                mb.Box(new Vector3(x, wallH * 0.5f, W), new Vector3(0.35f, wallH + 0.2f, 0.35f), CampBuilder.Wood);
                mb.Box(new Vector3(x, wallH * 0.5f, -W), new Vector3(0.35f, wallH + 0.2f, 0.35f), CampBuilder.Wood);
            }
            // a steep thatched roof down past the walls, hipped at the ends, and a ridge beam
            float eave = 1.1f, ey = wallH - 0.4f;
            Vector3 a0 = new Vector3(-L - eave, ey, W + eave), a1 = new Vector3(L + eave, ey, W + eave), r0 = new Vector3(-L + 2.2f, ridge, 0), r1 = new Vector3(L - 2.2f, ridge, 0);
            Vector3 b0 = new Vector3(-L - eave, ey, -W - eave), b1 = new Vector3(L + eave, ey, -W - eave);
            Sheet2(mb, a0, a1, r1, r0, Thatch);
            Sheet2(mb, b1, b0, r0, r1, ThatchDark);
            mb.Tri(a0, r0, b0, ThatchDark); mb.Tri(b0, r0, a0, ThatchDark);
            mb.Tri(a1, b1, r1, Thatch); mb.Tri(b1, a1, r1, Thatch);
            CampBuilder.Beam(mb, r0 + Vector3.up * 0.2f, r1 + Vector3.up * 0.2f, 0.3f, CampBuilder.Wood);
            foreach (float s in new[] { -1f, 1f })
            {
                CampBuilder.Beam(mb, r0 + new Vector3(0, 0.2f, 0), r0 + new Vector3(-0.8f, 1.4f, s * 0.6f), 0.14f, CampBuilder.Wood); // crossed gable horns
                CampBuilder.Beam(mb, r1 + new Vector3(0, 0.2f, 0), r1 + new Vector3(0.8f, 1.4f, s * 0.6f), 0.14f, CampBuilder.Wood);
            }
            // fire baskets either side of the door, and benches under the eaves
            foreach (float s in new[] { -1f, 1f })
            {
                var fb = new Vector3(s * 2.6f, 0, W + 1.6f);
                mb.Cylinder(fb, 0.09f, 0.09f, 1.5f, 6, CampBuilder.Wood);
                mb.Cylinder(fb + Vector3.up * 1.45f, 0.3f, 0.42f, 0.35f, 8, CampBuilder.Charcoal);
                mb.Cone(fb + Vector3.up * 1.8f, 0.32f, 0.7f, 6, new Color(1f, 0.6f, 0.2f));
                mb.Box(new Vector3(s * 6.5f, 0.35f, W + 1.0f), new Vector3(3.2f, 0.18f, 0.6f), CampBuilder.WoodLight);
            }
            var go = HubArt.Make(root, "Council longhouse", f, mb);
            go.layer = 2;
            foreach (float s in new[] { -1f, 1f }) HubArt.Glow(root, f.W(s * 2.6f, W + 1.6f) + Vector3.up * 2.2f, 10f, 1.6f);
            HubArt.Glow(root, f.W(0, W - 1.2f) + Vector3.up * 1.6f, 7f, 1.4f); // the hearth inside, through the door
            for (int i = -2; i <= 2; i++) WalkGrid.AddSolid(f.W(i * L / 2.6f, 0), W + 0.6f, Owner);
            // the chief in the doorway, and two knots of villagers arguing on either side of the way to the tower
            var door = f.W(0, W + 1.2f);
            var chief = Person("council_chief", "Chief", "Presides over the council.", "Every voice is heard in the longhouse.", new Color(0.55f, 0.18f, 0.16f), door, site.Position);
            var tun = new[] { new Color(0.48f, 0.36f, 0.27f), new Color(0.33f, 0.46f, 0.36f), new Color(0.58f, 0.48f, 0.30f), new Color(0.42f, 0.36f, 0.50f), new Color(0.62f, 0.42f, 0.30f), new Color(0.38f, 0.40f, 0.48f) };
            var lines = new[] { "The spring is ours by right.", "Then share the water first!", "Hear me out.", "The boundary stone moved.", "Who moved it?", "Ask the steward." };
            for (int k = 0; k < 2; k++)
            {
                float side = k == 0 ? -1f : 1f;
                var c = f.W(side * 6.2f, W + 4.2f);
                var group = new Npc[3];
                for (int i = 0; i < 3; i++)
                {
                    float a = (i * 120f + k * 40f) * Mathf.Deg2Rad;
                    var at = c + new Vector3(Mathf.Sin(a), 0, Mathf.Cos(a)) * 1.3f;
                    group[i] = Person("council_arguer_" + (k * 3 + i), "Villager", "Argues a dispute before the council.", lines[k * 3 + i], tun[k * 3 + i], at, c);
                }
                Chatter(root, "Council argument " + k, 31 + k, group);
            }
            Chatter(root, "Council chief", 37, chief).Moves = new[] { ActorKeys.Gesture, ActorKeys.Nod };
        }

        private static void Sheet2(MeshBuilder mb, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Color col) { mb.Quad(a, b, c, d, col); mb.Quad(d, c, b, a, col); }

        /// <summary>A trader's weighing table, granary bins and a farmer with a basket at the edge of the temple's forecourt.</summary>
        public static void BuildTempleTrade(Transform root, TempleForecourt tf)
        {
            if (tf == null) return;
            var f = tf.Frame;
            const float R = TempleView.Radius;
            var mb = new MeshBuilder();
            var table = new Vector3(5.6f, 0, R + 9.2f);
            mb.Box(table + new Vector3(0, 0.75f, 0), new Vector3(2.4f, 0.12f, 1.0f), CampBuilder.WoodLight);
            for (int i = 0; i < 4; i++) mb.Box(table + new Vector3((i % 2 - 0.5f) * 2.1f, 0.37f, (i / 2 - 0.5f) * 0.8f), new Vector3(0.12f, 0.74f, 0.12f), CampBuilder.Wood);
            // the balance: a post, a beam and two pans
            mb.Box(table + new Vector3(0, 1.25f, 0), new Vector3(0.08f, 0.9f, 0.08f), CampBuilder.Wood);
            mb.Box(table + new Vector3(0, 1.7f, 0), new Vector3(1.4f, 0.06f, 0.06f), CampBuilder.Wood);
            foreach (float s in new[] { -0.65f, 0.65f }) mb.Cylinder(table + new Vector3(s, 1.3f, 0), 0.25f, 0.28f, 0.06f, 8, CampBuilder.Ochre);
            HubArt.Basket(mb, table + new Vector3(-1.6f, 0, 0.9f), HubArt.Grain, 0);
            HubArt.Basket(mb, table + new Vector3(1.7f, 0, 0.8f), HubArt.Brick, 1);
            // granary bins on stilts
            for (int i = 0; i < 3; i++)
            {
                var g = new Vector3(-8.5f + i * 1.9f, 0, R + 8.8f + (i % 2) * 0.6f);
                for (int k = 0; k < 3; k++) { float a = k * 2.1f; mb.Cylinder(g + new Vector3(Mathf.Sin(a) * 0.5f, 0, Mathf.Cos(a) * 0.5f), 0.07f, 0.07f, 0.7f, 5, CampBuilder.Wood); }
                mb.Cylinder(g + Vector3.up * 0.7f, 0.75f, 0.8f, 1.2f, 9, CampBuilder.Clay);
                mb.Cone(g + Vector3.up * 1.9f, 0.95f, 0.9f, 9, HubArt.Straw);
            }
            HubArt.Make(root, "Temple trade", f, mb).layer = 2;
            WalkGrid.AddSolid(f.W(table.x, table.z), 1.4f, Owner);
            WalkGrid.AddSolid(f.W(-6.6f, R + 9.1f), 2.4f, Owner);
            var trader = Person("temple_trader", "Trader", "Weighs grain and bricks for the temple's builders.", "Fair weight, fair trade.", new Color(0.70f, 0.52f, 0.20f), f.W(table.x, table.z - 1.1f), f.W(table.x, table.z));
            var farmer = Person("temple_farmer", "Farmer", "Brings a basket of grain to trade.", "Grain for bricks, if the weight is fair.", new Color(0.40f, 0.48f, 0.26f), f.W(table.x + 0.4f, table.z + 1.3f), f.W(table.x, table.z));
            Chatter(root, "Temple trade talk", 41, trader, farmer);
        }

        /// <summary>A patient wrapped in a blanket beside the healer, coughing now and then.</summary>
        public static void BuildLodgePatient(Transform root, HealersLodge lodge)
        {
            if (lodge == null) return;
            var f = lodge.Frame;
            var healer = lodge.Healer != null ? lodge.Healer.transform.position : f.W(-1.4f, HealersLodge.BodyRadius + 2.4f);
            var at = f.W(-2.9f, HealersLodge.BodyRadius + 3.3f);
            var patient = Person("healer_patient", "Coughing villager", "Waits for the healer, wrapped in a blanket.", "*cough* The healer says it's spreading.", new Color(0.62f, 0.60f, 0.52f), at, healer);
            var c = Chatter(root, "Lodge patient", 43, patient);
            c.Moves = new[] { ActorKeys.Surprise, ActorKeys.Nod }; c.MinGap = 2.5f; c.MaxGap = 5f;
        }

        /// <summary>A scout holding up a lantern at the cave mouth, peering in.</summary>
        public static void BuildCaveScout(Transform root, Vector3 mouth, Vector3 outDir)
        {
            var side = new Vector3(outDir.z, 0, -outDir.x);
            var at = mouth - outDir * 3.2f + side * 1.6f;
            var scout = Person("cave_scout", "Scout", "Keeps watch at the cave mouth with a lantern.", "Something glitters down there.", new Color(0.30f, 0.38f, 0.42f), at, mouth + outDir * 2f);
            var mb = new MeshBuilder();
            var lp = at + side * 0.5f - outDir * 0.1f; lp.y = PlayerWalker.GroundHeight(lp);
            CampBuilder.Beam(mb, lp, lp + Vector3.up * 1.9f, 0.05f, CampBuilder.Wood);
            mb.Box(lp + Vector3.up * 1.65f, new Vector3(0.26f, 0.34f, 0.26f), new Color(1f, 0.82f, 0.45f));
            mb.ToObject("Scout lantern", root, MaterialFactory.CreateVertexColored(Color.white, "Scout lantern")).layer = 2;
            HubArt.Glow(root, lp + Vector3.up * 1.7f, 7f, 1.5f);
            var c = Chatter(root, "Cave scout", 47, scout);
            c.Moves = new[] { ActorKeys.Beckon, ActorKeys.Surprise }; c.MinGap = 3f; c.MaxGap = 6f;
        }

        /// <summary>The player's start fire in the near foreground band: a ring of stones, a small fire, two log seats.</summary>
        public static void BuildStartFire(Transform root, Vector3 at, Vector3 faceTowards)
        {
            at.y = 0;
            var mb = new MeshBuilder();
            var rnd = new PropRandom(5150);
            for (int i = 0; i < 9; i++) { float a = i * Mathf.PI * 2f / 9f; mb.Rock(at + new Vector3(Mathf.Sin(a), 0, Mathf.Cos(a)) * 1.0f, 0.35f, VistaTerrain.RockDark, rnd.Next); }
            for (int i = 0; i < 4; i++) { float a = i * Mathf.PI / 2f + 0.4f; CampBuilder.Beam(mb, at + new Vector3(Mathf.Sin(a) * 0.6f, 0.05f, Mathf.Cos(a) * 0.6f), at + Vector3.up * 0.55f, 0.1f, CampBuilder.Charcoal); }
            mb.Cone(at + Vector3.up * 0.1f, 0.45f, 0.95f, 7, new Color(1f, 0.55f, 0.18f));
            var f = faceTowards - at; f.y = 0; f = f.sqrMagnitude > 1e-3f ? f.normalized : Vector3.forward;
            var side = new Vector3(f.z, 0, -f.x);
            foreach (float s in new[] { -1f, 1f }) CampBuilder.Beam(mb, at + side * s * 2.0f - f * 0.9f + Vector3.up * 0.25f, at + side * s * 2.0f + f * 0.9f + Vector3.up * 0.25f, 0.4f, CampBuilder.Wood);
            mb.ToObject("Start fire", root, MaterialFactory.CreateVertexColored(Color.white, "Start fire")).layer = 2;
            var g = HubArt.Glow(root, at + Vector3.up * 1.2f, 12f, 2.2f);
            g.color = new Color(1f, 0.62f, 0.3f);
            WalkGrid.AddSolid(at, 1.3f, Owner);
            LandViews.AddClearing(at.x, at.z, 4f);
        }
    }
}
