using System.Collections.Generic;
using UnityEngine;
using World.Sim;
using World.Sim.Economy;
using Terrain = World.Sim.Terrain;

namespace World.Game
{
    /// <summary>
    /// First-person access to the economy: Quartermaster Bram gives the commission, shows its progress (buildings x/6,
    /// planks, bricks), points at the next thing to do with a beacon, and acknowledges completion; the town bell ends
    /// the round (refusals come back as the sim's sentence); the HUD objective follows the commission.
    /// </summary>
    public sealed class EconomyFirstPerson : MonoBehaviour
    {
        public static EconomyFirstPerson Instance { get; private set; }
        /// <summary>True once the CommissionCompleted event was seen (banner shown, Bram cheered).</summary>
        public bool CompletionSeen { get; private set; }
        /// <summary>True once Bram has acknowledged the finished commission in a dialogue.</summary>
        public bool QuartermasterAcknowledged { get; private set; }
        public bool OwnsObjective { get; private set; }
        public string CurrentObjective { get; private set; }
        /// <summary>The last "next step" Bram gave (sentence), for tests.</summary>
        public string LastHint { get; private set; }
        public Beacon LastBeacon { get; private set; }

        private WorldBootstrap _boot;
        private Npc _qm;
        private float _objectiveAt;

        public void Bind(WorldBootstrap boot)
        {
            Instance = this;
            _boot = boot;
            _qm = Npc.Find(NpcIds.Quartermaster);
            if (_qm != null)
            {
                _qm.Greeting = "We need planks and bricks. Roads first, then we build. Ask me about the commission.";
                _qm.DialogueBuilder = BuildQuartermasterDialogue;
            }
            TownBell.Rung += OnBell;
            boot.Clock.Ticked += OnTicked;
            GodCommands.Submitted += OnSubmitted;
            if (boot.Sim.State.Economy.Commission.Complete) CompletionSeen = true;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            TownBell.Rung -= OnBell;
            GodCommands.Submitted -= OnSubmitted;
            if (_boot != null && _boot.Clock != null) _boot.Clock.Ticked -= OnTicked;
        }

        /// <summary>The bell ends the round, exactly like the God View End Round button.</summary>
        private void OnBell() => GodCommands.EndRound();

        private void OnSubmitted(Command c, CommandResult r) { if (r.Ok && !(c is EndRound)) TakeObjective(); }

        private void OnTicked(IReadOnlyList<SimEvent> events)
        {
            foreach (var e in events)
                if (e is CommissionCompleted) { OnCompleted(); break; }
        }

        private void OnCompleted()
        {
            CompletionSeen = true;
            if (_qm != null)
            {
                _qm.Greeting = "The commission is complete! Come and see me.";
                if (_qm.Actor != null) _qm.Actor.PlayOneShot(ActorKeys.Cheer);
                if (LastBeacon != null) Destroy(LastBeacon.gameObject);
                LastBeacon = Beacon.Show(_qm.transform.position, "Quartermaster Bram: commission complete", 60f, new Color(0.45f, 1f, 0.45f));
            }
            TakeObjective();
            RefreshObjective(true);
        }

        // ---------------- quartermaster ----------------

        private void BuildQuartermasterDialogue(Npc npc)
        {
            var s = _boot.Sim.State;
            EconomyText.Commission(s, out int built, out int planks, out int bricks, out bool complete);
            var t = s.Economy.Tuning;
            npc.Lines.Clear();
            npc.Options.Clear();
            TakeObjective();
            if (complete)
            {
                QuartermasterAcknowledged = true;
                npc.Lines.Add("The commission is complete! The forester, woodcutter, sawmill, clay pit, charcoal kiln and brick kiln are working on the roads.");
                npc.Lines.Add("The storehouse holds " + planks + " planks and " + bricks + " bricks. Well done. The village can grow from here.");
                npc.Lines.Add("Carriers walk the roads between flags. No road, no delivery.");
                npc.Options.Add(DialogueOption.Do("Open the building plans", () => EconomyGodTools.OpenBuildPlans()));
                npc.Options.Add(DialogueOption.Leave("Thanks, Bram"));
                return;
            }
            var names = new List<string>();
            foreach (var bt in EconomyLook.CommissionTypes)
                names.Add((EconomyText.CommissionBuildingReady(s, bt) ? "<color=#8CE08C>" : "") + EconomyApi.DescribeType(s, bt).Name.ToLowerInvariant()
                          + (EconomyText.CommissionBuildingReady(s, bt) ? "</color>" : ""));
            var stock = new int[Wares.Count];
            stock[(int)Ware.Planks] = t.CommissionPlanks;
            stock[(int)Ware.Bricks] = t.CommissionBricks;
            npc.Lines.Add("The commission: a working " + EconomyText.JoinAnd(names) + ", all on the road, and " + EconomyApi.CostText(stock) + " in the storehouse.");
            npc.Lines.Add("<b>Progress</b>: buildings " + built + "/" + EconomyLook.CommissionTypes.Length + ",  planks " + planks + "/" + t.CommissionPlanks + ",  bricks " + bricks + "/" + t.CommissionBricks + ".");
            var step = NextStep(s);
            LastHint = step.Sentence;
            npc.Lines.Add("Next: " + step.Sentence);
            var target = step;
            npc.Options.Add(DialogueOption.Do("Show me where", () => ShowTarget(target)));
            npc.Options.Add(DialogueOption.Do("Open the building plans", () => EconomyGodTools.OpenBuildPlans(target.Type)));
            npc.Options.Add(DialogueOption.Leave());
        }

        /// <summary>What Bram points at next.</summary>
        public struct Step
        {
            public string Sentence;
            public Vector3 Where;
            public string Caption;
            public BuildingType? Type;
        }

        /// <summary>The next thing to do for the commission, from the state.</summary>
        public Step NextStep(WorldState s)
        {
            // 1. A commission building not planned yet: where it can go.
            foreach (var t in EconomyLook.CommissionTypes)
            {
                if (EconomyApi.FindBuilding(s, t) >= 0) continue;
                var view = EconomyApi.DescribeType(s, t);
                System.Func<int, bool> filter = null;
                var def = EconomyApi.Def(s, t);
                if (def.NeedsTerrain)
                {
                    int hex = CommissionDriver.HomeHex(s, def.Terrain);
                    if (hex >= 0) filter = CommissionDriver.Touches(s, hex);
                }
                int v = CommissionDriver.ChooseVertex(s, t, filter);
                var where = v >= 0 ? BoardSpace.Vertex(v) : _boot.Layout.Home;
                string terrain = def.NeedsTerrain ? " by the " + def.Terrain.ToString().ToLowerInvariant() : "";
                return new Step
                {
                    Sentence = "plan a " + view.Name.ToLowerInvariant() + terrain + " (costs " + view.CostText + ") and connect it by road. Use the Build tool in God View (Tab, then B).",
                    Where = where, Caption = view.Name + " goes here", Type = t,
                };
            }
            // 2. A planned one that is stalled: the sim's sentence.
            foreach (var t in EconomyLook.CommissionTypes)
            {
                int id = EconomyApi.FindBuilding(s, t);
                var b = EconomyApi.GetBuilding(s, id);
                if (b == null || (b.Reason == null && !b.Blocked)) continue;
                var bv = EconomyViews.Instance != null ? EconomyViews.Instance.Building(id) : null;
                return new Step
                {
                    Sentence = EconomyApi.WaitingFor(s, id).Sentence,
                    Where = bv != null ? bv.transform.position : BoardSpace.Vertex(b.Vertex),
                    Caption = EconomyApi.Describe(s, id).Title, Type = t,
                };
            }
            // 3. Still building.
            foreach (var t in EconomyLook.CommissionTypes)
            {
                int id = EconomyApi.FindBuilding(s, t);
                var b = EconomyApi.GetBuilding(s, id);
                if (b == null || b.State == BuildingState.Active) continue;
                return new Step { Sentence = EconomyApi.WaitingFor(s, id).Sentence + " Carriers are on it.", Where = BoardSpace.Vertex(b.Vertex), Caption = EconomyApi.Describe(s, id).Title, Type = t };
            }
            // 4. Running: wait for stock, ring the bell for new batches.
            int forest = CommissionDriver.HomeHex(s, Terrain.Forest), hills = CommissionDriver.HomeHex(s, Terrain.Hills);
            bool batchesLeft = (forest >= 0 && s.Economy.HexBatchRemaining[forest] > 0) || (hills >= 0 && s.Economy.HexBatchRemaining[hills] > 0);
            var bell = VillageBuilder.Bell != null ? VillageBuilder.Bell.transform.position : _boot.Layout.Bell;
            return batchesLeft
                ? new Step { Sentence = "the workshops are running. Let the carriers bring the planks and bricks home; ring the town bell once the forest and hills batches are used up.", Where = bell, Caption = "Town bell (ends the round)" }
                : new Step { Sentence = "the forest and hills batches are used up. Ring the town bell to start the next round and draw new batches.", Where = bell, Caption = "Ring the town bell" };
        }

        /// <summary>Puts a beacon on the step's target (replacing the previous one) and a toast saying what it marks.</summary>
        public void ShowTarget(Step step)
        {
            if (LastBeacon != null) Destroy(LastBeacon.gameObject);
            LastBeacon = Beacon.Show(step.Where, step.Caption, 45f);
            EconomyHud.Toast("Follow the light column: " + step.Caption.ToLowerInvariant() + ".", false);
        }

        // ---------------- objective ----------------

        private void TakeObjective()
        {
            if (OwnsObjective) return;
            OwnsObjective = true;
            RefreshObjective(true);
        }

        private void Update()
        {
            if (!OwnsObjective || Time.unscaledTime < _objectiveAt) return;
            _objectiveAt = Time.unscaledTime + 0.5f;
            RefreshObjective(false);
        }

        /// <summary>Sets the HUD objective from the commission state (only while nobody else replaced our text).</summary>
        public void RefreshObjective(bool force)
        {
            if (!force && Hud.Objective != CurrentObjective) return;
            var s = _boot.Sim.State;
            string text = s.Economy.Commission.Complete
                ? (QuartermasterAcknowledged ? "Commission complete. Grow the village (Tab: God View)." : "Commission complete! Talk to Quartermaster Bram.")
                : "Commission: " + EconomyText.CommissionProgress(s) + "   (Tab: God View)";
            CurrentObjective = text;
            if (text != Hud.Objective) Hud.SetObjective(text);
        }
    }
}
