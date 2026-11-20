using System.Collections.Generic;
using UnityEngine;
using World.Sim;
using World.Sim.Plague;

namespace World.Game
{
    /// <summary>
    /// HUB-HEALER (WORLD_UI_PLAN 2.1): a thatched healer's lodge with herb racks, a cot under an awning, a mortar and water
    /// bowls, a healer, an assistant sorting herbs and a runner who comes in on every outbreak. The plague's props (map table, warning post,
    /// rate stick, vessels, satchel, charm peg, draw sign) are <see cref="LodgeTable"/> (WU-3), inspection only. While the plague runs the healer says how many spirits of it are loose; after the plague ends
    /// (Historic) she is an ordinary healer and says the sickness is gone. Reads the sim only. Owner: WU-1.
    /// </summary>
    public sealed class HealersLodge : MonoBehaviour
    {
        public const float BodyRadius = 3.3f, RunSeconds = 14f, ReturnAfter = 6f;
        public static HealersLodge Instance { get; private set; }

        public HubFrame Frame { get; private set; }
        public Npc Healer { get; private set; }
        public Npc Assistant { get; private set; }
        public Npc Runner { get; private set; }
        public Vector3 Door { get; private set; }
        public Vector3 RunnerRest { get; private set; }
        public int RunnerTrips { get; private set; }
        public string LastHealerLine { get; private set; }
        /// <summary>The plague props in front of the lodge (WU-3).</summary>
        public LodgeTable Plague { get; private set; }

        public const string WalkSolidOwner = "hub.lodge";

        private WorldBootstrap _boot;
        private bool _running; private float _runStart, _returnAt;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Instance = null;

        public static HealersLodge Build(Transform parent, WorldBootstrap boot, HubPlace place)
        {
            var go = new GameObject("Healer's lodge");
            go.transform.SetParent(parent, false);
            var l = go.AddComponent<HealersLodge>();
            Instance = l;
            l._boot = boot;
            l.Frame = new HubFrame(place.Position, place.Yaw);
            l.BuildShell(go.transform);
            l.BuildPeople();
            l.Plague = LodgeTable.Build(l, boot);
            WalkGrid.RemoveStaticBlockers(WalkSolidOwner);
            WalkGrid.AddSolid(place.Position, BodyRadius * 0.95f, WalkSolidOwner);
            LandViews.AddClearing(place.Position.x, place.Position.z, place.Radius + 3f);
            boot.Clock.Ticked += l.OnTicked;
            return l;
        }

        private void OnDestroy() { if (_boot != null && _boot.Clock != null) _boot.Clock.Ticked -= OnTicked; if (Instance == this) Instance = null; WalkGrid.RemoveStaticBlockers(WalkSolidOwner); }

        private void BuildShell(Transform root)
        {
            var mb = new MeshBuilder();
            var clay = new Color(0.70f, 0.60f, 0.46f);
            // Round clay-and-wattle walls, a thick thatched cone roof, a dark doorway facing the hearth (+Z).
            mb.Cylinder(Vector3.zero, BodyRadius, BodyRadius - 0.1f, 2.3f, 12, clay);
            mb.Cylinder(Vector3.up * 2.3f, BodyRadius + 1.1f, BodyRadius + 0.8f, 0.25f, 12, HubArt.StrawDark);
            mb.Cone(Vector3.up * 2.5f, BodyRadius + 1.1f, 2.9f, 12, HubArt.Straw);
            mb.Cone(Vector3.up * 4.3f, BodyRadius * 0.45f, 1.4f, 8, HubArt.Straw * 0.9f);
            mb.Box(new Vector3(0, 0.95f, BodyRadius - 0.05f), new Vector3(1.3f, 1.9f, 0.3f), CampBuilder.Charcoal);
            CampBuilder.Beam(mb, new Vector3(-0.8f, 0, BodyRadius + 0.05f), new Vector3(-0.8f, 2.05f, BodyRadius + 0.05f), 0.12f, CampBuilder.Wood);
            CampBuilder.Beam(mb, new Vector3(0.8f, 0, BodyRadius + 0.05f), new Vector3(0.8f, 2.05f, BodyRadius + 0.05f), 0.12f, CampBuilder.Wood);
            CampBuilder.Beam(mb, new Vector3(-0.9f, 2.05f, BodyRadius + 0.05f), new Vector3(0.9f, 2.05f, BodyRadius + 0.05f), 0.12f, CampBuilder.Wood);
            // Two herb-drying racks against the walls, hung with bundles.
            foreach (float x in new[] { -4.9f, 4.9f })
            {
                CampBuilder.Beam(mb, new Vector3(x, 0, -0.6f), new Vector3(x, 2.1f, -0.6f), 0.1f, CampBuilder.Wood);
                CampBuilder.Beam(mb, new Vector3(x, 0, 2.4f), new Vector3(x, 2.1f, 2.4f), 0.1f, CampBuilder.Wood);
                CampBuilder.Beam(mb, new Vector3(x, 2.0f, -0.7f), new Vector3(x, 2.0f, 2.5f), 0.08f, CampBuilder.WoodLight);
                for (int i = 0; i < 6; i++)
                {
                    var p = new Vector3(x, 2.0f, -0.4f + i * 0.5f);
                    mb.Cone(p + Vector3.down * 0.7f, 0.04f, 0.7f, 5, i % 2 == 0 ? HubArt.Herb : HubArt.HerbDry);
                }
            }
            // The cot under a small hide awning to the right of the door: frame, folded bedding, pillow.
            var cot = new Vector3(3.6f, 0, 5.0f);
            mb.Box(cot + new Vector3(0, 0.38f, 0), new Vector3(0.95f, 0.1f, 2.1f), CampBuilder.WoodLight);
            for (int i = 0; i < 4; i++) mb.Box(cot + new Vector3((i % 2 - 0.5f) * 0.85f, 0.18f, (i / 2 - 0.5f) * 1.9f), new Vector3(0.09f, 0.36f, 0.09f), CampBuilder.Wood);
            mb.Box(cot + new Vector3(0, 0.5f, -0.2f), new Vector3(0.85f, 0.14f, 1.4f), CampBuilder.Hide);
            mb.Box(cot + new Vector3(0, 0.5f, 0.78f), new Vector3(0.5f, 0.14f, 0.35f), HubArt.Wool);
            for (int i = 0; i < 4; i++) CampBuilder.Beam(mb, cot + new Vector3((i % 2 - 0.5f) * 1.7f, 0, (i / 2 - 0.5f) * 2.6f), cot + new Vector3((i % 2 - 0.5f) * 1.7f, 2.0f + (i / 2) * 0.3f, (i / 2 - 0.5f) * 2.6f), 0.08f, CampBuilder.Wood);
            CampBuilder.Sheet(mb, cot + new Vector3(-0.85f, 2.0f, -1.3f), cot + new Vector3(-0.85f, 2.3f, 1.3f), cot + new Vector3(0.85f, 2.3f, 1.3f), cot + new Vector3(0.85f, 2.0f, -1.3f), CampBuilder.HideDark);
            // A stump table with a mortar and pestle, and two bowls of water, left of the door.
            var tab = new Vector3(-3.4f, 0, 4.9f);
            mb.Cylinder(tab, 0.55f, 0.5f, 0.75f, 8, CampBuilder.WoodLight);
            mb.Cylinder(tab + new Vector3(0.1f, 0.75f, 0), 0.22f, 0.3f, 0.2f, 8, CampBuilder.Stone);
            CampBuilder.Beam(mb, tab + new Vector3(0.1f, 0.85f, 0), tab + new Vector3(0.35f, 1.25f, 0.1f), 0.07f, CampBuilder.Stone);
            for (int i = 0; i < 2; i++) { mb.Cylinder(new Vector3(-4.9f + i * 0.5f, 0, 3.7f), 0.28f, 0.22f, 0.2f, 8, CampBuilder.HideDark); mb.Cylinder(new Vector3(-4.9f + i * 0.5f, 0.17f, 3.7f), 0.24f, 0.24f, 0.04f, 8, HubArt.Water); }
            // Folded bedding by the wall and a firewood stack for boiling.
            mb.Box(new Vector3(-2.2f, 0.18f, BodyRadius + 0.35f), new Vector3(0.9f, 0.3f, 0.6f), CampBuilder.Hide);
            mb.Box(new Vector3(-2.2f, 0.4f, BodyRadius + 0.35f), new Vector3(0.8f, 0.14f, 0.5f), HubArt.Wool);
            var body = HubArt.Make(root, "Lodge", Frame, mb);
            body.layer = 2;
            var tap = HubArt.Pick<LodgeTap>(root, "Lodge tap", Frame, new Vector3(0, 1.2f, 0), new Vector3(BodyRadius * 2f, 2.4f, BodyRadius * 2f));
            tap.Lodge = this;
            Door = Frame.W(0, BodyRadius + 2.3f);
            HubArt.Glow(root, Frame.W(0, BodyRadius + 1.6f) + Vector3.up * 2.2f, 11f, 1.4f);
            RunnerRest = Frame.W(6f, -9f);
        }

        private void BuildPeople()
        {
            Healer = Npc.Spawn("healer", "Healer", "Keeps the healer's lodge.", "Come in out of the cold.", new Color(0.28f, 0.52f, 0.50f), Frame.W(-1.4f, BodyRadius + 2.4f), Frame.Yaw);
            Healer.DialogueBuilder = BuildDialogue;
            Assistant = Npc.Spawn("healer_assistant", "Assistant", "Sorts herbs by the door.", "", new Color(0.46f, 0.55f, 0.36f), Frame.W(1.8f, BodyRadius + 2.0f), Frame.Yaw + 200f);
            Assistant.Doing = () => "Mint, yarrow, willow bark. Everything has its basket.";
            Assistant.ShowNamePlate = false;
            Runner = Npc.Spawn("healer_runner", "Runner", "Carries word of the sickness.", "", new Color(0.62f, 0.40f, 0.22f), RunnerRest, Frame.Yaw + 90f);
            Runner.Doing = () => "I run the island's news to the lodge. Not good news, lately.";
            Runner.ShowNamePlate = false;
        }

        private WorldState State => _boot != null && _boot.Sim != null ? _boot.Sim.State : null;

        /// <summary>True while the plague runs in a prehistoric game (the lodge's season).</summary>
        public bool PlagueRuns => State != null && State.Historic == null && PlagueApi.Active(State);

        /// <summary>What the healer says now (also her dialogue's line).</summary>
        public string HealerSays()
        {
            var s = State;
            if (s == null) return "Come in out of the cold.";
            if (s.Historic != null || (s.Plague != null && s.Plague.Ended)) return "The sickness is gone. I only tend coughs and cuts now.";
            if (s.Plague == null) return "All is quiet. I dry herbs and watch the island for sickness.";
            if (s.Plague.IslandFell) return "The island has fallen to the sickness. I tend those I can.";
            int n = PlagueApi.TotalDemons(s);
            return "The sickness is abroad: " + n + " of its spirits are loose on the island. Read my table, the post and the vessels. Banish them at the shrines.";
        }

        /// <summary>The exact numbers, said when she is asked (the post's ledger opens as well).</summary>
        public string OutbreakSays()
        {
            var p = State?.Plague;
            if (p == null) return "All is quiet.";
            if (p.IslandFell) return "Eight outbreaks. The island has fallen.";
            return p.Outbreaks + " of " + PlagueRules.OutbreaksToFall + " outbreaks so far. At eight the island falls. Each round " + PlagueApi.Rate(State) + " shrine" + (PlagueApi.Rate(State) > 1 ? "s are" : " is") + " struck.";
        }

        public string CharmSays()
        {
            var s = State; if (s?.Plague == null) return "The charm is cold.";
            long left = PlagueApi.BanishCooldownLeft(s);
            return left > 0 ? "Your charm still smoulders: ready in " + ((left + WorldSim.TicksPerSecond - 1) / WorldSim.TicksPerSecond) + " seconds." : "Your charm glows. It is ready.";
        }

        /// <summary>Everything the healer and the runner can say about the plague (tests: no word of what is left to come).</summary>
        public IEnumerable<string> AllSayings()
        {
            yield return HealerSays(); yield return OutbreakSays(); yield return CharmSays();
            yield return "An epidemic! The sickness is spreading fast."; yield return "Sickness has broken out on the island!";
        }

        private void BuildDialogue(Npc n)
        {
            n.Lines.Clear(); n.Options.Clear();
            LastHealerLine = HealerSays();
            n.Lines.Add(LastHealerLine);
            if (PlagueRuns && Plague != null)
            {
                n.Options.Add(DialogueOption.Do("How bad is it?", () => { SpeechBubbles.Say(Healer, OutbreakSays(), 6f); if (Plague.Post != null) WorldActions.Inspect(Plague.Post); }));
                n.Options.Add(DialogueOption.Do("Is my charm ready?", () => { SpeechBubbles.Say(Healer, CharmSays(), 5f); if (Plague.Charm != null) WorldActions.Inspect(Plague.Charm); }));
            }
            n.Options.Add(DialogueOption.Leave("Thank you"));
        }

        /// <summary>A tap on the lodge itself: the healer answers.</summary>
        public void Tap() { if (Healer != null) Healer.Interact(); }

        private void OnTicked(IReadOnlyList<SimEvent> events)
        {
            if (!PlagueRuns || _running || Runner == null) return;
            foreach (var ev in events)
                if (ev is Outbreak || ev is Epidemic) { SendRunner(ev is Epidemic ? "An epidemic! The sickness is spreading fast." : "Sickness has broken out on the island!"); break; }
        }

        /// <summary>The runner comes in from behind the lodge with news (each outbreak).</summary>
        public void SendRunner(string news)
        {
            if (_running || Runner == null) return;
            _running = true; _runStart = Time.time; _returnAt = -1f; RunnerTrips++;
            Runner.WalkSpeed = 8f;
            Runner.WalkTo(Door + Frame.Forward * 0.5f);
            _news = news;
        }
        private string _news;

        private void Update()
        {
            if (!_running || Runner == null) return;
            if (_returnAt < 0f)
            {
                if ((!Runner.IsWalking && BoardSpace.Flat(Runner.transform.position - Door) < 2.5f) || Time.time - _runStart > RunSeconds)
                {
                    Runner.StopWalking();
                    SpeechBubbles.Say(Runner, _news, 3.5f);
                    Runner.FaceTowards(Healer.transform.position);
                    Healer.FaceTowards(Runner.transform.position);
                    Healer.Actor?.PlayOneShot(ActorKeys.Surprise);
                    _returnAt = Time.time + ReturnAfter;
                }
            }
            else if (Time.time >= _returnAt)
            {
                Runner.WalkSpeed = Npc.StrollSpeed;
                Runner.WalkTo(RunnerRest);
                _running = false;
            }
        }
    }

    /// <summary>A tap on the lodge opens the healer's conversation.</summary>
    public sealed class LodgeTap : MonoBehaviour, IInteractable, IInspectable
    {
        public HealersLodge Lodge;
        public string Prompt => "Speak with the healer";
        public string Title => "Healer's lodge";
        public IEnumerable<string> Lines => new[] { "A thatched lodge. Herbs dry on the racks and a cot waits under the awning." };
        public void Interact() { if (Lodge != null) Lodge.Tap(); }
    }
}
