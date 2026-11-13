using System;
using System.Collections.Generic;
using System.Linq;
using World.Sim.Economy;
using World.Sim.Military;
using World.Sim.Strategy;

namespace World.Sim.Tests
{
    /// <summary>
    /// Headless scripted players. Competent: commission, farm, second and third city, cards, wonder,
    /// power plant, barracks, a squad and an attack on the outpost, hero clearing demons, trading for what
    /// the village cannot make. Passive: picks the first draft card and ends rounds. One command per tick.
    /// </summary>
    internal sealed class StrategyPlayer
    {
        public readonly WorldSim Sim;
        public readonly bool Passive;
        /// <summary>Completionist: also builds the army and attacks, and lets each round's deliveries finish (longer game).</summary>
        public readonly bool Completionist;
        private readonly Queue<Command> _queue = new Queue<Command>();
        private long _roundStartTick;
        private int _lastRound;

        /// <summary>Milestone name -> tick, for reports.</summary>
        public readonly SortedDictionary<string, long> Milestones = new SortedDictionary<string, long>(StringComparer.Ordinal);

        public int MinRoundTicks = 150;
        public int MaxRoundTicks = 700;

        public StrategyPlayer(WorldSim sim, bool passive = false, bool completionist = false)
        {
            Sim = sim;
            Passive = passive;
            Completionist = completionist;
            if (completionist) MaxRoundTicks = 1500;
        }

        private WorldState S => Sim.State;

        private void Mark(string name)
        {
            if (!Milestones.ContainsKey(name)) Milestones[name] = S.Tick;
        }

        /// <summary>Submit at most one command (call once before each Step).</summary>
        public void Act()
        {
            var s = S;
            if (s.Strategy.GameOver) return;
            if (s.Economy.Round != _lastRound)
            {
                _lastRound = s.Economy.Round;
                _roundStartTick = s.Tick;
            }
            if (Sim.PendingCommands.Count > 0) return;
            if (s.Economy.Commission.Complete) Mark("commission");
            if (s.Strategy.Started) Mark("age1");

            if (StrategyApi.DraftPending(s))
            {
                Submit(new DraftPick { CardId = ChooseDraft(s) });
                return;
            }
            if (Passive)
            {
                if (s.Tick - _roundStartTick >= MinRoundTicks && !s.Economy.RoundStartPending) Submit(new EndRound());
                return;
            }
            while (_queue.Count > 0)
            {
                var c = _queue.Dequeue();
                if (Submit(c)) return;
            }
            if (MoveHero(s)) return;
            if (Plan(s)) return;
            if (ShouldEndRound(s)) Submit(new EndRound());
        }

        private bool Submit(Command c)
        {
            c.PlayerId = PlayerIds.Human;
            return Sim.Submit(c).Ok;
        }

        // ---------------- Draft and cards ----------------

        private static readonly Dictionary<string, int> Preference = new Dictionary<string, int>
        {
            { "great_hall", 100 }, { "monument", 95 }, { "laurel_arch", 90 }, { "census_hall", 85 },
            { "lumber_camp", 60 }, { "stone_cutters", 58 }, { "harvest_festival", 50 }, { "granary", 40 },
            { "trade_charter", 45 }, { "toll_gate", 35 }, { "mess_hall", 30 }, { "drill_yard", 28 },
            { "armoury", 26 }, { "ranger_post", 20 }, { "beacon_tower", 22 }, { "ward_stones", 18 },
            { "ore_survey", 5 }, { "merchant_guild", 8 },
        };

        private static int Pref(WorldState s, int cardId) => Preference.TryGetValue(StrategyApi.CardDefOf(s, cardId).Id, out int p) ? p : 0;

        private static int ChooseDraft(WorldState s) =>
            s.Strategy.Sides[0].DraftHand.OrderByDescending(id => Pref(s, id)).ThenBy(id => id).First();

        // ---------------- Hero ----------------

        private bool MoveHero(WorldState s)
        {
            if (!s.Strategy.Started || s.Strategy.HeroMovesLeft <= 0) return false;
            var topo = s.Board.Topology;
            int hero = s.Strategy.HeroVertex;
            // Demons on the road route to a stalled building matter most, then cities, then any road flag.
            var critical = new HashSet<int>();
            int store = s.Board.StorehouseVertex;
            foreach (var b in s.Economy.Buildings)
            {
                if (b.Reason != Reasons.DemonOnRoute) continue;
                var route = topo.FindPath(store, b.Vertex, e => s.Economy.Roads[e]);
                if (route != null) foreach (int v in route) if (StrategyApi.DemonsAt(s, v) > 0) critical.Add(v);
            }
            int best = -1, bestScore = int.MaxValue;
            foreach (var kv in StrategyApi.Demons(s))
            {
                if (!Relevant(s, kv.Key)) continue;
                var p = topo.FindPath(hero, kv.Key);
                if (p == null) continue;
                int score = p.Length + (critical.Contains(kv.Key) ? 0 : StrategyApi.PlayerCities(s).Contains(kv.Key) ? 3 : 6);
                if (score < bestScore)
                {
                    best = kv.Key;
                    bestScore = score;
                }
            }
            if (best < 0) return false;
            var path = topo.FindPath(hero, best);
            return Submit(new MoveHero { Vertex = path[1] });
        }

        /// <summary>A demon matters when it sits on the player's road network or a city.</summary>
        private static bool Relevant(WorldState s, int v)
        {
            if (StrategyApi.PlayerCities(s).Contains(v)) return true;
            foreach (int e in s.Board.Topology.VertexEdges(v)) if (s.Economy.Roads[e]) return true;
            return false;
        }

        // ---------------- Plan ----------------

        private int Have(Ware w) => EconomyApi.StorehouseCount(S, w);

        private bool Exists(BuildingType t) => EconomyApi.FindBuilding(S, t) >= 0;

        private bool Active(BuildingType t) => EconomyApi.FindBuilding(S, t, true) >= 0;

        /// <summary>Wares the current goal still needs from the storehouse (cards and trades leave them alone).</summary>
        private int[] _reserve = new int[Wares.Count];

        private sealed class Goal
        {
            public string Name;
            public Func<bool> Done;
            /// <summary>Cost in storehouse wares, or null when the goal cannot be acted on now (skip it).</summary>
            public Func<int[]> Cost;
            public Func<bool> Act;
        }

        private List<Goal> _goals;

        private List<Goal> Goals(WorldState s)
        {
            if (_goals != null) return _goals;
            int fields = EconomyTestKit.HomeHex(s, Terrain.Fields);
            var g = new List<Goal>();
            g.Add(Building("farm", BuildingType.Farm, EconomyTestKit.Touches(s, fields)));
            g.Add(CityGoal("city2", 2));
            g.Add(new Goal { Name = "cover", Done = () => CoverTarget(S, out _, out _) < 0, Cost = () => CoverCost(S), Act = () => Cover(S) });
            if (!Completionist)
            {
                g.Add(CityGoal("city3", 3));
                g.Add(Building("wonder_site", BuildingType.WonderSite, null));
                g.Add(Stage(1));
                _goals = g;
                return g;
            }
            g.Add(Building("power_plant", BuildingType.PowerPlant, null));
            g.Add(Building("barracks", BuildingType.Barracks, null));
            g.Add(new Goal
            {
                Name = "units",
                Done = () => Milestones.ContainsKey("attack") || UnitsTrainedOrQueued(S) >= 3,
                Cost = () => !Active(BuildingType.Barracks) ? null : EconomyTuning.W(Ware.Rations, UnitRations(S), Ware.Ingots, S.Military.Tuning.UnitIngots),
                Act = () => Submit(new QueueUnit { BarracksId = EconomyApi.FindBuilding(S, BuildingType.Barracks, true) }),
            });
            g.Add(Building("wonder_site", BuildingType.WonderSite, null));
            g.Add(Stage(1));
            g.Add(CityGoal("city3", 3));
            g.Add(Stage(2));
            _goals = g;
            return g;
        }

        private Goal Building(string name, BuildingType t, Func<int, bool> filter, int minAge = 1) => new Goal
        {
            Name = name,
            Done = () => Active(t),
            Cost = () =>
            {
                if (S.Strategy.Age < minAge) return null;
                if (Exists(t)) return new int[Wares.Count]; // a site on its way: wait for it (its wares are already requested)
                int v = EconomyTestKit.ChooseVertex(S, t, filter);
                if (v < 0) return null;
                var c = (int[])EconomyApi.Def(S, t).Cost.Clone();
                c[(int)Ware.Planks] += EconomyTestKit.RoadsToConnect(S, v).Count;
                return c;
            },
            Act = () => Place(S, t, filter),
        };

        private Goal Stage(int stage) => new Goal
        {
            Name = "wonder" + stage,
            Done = () => S.Strategy.Wonder.StagesDone >= stage || S.Strategy.Wonder.InProgressStage == stage,
            Cost = () => !Active(BuildingType.WonderSite) || S.Strategy.Wonder.StagesDone != stage - 1 || S.Strategy.Wonder.InProgressStage != 0
                ? null : S.Strategy.Tuning.WonderStages[stage - 1],
            Act = () =>
            {
                Mark("wonder" + stage + "_ordered");
                return Submit(new BuildWonderStage());
            },
        };

        private Goal CityGoal(string name, int n) => new Goal
        {
            Name = name,
            Done = () => StrategyApi.PlayerCities(S).Count >= n,
            Cost = () =>
            {
                int site = CitySite(S, out var roads);
                if (site < 0) return null;
                var c = (int[])S.Strategy.Tuning.CityCost.Clone();
                c[(int)Ware.Planks] += roads.Count;
                return c;
            },
            Act = () =>
            {
                int site = CitySite(S, out var roads);
                if (roads.Count > 0)
                {
                    foreach (int e in roads) _queue.Enqueue(new PlaceRoad { Edge = e });
                    return Submit(_queue.Dequeue());
                }
                return Submit(new FoundCity { Vertex = site });
            },
        };

        private bool Plan(WorldState s)
        {
            int forest = EconomyTestKit.HomeHex(s, Terrain.Forest);
            int hills = EconomyTestKit.HomeHex(s, Terrain.Hills);
            Array.Clear(_reserve, 0, _reserve.Length);
            if (!Exists(BuildingType.Woodcutter)) return Place(s, BuildingType.Woodcutter, EconomyTestKit.Touches(s, forest));
            if (!Exists(BuildingType.Quarry)) return Place(s, BuildingType.Quarry, EconomyTestKit.Touches(s, hills));
            if (!Exists(BuildingType.Sawmill)) return Place(s, BuildingType.Sawmill, null);
            // Commission first: hold its planks and stone until it is done.
            if (!s.Economy.Commission.Complete)
            {
                _reserve[(int)Ware.Planks] = 10;
                _reserve[(int)Ware.Stone] = 10;
            }

            if (AttackIfReady(s)) return true;

            // Deliveries for the wonder and the barracks go first.
            foreach (var b in s.Economy.Buildings)
            {
                if (b.State == BuildingState.Removed || b.Priority == Priority.High) continue;
                bool wonderBusy = b.Type == BuildingType.WonderSite && (b.State != BuildingState.Active || s.Strategy.Wonder.InProgressStage > 0);
                bool barracksBusy = b.Type == BuildingType.Barracks && UnitsTrainedOrQueued(s) > 0;
                if (wonderBusy || barracksBusy) return Submit(new SetPriority { BuildingId = b.Id, Priority = Priority.High });
            }

            // A wonder stage being supplied keeps its missing wares.
            var wonder = s.Strategy.Wonder;
            if (wonder.InProgressStage > 0)
            {
                var c = s.Strategy.Tuning.WonderStages[wonder.InProgressStage - 1];
                for (int w = 0; w < Wares.Count; w++)
                    _reserve[w] += Math.Max(0, c[w] - EconomyApi.Available(s, wonder.BuildingId, (Ware)w) - s.Economy.Buildings[wonder.BuildingId].Incoming[w]);
            }

            foreach (var g in Goals(s))
            {
                if (g.Done()) { Mark(g.Name); continue; }
                var cost = g.Cost();
                if (cost == null) continue;
                if (cost.All(c => c == 0) && !g.Done()) break; // waiting for a site under construction
                bool fits = true;
                for (int w = 0; w < Wares.Count; w++) if (Have((Ware)w) < cost[w] + _reserve[w]) fits = false;
                if (fits) return g.Act();
                for (int w = 0; w < Wares.Count; w++) _reserve[w] += cost[w];
                for (int w = 0; w < Wares.Count; w++)
                    if (Have((Ware)w) < _reserve[w] && TradeFor(s, (Ware)w, _reserve[w])) return true;
                break;
            }
            // Point cards (and other useful cards) from what the goals leave over.
            return BuildCards(s, true) || BuildCards(s, false) || DiscardJunk(s);
        }

        private bool Covers(int[] cost)
        {
            for (int w = 0; w < Wares.Count; w++) if (Have((Ware)w) < cost[w]) return false;
            return true;
        }

        /// <summary>Storehouse count beyond what the current goal needs.</summary>
        private int Spare(Ware w) => Have(w) - _reserve[(int)w];

        private bool BuildCards(WorldState s, bool pointsOnly)
        {
            var room = StrategyApi.CitiesWithCardRoom(s);
            if (room.Count == 0) return false;
            foreach (var cv in StrategyApi.Hand(s).OrderByDescending(c => Pref(s, c.CardId)))
            {
                var d = StrategyApi.CardDefOf(s, cv.CardId);
                bool points = d.Vp > 0 || d.VpPerExtraCity > 0;
                if (pointsOnly && !points) continue;
                if (!Useful(s, d)) continue;
                bool fits = d.CoinCost <= s.Player(0).Coin;
                for (int w = 0; w < Wares.Count; w++) if (d.Cost[w] > Spare((Ware)w)) fits = false;
                if (fits) return Submit(new BuildCard { CardId = cv.CardId, CityVertex = room[0] });
                if (points && d.CoinCost <= s.Player(0).Coin)
                    for (int w = 0; w < Wares.Count; w++)
                        if (d.Cost[w] > Have((Ware)w) && TradeFor(s, (Ware)w, d.Cost[w])) return true;
            }
            return false;
        }

        /// <summary>Points always; yield cards in Age I; military cards once a barracks exists; the rest is sold for coin.</summary>
        private bool Useful(WorldState s, CardDef d)
        {
            if (d.Vp > 0 || d.VpPerExtraCity > 0) return true;
            if (d.ForestBatch + d.HillsBatch + d.FieldsBatch + d.AllBatch > 0) return s.Strategy.Age <= 1;
            if (d.TrainingPercent < 0 || d.UnitRationDiscount > 0 || d.DamageBonus > 0) return Exists(BuildingType.Barracks);
            return d.TradeRateBonus > 0;
        }

        private bool DiscardJunk(WorldState s)
        {
            foreach (var cv in StrategyApi.Hand(s))
                if (!Useful(s, StrategyApi.CardDefOf(s, cv.CardId))) return Submit(new DiscardCard { CardId = cv.CardId });
            return false;
        }

        /// <summary>Trade the ware with the most to spare (beyond the goal's own needs and a small floor) for one unit.</summary>
        private bool TradeFor(WorldState s, Ware want, int target)
        {
            if (Have(want) >= target) return false;
            int rate = StrategyApi.TradeRate(s);
            Ware best = Ware.Timber;
            int bestSpare = 0;
            foreach (var w in Wares.All)
            {
                if (w == want || w == Ware.Rations || w == Ware.Ingots || w == Ware.Grain) continue;
                int floor = w == Ware.Timber ? 0 : 2;
                int spare = Spare(w) - floor;
                if (spare >= rate && spare > bestSpare)
                {
                    best = w;
                    bestSpare = spare;
                }
            }
            if (bestSpare < rate)
            {
                // Coin from discarded cards buys what the village cannot make.
                if (s.Player(0).Coin < StrategyApi.BuyPrice(s)) return false;
                Mark("first_buy");
                return Submit(new Buy { Ware = want, Count = 1 });
            }
            Mark("first_trade");
            return Submit(new Trade { Give = best, Get = want, Count = 1 });
        }

        // ---------------- Buildings and cities ----------------

        private bool Place(WorldState s, BuildingType t, Func<int, bool> filter)
        {
            int v = EconomyTestKit.ChooseVertex(s, t, filter);
            if (v < 0) return false;
            var roads = EconomyTestKit.RoadsToConnect(s, v);
            var cost = EconomyApi.Def(s, t).Cost;
            for (int w = 0; w < Wares.Count; w++)
            {
                int need = cost[w] + (w == (int)Ware.Planks ? roads.Count : 0);
                if (Have((Ware)w) < need) return TradeFor(s, (Ware)w, need);
            }
            Mark("place_" + t);
            _queue.Enqueue(new PlaceBuilding { Type = t, Vertex = v });
            foreach (int e in roads) _queue.Enqueue(new PlaceRoad { Edge = e });
            return Submit(_queue.Dequeue());
        }

        /// <summary>Best city flag: legal now, or one that only lacks roads (returned in placeable order); -1 if none.</summary>
        private static int CitySite(WorldState s, out List<int> roads)
        {
            var topo = s.Board.Topology;
            roads = new List<int>();
            var mine = StrategyApi.PlayerCities(s);
            int Value(int v)
            {
                int val = 0;
                foreach (int h in topo.VertexHexes(v))
                {
                    var terr = s.Board.HexTerrain[h];
                    if (terr == Terrain.Meadow) continue;
                    bool covered = mine.Any(c => topo.VertexHexes(c).Contains(h));
                    val += covered ? 1 : terr == Terrain.Forest ? 6 : terr == Terrain.Hills ? 5 : 3;
                }
                return val;
            }
            var legal = StrategyApi.LegalCitySites(s, ignoreCost: true);
            if (legal.Count > 0) return legal.OrderByDescending(Value).ThenBy(v => v).First();
            int best = -1, bestScore = int.MinValue;
            for (int v = 0; v < topo.VertexCount; v++)
            {
                if (StrategyApi.CheckCity(s, v).Reason != StrategyRefusals.NoRoad) continue;
                var p = EconomyTestKit.PathToNetwork(s, v);
                if (p == null || p.Length > 4) continue;
                int score = Value(v) - 3 * (p.Length - 1);
                if (score > bestScore) { best = v; bestScore = score; }
            }
            if (best < 0) return -1;
            var edges = topo.PathEdges(EconomyTestKit.PathToNetwork(s, best)).Where(e => !s.Economy.Roads[e]).ToList();
            edges.Reverse();
            roads = edges;
            return best;
        }

        /// <summary>A forest or hills hex at a newer city without a harvester; returns the hex or -1.</summary>
        private static int CoverTarget(WorldState s, out BuildingType type, out Func<int, bool> filter)
        {
            var topo = s.Board.Topology;
            type = BuildingType.Woodcutter;
            filter = null;
            foreach (int city in StrategyApi.PlayerCities(s).Skip(1))
                foreach (int h in topo.VertexHexes(city))
                {
                    var terr = s.Board.HexTerrain[h];
                    BuildingType t;
                    if (terr == Terrain.Forest) t = BuildingType.Woodcutter;
                    else if (terr == Terrain.Hills) t = BuildingType.Quarry;
                    else continue;
                    int hex = h;
                    bool covered = s.Economy.Buildings.Any(b => b.Type == t && b.State != BuildingState.Removed && topo.VertexHexes(b.Vertex).Contains(hex));
                    if (covered) continue;
                    var f = EconomyTestKit.Touches(s, h);
                    if (EconomyTestKit.ChooseVertex(s, t, f) < 0) continue;
                    type = t;
                    filter = f;
                    return h;
                }
            return -1;
        }

        private int[] CoverCost(WorldState s)
        {
            if (CoverTarget(s, out var t, out var f) < 0) return null;
            var c = (int[])EconomyApi.Def(s, t).Cost.Clone();
            c[(int)Ware.Planks] += EconomyTestKit.RoadsToConnect(s, EconomyTestKit.ChooseVertex(s, t, f)).Count;
            return c;
        }

        private bool Cover(WorldState s)
        {
            if (CoverTarget(s, out var t, out var f) < 0) return false;
            Mark("cover_" + t);
            return Place(s, t, f);
        }

        // ---------------- Military ----------------

        private static int UnitRations(WorldState s) =>
            Math.Max(0, s.Military.Tuning.UnitRations - StrategyApi.Effects(s, 0).UnitRationDiscount);

        private static int UnitsTrainedOrQueued(WorldState s)
        {
            int n = MilitaryApi.Squads(s, PlayerIds.Human).Sum(x => x.Units);
            foreach (var q in s.Military.Queues) n += q.Orders.Count;
            return n;
        }

        private bool AttackIfReady(WorldState s)
        {
            if (s.Military.Outpost.Destroyed) return false;
            var squads = MilitaryApi.Squads(s, PlayerIds.Human);
            bool last = StrategyApi.AgeRound(s) >= s.Strategy.Tuning.Ages * s.Strategy.Tuning.RoundsPerAge;
            int need = last ? 1 : 3;
            var ready = squads.Where(x => x.Order == OrderKind.None && x.Units >= need).ToList();
            if (ready.Count == 0) return false;
            Mark("attack");
            return Submit(new OrderSquad { SquadId = ready[0].Id, Order = OrderKind.Attack, Target = s.Board.RivalOutpostVertex });
        }

        // ---------------- Rounds ----------------

        private bool ShouldEndRound(WorldState s)
        {
            if (s.Economy.RoundStartPending) return false;
            long age = s.Tick - _roundStartTick;
            if (age < MinRoundTicks) return false;
            if (age >= MaxRoundTicks) return true;
            if (Completionist && s.Strategy.Started)
            {
                // Let sites, wonder stages, training and marches finish inside the round.
                if (s.Economy.Buildings.Any(b => b.State == BuildingState.Site || b.State == BuildingState.Constructing)) return false;
                if (s.Strategy.Wonder.InProgressStage > 0) return false;
                if (s.Military.Queues.Any(q => q.Orders.Count > 0)) return false;
                if (MilitaryApi.Squads(s, 0).Any(x => x.Order == OrderKind.Attack) && !s.Military.Outpost.Destroyed) return false;
            }
            // Wait for the military plan to finish an attack in the last round.
            var topo = s.Board.Topology;
            foreach (var b in s.Economy.Buildings)
            {
                if (b.State != BuildingState.Active) continue;
                var def = EconomyApi.Def(s, b.Type);
                if (def.Kind == BuildingKind.Harvester)
                {
                    if (b.WorkProgress > 0) return false;
                    foreach (int h in topo.VertexHexes(b.Vertex))
                        if (s.Board.HexTerrain[h] == def.Terrain && s.Economy.HexBatchRemaining[h] > 0 && b.Reason != Reasons.OutputFull
                            && b.Reason != Reasons.DemonOnRoute && b.Reason != Reasons.NoRoad) return false;
                }
                if (def.Kind == BuildingKind.Producer && b.WorkProgress > 0) return false;
            }
            if (s.Economy.Carriers.Any(c => c.HasLoad)) return false;
            if (s.Strategy.Started && StrategyLogicAgeRound(s) >= s.Strategy.Tuning.Ages * s.Strategy.Tuning.RoundsPerAge)
            {
                // Last round: let a marching squad finish its attack first.
                if (MilitaryApi.Squads(s, 0).Any(x => x.Order == OrderKind.Attack) && !s.Military.Outpost.Destroyed && age < MaxRoundTicks) return false;
            }
            return true;
        }

        private static int StrategyLogicAgeRound(WorldState s) => StrategyApi.AgeRound(s);
    }
}
