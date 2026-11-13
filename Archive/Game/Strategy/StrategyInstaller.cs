using UnityEngine;
using World.Sim;
using World.Sim.Military;
using World.Sim.Strategy;

namespace World.Game
{
    /// <summary>
    /// Strategy and military presentation, installed by one line in WorldBootstrap.Awake after the economy:
    /// world views, God View tools, the HUD strip / draft modal / end screen, first-person NPC panels and the
    /// "full_game" AutoPlay script. See Docs/STRATEGY_UI.md.
    /// </summary>
    public static class StrategyInstaller
    {
        public static void Install(WorldBootstrap boot)
        {
            var go = new GameObject("Strategy");
            go.transform.SetParent(boot.transform, false);
            go.AddComponent<StrategyViews>().Bind(boot);
            go.AddComponent<StrategyWindow>().Bind(boot);
            go.AddComponent<StrategyHud>().Bind(boot);
            go.AddComponent<StrategyFirstPerson>().Bind(boot);
            StrategyGodTools.RegisterDefaults();
            AutoPlayScripts.Register("full_game", () => FullGameDriver.Script());
        }
    }

    /// <summary>
    /// First-person hooks (Npc / DialoguePanel pattern): Hesta opens the market, Captain Orla the barracks and squads,
    /// Scribe Pell the draft when a pick is waiting (else she explains the age and opens the cards and the wonder).
    /// The bell already ends the round (EconomyFirstPerson).
    /// </summary>
    public sealed class StrategyFirstPerson : MonoBehaviour
    {
        public static StrategyFirstPerson Instance { get; private set; }
        private WorldBootstrap _boot;

        public void Bind(WorldBootstrap boot)
        {
            Instance = this;
            _boot = boot;
            var keeper = Npc.Find(NpcIds.MarketKeeper);
            if (keeper != null) { keeper.Greeting = "Fair trades at the market. Ask me."; keeper.DialogueBuilder = Keeper; }
            var cap = Npc.Find(NpcIds.Captain);
            if (cap != null) { cap.Greeting = "The rival outpost watches us. Talk to me about soldiers."; cap.DialogueBuilder = Captain; }
            var scribe = Npc.Find(NpcIds.Scribe);
            if (scribe != null) { scribe.Greeting = "Cards, ages and the wonder: I keep the ledgers."; scribe.DialogueBuilder = Scribe; }
        }

        private void OnDestroy() { if (Instance == this) Instance = null; }

        private WorldState S => _boot.Sim.State;

        private void Keeper(Npc n)
        {
            var s = S;
            n.Lines.Clear(); n.Options.Clear();
            n.Lines.Add(StrategyApi.MarketText(s));
            n.Lines.Add("You have " + s.Player(PlayerIds.Human).Coin + " coin. Spare goods can buy what the village is short of.");
            n.Options.Add(DialogueOption.Do("Open the market", () => StrategyWindow.Open(StrategyPanels.Kind.Market)));
            n.Options.Add(DialogueOption.Leave());
        }

        private void Captain(Npc n)
        {
            var s = S;
            n.Lines.Clear(); n.Options.Clear();
            int bid = StrategyUi.FirstBarracks(s);
            n.Lines.Add(bid < 0 ? "We have no barracks. Build one, power it, and bring weapons and rations: " + MilitaryApi.UnitCostText(s) + "."
                                : MilitaryApi.DescribeBarracks(s, bid).Status);
            n.Lines.Add(MilitaryApi.ShieldSummary(s).Sentence);
            n.Lines.Add(MilitaryApi.Outpost(s).Status);
            n.Options.Add(DialogueOption.Do("Barracks and squads", () => StrategyWindow.Open(StrategyPanels.Kind.Army)));
            n.Options.Add(DialogueOption.Leave());
        }

        private void Scribe(Npc n)
        {
            var s = S;
            n.Lines.Clear(); n.Options.Clear();
            if (StrategyApi.DraftPending(s))
            {
                n.Lines.Add("The draft is waiting, and the village clock with it. Pick one card; the rival picks at the same time.");
                n.Options.Add(DialogueOption.Do("Pick a card", () => StrategyWindow.Open(StrategyPanels.Kind.Draft)));
                n.Options.Add(DialogueOption.Do("Show my cards", () => StrategyWindow.Open(StrategyPanels.Kind.Cards)));
                n.Options.Add(DialogueOption.Leave());
                return;
            }
            n.Lines.Add(StrategyApi.Headline(s));
            if (s.Strategy.Started)
            {
                var st = StrategyApi.Status(s);
                n.Lines.Add("Points: " + StrategyApi.ScoreText(s, PlayerIds.Human) + " The rival: " + StrategyApi.ScoreText(s, PlayerIds.Rival));
                n.Lines.Add(StrategyApi.Forecast(s).Sentence + " " + st.RoundsLeft + " rounds left in the game.");
            }
            else n.Lines.Add("When the commission is done, Age I begins: cards each round, cities, the wonder, and demons at every round end.");
            n.Options.Add(DialogueOption.Do("Show my cards", () => StrategyWindow.Open(StrategyPanels.Kind.Cards)));
            n.Options.Add(DialogueOption.Do("The wonder", () => StrategyWindow.Open(StrategyPanels.Kind.Wonder)));
            n.Options.Add(DialogueOption.Leave());
        }
    }
}
