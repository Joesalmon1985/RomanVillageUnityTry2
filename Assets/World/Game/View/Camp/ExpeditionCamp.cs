using System.Collections.Generic;
using UnityEngine;

namespace World.Game
{
    /// <summary>
    /// HUB-CAVE (WORLD_UI_PLAN 2.1): Mira's expedition camp at the edge of the hub: packs, a bedroll, a lantern on a pole, two
    /// companions waiting and Mira studying a hide map on a trestle. Tapping Mira or the map opens her existing conversation
    /// (<see cref="MiraOffer"/>: set out for the crystals, practice duel, maybe later). Owner: WU-1.
    /// </summary>
    public sealed class ExpeditionCamp : MonoBehaviour
    {
        public static ExpeditionCamp Instance { get; private set; }
        public HubFrame Frame { get; private set; }
        public Npc[] Companions { get; private set; }
        public Vector3 MapSpot { get; private set; }
        public Light Lantern { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Instance = null;

        public static ExpeditionCamp Build(Transform parent, CampLayout layout)
        {
            var go = new GameObject("Expedition camp");
            go.transform.SetParent(parent, false);
            var c = go.AddComponent<ExpeditionCamp>();
            Instance = c;
            c.Frame = new HubFrame(layout.MiraSpot, layout.MiraYaw);
            c.BuildProps(go.transform);
            c.BuildPeople();
            LandViews.AddClearing(layout.MiraSpot.x, layout.MiraSpot.z, 7f);
            return c;
        }

        private void OnDestroy() { if (Instance == this) Instance = null; }

        private void BuildProps(Transform root)
        {
            var mb = new MeshBuilder();
            // Packs: hide bundles with straps, a rolled bedroll and a water skin.
            Vector3[] packs = { new Vector3(-1.7f, 0, -2.5f), new Vector3(1.4f, 0, -3.0f), new Vector3(3.4f, 0, -1.4f) };
            for (int i = 0; i < packs.Length; i++)
            {
                mb.Box(packs[i] + new Vector3(0, 0.4f, 0), new Vector3(0.8f, 0.8f, 0.6f), i % 2 == 0 ? CampBuilder.Hide : CampBuilder.HideDark, i * 25f);
                mb.Box(packs[i] + new Vector3(0, 0.6f, 0.02f), new Vector3(0.86f, 0.08f, 0.66f), CampBuilder.Ochre, i * 25f);
                mb.Cylinder(packs[i] + new Vector3(0, 0.78f, 0), 0.2f, 0.2f, 0.7f, 8, CampBuilder.Fur);
            }
            mb.Cylinder(new Vector3(-3.2f, 0.2f, 0.4f), 0.2f, 0.2f, 1.1f, 8, CampBuilder.Fur);
            // The lantern: a pole with a hanging horn lantern that glows warm.
            var pole = new Vector3(2.4f, 0, 0.9f);
            CampBuilder.Beam(mb, pole, pole + Vector3.up * 2.2f, 0.1f, CampBuilder.Wood);
            CampBuilder.Beam(mb, pole + Vector3.up * 2.1f, pole + new Vector3(-0.5f, 2.1f, 0), 0.06f, CampBuilder.Wood);
            mb.Box(pole + new Vector3(-0.5f, 1.8f, 0), new Vector3(0.28f, 0.4f, 0.28f), new Color(1f, 0.78f, 0.42f));
            // The hide map on a low trestle in front of Mira: a tilted board with a hide sheet and inked lines.
            MapSpot = Frame.W(0, 1.5f);
            var map = new Vector3(0, 0, 1.5f);
            foreach (float x in new[] { -0.7f, 0.7f })
            {
                CampBuilder.Beam(mb, map + new Vector3(x, 0, -0.4f), map + new Vector3(x, 0.9f, -0.1f), 0.07f, CampBuilder.Wood);
                CampBuilder.Beam(mb, map + new Vector3(x, 0, 0.4f), map + new Vector3(x, 0.9f, 0.1f), 0.07f, CampBuilder.Wood);
            }
            var a = map + new Vector3(-0.85f, 0.93f, -0.35f); var b = map + new Vector3(0.85f, 0.93f, -0.35f);
            var c = map + new Vector3(0.85f, 0.93f, 0.35f); var d = map + new Vector3(-0.85f, 0.93f, 0.35f);
            CampBuilder.Sheet(mb, a, b, c, d, CampBuilder.Clay);
            for (int i = 0; i < 4; i++) mb.Box(map + new Vector3(-0.5f + i * 0.33f, 0.95f, -0.1f + 0.15f * Mathf.Sin(i * 2.1f)), new Vector3(0.22f, 0.012f, 0.03f), CampBuilder.Charcoal, 20f * i);
            mb.Box(map + new Vector3(0.3f, 0.955f, 0.1f), new Vector3(0.12f, 0.02f, 0.12f), CampBuilder.Ochre);
            var body = HubArt.Make(root, "Expedition camp", Frame, mb);
            body.layer = 2;
            var tap = HubArt.Pick<MapTap>(root, "Map tap", Frame, new Vector3(0, 0.85f, 1.5f), new Vector3(1.9f, 0.3f, 1.0f));
            var lg = new GameObject("Lantern light");
            lg.transform.SetParent(root, false);
            lg.transform.position = Frame.W(2.4f - 0.5f, 0.9f) + Vector3.up * 1.9f;
            Lantern = lg.AddComponent<Light>();
            Lantern.type = LightType.Point; Lantern.range = 9f; Lantern.intensity = 1.3f; Lantern.color = new Color(1f, 0.78f, 0.45f);
        }

        private void BuildPeople()
        {
            Companions = new Npc[2];
            string[] names = { "Scout", "Porter" };
            Color[] tun = { new Color(0.32f, 0.50f, 0.34f), new Color(0.58f, 0.46f, 0.30f) };
            Vector3[] at = { Frame.W(-2.8f, -1.0f), Frame.W(2.9f, -1.8f) };
            for (int i = 0; i < 2; i++)
            {
                var n = Npc.Spawn("camp_companion_" + i, names[i], "Waits with Mira's expedition.", "", tun[i], at[i], BoardSpace.YawOf(Frame.W(0, 0.5f) - at[i]));
                n.ShowNamePlate = false;
                n.Doing = () => "We leave when Mira says. The crystals will keep.";
                Companions[i] = n;
            }
        }
    }

    /// <summary>A tap on Mira's hide map opens her conversation.</summary>
    public sealed class MapTap : MonoBehaviour, IInteractable, IInspectable
    {
        public string Prompt => "Ask Mira about the map";
        public string Title => "Mira's hide map";
        public IEnumerable<string> Lines => new[] { "A hide map inked with the road to the crystals." };
        public void Interact() { var m = Npc.Find(NpcIds.Mira); if (m != null) m.Interact(); }
    }
}
