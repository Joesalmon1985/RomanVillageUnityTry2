using System.Collections.Generic;
using UnityEngine;
using World.Sim;

namespace World.Game
{
    /// <summary>
    /// A people's temple (GB-1): a stone platform, six pillars, a roof in the people's colour, a banner and an altar with a
    /// lamb. Original primitive art. Smoke rises from the roof while <see cref="Smoking"/> (that people is taking its turn).
    /// Only the player's own temple has a working altar (<see cref="TempleAltar"/>); the others are scenery.
    /// </summary>
    public sealed class TempleView : MonoBehaviour, IInspectable
    {
        public const float Radius = 5.2f;
        public const int PuffCount = 6;

        public int People { get; private set; }
        public string PeopleName { get; private set; }
        public Color Colour { get; private set; }
        public Transform AltarSpot { get; private set; }
        public bool Smoking { get; set; }
        /// <summary>Number of smoke puffs showing now (tests).</summary>
        public int PuffsShown { get; private set; }

        private Transform[] _puffs;
        private Material _smokeMat;
        private float _clock;

        public string Title => "Temple of " + PeopleName;
        public IEnumerable<string> Lines => new[] { People == PlayerIds.Human ? "Our temple. Sacrifice a lamb at the altar to open the god board." : "A rival temple. Smoke rises while they take their turn." };

        public static Color PeopleColour(People p) => new Color(p.ColorR / 255f, p.ColorG / 255f, p.ColorB / 255f);

        public static TempleView Create(Transform parent, People people, Vector3 at, float yaw, bool playerAltar)
        {
            var go = new GameObject("Temple " + people.Id + " (" + people.Name + ")");
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(at, Quaternion.Euler(0, yaw, 0));
            var v = go.AddComponent<TempleView>();
            v.People = people.Id;
            v.PeopleName = people.Name.StartsWith("the ") ? char.ToUpper(people.Name[4]) + people.Name.Substring(5) : people.Name;
            v.Colour = PeopleColour(people);
            v.Build(playerAltar);
            return v;
        }

        private void Build(bool playerAltar)
        {
            var stone = new Color(0.72f, 0.69f, 0.62f);
            var dark = new Color(0.36f, 0.34f, 0.31f);
            var mb = new MeshBuilder();
            mb.Cylinder(Vector3.zero, Radius, Radius - 0.4f, 0.5f, 12, stone * 0.85f);
            mb.Cylinder(Vector3.up * 0.5f, Radius - 0.8f, Radius - 1.1f, 0.45f, 12, stone * 0.95f);
            for (int i = 0; i < 6; i++)
            {
                float a = i * Mathf.PI / 3f;
                var p = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * (Radius - 1.7f);
                mb.Cylinder(p + Vector3.up * 0.95f, 0.38f, 0.32f, 3.6f, 7, stone);
                mb.Box(p + Vector3.up * 4.65f, new Vector3(1.0f, 0.35f, 1.0f), stone * 0.9f);
            }
            // Roof: a round cap in the people's colour with a darker rim.
            mb.Cylinder(Vector3.up * 4.8f, Radius - 0.5f, Radius - 1.0f, 0.4f, 12, Colour * 0.6f);
            mb.Cone(Vector3.up * 5.2f, Radius - 1.0f, 2.6f, 12, Colour);
            // Banner on a pole at the front.
            var pole = new Vector3(0, 0, -(Radius + 0.6f));
            mb.Cylinder(pole, 0.12f, 0.1f, 7f, 5, dark);
            mb.Quad(pole + new Vector3(0, 6.9f, 0), pole + new Vector3(1.9f, 6.9f, 0), pole + new Vector3(1.9f, 5.2f, 0), pole + new Vector3(0, 5.2f, 0), Colour);
            mb.Quad(pole + new Vector3(0, 6.9f, 0), pole + new Vector3(0, 5.2f, 0), pole + new Vector3(1.9f, 5.2f, 0), pole + new Vector3(1.9f, 6.9f, 0), Colour * 0.85f);
            // Altar block with a lamb on it and a bowl of embers.
            mb.Box(new Vector3(0, 1.4f, 0), new Vector3(2.4f, 0.9f, 1.4f), dark);
            mb.Box(new Vector3(0, 2.0f, 0), new Vector3(2.7f, 0.3f, 1.7f), stone * 0.8f);
            mb.Box(new Vector3(-0.5f, 2.35f, 0), new Vector3(0.9f, 0.45f, 0.4f), new Color(0.95f, 0.94f, 0.9f)); // the lamb's body
            mb.Box(new Vector3(0.05f, 2.45f, 0), new Vector3(0.3f, 0.3f, 0.28f), new Color(0.9f, 0.88f, 0.82f)); // head
            mb.Cylinder(new Vector3(0.8f, 2.15f, 0), 0.35f, 0.25f, 0.2f, 8, new Color(0.25f, 0.22f, 0.2f));
            mb.Cylinder(new Vector3(0.8f, 2.35f, 0), 0.22f, 0f, 0.35f, 6, new Color(1f, 0.55f, 0.15f));
            var body = mb.ToObject("Body", transform, MaterialFactory.CreateVertexColored(Color.white, "Temple"));
            body.layer = 2;

            var spot = new GameObject("AltarSpot");
            spot.transform.SetParent(transform, false);
            spot.transform.localPosition = new Vector3(0, 2.2f, 0);
            AltarSpot = spot.transform;

            var pick = new GameObject("Pick");
            pick.transform.SetParent(transform, false);
            LandLook.PickBox(pick, new Vector3(0, 2.5f, 0), new Vector3(Radius * 1.6f, 5f, Radius * 1.6f));
            if (playerAltar) pick.AddComponent<TempleAltar>().Temple = this;
            else pick.AddComponent<TempleScenery>().Temple = this;

            _smokeMat = MaterialFactory.CreateUnlit(new Color(0.55f, 0.55f, 0.56f), "TempleSmoke");
            _puffs = new Transform[PuffCount];
            for (int i = 0; i < PuffCount; i++)
            {
                var pm = new MeshBuilder();
                pm.Cylinder(Vector3.zero, 1f, 0.6f, 1.2f, 7, Color.white);
                var g = pm.ToObject("Puff" + i, transform, _smokeMat);
                g.layer = 2;
                g.SetActive(false);
                _puffs[i] = g.transform;
            }
        }

        private void Update()
        {
            _clock += Time.deltaTime;
            int shown = 0;
            for (int i = 0; i < PuffCount; i++)
            {
                var t = _puffs[i];
                if (!Smoking) { if (t.gameObject.activeSelf) t.gameObject.SetActive(false); continue; }
                float u = Mathf.Repeat(_clock * 0.22f + i / (float)PuffCount, 1f);
                float s = Mathf.Sin(u * Mathf.PI);
                t.gameObject.SetActive(s > 0.02f);
                if (s > 0.02f) shown++;
                t.localPosition = new Vector3(Mathf.Sin(i * 2.1f + u * 2f) * 0.8f, 8.2f + u * 13f, Mathf.Cos(i * 1.7f) * 0.6f);
                t.localScale = Vector3.one * (0.6f + 2.2f * s) * (0.5f + 0.5f * (1f - u));
            }
            PuffsShown = shown;
        }

        private void OnDestroy() { if (_smokeMat != null) Destroy(_smokeMat); }
    }

    /// <summary>A rival temple: only scenery, it says so when tapped.</summary>
    public sealed class TempleScenery : MonoBehaviour, IInspectable
    {
        public TempleView Temple;
        public string Title => Temple != null ? Temple.Title : "Temple";
        public IEnumerable<string> Lines => Temple != null ? Temple.Lines : new string[0];
    }

    /// <summary>The player's altar: pressing it (or E close by) sacrifices a lamb and opens the god board (GB-1).</summary>
    public sealed class TempleAltar : MonoBehaviour, IInteractable, IInspectable
    {
        public TempleView Temple;
        public string Prompt => "Sacrifice a lamb";
        public string Title => "The altar";
        public IEnumerable<string> Lines => new[] { "Sacrifice a lamb here to open the god board." };
        public void Interact() { if (GodBoardDirector.Instance != null) GodBoardDirector.Instance.TrySacrifice(); }
    }

    /// <summary>Where a temple stands: clear ground near the people's first camp (no plot, path, patch or camp prop).</summary>
    public static class TempleSites
    {
        /// <summary>Metres from the corner searched, nearest first.</summary>
        public const float MinDistance = 14f, MaxDistance = 44f;
        public const float Footprint = 6.2f;

        public static bool Find(Board board, int corner, IReadOnlyList<CampProp> avoid, out Vector3 at, out float yawToCorner)
        {
            var t = board.Topology;
            var c = BoardSpace.Vertex(t, corner);
            var outward = c; outward.y = 0;
            float baseYaw = outward.sqrMagnitude > 1 ? BoardSpace.YawOf(outward) : 0f;
            for (float r = MinDistance; r <= MaxDistance; r += 2f)
            {
                for (int k = 0; k < 36; k++)
                {
                    float off = ((k + 1) / 2) * 10f * (k % 2 == 0 ? 1 : -1);
                    var p = c + BoardSpace.DirOf(baseYaw + off) * r;
                    if (Ok(board, p, avoid)) { at = p; yawToCorner = BoardSpace.YawOf(c - p); return true; }
                }
            }
            at = c; yawToCorner = 0; return false;
        }

        private static bool Ok(Board board, Vector3 p, IReadOnlyList<CampProp> avoid)
        {
            if (board.Topology.HexAt(p.x, p.z) < 0) return false;
            // Whole footprint on the board and clear of every plot, patch, path and corner.
            for (int i = 0; i < 6; i++)
            {
                var q = p + BoardSpace.DirOf(i * 60f) * Footprint;
                if (board.Topology.HexAt(q.x, q.z) < 0) return false;
            }
            if (CampLayout.Obstruction(board, p, Footprint) != null) return false;
            if (avoid != null)
                foreach (var a in avoid)
                    if (BoardSpace.Flat(a.Position - p) < a.Radius + Footprint + 1.5f) return false;
            return true;
        }
    }
}
