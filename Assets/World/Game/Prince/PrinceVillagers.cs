using UnityEngine;
using World.Sim;
using Cell = World.Game.WalkLattice.Cell;

namespace World.Game
{
    /// <summary>
    /// Spawns and dresses package N's villagers (plain primitives, original look): the prince (deep blue with a gold
    /// circlet and a short cape), the sawyer (green-brown, an axe on his back), the merchant (saffron, a pack), the
    /// builder (grey with a mallet) and, for the loss scene, the Ash Clan's war leader and warriors (red, horned).
    /// </summary>
    public static class PrinceVillagers
    {
        public static readonly Color PrinceTunic = new Color(0.20f, 0.24f, 0.62f);
        public static readonly Color SawyerTunic = new Color(0.36f, 0.42f, 0.20f);
        public static readonly Color MerchantTunic = new Color(0.90f, 0.62f, 0.16f);
        public static readonly Color BuilderTunic = new Color(0.50f, 0.48f, 0.44f);
        public static readonly Color AshTunic = new Color(0.62f, 0.14f, 0.10f);
        private static readonly Color Gold = new Color(1f, 0.80f, 0.25f), Wood = new Color(0.45f, 0.30f, 0.16f), Iron = new Color(0.55f, 0.57f, 0.60f);

        public static Npc SpawnPrince(Vector3 at, float yaw)
        {
            var n = Spawn(NpcIds.Prince, "The prince", "The prince. He plans what your people build, and needs your spells to start it.", PrinceTunic, at, yaw);
            if (n.Actor is PlaceholderActor a)
            {
                var gold = MaterialFactory.Shared(Gold);
                Signs.Prim(PrimitiveType.Cylinder, a.HeadPivot, "Circlet", new Vector3(0, 0.26f, 0), new Vector3(0.31f, 0.035f, 0.31f), gold);
                for (int i = 0; i < 5; i++)
                {
                    float ang = i * 72f * Mathf.Deg2Rad;
                    Signs.Prim(PrimitiveType.Cube, a.HeadPivot, "Point", new Vector3(Mathf.Sin(ang) * 0.15f, 0.32f, Mathf.Cos(ang) * 0.15f), new Vector3(0.04f, 0.09f, 0.04f), gold, new Vector3(0, i * 72f, 45f));
                }
                Signs.Prim(PrimitiveType.Cube, a.BodyPivot, "Cape", new Vector3(0, 1.0f, -0.2f), new Vector3(0.52f, 0.95f, 0.05f), MaterialFactory.Shared(new Color(0.55f, 0.10f, 0.16f)), new Vector3(8f, 0, 0));
            }
            return n;
        }

        public static Npc SpawnSawyer(Vector3 at, float yaw)
        {
            var n = Spawn(NpcIds.Sawyer, "Holt the sawyer", "Holt, the sawyer. He wants wood cut and logs turned into planks.", SawyerTunic, at, yaw);
            if (n.Actor is PlaceholderActor a)
            {
                Signs.Prim(PrimitiveType.Cube, a.BodyPivot, "AxeHaft", new Vector3(0.1f, 1.15f, -0.24f), new Vector3(0.05f, 0.8f, 0.05f), MaterialFactory.Shared(Wood), new Vector3(0, 0, 25f));
                Signs.Prim(PrimitiveType.Cube, a.BodyPivot, "AxeHead", new Vector3(0.24f, 1.48f, -0.24f), new Vector3(0.18f, 0.12f, 0.03f), MaterialFactory.Shared(Iron), new Vector3(0, 0, 25f));
            }
            return n;
        }

        public static Npc SpawnMerchant(Vector3 at, float yaw)
        {
            var n = Spawn(NpcIds.Merchant, "Wenna the merchant", "Wenna, the merchant. She wants roads, and trade along them.", MerchantTunic, at, yaw);
            if (n.Actor is PlaceholderActor a)
            {
                Signs.Prim(PrimitiveType.Cube, a.BodyPivot, "Pack", new Vector3(0, 1.15f, -0.3f), new Vector3(0.42f, 0.5f, 0.25f), MaterialFactory.Shared(new Color(0.48f, 0.34f, 0.22f)));
                Signs.Prim(PrimitiveType.Cylinder, a.HeadPivot, "Hat", new Vector3(0, 0.27f, 0), new Vector3(0.42f, 0.03f, 0.42f), MaterialFactory.Shared(new Color(0.62f, 0.25f, 0.12f)));
            }
            return n;
        }

        public static Npc SpawnBuilder(Vector3 at, float yaw)
        {
            var n = Spawn(NpcIds.Builder, "Tam the builder", "Tam, the builder. Puts up whatever the spells start.", BuilderTunic, at, yaw);
            if (n.Actor is PlaceholderActor a)
                Signs.Prim(PrimitiveType.Cube, a.BodyPivot, "Mallet", new Vector3(0.32f, 0.95f, 0.05f), new Vector3(0.14f, 0.12f, 0.24f), MaterialFactory.Shared(Wood));
            return n;
        }

        public static Npc SpawnAsh(string id, string name, bool leader, Vector3 at, float yaw)
        {
            var n = Spawn(id, name, leader ? "War leader of the Ash Clan." : "A warrior of the Ash Clan.", AshTunic, at, yaw);
            if (n.Actor is PlaceholderActor a)
            {
                var horn = MaterialFactory.Shared(new Color(0.92f, 0.88f, 0.78f));
                Signs.Prim(PrimitiveType.Capsule, a.HeadPivot, "HornL", new Vector3(-0.15f, 0.3f, 0), new Vector3(0.05f, 0.12f, 0.05f), horn, new Vector3(0, 0, 35f));
                Signs.Prim(PrimitiveType.Capsule, a.HeadPivot, "HornR", new Vector3(0.15f, 0.3f, 0), new Vector3(0.05f, 0.12f, 0.05f), horn, new Vector3(0, 0, -35f));
                Signs.Prim(PrimitiveType.Cube, a.BodyPivot, "Spear", new Vector3(0.35f, 1.2f, 0.1f), new Vector3(0.04f, 2.2f, 0.04f), MaterialFactory.Shared(Wood));
                if (leader) Signs.Prim(PrimitiveType.Cube, a.BodyPivot, "Pelt", new Vector3(0, 1.32f, 0), new Vector3(0.6f, 0.14f, 0.42f), MaterialFactory.Shared(new Color(0.25f, 0.2f, 0.18f)));
            }
            return n;
        }

        private static Npc Spawn(string id, string name, string role, Color tunic, Vector3 at, float yaw)
        {
            at.y = PlayerWalker.GroundHeight(at);
            var n = Npc.Spawn(id, name, role, "", tunic, at, yaw);
            n.Directed = true;
            return n;
        }

        /// <summary>
        /// A free lattice spot near the home camp for a villager to stand when idle: <paramref name="ring"/> steps from
        /// the corner, between the spokes, at least 7 m from every plot centre of that corner, the spawn and Mira.
        /// </summary>
        public static Vector3 HomeSpot(WorldState s, CampLayout camp, int preference)
        {
            var t = s.Board.Topology;
            var corner = WalkLattice.ToCell(camp.Hearth);
            Vector3 best = WalkLattice.ToWorld(corner + WalkLattice.Dir(preference % 6) * 2);
            for (int ring = 2; ring <= 4; ring++)
                for (int k = 0; k < 6; k++)
                {
                    int h = (preference + k) % 6;
                    for (int side = 0; side < 2; side++)
                    {
                        var c = corner + WalkLattice.Dir(h) * ring + (side == 0 ? new Cell() : WalkLattice.Dir(h + 1));
                        if (WalkGrid.IsBlocked(c) || c == camp.Spawn || c == camp.PrinceStart) continue;
                        var p = WalkLattice.ToWorld(c);
                        if (BoardSpace.Flat(p - camp.MiraSpot) < 4f || BoardSpace.Flat(p - camp.DanceCircleCentre) < 10f) continue;
                        bool clear = true;
                        for (int plot = camp.HomeCorner * PlotLayout.PlotsPerCorner; plot < (camp.HomeCorner + 1) * PlotLayout.PlotsPerCorner; plot++)
                        {
                            var pp = new Vector3((float)PlotLayout.PlotX(t, plot), 0, (float)PlotLayout.PlotZ(t, plot));
                            if (BoardSpace.Flat(p - pp) < 7f) { clear = false; break; }
                        }
                        if (clear) return p;
                    }
                }
            return best;
        }
    }
}
