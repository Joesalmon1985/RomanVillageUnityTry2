using UnityEngine;
using World.Sim;

namespace World.Game
{
    /// <summary>What a figure is doing (drives its little animation).</summary>
    public enum FigurePose { Stand, Walk, Work, Slump }

    /// <summary>
    /// A primitive person of the land views (carrier, porter, gatherer, builder, warrior, demon): one shared mesh,
    /// a load part, and a bob or bend animated by its owner view (no Update of its own).
    /// </summary>
    public sealed class LandFigure : MonoBehaviour
    {
        public enum Kind { Villager, Warrior, Demon }

        public Kind FigureKind { get; private set; }
        public int Faction { get; private set; }
        public FigurePose Pose = FigurePose.Stand;
        public Transform Body { get; private set; }

        private GameObject _load;
        private string _loadKey;
        private float _phase;

        public static LandFigure Create(Transform parent, string name, int faction, Kind kind = Kind.Villager, float scale = 1f)
        {
            var root = new GameObject(name);
            root.transform.SetParent(parent, false);
            var f = root.AddComponent<LandFigure>();
            f.FigureKind = kind;
            f.Faction = faction;
            f._phase = (name.GetHashCode() & 1023) / 163f;
            var body = new GameObject("Body").transform;
            body.SetParent(root.transform, false);
            body.localScale = Vector3.one * scale;
            f.Body = body;
            string key = kind + "/" + faction;
            var mesh = LandLook.Cached("person/" + key, mb =>
            {
                if (kind == Kind.Demon) LandShapes.Person(mb, new Color(0.45f, 0.08f, 0.12f), new Color(0.12f, 0.02f, 0.04f), false, true);
                else LandShapes.Person(mb, LandLook.People(faction), LandLook.PeopleTrim(faction), kind == Kind.Warrior);
            });
            LandLook.Part(body, "Mesh", mesh, null, false);
            if (kind == Kind.Demon)
                LandLook.Part(body, "Eyes", LandLook.Cached("person/demon-eyes", LandShapes.DemonEyes), LandLook.Unlit, false);
            if (kind == Kind.Warrior)
                LandLook.Part(body, "Spear", LandLook.Cached("person/spear", mb =>
                {
                    LandShapes.Beam(mb, new Vector3(0.38f, 0.1f, 0.12f), new Vector3(0.38f, 2.4f, 0.12f), 0.03f, 4, LandLook.Wood);
                    mb.Cone(new Vector3(0.38f, 2.4f, 0.12f), 0.07f, 0.35f, 4, LandLook.Stone);
                }), null, false);
            return f;
        }

        /// <summary>Shows a load: a bundle on the head (carriers, gatherers) or a basket on the back with up to 4 bundles (porters).</summary>
        public void SetLoad(bool has, Ware ware, int count, bool shipment)
        {
            string key = has ? "load/" + (int)ware + "/" + (shipment ? Mathf.Clamp(count, 1, 4) : 1) + "/" + (shipment ? "s" : "c") : null;
            if (key == _loadKey) return;
            _loadKey = key;
            if (_load != null) { Destroy(_load); _load = null; }
            if (key == null) return;
            var col = LandLook.WareColor(ware);
            int n = shipment ? Mathf.Clamp(count, 1, 4) : 1;
            var mesh = LandLook.Cached(key, mb =>
            {
                if (!shipment)
                {
                    mb.Box(new Vector3(0, 1.98f, 0), new Vector3(0.55f, 0.4f, 0.45f), col);
                    mb.Box(new Vector3(0, 1.98f, 0), new Vector3(0.6f, 0.08f, 0.5f), col * 0.6f);
                    return;
                }
                // Porter: a frame basket on the back, one bundle per unit, plus a staff.
                LandShapes.Basket(mb, new Vector3(0, 0.95f, -0.35f), 0.38f, 0.75f, new Color(0.55f, 0.42f, 0.24f));
                for (int i = 0; i < n; i++) mb.Box(new Vector3((i % 2 - 0.5f) * 0.28f, 1.72f + (i / 2) * 0.3f, -0.35f), new Vector3(0.3f, 0.28f, 0.34f), col * (0.95f + 0.05f * i));
                LandShapes.Beam(mb, new Vector3(0.42f, 0.0f, 0.25f), new Vector3(0.42f, 2.0f, 0.3f), 0.03f, 4, LandLook.DarkWood);
            });
            _load = LandLook.Part(Body, "Load", mesh, null, false);
        }

        public bool HasLoad => _load != null;

        /// <summary>Animates the body (call once per frame from the owner).</summary>
        public void Animate(float time)
        {
            float t = time * 1f + _phase;
            switch (Pose)
            {
                case FigurePose.Walk:
                    Body.localPosition = new Vector3(0, Mathf.Abs(Mathf.Sin(t * 7.5f)) * 0.07f, 0);
                    Body.localRotation = Quaternion.Euler(4f, 0, Mathf.Sin(t * 7.5f) * 3f);
                    break;
                case FigurePose.Work:
                    Body.localPosition = Vector3.zero;
                    Body.localRotation = Quaternion.Euler(18f + Mathf.Sin(t * 6f) * 18f, 0, 0);
                    break;
                case FigurePose.Slump:
                    Body.localPosition = new Vector3(0, -0.35f, 0);
                    Body.localRotation = Quaternion.Euler(28f, 0, 0);
                    break;
                default:
                    Body.localPosition = Vector3.zero;
                    Body.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(t * 1.3f) * 1.5f);
                    break;
            }
        }

        /// <summary>Figures closer to the viewer than this are not drawn (the camera would be inside them).</summary>
        public const float HideWithin = 1.1f;

        public void Place(Vector3 p, float yaw)
        {
            transform.SetPositionAndRotation(p, Quaternion.Euler(0, yaw, 0));
            // People never block the walker, so it can pass through one: do not draw a figure the eye is inside of.
            bool show = BoardSpace.Flat(LandViews.Eye - p) > HideWithin || Mathf.Abs(LandViews.Eye.y - p.y) > 4f;
            if (show != Body.gameObject.activeSelf) Body.gameObject.SetActive(show);
        }
    }
}
