using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using World.Sim.Campaign;
using World.Sim.Encounters.Ward;
using World.Sim.Eras;
using World.Sim.Factions;
using World.Sim.Ledger;
using World.Sim.Save;

namespace World.Sim.Tests
{
    /// <summary>
    /// Scripted players for the campaign proof (PRD_CRYSTALS 9.1, package I4). A player only sends commands and reads
    /// CampaignApi and the state, as the game does. Every run starts from dev.historic (the faction game really runs: the
    /// prince autonomous, the Ash Clan active, demons on) plus StartCampaign; the driver checks the campaign's
    /// conservation rules on every tick (section I).
    /// </summary>
    public sealed class ProofPolicy
    {
        public string Name = "prompt";
        /// <summary>Ticks the player takes over each decision (a lead, a choice, "Fight", "Set it").</summary>
        public int Delay;
        /// <summary>"Go on without me" at the start.</summary>
        public bool Away;
        /// <summary>Away: "I'm with you" once Mira waits for the player (at a tower, or holding at a threshold).</summary>
        public bool RejoinWhenWaiting;
        /// <summary>
        /// With the prince: away at the start; comes back (after <see cref="Delay"/>) only when Mira waits for the player (a
        /// tower, a held threshold), does what she needs promptly ("Set it", "Fight"), and says "Go on without me" again
        /// as soon as she sets out.
        /// </summary>
        public bool Wander;
        /// <summary>"Not that way" to every lead of a crystal the party still believes on its pedestal.</summary>
        public bool Reluctant;
        /// <summary>Lethal duels: these results first (injected through ResolveEncounter), then <see cref="Duel"/>.</summary>
        public List<DuelResult> Inject = new List<DuelResult>();
        /// <summary>After the injected results: a real Ward duel with this player model, or null for Victory.</summary>
        public WardBalanceTests.PlayerModel? Duel;
        /// <summary>Do not commit lethal encounters (the run stops at the threshold).</summary>
        public bool NeverFight;
        /// <summary>Leads the player agrees to (null = all); any other top lead is answered "Not that way".</summary>
        public Func<CampaignState, LeadView, bool> LeadFilter;
        public ProofPolicy Clone() { var p = (ProofPolicy)MemberwiseClone(); p.Inject = new List<DuelResult>(Inject); return p; }
    }

    public sealed class DuelRecord
    {
        public long Tick;
        public int AttemptId;
        public string Encounter, Volunteer;
        public DuelResult Result;
        public bool Real;
    }

    public sealed class ProofRun
    {
        public WorldSim Sim;
        public ProofPolicy Policy;
        public readonly List<SimEvent> Events = new List<SimEvent>();
        public readonly List<DuelRecord> Duels = new List<DuelRecord>();
        public readonly List<string> Failures = new List<string>();
        public int[] SpellCasts = new int[PlayerIds.Count];
        public int OverrunsOfYou, OverrunsOfAsh, Looted;
        public int MaxPoints, MaxAshPoints;
        public bool RosterUnlocked;
        public int InvariantChecks, TicksChecked, Rejoins;
        private string _key;
        private long _opened;
        private bool _wentAway, _served;
        private readonly Custody[] _last = new Custody[4];
        private readonly HashSet<int> _resolvedIds = new HashSet<int>();
        private readonly HashSet<string> _dead = new HashSet<string>();

        public WorldState S => Sim.State;
        public CampaignState C => Sim.State.Campaign;
        public long Since(long tick) => tick - C.StartTick;

        public ProofRun(WorldSim sim, ProofPolicy policy)
        {
            Sim = sim;
            Policy = policy;
            for (int i = 0; i < 4; i++) _last[i] = C.Crystal((Element)i).Custody;
            foreach (var k in C.Companions) if (!k.Alive) _dead.Add(k.Id);
        }

        public void Fail(string msg) { if (Failures.Count < 20) Failures.Add("tick " + S.Tick + ": " + msg); }

        /// <summary>Submits a command (it must be accepted unless allowRefusal).</summary>
        public CommandResult Send(Command c, bool allowRefusal = false)
        {
            var r = Sim.Submit(c);
            if (!r.Ok && !allowRefusal) Fail(c.GetType().Name + " refused: " + r.Reason);
            return r;
        }

        /// <summary>One decision (if any) and one tick.</summary>
        public void Tick()
        {
            Decide();
            Sim.Step();
            Absorb(Sim.DrainEvents());
            Check();
        }

        public void Run(Func<ProofRun, bool> done, long maxTicks)
        {
            long end = S.Tick + maxTicks;
            while (S.Tick < end && !done(this)) Tick();
        }

        /// <summary>Steps without deciding (the player does nothing).</summary>
        public void Idle(int ticks)
        {
            for (int i = 0; i < ticks; i++) { Sim.Step(); Absorb(Sim.DrainEvents()); Check(); }
        }

        /// <summary>Submits then steps once (the command applies on that tick).</summary>
        public void Do(Command c)
        {
            Send(c);
            Sim.Step();
            Absorb(Sim.DrainEvents());
            Check();
        }

        private void Absorb(IReadOnlyList<SimEvent> ev)
        {
            foreach (var e in ev)
            {
                switch (e)
                {
                    case SpellCast sc: if (sc.Faction >= 0 && sc.Faction < SpellCasts.Length) SpellCasts[sc.Faction]++; continue;
                    case CampOverrun co:
                        if (co.Defender == PlayerIds.Human) OverrunsOfYou++; else OverrunsOfAsh++;
                        if (co.Kind == CampOverrunKind.Looted) Looted++;
                        Events.Add(e);
                        continue;
                    case RosterUnlocked _: RosterUnlocked = true; Events.Add(e); continue;
                    case CrystalCustodyChanged cc:
                        if (cc.From != _last[(int)cc.Crystal]) Fail("custody of " + cc.Crystal + " jumped from " + _last[(int)cc.Crystal] + " (event says " + cc.From + ")");
                        if (cc.From == Custody.Restored) Fail(cc.Crystal + " left Restored");
                        if (cc.From == Custody.Carried && cc.To != Custody.Restored) Fail(cc.Crystal + " was lost while carried");
                        _last[(int)cc.Crystal] = cc.To;
                        break;
                    case EncounterResolved er:
                        if (!_resolvedIds.Add(er.AttemptId)) Fail("attempt " + er.AttemptId + " resolved twice");
                        break;
                    case CompanionFell cf:
                        if (_dead.Contains(cf.CompanionId)) Fail(cf.CompanionId + " fell twice");
                        _dead.Add(cf.CompanionId);
                        break;
                }
                if (e.GetType().Namespace == typeof(CampaignState).Namespace || e is ChapterReady || e is PointsAwarded) Events.Add(e);
            }
        }

        /// <summary>Section I, every tick: custody in one valid state at a consistent place, the ledger, the dead are silent.</summary>
        private void Check()
        {
            TicksChecked++;
            var c = C;
            var d = c.Definition;
            for (int i = 0; i < 4; i++)
            {
                var e = (Element)i;
                var st = c.Crystal(e);
                if (st == null) { Fail(e + " missing"); continue; }
                var cd = d.Crystal(e);
                var step = CampaignRuntime.RivalStepFor(c, e);
                switch (st.Custody)
                {
                    case Custody.AtSite:
                    case Custody.RivalTransit:
                        if (st.At != cd.Site) Fail(e + " " + st.Custody + " away from its site");
                        break;
                    case Custody.AtRivalAltar:
                        if (step == null || st.At != step.Altar) Fail(e + " on an altar that is not its step's");
                        if (!c.EverOnAltar.Contains(e)) Fail(e + " on an altar but not in EverOnAltar");
                        break;
                    case Custody.Restored:
                        if (st.At != CampaignRuntime.TowerRef(c, e)) Fail(e + " restored away from its tower");
                        if (!c.RestoredOrder.Contains(e)) Fail(e + " restored but not in the order");
                        break;
                    case Custody.Carried:
                        break;
                    default:
                        Fail(e + " in an unknown custody " + st.Custody);
                        break;
                }
                if (st.Custody != _last[i]) Fail(e + " custody changed without its event");
            }
            if (c.Resolved.Select(r => r.Id).Distinct().Count() != c.Resolved.Count) Fail("an attempt is recorded twice");
            int pts = LedgerApi.Points(S, PlayerIds.Human), ash = LedgerApi.Points(S, PlayerIds.Rival);
            MaxPoints = Math.Max(MaxPoints, pts);
            MaxAshPoints = Math.Max(MaxAshPoints, ash);
            if (pts != c.RestoredOrder.Count + PaidOther(PlayerIds.Human)) Fail("player points " + pts + " but " + c.RestoredOrder.Count + " restored");
            if (ash != c.EverOnAltar.Count + PaidOther(PlayerIds.Rival)) Fail("Ash points " + ash + " but " + c.EverOnAltar.Count + " ever on an altar");
            if (S.Tick % 600 == 0)
            {
                InvariantChecks++;
                foreach (var id in _dead)
                    foreach (var l in d.Lines)
                        if (l.Speaker == id && CampaignApi.Line(S, l.Id) != null) Fail("dead " + id + " speaks " + l.Id);
                var inv = SimTestKit.CheckInvariants(S);
                if (inv != null) Fail("faction game: " + inv);
            }
        }

        private int PaidOther(int faction)
        {
            var l = LedgerApi.Of(S, faction);
            return l == null ? 0 : l.Paid.Count(k => !k.StartsWith("crystal:", StringComparison.Ordinal));
        }

        public static bool AtCarriedTower(CampaignState c)
        {
            var p = c.Party;
            if (p.At.IsInterior) return false;
            for (int i = 0; i < 4; i++)
            {
                if (c.Crystal((Element)i).Custody != Custody.Carried) continue;
                var cd = c.Definition.Crystal((Element)i);
                var t = c.Definition.Tower(cd.Tower);
                if (t != null && t.Location == p.At.Location) return true;
            }
            return false;
        }

        private void Decide()
        {
            var c = C;
            if (c.Phase != CampaignPhase.Running) return;
            var p = c.Party;
            var pol = Policy;
            if ((pol.Away || pol.Wander) && !_wentAway) { _wentAway = true; Send(new SetCompany { Present = false }); return; }
            var top = CampaignApi.TopLead(S);
            string key = p.Mode + "|" + (top?.Id ?? 0) + "|" + p.ChoiceId + "|" + p.EncounterId + "|" + (c.Attempt?.Id ?? 0) + "|" + p.At;
            if (key != _key) { _key = key; _opened = S.Tick; }
            bool ready = S.Tick - _opened >= pol.Delay;

            if (c.Attempt != null) { ResolveNow(c.Attempt); return; }
            if (!p.PlayerPresent)
            {
                bool waiting = p.Mode == PartyMode.WaitingForPlayer || (p.Mode == PartyMode.AtThreshold && p.HoldAtThreshold);
                // The player is called when she starts to wait and comes back Delay later (her queue may change meanwhile).
                long since = p.Mode == PartyMode.AtThreshold ? p.WaitSinceTick : p.ModeSinceTick;
                if ((pol.RejoinWhenWaiting || pol.Wander) && waiting && S.Tick - since >= pol.Delay) { Send(new SetCompany { Present = true }); Rejoins++; }
                return;
            }
            if (pol.Wander && _served && p.Mode == PartyMode.Travelling) { _served = false; Send(new SetCompany { Present = false }); return; }
            if (pol.Wander) ready = true;   // back with her: prompt
            switch (p.Mode)
            {
                case PartyMode.AtThreshold:
                    if (!pol.NeverFight && ready && Sim.Check(new CommitEncounter { EncounterId = p.EncounterId }).Ok)
                    {
                        Send(new CommitEncounter { EncounterId = p.EncounterId });
                        _served = true;
                    }
                    return;
                case PartyMode.AskingChoice:
                {
                    var ch = c.Definition.Choice(p.ChoiceId);
                    int right = ch?.Answers?.FindIndex(a => a.Correct) ?? -1;
                    if (ready && right >= 0) Send(new AdviseMira { ChoiceId = p.ChoiceId, Answer = right });
                    return;
                }
                case PartyMode.WaitingForPlayer:
                case PartyMode.Idle:
                {
                    if (AtCarriedTower(c))
                    {
                        for (int i = 0; i < 4; i++)
                        {
                            var cr = new ConfirmRestore { Crystal = (Element)i };
                            if (ready && Sim.Check(cr).Ok) { Send(cr); _served = true; return; }
                        }
                    }
                    if (top == null) return;
                    if ((pol.Reluctant && Unreported(c, top)) || (pol.LeadFilter != null && !pol.LeadFilter(c, top)))
                    {
                        Send(new DeclineLead { LeadId = top.Id });
                        return;
                    }
                    if (ready && Sim.Check(new AcceptLead { LeadId = top.Id }).Ok) Send(new AcceptLead { LeadId = top.Id });
                    return;
                }
            }
        }

        /// <summary>The lead's crystal is still believed on its pedestal (no theft reported or seen).</summary>
        private static bool Unreported(CampaignState c, LeadView l)
        {
            var cd = c.Definition.Crystal(l.Crystal ?? "");
            return cd != null && c.BeliefOf(cd.Element).Custody == Custody.AtSite;
        }

        public static readonly WardOpponents Opponents = WardOpponents.BuiltIn();

        /// <summary>A real Ward duel against the attempt's profile on the attempt's seed.</summary>
        public static DuelResult PlayWard(string profile, ulong seed, WardBalanceTests.PlayerModel m)
        {
            var o = WardOpponentTests.Simulate(Opponents.Spec(profile, seed), seed, m);
            DuelResults.TryFrom(o, out var r);
            return r;
        }

        private void ResolveNow(AttemptState a)
        {
            DuelResult r;
            bool real = false;
            if (Policy.Inject.Count > 0) { r = Policy.Inject[0]; Policy.Inject.RemoveAt(0); }
            else if (Policy.Duel != null)
            {
                var prof = C.Definition.Encounter(a.EncounterId)?.Profile ?? "keeper";
                r = PlayWard(prof, a.Seed, Policy.Duel.Value);
                real = true;
            }
            else r = DuelResult.Victory;
            Duels.Add(new DuelRecord { Tick = S.Tick, AttemptId = a.Id, Encounter = a.EncounterId, Volunteer = a.Volunteer, Result = r, Real = real });
            Send(new ResolveEncounter { AttemptId = a.Id, Result = r });
        }

        public IEnumerable<T> Of<T>() where T : SimEvent => Events.OfType<T>();

        public Custody[] Chain(Element e)
        {
            var list = Of<CrystalCustodyChanged>().Where(x => x.Crystal == e).ToList();
            if (list.Count == 0) return new Custody[0];
            var chain = new List<Custody> { list[0].From };
            chain.AddRange(list.Select(x => x.To));
            return chain.ToArray();
        }
    }

    /// <summary>Starting points: dev.historic per seed (built once, then loaded from its save) plus a campaign.</summary>
    public static class ProofStarts
    {
        private static readonly ConcurrentDictionary<ulong, Lazy<string>> Historic = new ConcurrentDictionary<ulong, Lazy<string>>();

        public static string HistoricJson(ulong seed) =>
            Historic.GetOrAdd(seed, s => new Lazy<string>(() => SaveCodec.ToJson(ScenarioStarts.Create(ScenarioStarts.DevHistoric, s).State))).Value;

        public static ProofRun Start(string pack, ulong seed, ProofPolicy policy, Action<CampaignDefinition> edit = null, Action<WorldSim> before = null)
        {
            var sim = SaveCodec.Load(HistoricJson(seed), out _);
            before?.Invoke(sim);
            var d = PackFiles.Load(pack);
            edit?.Invoke(d);
            var r = sim.Submit(new StartCampaign { Definition = d });
            if (!r.Ok) throw new InvalidOperationException("StartCampaign refused: " + r.Reason);
            sim.Step();
            var run = new ProofRun(sim, policy);
            foreach (var e in sim.DrainEvents()) if (e.GetType().Namespace == typeof(CampaignState).Namespace) run.Events.Add(e);
            return run;
        }

        /// <summary>
        /// The proof's whole-campaign knob (section H): the skeleton made race-sized, on a copy only (the shipped skeleton is
        /// not changed). It takes the authored pack's tuning and expedition timings (the first march with its spread, the
        /// later ones, the carry), lets the expedition open with any of the four crystals, and gives every skeleton route
        /// the shape of the Earth route: one more reading before the pedestal (a marker at the site's location that Mira
        /// must read, a lead to it and an event that opens the way to the pedestal), so each route has the Earth route's
        /// number of decisions and readings. The pedestal visit is padded by <paramref name="padTicks"/> for the Earth
        /// route's stairs and underground walking that the skeleton's sites do not have.
        /// </summary>
        public static void RaceSkeleton(CampaignDefinition d, int padTicks)
        {
            var a = PackFiles.Load(PackFiles.Authored);
            d.Tuning = Newtonsoft.Json.JsonConvert.DeserializeObject<CampaignTuning>(Newtonsoft.Json.JsonConvert.SerializeObject(a.Tuning));
            d.Tuning.TakeTicks += padTicks;
            for (int i = 0; i < d.Rival.Steps.Count && i < a.Rival.Steps.Count; i++)
            {
                d.Rival.Steps[i].MarchCost = a.Rival.Steps[i].MarchCost;
                d.Rival.Steps[i].MarchSpread = a.Rival.Steps[i].MarchSpread;
                d.Rival.Steps[i].CarryCost = a.Rival.Steps[i].CarryCost;
            }
            d.Rival.OpenWith = d.Crystals.Select(x => x.Id).ToList();
            foreach (var cd in d.Crystals)
            {
                string loc = cd.Site.Location, id = cd.Id;
                d.Location(loc).Markers.Add(new MarkerDef { Id = id + "_marks", Kind = MarkerKind.Inscription, Name = "the old marks", I = 0, J = -1 });
                var at = MarkerRef.Surface(loc, id + "_marks");
                var site = d.Leads.First(l => l.Id == id + "_site");
                var known = site.Requires.ToList();
                d.Leads.Add(new LeadDef { Id = id + "_marks", Title = "The old marks", Goal = new GoalDef { At = at, Action = QuestAction.Examine }, Requires = known, DoneWhen = id + ".way_known", ReasonLine = site.ReasonLine, Priority = site.Priority, Crystal = id });
                d.Events.Add(new QuestEventDef { Id = id + "_read_marks", At = at, Action = QuestAction.Examine, Requires = known, Sets = new List<string> { id + ".way_known" } });
                site.Requires = known.Concat(new[] { id + ".way_known" }).ToList();
            }
        }

        /// <summary>A run continued from a save of another (same policy copy, its events so far kept).</summary>
        public static ProofRun Reload(ProofRun from, ProofPolicy policy = null)
        {
            var sim = SaveCodec.Load(SaveCodec.ToJson(from.S), out _);
            var run = new ProofRun(sim, policy ?? from.Policy.Clone());
            run.Events.AddRange(from.Events);
            run.Duels.AddRange(from.Duels);
            run.Failures.AddRange(from.Failures);
            run.TicksChecked = from.TicksChecked;
            run.InvariantChecks = from.InvariantChecks;
            return run;
        }
    }
}
