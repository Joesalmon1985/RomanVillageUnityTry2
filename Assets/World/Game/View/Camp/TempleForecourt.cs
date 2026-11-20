using System.Collections.Generic;
using UnityEngine;
using World.Sim;

namespace World.Game
{
    /// <summary>
    /// HUB-TEMPLE (WORLD_UI_PLAN 2.1): the player's temple has a level stone forecourt in front of its doorway with a lamb pen,
    /// offering baskets, bundled timber, two builders carrying planks and a temple keeper who greets the visitor and walks to the
    /// altar. Tapping the keeper, the altar or the doorway all take the same path (<c>GodBoardDirector.TrySacrifice</c>, the
    /// <c>Sacrifice</c> command). In Historic the temple falls (HI-2): the ruin stays and the keeper says the age has turned.
    /// Owner: WU-1. Original primitive art, placeholder actors.
    /// </summary>
    public sealed class TempleForecourt : MonoBehaviour
    {
        public const float GreetDistance = 14f, SettleDistance = 26f, OfferWaitSeconds = 7f;
        public static TempleForecourt Instance { get; private set; }

        public HubFrame Frame { get; private set; }
        public Npc Keeper { get; private set; }
        public Npc[] Builders { get; private set; }
        public Vector3 AltarApproach { get; private set; }
        public Vector3 KeeperHome { get; private set; }
        public bool RuinShown { get; private set; }
        public bool Offering { get; private set; }
        /// <summary>WU-1b: the keeper's two-tap entry (the lamb pen).</summary>
        public HubEntryAction Offer { get; private set; }
        public bool Greeted { get; private set; }
        public string LastKeeperLine { get; private set; }
        public Transform Ruin { get; private set; }

        private WorldBootstrap _boot;
        private Vector3 _stack, _flank;
        private float _offerAt, _nextBuilder;
        private readonly List<Transform> _planks = new List<Transform>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Instance = null;

        private WorldState State => _boot != null && _boot.Sim != null ? _boot.Sim.State : null;

        public static TempleForecourt Build(Transform parent, WorldBootstrap boot, HubPlace place)
        {
            var go = new GameObject("Temple forecourt");
            go.transform.SetParent(parent, false);
            var f = go.AddComponent<TempleForecourt>();
            Instance = f;
            f._boot = boot;
            f.Frame = new HubFrame(place.Position, place.Yaw);
            f.BuildScenery(go.transform);
            f.BuildPeople();
            return f;
        }

        private void BuildScenery(Transform root)
        {
            const float R = TempleView.Radius;
            var mb = new MeshBuilder();
            // The paved forecourt: a wide stone slab with a shallow step up to the temple's doorway.
            mb.Box(new Vector3(0, 0.05f, R + 3.6f), new Vector3(9.5f, 0.1f, 7.4f), HubArt.Slab);
            mb.Box(new Vector3(0, 0.14f, R + 0.6f), new Vector3(4.2f, 0.18f, 1.2f), HubArt.Slab * 0.9f);
            // Lamb pen to the right: a fenced ring with three lambs and a trough.
            var pen = new Vector3(6.2f, 0, R + 2.6f);
            HubArt.FenceRing(mb, pen, 2.1f, 12, 200f, 40f, 1.0f, CampBuilder.Wood, CampBuilder.WoodLight);
            for (int i = 0; i < 3; i++) HubArt.Lamb(mb, pen + new Vector3(Mathf.Sin(i * 2.3f) * 0.9f, 0, Mathf.Cos(i * 2.3f) * 0.8f), i * 110f);
            mb.Box(pen + new Vector3(0, 0.15f, -1.2f), new Vector3(1.1f, 0.3f, 0.35f), CampBuilder.WoodLight);
            // Offering baskets to the left: sheaves, bricks, logs, fleece; a farmer's trading table beside them.
            Color[] c = { HubArt.Grain, HubArt.Brick, CampBuilder.Wood, HubArt.Wool };
            for (int i = 0; i < 4; i++) HubArt.Basket(mb, new Vector3(-5.2f - (i % 2) * 1.3f, 0, R + 1.8f + (i / 2) * 1.4f), c[i], i);
            mb.Box(new Vector3(-6.6f, 0.8f, R + 4.8f), new Vector3(2.2f, 0.12f, 0.9f), CampBuilder.WoodLight);
            for (int i = 0; i < 4; i++) mb.Box(new Vector3(-7.5f + (i % 2) * 1.8f, 0.4f, R + 4.5f + (i / 2) * 0.6f), new Vector3(0.1f, 0.8f, 0.1f), CampBuilder.Wood);
            // Bundled timber, and a half-built scaffold on the flank where the builders work.
            _stack = Frame.W(-7.5f, R + 7.5f);
            for (int row = 0; row < 3; row++)
                for (int i = 0; i < 4 - row; i++)
                    CampBuilder.Beam(mb, new Vector3(-9f + i * 0.34f + row * 0.17f, 0.2f + row * 0.3f, R + 7f), new Vector3(-9f + i * 0.34f + row * 0.17f, 0.2f + row * 0.3f, R + 9.6f), 0.28f, Color.Lerp(CampBuilder.Wood, CampBuilder.WoodLight, 0.2f * i));
            _flank = Frame.W(-R - 2.4f, 0.5f);
            CampBuilder.Beam(mb, new Vector3(-R - 1.6f, 0, 0.5f), new Vector3(-R - 1.6f, 3.2f, 0.5f), 0.2f, CampBuilder.Wood);
            CampBuilder.Beam(mb, new Vector3(-R - 1.6f, 0, 3.2f), new Vector3(-R - 1.6f, 3.2f, 3.2f), 0.2f, CampBuilder.Wood);
            CampBuilder.Beam(mb, new Vector3(-R - 1.6f, 3.0f, 0.5f), new Vector3(-R - 1.6f, 3.0f, 3.2f), 0.16f, CampBuilder.WoodLight);
            // Braziers either side of the doorway so the forecourt reads at night.
            foreach (float x in new[] { -2.6f, 2.6f })
            {
                mb.Cylinder(new Vector3(x, 0, R + 1.2f), 0.2f, 0.12f, 1.0f, 6, CampBuilder.Wood);
                mb.Cylinder(new Vector3(x, 1.0f, R + 1.2f), 0.36f, 0.28f, 0.2f, 8, CampBuilder.Charcoal);
                mb.Cone(new Vector3(x, 1.2f, R + 1.2f), 0.25f, 0.5f, 6, new Color(1f, 0.55f, 0.15f));
            }
            var body = HubArt.Make(root, "Forecourt", Frame, mb);
            body.layer = 2;
            // Hit target: a low slab over the whole forecourt (low, so a tap on a person or prop standing on it reaches them first).
            // WU-1b: only the doorway step (not the whole forecourt) is the hit target, so a ground tap on the paving walks there.
            var door = HubArt.Pick<TempleDoorway>(root, "Doorway", Frame, new Vector3(0, 0.25f, R + 1.0f), new Vector3(3.6f, 0.5f, 1.8f));
            door.Forecourt = this;
            // WU-1b: the lamb pen is the keeper's two-tap entry ("Take a lamb to the altar?"): the same call as "Make the offering".
            Offer = HubEntryAction.Make(root, "Lamb pen offering", Frame.W(6.2f, R + 0.9f), Frame.Yaw, new Vector2(1.6f, 1.0f),
                "The lamb pen", "Take a lamb to the altar?", "Make the offering",
                () => new[] { "The keeper brings a lamb to the altar for the offering." },
                () =>
                {
                    var st = State;
                    if (st != null && st.Historic != null) return WorldActionCheck.Disable("The age has turned. The altar is silent.");
                    var d = GodBoardDirector.Instance;
                    if (d == null || d.OwnTemple == null) return WorldActionCheck.Disable("There is no temple here to keep.");
                    if (Offering) return WorldActionCheck.Refuse("The keeper is already on the way.");
                    return d.CheckSacrifice();
                },
                () => { if (Offering || Keeper == null) return false; BeginOffering(); return true; });
            Offer.Reach = 10f;
            for (int i = 0; i < 2; i++)
            {
                var plank = new MeshBuilder();
                CampBuilder.Beam(plank, new Vector3(0, 0, -0.9f), new Vector3(0, 0, 0.9f), 0.16f, CampBuilder.WoodLight);
                var pg = plank.ToObject("Carried plank " + i, transform, MaterialFactory.CreateVertexColored(Color.white, "Plank"));
                pg.layer = 2; pg.SetActive(false);
                _planks.Add(pg.transform);
            }
            AltarApproach = Frame.W(0, R + 1.2f);
            HubArt.Glow(root, Frame.W(0, R + 2.5f) + Vector3.up * 2.4f, 13f, 1.4f);
            LandViews.AddClearing(Frame.O.x, Frame.O.z, 13f);
        }

        private void BuildPeople()
        {
            const float R = TempleView.Radius;
            KeeperHome = Frame.W(1.2f, R + 3.6f);
            Keeper = Npc.Spawn("temple_keeper", "Temple keeper", "Keeps the temple and its lambs.", "Welcome. The altar is ready for a lamb.", new Color(0.62f, 0.55f, 0.72f), KeeperHome, Frame.Yaw + 180f);
            Keeper.DialogueBuilder = BuildKeeperDialogue;
            Builders = new Npc[2];
            for (int i = 0; i < 2; i++)
            {
                var b = Npc.Spawn("temple_builder_" + i, "Builder", "Carries timber for the temple.", "", new Color(0.50f, 0.38f, 0.26f), Frame.W(-7f + i * 1.4f, R + 6.2f), Frame.Yaw + 90f);
                b.Doing = () => "Timber for the temple's flank. The scaffold has to stand before the rains.";
                b.ShowNamePlate = false;
                Builders[i] = b;
            }
            _nextBuilder = Time.time + 1f;
        }

        private void BuildKeeperDialogue(Npc n)
        {
            n.Lines.Clear(); n.Options.Clear();
            var s = State;
            if (s != null && s.Historic != null)
            {
                n.Lines.Add("The age has turned. The altar is silent and the old ways are ash.");
                n.Options.Add(DialogueOption.Leave("I understand"));
                return;
            }
            var d = GodBoardDirector.Instance;
            if (d == null || d.OwnTemple == null) { n.Lines.Add("There is no temple here to keep."); n.Options.Add(DialogueOption.Leave("I see")); return; }
            n.Lines.Add("The lamb is ready. Shall I take you to the altar?");
            n.Options.Add(DialogueOption.Do("Make the offering", BeginOffering));
            n.Options.Add(DialogueOption.Leave("Not now"));
        }

        /// <summary>The keeper walks to the altar, then the offering goes through the normal <c>Sacrifice</c> path.</summary>
        public void BeginOffering()
        {
            if (Offering || Keeper == null) return;
            Offering = true; _offerAt = Time.time + OfferWaitSeconds;
            Keeper.WalkTo(AltarApproach);
            LastKeeperLine = "This way.";
            SpeechBubbles.Say(Keeper, LastKeeperLine, 2.5f);
        }

        /// <summary>Tap on the doorway or the forecourt (the altar's own tap does the same).</summary>
        public void TapDoorway()
        {
            if (State != null && State.Historic != null) { if (Keeper != null) Keeper.Interact(); return; }
            var d = GodBoardDirector.Instance;
            if (d == null || d.OwnTemple == null) return;
            var altar = d.OwnTemple.GetComponentInChildren<TempleAltar>();
            if (altar != null) WorldActions.Tap(altar); // the same two-tap path as the altar: preview, then confirm
        }

        private void Update()
        {
            var s = State;
            if (s != null && s.Historic != null && !RuinShown)
            {
                var d = GodBoardDirector.Instance;
                var t = d != null ? d.OwnTemple : null;
                if (t == null || t.Gone) ShowRuin();
            }
            UpdateKeeper();
            UpdateBuilders();
        }

        private void UpdateKeeper()
        {
            if (Keeper == null) return;
            var w = PlayerWalker.Instance;
            if (Offering)
            {
                if ((!Keeper.IsWalking && BoardSpace.Flat(Keeper.transform.position - AltarApproach) < 1.5f) || Time.time >= _offerAt)
                {
                    Offering = false;
                    Keeper.StopWalking();
                    var d = GodBoardDirector.Instance;
                    var t = d != null ? d.OwnTemple : null;
                    if (t != null) Keeper.FaceTowards(t.transform.position);
                    Keeper.Actor?.PlayOneShot(ActorKeys.Greet);
                    if (t != null) d.TrySacrifice(true);
                }
                return;
            }
            if (w == null || (State != null && State.Historic != null) || DialoguePanel.IsOpen) return;
            float dist = BoardSpace.Flat(w.Position - Frame.O);
            if (!Greeted && dist < GreetDistance && !Keeper.IsWalking)
            {
                Greeted = true;
                LastKeeperLine = "Welcome. The altar is ready for a lamb.";
                SpeechBubbles.Say(Keeper, LastKeeperLine, 3f);
                Keeper.Actor?.PlayOneShot(ActorKeys.Greet);
                Keeper.WalkTo(AltarApproach);
            }
            else if (Greeted && dist > SettleDistance && !Keeper.IsWalking && BoardSpace.Flat(Keeper.transform.position - KeeperHome) > 1f)
            {
                Greeted = false;
                Keeper.WalkTo(KeeperHome);
            }
        }

        private void UpdateBuilders()
        {
            if (Builders == null) return;
            for (int i = 0; i < Builders.Length; i++)
            {
                var b = Builders[i];
                if (b == null) continue;
                bool towardsFlank = b.Goal.HasValue && BoardSpace.Flat(b.Goal.Value - _flank) < 3f;
                _planks[i].gameObject.SetActive(b.IsWalking && towardsFlank);
                if (b.IsWalking)
                {
                    float yaw = b.transform.eulerAngles.y;
                    _planks[i].SetPositionAndRotation(b.transform.position + Vector3.up * 1.25f + BoardSpace.DirOf(yaw) * 0.1f, Quaternion.Euler(0, yaw, 0));
                    continue;
                }
                if (Time.time < _nextBuilder) continue;
                _nextBuilder = Time.time + 1.7f;
                var next = BoardSpace.Flat(b.transform.position - _stack) < BoardSpace.Flat(b.transform.position - _flank) ? _flank : _stack;
                b.WalkTo(next + Vector3.right * (i * 1.3f - 0.65f));
            }
        }

        /// <summary>The temple has fallen (Historic): a low ruin where it stood, still tappable.</summary>
        public void ShowRuin()
        {
            if (RuinShown) return;
            RuinShown = true;
            var mb = new MeshBuilder();
            var stone = new Color(0.60f, 0.57f, 0.52f);
            mb.Cylinder(Vector3.zero, TempleView.Radius, TempleView.Radius - 0.5f, 0.35f, 12, stone * 0.8f);
            var rnd = new PropRandom(77);
            for (int i = 0; i < 6; i++)
            {
                float a = i * Mathf.PI / 3f; var p = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * (TempleView.Radius - 1.7f);
                float h = i % 3 == 0 ? 2.2f : 0.7f + rnd.Next();
                mb.Cylinder(p + Vector3.up * 0.3f, 0.38f, 0.3f, h, 7, stone);
            }
            for (int i = 0; i < 9; i++)
            {
                float a = rnd.Next() * 6.28f, r = 1f + rnd.Next() * 3.2f;
                mb.Rock(new Vector3(Mathf.Cos(a) * r, 0.3f, Mathf.Sin(a) * r), 0.5f + rnd.Next() * 0.7f, stone * (0.8f + 0.3f * rnd.Next()), rnd.Next);
            }
            mb.Box(new Vector3(0, 0.6f, 0), new Vector3(2.4f, 0.9f, 1.4f), new Color(0.36f, 0.34f, 0.31f)); // the altar block, cracked and bare
            var go = HubArt.Make(transform, "Temple ruin", Frame, mb);
            go.layer = 2;
            LandLook.PickBox(go, new Vector3(0, 1.2f, 0), new Vector3(TempleView.Radius * 1.6f, 2.4f, TempleView.Radius * 1.6f));
            var info = go.AddComponent<CampPropInfo>();
            info.Kind = "ruin"; info.TitleText = "The fallen temple"; info.Line = "The temple fell when the age turned. Nothing is offered here now.";
            Ruin = go.transform;
        }
    }

    /// <summary>The temple's doorway and forecourt: tap it to make the offering, the same path as the altar (E close by works too).</summary>
    public sealed class TempleDoorway : MonoBehaviour, IInteractable, IInspectable
    {
        public TempleForecourt Forecourt;
        public string Prompt => "Make the offering";
        public string Title => "The temple doorway";
        public IEnumerable<string> Lines => new[] { "The doorway to the altar. A lamb is waiting in the pen." };
        public void Interact() { if (Forecourt != null) Forecourt.TapDoorway(); }
    }
}
