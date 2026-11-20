using System.Collections.Generic;
using UnityEngine;

namespace World.Game
{
    /// <summary>
    /// HUB-DUEL (WORLD_UI_PLAN 2.1): a fenced practice ring on the hub's level ground with two villagers sparring, a referee at the
    /// gate, a rack of staves and shields and two onlookers. Tapping the referee or the ring offers "Practice duel": the existing
    /// Ward encounter with Mira (<see cref="EncounterDirectory.WardMira"/>); Mira walks over from the expedition camp and walks
    /// back afterwards. The sparring is dressing only; the duel is the Ward contest. Owner: WU-1.
    /// </summary>
    public sealed class DuelRing : MonoBehaviour
    {
        public const float FenceRadius = 5.2f, StandOff = 3.2f, MiraWaitSeconds = 25f;
        public const string WalkSolidOwner = "hub.duelring";
        public static DuelRing Instance { get; private set; }

        public enum Errand { None, MiraComing, Dueling, MiraReturning }

        public HubFrame Frame { get; private set; }
        public Npc Referee { get; private set; }
        public Npc[] Sparrers { get; private set; }
        public Npc[] Onlookers { get; private set; }
        public Errand State { get; private set; }
        public Vector3 MiraStand { get; private set; }
        public int DuelsStarted { get; private set; }

        private Vector3 _miraHome; private float _miraYaw, _errandAt;
        private float _clock; private int _lunger;
        private readonly Vector3[] _base = new Vector3[2];

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Instance = null;

        public static DuelRing Build(Transform parent, HubPlace place, CampLayout layout)
        {
            var go = new GameObject("Duel ring");
            go.transform.SetParent(parent, false);
            var r = go.AddComponent<DuelRing>();
            Instance = r;
            r.Frame = new HubFrame(place.Position, place.Yaw);
            r._miraHome = layout.MiraSpot; r._miraYaw = layout.MiraYaw;
            r.BuildShell(go.transform);
            r.BuildPeople();
            WalkGrid.RemoveStaticBlockers(WalkSolidOwner);
            WalkGrid.AddSolid(place.Position, FenceRadius - 0.4f, WalkSolidOwner);
            LandViews.AddClearing(place.Position.x, place.Position.z, place.Radius + 3f);
            return r;
        }

        private void OnDestroy() { if (Instance == this) Instance = null; WalkGrid.RemoveStaticBlockers(WalkSolidOwner); }

        private void BuildShell(Transform root)
        {
            var mb = new MeshBuilder();
            // Trampled sand floor and a fence of posts and two rails with a gate gap facing the hearth.
            const int seg = 24;
            for (int i = 0; i < seg; i++)
            {
                float a0 = i * Mathf.PI * 2f / seg, a1 = (i + 1) * Mathf.PI * 2f / seg;
                Vector3 P(float a, float r) => new Vector3(Mathf.Sin(a) * r, 0.03f, Mathf.Cos(a) * r);
                var c = Color.Lerp(HubArt.Sand, HubArt.Sand * 0.8f, (i % 3) * 0.2f);
                mb.Quad(P(a0, 0), P(a1, 0), P(a1, FenceRadius - 0.3f), P(a0, FenceRadius - 0.3f), c);
            }
            HubArt.FenceRing(mb, Vector3.zero, FenceRadius, 20, 0f, 40f, 1.25f, CampBuilder.Wood, CampBuilder.WoodLight);
            // Two practice posts with straw heads inside the ring.
            foreach (float x in new[] { -3.3f, 3.3f })
            {
                mb.Cylinder(new Vector3(x, 0, -2.2f), 0.18f, 0.16f, 1.7f, 6, CampBuilder.Wood);
                mb.Cylinder(new Vector3(x, 1.5f, -2.2f), 0.3f, 0.26f, 0.35f, 7, HubArt.Straw);
            }
            // A rack of wooden staves and round bucklers outside the fence, to the left of the gate.
            var rack = new Vector3(-6.3f, 0, 3.4f);
            CampBuilder.Beam(mb, rack + new Vector3(-1.2f, 0, 0), rack + new Vector3(-1.2f, 1.4f, 0), 0.1f, CampBuilder.Wood);
            CampBuilder.Beam(mb, rack + new Vector3(1.2f, 0, 0), rack + new Vector3(1.2f, 1.4f, 0), 0.1f, CampBuilder.Wood);
            CampBuilder.Beam(mb, rack + new Vector3(-1.3f, 1.3f, 0), rack + new Vector3(1.3f, 1.3f, 0), 0.08f, CampBuilder.WoodLight);
            CampBuilder.Beam(mb, rack + new Vector3(-1.3f, 0.5f, 0), rack + new Vector3(1.3f, 0.5f, 0), 0.08f, CampBuilder.WoodLight);
            for (int i = 0; i < 5; i++) CampBuilder.Beam(mb, rack + new Vector3(-1f + i * 0.5f, 0.1f, 0.15f), rack + new Vector3(-1f + i * 0.5f + 0.15f, 1.9f, 0.2f), 0.07f, CampBuilder.WoodLight);
            for (int i = 0; i < 3; i++)
            {
                mb.Cylinder(rack + new Vector3(-0.8f + i * 0.8f, 0.55f, -0.18f), 0.36f, 0.36f, 0.05f, 10, i % 2 == 0 ? CampBuilder.Ochre : CampBuilder.Clay);
            }
            var body = HubArt.Make(root, "Ring", Frame, mb);
            body.layer = 2;
            var tap = HubArt.Pick<RingTap>(root, "Ring tap", Frame, new Vector3(0, 0.3f, 0), new Vector3(FenceRadius * 2f, 0.6f, FenceRadius * 2f));
            tap.Ring = this;
            HubArt.Glow(root, Frame.W(0, FenceRadius + 0.5f) + Vector3.up * 2.6f, 12f, 1.5f);
        }

        private void BuildPeople()
        {
            Sparrers = new Npc[2];
            Color[] tun = { new Color(0.62f, 0.30f, 0.26f), new Color(0.28f, 0.40f, 0.60f) };
            for (int i = 0; i < 2; i++)
            {
                _base[i] = Frame.W(i == 0 ? -1.6f : 1.6f, -0.4f);
                var n = Npc.Spawn("ring_sparrer_" + i, "Sparring villager", "Practises with a wooden staff.", "", tun[i], _base[i], Frame.Yaw + (i == 0 ? 90f : -90f));
                n.Directed = true; n.ShowNamePlate = false;
                n.Doing = () => "Staff up! Mira's Ward duel is another kind of fight, but the footwork helps.";
                Sparrers[i] = n;
            }
            Referee = Npc.Spawn("ring_referee", "Referee", "Judges the practice ring.", "Care for a practice duel?", new Color(0.78f, 0.74f, 0.55f), Frame.W(1.9f, FenceRadius + 1.4f), Frame.Yaw + 190f);
            Referee.DialogueBuilder = BuildDialogue;
            Onlookers = new Npc[2];
            for (int i = 0; i < 2; i++)
            {
                var o = Npc.Spawn("ring_onlooker_" + i, "Onlooker", "Watches the sparring.", "", new Color(0.55f, 0.45f, 0.34f), Frame.W(-3.6f + i * 7.2f, FenceRadius + 1.1f), Frame.Yaw + 180f + (i == 0 ? 20f : -20f));
                o.ShowNamePlate = false; o.Directed = true;
                o.Doing = () => i == 0 ? "Did you see that feint? Lovely." : "Hold your guard! Oh, he is hopeless.";
                Onlookers[i] = o;
            }
        }

        private void BuildDialogue(Npc n)
        {
            n.Lines.Clear(); n.Options.Clear();
            n.Lines.Add("They keep the ring warm. Would you like a practice duel? Mira will come over.");
            n.Options.Add(DialogueOption.Do("Practice duel", () => StartDuel()));
            n.Options.Add(DialogueOption.Leave("Not now"));
        }

        /// <summary>A tap on the ring: the referee offers the duel.</summary>
        public void Tap() { if (Referee != null) Referee.Interact(); }

        /// <summary>The "Practice duel": Mira walks over to the ring's gate, then the existing Ward encounter starts with her.</summary>
        public bool StartDuel()
        {
            var mira = Npc.Find(NpcIds.Mira);
            if (mira == null || State != Errand.None) return false;
            if (EncounterHost.Instance != null && EncounterHost.Instance.IsRunning) return false;
            State = Errand.MiraComing; _errandAt = Time.time + MiraWaitSeconds;
            MiraStand = Frame.W(0f, FenceRadius + StandOff);
            mira.Directed = true;
            mira.WalkSpeed = 6f;
            mira.WalkTo(MiraStand);
            SpeechBubbles.Say(mira, "On my way. Do not peek at my Ward.", 2.5f);
            return true;
        }

        private void Update()
        {
            UpdateSparring();
            var mira = Npc.Find(NpcIds.Mira);
            if (mira == null) { State = Errand.None; return; }
            switch (State)
            {
                case Errand.MiraComing:
                    if ((!mira.IsWalking && BoardSpace.Flat(mira.transform.position - MiraStand) < 1.5f) || Time.time >= _errandAt)
                    {
                        mira.StopWalking();
                        mira.PlaceAt(MiraStand, Frame.Yaw);
                        mira.Directed = false; mira.WalkSpeed = Npc.StrollSpeed;
                        DuelsStarted++;
                        State = Errand.Dueling; _errandAt = Time.time + 3f;
                        if (!EncounterDirectory.TryStart(EncounterDirectory.WardMira, mira)) State = Errand.MiraReturning;
                    }
                    break;
                case Errand.Dueling:
                    if (Time.time < _errandAt) break;
                    if (EncounterHost.Instance == null || !EncounterHost.Instance.IsRunning) { State = Errand.MiraReturning; mira.WalkTo(_miraHome); _errandAt = Time.time + MiraWaitSeconds; }
                    break;
                case Errand.MiraReturning:
                    if ((!mira.IsWalking && BoardSpace.Flat(mira.transform.position - _miraHome) < 1.5f) || Time.time >= _errandAt)
                    {
                        mira.StopWalking();
                        mira.PlaceAt(_miraHome, _miraYaw);
                        State = Errand.None;
                    }
                    break;
            }
        }

        private void UpdateSparring()
        {
            if (Sparrers == null) return;
            _clock += Time.deltaTime;
            // Every 1.6 s one of the two lunges at the other (0.8 m forward and back), the other gives ground.
            int phase = Mathf.FloorToInt(_clock / 1.6f);
            float u = Mathf.Repeat(_clock, 1.6f) / 1.6f;
            float k = Mathf.Sin(Mathf.Clamp01(u * 2f) * Mathf.PI) * (u < 0.5f ? 1f : 0f);
            if (phase != _lunger) { _lunger = phase; Sparrers[phase % 2].Actor?.PlayOneShot(ActorKeys.Cheer); }
            for (int i = 0; i < 2; i++)
            {
                var n = Sparrers[i];
                if (n == null) continue;
                var toOther = _base[1 - i] - _base[i]; toOther.y = 0; toOther.Normalize();
                float push = (phase % 2 == i ? 1f : -0.6f) * k * 0.8f;
                var p = _base[i] + toOther * push; p.y = PlayerWalker.GroundHeight(p);
                n.transform.position = p;
                n.FaceTowards(_base[1 - i]);
            }
        }
    }

    /// <summary>A tap on the ring: the referee offers the practice duel.</summary>
    public sealed class RingTap : MonoBehaviour, IInteractable, IInspectable
    {
        public DuelRing Ring;
        public string Prompt => "Practice duel";
        public string Title => "The practice ring";
        public IEnumerable<string> Lines => new[] { "A fenced ring where two villagers spar. The referee can set up a practice duel." };
        public void Interact() { if (Ring != null) Ring.Tap(); }
    }
}
