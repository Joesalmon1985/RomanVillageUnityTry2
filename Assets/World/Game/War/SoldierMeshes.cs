using UnityEngine;
using World.Sim.War;

namespace World.Game
{
    /// <summary>
    /// Original primitive soldier parts (G3-3): the torso with head and look gear, legs hanging from the hip, a weapon arm and a
    /// shield arm, all facing +Z with feet at y = 0. Gear levels (0..3 per stat) show as more bulk, longer and larger weapon
    /// heads, bigger rimmed shields with clay plates, more paint stripes and feathers.
    /// </summary>
    public static class SoldierMeshes
    {
        private static readonly Color Flint = new Color(0.40f, 0.42f, 0.44f), Obsidian = new Color(0.16f, 0.16f, 0.20f);
        private static readonly Color Fur = new Color(0.34f, 0.25f, 0.16f), Clay = new Color(0.70f, 0.42f, 0.28f);

        /// <summary>The colour of a look's pip on the board marker.</summary>
        public static Color LookColour(SoldierLook l)
        {
            switch (l)
            {
                case SoldierLook.Brute: return new Color(0.55f, 0.35f, 0.15f);
                case SoldierLook.Raider: return new Color(1f, 0.25f, 0.15f);
                case SoldierLook.Shieldbearer: return new Color(0.9f, 0.95f, 1f);
                case SoldierLook.Skirmisher: return new Color(0.35f, 0.95f, 0.45f);
                default: return Color.white;
            }
        }

        private static Color Hide(Color people) => Color.Lerp(new Color(0.52f, 0.38f, 0.24f), people, 0.55f);

        public static void Leg(MeshBuilder mb, Color people)
        {
            mb.Box(new Vector3(0, -0.40f, 0), new Vector3(0.15f, 0.8f, 0.17f), LandLook.Skin);
            mb.Box(new Vector3(0, -0.70f, 0.02f), new Vector3(0.19f, 0.28f, 0.25f), Fur);
        }

        public static void Torso(MeshBuilder mb, SoldierLook look, Color people, int[] lv)
        {
            var hide = Hide(people);
            float wide = 0.46f + (look == SoldierLook.Brute ? 0.1f : 0f);
            mb.Box(new Vector3(0, 1.10f, 0), new Vector3(wide, 0.52f, 0.27f), hide);
            mb.Box(new Vector3(0, 0.74f, 0), new Vector3(wide + 0.08f, 0.30f, 0.32f), Fur); // kilt
            mb.Box(new Vector3(0, 0.90f, 0), new Vector3(wide + 0.06f, 0.07f, 0.30f), Color.Lerp(people, Color.white, 0.4f)); // sash in the people's colour
            mb.Box(new Vector3(0, 1.5f, 0), new Vector3(0.26f, 0.3f, 0.26f), LandLook.Skin); // head
            mb.Box(new Vector3(0, 1.68f, -0.03f), new Vector3(0.28f, 0.1f, 0.3f), LandLook.Hair);
            // Health: fur mantle (1), horned antler band (3).
            if (lv[Stat.Health] >= 1) mb.Box(new Vector3(0, 1.36f, 0), new Vector3(wide + 0.22f, 0.14f, 0.36f), Fur);
            if (lv[Stat.Health] >= 2) mb.Box(new Vector3(0, 1.28f, -0.02f), new Vector3(wide + 0.3f, 0.1f, 0.4f), Fur * 1.2f);
            if (lv[Stat.Health] >= 3)
            {
                mb.Cone(new Vector3(-0.14f, 1.74f, 0), 0.05f, 0.34f, 4, new Color(0.88f, 0.84f, 0.72f));
                mb.Cone(new Vector3(0.14f, 1.74f, 0), 0.05f, 0.34f, 4, new Color(0.88f, 0.84f, 0.72f));
            }
            // Defence: clay plates on the chest (2) and shoulders (3).
            if (lv[Stat.Defence] >= 2) mb.Box(new Vector3(0, 1.14f, 0.15f), new Vector3(wide * 0.8f, 0.4f, 0.05f), Clay);
            if (lv[Stat.Defence] >= 3) { mb.Box(new Vector3(-0.28f, 1.36f, 0), new Vector3(0.2f, 0.08f, 0.3f), Clay); mb.Box(new Vector3(0.28f, 1.36f, 0), new Vector3(0.2f, 0.08f, 0.3f), Clay); }
            // Evade: ochre paint stripes (1..3) across the chest and the face, feathers in the hair (3).
            var paint = new Color(0.85f, 0.45f, 0.12f);
            for (int i = 0; i < lv[Stat.Evade]; i++)
            {
                mb.Box(new Vector3(0, 1.22f - i * 0.1f, 0.14f), new Vector3(wide * 0.9f, 0.04f, 0.02f), paint);
                mb.Box(new Vector3(-0.07f + i * 0.07f, 1.5f, 0.135f), new Vector3(0.03f, 0.2f, 0.02f), paint);
            }
            if (lv[Stat.Evade] >= 3) mb.Box(new Vector3(0.12f, 1.9f, -0.05f), new Vector3(0.05f, 0.35f, 0.04f), new Color(0.2f, 0.75f, 0.35f), 15f);
            switch (look)
            {
                case SoldierLook.Brute:
                    mb.Box(new Vector3(0, 1.38f, 0), new Vector3(wide + 0.1f, 0.18f, 0.38f), Fur);
                    mb.Box(new Vector3(0, 1.0f, 0.15f), new Vector3(wide * 0.9f, 0.12f, 0.03f), new Color(0.3f, 0.22f, 0.14f)); // broad belt
                    break;
                case SoldierLook.Raider:
                    // Red war paint: a band across the eyes and a slash over the chest.
                    mb.Box(new Vector3(0, 1.55f, 0.135f), new Vector3(0.27f, 0.07f, 0.02f), new Color(0.80f, 0.12f, 0.08f));
                    mb.Box(new Vector3(0, 1.12f, 0.145f), new Vector3(0.08f, 0.5f, 0.02f), new Color(0.80f, 0.12f, 0.08f), 28f);
                    break;
                case SoldierLook.Shieldbearer:
                    mb.Box(new Vector3(0, 1.74f, 0), new Vector3(0.3f, 0.12f, 0.3f), Clay * 0.9f); // clay cap
                    break;
                case SoldierLook.Skirmisher:
                    mb.Box(new Vector3(0, 1.64f, 0.1f), new Vector3(0.3f, 0.06f, 0.08f), people); // headband
                    mb.Box(new Vector3(-0.1f, 1.86f, -0.04f), new Vector3(0.04f, 0.3f, 0.04f), new Color(0.2f, 0.75f, 0.35f), -12f);
                    mb.Box(new Vector3(0.0f, 0.98f, -0.17f), new Vector3(0.16f, 0.5f, 0.08f), new Color(0.4f, 0.3f, 0.18f)); // javelin quiver
                    break;
                default:
                    mb.Box(new Vector3(0, 1.74f, 0), new Vector3(0.3f, 0.08f, 0.3f), Color.Lerp(people, Color.white, 0.4f));
                    mb.Cone(new Vector3(0, 1.78f, 0), 0.1f, 0.35f, 4, people); // the plain warrior's crest
                    break;
            }
        }

        /// <summary>The weapon arm: pivot at the shoulder, arm hanging down, the weapon standing up out of the fist.</summary>
        public static void WeaponArm(MeshBuilder mb, SoldierLook look, Color people, int[] lv)
        {
            int a = lv[Stat.Attack];
            mb.Box(new Vector3(0, -0.28f, 0), new Vector3(0.13f, 0.56f, 0.15f), LandLook.Skin);
            var hand = new Vector3(0, -0.58f, 0.04f);
            var head = a >= 3 ? Obsidian : Flint;
            switch (look)
            {
                case SoldierLook.Brute:
                {   // a maul: a thick log with a stone lashed on; the stone grows with level
                    LandShapes.Beam(mb, hand + Vector3.down * 0.35f, hand + Vector3.up * (1.05f + 0.07f * a), 0.06f, 5, LandLook.Wood);
                    float sz = 0.2f + 0.05f * a;
                    mb.Box(hand + Vector3.up * (1.0f + 0.07f * a), new Vector3(sz * 1.3f, sz, sz), head);
                    mb.Box(hand + Vector3.up * (0.88f + 0.07f * a), new Vector3(sz * 1.35f, 0.05f, sz * 1.1f), LandLook.Thatch);
                    break;
                }
                case SoldierLook.Raider:
                {   // a hafted stone axe: one blade, a second at level 2, a long beard at level 3
                    LandShapes.Beam(mb, hand + Vector3.down * 0.25f, hand + Vector3.up * 0.95f, 0.04f, 5, LandLook.Wood);
                    float bl = 0.26f + 0.06f * a;
                    mb.Box(hand + new Vector3(0, 0.88f, 0.12f), new Vector3(0.05f, bl, bl + 0.1f), head);
                    if (a >= 2) mb.Box(hand + new Vector3(0, 0.88f, -0.12f), new Vector3(0.05f, bl * 0.8f, bl), head);
                    if (a >= 3) mb.Box(hand + new Vector3(0, 0.62f, 0.14f), new Vector3(0.04f, 0.2f, 0.12f), head);
                    break;
                }
                case SoldierLook.Skirmisher:
                {   // a light javelin and, with level, a flint knife on the belt
                    LandShapes.Beam(mb, hand + Vector3.down * 0.4f, hand + Vector3.up * (1.5f + 0.1f * a), 0.025f, 4, LandLook.Wood);
                    mb.Cone(hand + Vector3.up * (1.5f + 0.1f * a), 0.05f, 0.25f + 0.04f * a, 4, head);
                    break;
                }
                default:
                {   // the spear: Warrior and Shieldbearer
                    float len = (look == SoldierLook.Shieldbearer ? 1.45f : 1.7f) + 0.1f * a;
                    LandShapes.Beam(mb, hand + Vector3.down * 0.5f, hand + Vector3.up * len, 0.035f, 5, LandLook.Wood);
                    mb.Cone(hand + Vector3.up * len, 0.07f + 0.02f * a, 0.3f + 0.05f * a, 4, head);
                    for (int i = 0; i < a; i++) mb.Box(hand + Vector3.up * (len - 0.1f - 0.12f * i), new Vector3(0.09f, 0.04f, 0.09f), Color.Lerp(people, Color.white, 0.2f));
                    break;
                }
            }
            mb.Box(hand, new Vector3(0.12f, 0.12f, 0.14f), LandLook.Skin);
        }

        /// <summary>The off arm: pivot at the shoulder, arm hanging; the shield is its own mesh so it can drop.</summary>
        public static void ShieldArm(MeshBuilder mb, SoldierLook look, Color people, int[] lv, bool shield)
        {
            mb.Box(new Vector3(0, -0.28f, 0), new Vector3(0.13f, 0.56f, 0.15f), LandLook.Skin);
            mb.Box(new Vector3(0, -0.58f, 0.04f), new Vector3(0.12f, 0.12f, 0.14f), LandLook.Skin);
            if (look == SoldierLook.Skirmisher) // a second javelin held low
                LandShapes.Beam(mb, new Vector3(0, -0.9f, 0.05f), new Vector3(0, 0.9f, 0.05f), 0.02f, 4, LandLook.Wood);
        }

        /// <summary>The shield as a disc standing on the Y axis (the view turns it to face forward).</summary>
        public static void Shield(MeshBuilder mb, SoldierLook look, Color people, int[] lv)
        {
            int d = lv[Stat.Defence];
            float r = look == SoldierLook.Shieldbearer ? 0.36f + 0.06f * d : 0.22f + 0.04f * d;
            mb.Cylinder(new Vector3(0, -0.03f, 0), r * 1.07f, r * 1.07f, 0.04f, 12, Color.Lerp(people, Color.black, 0.15f), true);
            mb.Cylinder(new Vector3(0, 0f, 0), r, r, 0.07f, 12, new Color(0.62f, 0.46f, 0.28f), true);
            for (int i = 0; i < d; i++) mb.Box(new Vector3(0, 0.08f, 0), new Vector3(r * 1.8f, 0.03f, 0.05f), Color.Lerp(people, Color.white, 0.35f), i * 60f - 30f);
            mb.Box(new Vector3(0, 0.1f, 0), new Vector3(0.1f, 0.06f, 0.1f), LandLook.Stone);
        }
    }
}
