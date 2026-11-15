using UnityEngine;
using World.Sim;
using World.Sim.Campaign;

namespace World.Game
{
    /// <summary>
    /// The surface end of a way into an interior (package I5): a stair mouth framed by standing stones, with a stone
    /// kerb round a dark well and steps fading into the black. Shut, a heavy slab cut with three marks and a ring lies
    /// over it; open, the slab lies pushed aside and two braziers burn at the mouth. Driven by the way's facts.
    /// </summary>
    public sealed class SurfaceWayView : MonoBehaviour, IInteriorView
    {
        public InteriorLink Link;
        public Transform Slab;
        public FireLight[] Lights;
        public Vector3 SlabShut, SlabOpen;
        public Quaternion SlabShutRot, SlabOpenRot;
        public bool Open { get; private set; }
        private bool _synced;

        /// <summary>Clearing radius kept free of trees and crops round the mouth (m).</summary>
        public const float ClearingRadius = 6f;

        public static SurfaceWayView Create(Transform parent, InteriorLink link, InteriorDirector d)
        {
            var root = new GameObject("Way " + link.End).transform;
            root.SetParent(parent, false);
            var at = PlayerWalker.CellPosition(link.Cell);
            root.position = at;
            int down = link.OutHeading + 3;
            root.rotation = Quaternion.Euler(0, WalkLattice.Yaw(down), 0);
            LandViews.AddClearing(at.x, at.z, ClearingRadius);
            var v = root.gameObject.AddComponent<SurfaceWayView>();
            v.Link = link;

            var stone = new Color(0.58f, 0.56f, 0.52f);
            var mb = new MeshBuilder();
            // The well: the steps go down along +z (away from the place's anchor), fading to black.
            for (int k = 0; k < 6; k++)
            {
                float z = -0.6f + 0.42f * k, shade = Mathf.Lerp(0.5f, 0.015f, k / 5f);
                float y = 0.03f + 0.001f * k;
                mb.Quad(new Vector3(-1.05f, y, z - 0.21f), new Vector3(-1.05f, y, z + 0.21f), new Vector3(1.05f, y, z + 0.21f), new Vector3(1.05f, y, z - 0.21f), stone * shade);
                mb.Strip(new Vector3(-1.05f, y + 0.004f, z - 0.21f), new Vector3(1.05f, y + 0.004f, z - 0.21f), 0.05f, stone * (shade * 1.5f + 0.04f));
            }
            mb.Quad(new Vector3(-1.1f, 0.036f, 1.85f), new Vector3(-1.1f, 0.036f, 2.4f), new Vector3(1.1f, 0.036f, 2.4f), new Vector3(1.1f, 0.036f, 1.85f), new Color(0.01f, 0.01f, 0.01f));
            // The kerb.
            var rnd = new PropRandom(link.Cell.I * 31 + link.Cell.J * 7);
            for (int k = 0; k < 4; k++)
                foreach (float sgn in new[] { -1f, 1f })
                    mb.Box(new Vector3(1.4f * sgn, 0.25f, -0.6f + 0.85f * k), new Vector3(0.5f, 0.5f + rnd.Range(0, 0.15f), 0.8f), InteriorBuilder.Shade(stone * 0.85f, root.position + Vector3.right * k * sgn, 0.12f), rnd.Range(-4f, 4f));
            // The mouth: two standing stones and a lintel at the far end of the well, like a dolmen over the stair.
            foreach (float sgn in new[] { -1f, 1f })
                mb.Box(new Vector3(1.55f * sgn, 1.25f, 2.55f), new Vector3(0.6f, 2.5f + rnd.Range(0f, 0.3f), 0.7f), InteriorBuilder.Shade(stone * 0.8f, root.position + Vector3.up * sgn, 0.1f), rnd.Range(-5f, 5f));
            mb.Box(new Vector3(0, 2.65f, 2.55f), new Vector3(4.0f, 0.55f, 0.95f), stone * 0.75f, rnd.Range(-3f, 3f));
            mb.Box(new Vector3(0, 1.3f, 2.95f), new Vector3(3.2f, 2.6f, 0.3f), new Color(0.02f, 0.02f, 0.02f)); // the dark behind the mouth
            var body = mb.ToObject("Stair mouth", root, InteriorBuilder.RockMaterial);
            LightShapes.Apply(body.GetComponent<MeshRenderer>(), LightShapes.Read.Prop);

            // The slab with its marks: three cuts and a ring.
            var smb = new MeshBuilder();
            smb.Box(new Vector3(0, 0.14f, 0), new Vector3(2.5f, 0.24f, 3.0f), stone * 0.95f);
            var cut = new Color(0.14f, 0.13f, 0.12f);
            for (int k = 0; k < 3; k++) smb.Box(new Vector3(-0.7f + 0.22f * k, 0.265f, -0.6f), new Vector3(0.06f, 0.02f, 0.9f), cut);
            for (int k = 0; k < 12; k++)
            {
                float a = k * Mathf.PI / 6f;
                smb.Box(new Vector3(0.45f + Mathf.Cos(a) * 0.45f, 0.265f, 0.5f + Mathf.Sin(a) * 0.45f), new Vector3(0.24f, 0.02f, 0.07f), cut, -a * Mathf.Rad2Deg + 90f);
            }
            var slab = new GameObject("Slab").transform;
            slab.SetParent(root, false);
            var sg = smb.ToObject("Stone", slab, InteriorBuilder.RockMaterial);
            LightShapes.Apply(sg.GetComponent<MeshRenderer>(), LightShapes.Read.Prop);
            v.Slab = slab;
            v.SlabShut = new Vector3(0, 0, 0.7f); v.SlabShutRot = Quaternion.identity;
            v.SlabOpen = new Vector3(-3.2f, 0.25f, 0.4f); v.SlabOpenRot = Quaternion.Euler(0, -18f, 9f);
            slab.localPosition = v.SlabShut;

            v.Lights = new FireLight[2];
            for (int k = 0; k < 2; k++)
            {
                var p = root.TransformPoint(new Vector3(k == 0 ? -2.3f : 2.3f, 0, 2.2f));
                var f = WorldLights.Torch(root, p, false, true, "Mouth brazier");
                f.DayLevel = 0.4f;
                v.Lights[k] = f;
            }
            var thing = root.gameObject.AddComponent<SurfaceWayThing>();
            thing.Link = link;
            LandLook.PickBox(root.gameObject, new Vector3(0, 1f, 1f), new Vector3(3.6f, 2.2f, 4f));
            return v;
        }

        public void Sync(WorldState s)
        {
            Open = Link.IsOpen;
            foreach (var f in Lights) if (f != null) f.On = Open;
            if (!_synced)
            {
                _synced = true;
                Slab.localPosition = Open ? SlabOpen : SlabShut;
                Slab.localRotation = Open ? SlabOpenRot : SlabShutRot;
            }
        }

        private void Update()
        {
            if (!_synced) return;
            var p = Open ? SlabOpen : SlabShut; var r = Open ? SlabOpenRot : SlabShutRot;
            Slab.localPosition = Vector3.MoveTowards(Slab.localPosition, p, 1.6f * Time.deltaTime);
            Slab.localRotation = Quaternion.RotateTowards(Slab.localRotation, r, 20f * Time.deltaTime);
        }
    }

    /// <summary>A tap on a stair mouth: what it is, and the way down when it is open.</summary>
    public sealed class SurfaceWayThing : MonoBehaviour, IInteractable, IInspectable
    {
        public InteriorLink Link;
        public string Title => LandLook.Capitalise(Link?.Name ?? "a stair");
        public System.Collections.Generic.IEnumerable<string> Lines
        {
            get { yield return Link != null && Link.IsOpen ? "Steps go down into the dark." : "A heavy slab lies over it, cut with marks. It does not move."; }
        }
        public string Prompt => Title;

        public void Interact()
        {
            bool open = Link != null && Link.IsOpen;
            var lines = new System.Collections.Generic.List<string>(Lines);
            SpeechBubbles.Inspect(transform, Vector3.up * 2.2f, Title, lines, 6f, open ? new[] { "Go down" } : null, i =>
            {
                var w = PlayerWalker.Instance;
                if (i == 0 && w != null && w.Cell != Link.Cell) w.WalkTo(Link.Cell);
            });
        }
    }
}
