using System.Collections.Generic;
using World.Sim.Economy;
using World.Sim.Factions;

namespace World.Sim.Eras
{
    /// <summary>
    /// Standalone starts (PRD_AGES 4.7, AG-7): a full sim for a chapter without playing earlier ones. Ordinary new games
    /// otherwise; reachable from F1 and the command line. Unknown or not yet built ids return null (never throw). Mode is
    /// DevStart; a campaign started on it switches to Campaign through ErasModule.EnterCampaign. Options are per start
    /// (M1 adds "modernOutcome" for dev.future.core); H1 reads none.
    ///
    /// dev.historic (H1-1): the end state of a short scripted run, so the state is legal (nothing is granted). From a new
    /// game in DevStart mode, a director casts the top proposal of both peoples as soon as it is legal (the opening, banishes,
    /// collectors, workshops, roads, camps, the city) until each has its home raised to a city, at least two more camps,
    /// a Sawmill, a Kiln, a Bakery and a Smokehouse and at least two improved collectors (or BuildLimitTicks pass). No
    /// overrun can start while it is built. Then both peoples have handed over (the first planks reach a site on the way;
    /// the milestone is applied if it was not), both are autonomous, and the island takes the Historic step exactly as
    /// AdvanceChapter does (the smith's chest). The game then runs on from that tick (Eras.ChapterStartTick); its events
    /// are drained. The same seed always gives the same start. M1-4 and F1-2 add their starts here (additively).
    /// </summary>
    public static class ScenarioStarts
    {
        public const string DevHistoric = "dev.historic";
        public const string DevModernHarbour = "dev.modern.harbour";
        public const string DevModernSkirmish = "dev.modern.skirmish";
        public const string DevFutureCore = "dev.future.core";

        /// <summary>dev.historic: the scripted build stops here at the latest (30 game minutes).</summary>
        public const int BuildLimitTicks = 18000;

        private static readonly string[] Available = { DevHistoric };

        /// <summary>Start ids that Create can build now (grows with M1 and F1).</summary>
        public static IReadOnlyList<string> Ids => Available;

        public static WorldSim Create(string startId, ulong seed, IDictionary<string, string> options = null)
        {
            switch (startId)
            {
                case DevHistoric: return DevHistoricStart(seed);
                default: return null;
            }
        }

        /// <summary>True when a people has what dev.historic promises (4.7).</summary>
        public static bool HistoricStartReached(WorldState s, int faction)
        {
            var e = s?.Economy;
            if (e == null) return false;
            var starts = s.Board.StartCorners;
            var home = faction < starts.Length ? EconomyLogic.SettlementAt(s, starts[faction]) : null;
            if (home == null || home.Faction != faction || home.State != SettlementState.Active || !home.IsCity) return false;
            int camps = 0;
            foreach (var st in e.Settlements) if (st.Faction == faction && st.State == SettlementState.Active) camps++;
            if (camps < 3) return false;
            bool saw = false, kiln = false, bakery = false, smoke = false;
            int improved = 0;
            foreach (var b in e.Buildings)
            {
                if (b.Faction != faction || b.State != BuildingState.Active) continue;
                switch (b.Type)
                {
                    case BuildingType.Sawmill: saw = true; break;
                    case BuildingType.Kiln: kiln = true; break;
                    case BuildingType.Bakery: bakery = true; break;
                    case BuildingType.Smokehouse: smoke = true; break;
                }
                if (BuildingDefs.Get(b.Type).Class == BuildingClass.ImprovedCollector) improved++;
            }
            return saw && kiln && bakery && smoke && improved >= 2;
        }

        private static WorldSim DevHistoricStart(ulong seed)
        {
            var sim = WorldSimFactory.CreateNew(seed);
            var s = sim.State;
            s.Eras.Mode = PlayMode.DevStart;
            s.Eras.StartId = DevHistoric;
            var ft = s.Factions.Tuning;
            int margin = ft.OverrunMargin;
            ft.OverrunMargin = int.MaxValue / 4; // no overrun warning while the start is built
            foreach (var fa in s.Factions.Factions) fa.Auto = false;

            var done = new bool[PlayerIds.Count];
            while (s.Tick < BuildLimitTicks && !s.Factions.GameOver)
            {
                bool all = true;
                for (int f = 0; f < PlayerIds.Count; f++)
                {
                    if (!done[f]) done[f] = HistoricStartReached(s, f);
                    if (done[f]) continue;
                    all = false;
                    var top = FactionApi.Top(s, f);
                    if (top != null && sim.Check(new CastSpell { PlayerId = f, ProposalId = top.Id }).Ok)
                        sim.Submit(new CastSpell { PlayerId = f, ProposalId = top.Id });
                }
                if (all) break;
                sim.Step();
                sim.DrainEvents();
            }

            var ev = new EventBuffer();
            ft.OverrunMargin = margin;
            for (int f = 0; f < PlayerIds.Count; f++) AutonomyLogic.Handover(s, f, ev);
            foreach (var fa in s.Factions.Factions)
            {
                fa.Auto = true;
                fa.AnnouncedProposalId = -1;
                fa.AnnounceExecuteTick = -1;
            }
            ErasLogic.MarkReady(s, ChapterIds.Historic, ev);
            ErasLogic.Advance(s, ChapterIds.Historic, ev);
            sim.DrainEvents();
            return sim;
        }
    }
}
