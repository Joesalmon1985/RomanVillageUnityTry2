using System;
using System.Collections.Generic;
using System.Linq;
using World.Sim.Economy;
using World.Sim.Military;
using World.Sim.Strategy;

namespace World.Sim.Tests
{
    /// <summary>
    /// Headless scripted players (one command per tick, plain visible rules).
    /// Competent: the commission chain (wood, clay, coal, bricks), the food chain (farm, mill, bakery, a second city
    /// on pasture, sheepfold, butcher, tavern), the ore chain (mine, power plant, smelter, smithy), a barracks and a
    /// squad held at home for the age-end comparisons, the wonder (stage 1 Planks + Bricks, stage 2 Iron + Rations),
    /// a third city, cards from what is left, the hero clearing demons, trading spare goods for what is short.
    /// Completionist: the same, then attacks the rival outpost with a full squad in the last round.
    /// Passive: picks the first draft card and ends rounds. Careless: competent but never moves the hero.
    /// </summary>
    internal sealed class StrategyPlayer
    {
        public readonly WorldSim Sim;
        public readonly bool Passive;
        /// <summary>Completionist: also attacks the outpost with a full squad in the last round.</summary>
        public readonly bool Completionist;
        /// <summary>Careless: never moves the hero (demons pile up and outbreak).</summary>
        public bool IgnoreDemons;
        /// <summary>No military at all (for the paired comparison run).</summary>
        public bool NoArmy;
        private readonly Queue<Command> _queue = new Queue<Command>();
        private long _roundStartTick;
        private int _lastRound;

        /// <summary>Milestone name -> tick, for reports.</summary>
        public readonly SortedDictionary<string, long> Milestones = new SortedDictionary<string, long>(StringComparer.Ordinal);

        public int MinRoundTicks = 150;
        public int MaxRoundTicks = 1000;

        public StrategyPlayer(WorldSim sim, bool passive = false, bool completionist = false)
        {
            Sim = sim;
            Passive = passive;
            Completionist = completionist;
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
            if (s.Military.UnitsTrained > 0) Mark("first_soldier");
            if (s.Strategy.Wonder.StagesDone >= 1) Mark("wonder1");
            if (s.Strategy.Wonder.StagesDone >= 2) Mark("wonder2");

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
            if (!IgnoreDemons && MoveHero(s)) return;
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
            { "great_hall", 100 }, { "monument", 95 }, { "census_hall", 90 }, { "shrine", 85 },
            { "armoury", 80 }, { "drill_yard", 75 }, { "palisade", 70 },
            { "lumber_camp", 60 }, { "clay_diggers", 58 }, { "granary", 50 }, { "charcoal_guild", 48 },
            { "ward_stones", 45 }, { "beacon_tower", 44 }, { "toll_gate", 40 }, { "ranger_post", 30 },
            { "road_guild", 25 }, { "master_smiths", 22 }, { "merchant_guild", 20 },
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
            int cap = StrategyApi.DemonCap(s);
            // Demons on the road route to a stalled building matter most, then cities, then any road flag;
            // sites at the cap (one more outbreaks) come before everything.
            var critical = new HashSet<int>();
            int store = s.Board.StorehouseVertex;
            foreach (var b in s.Economy.Buildings)
            {
                if (b.Reason != Reasons.DemonOnRoute) continue;
                var route = topo.FindPath(store, b.Vertex, e => s.Economy.Roads[e]);
                if (route != null) foreach (int v in route) if (StrategyApi.DemonsAt(s, v) > 0) critical.Add(v);
                if (StrategyApi.DemonsAt(s, b.Vertex) > 0) critical.Add(b.Vertex);
            }
            var cities = StrategyApi.PlayerCities(s);
            int bestMove = -1, bestScore = int.MaxValue;
            foreach (var kv in StrategyApi.Demons(s))
            {
                bool danger = kv.Value >= cap - 1;
                if (!Relevant(s, kv.Key) && !danger) continue;
                if (!HeroRoute(topo, hero, kv.Key, out int first, out int moves)) continue;
                int score = moves + 1 + (kv.Value >= cap ? -12 : danger ? -6 : 0)
                            + (critical.Contains(kv.Key) ? 0 : cities.Contains(kv.Key) ? 2 : 6);
                if (score < bestScore)
                {
                    bestMove = first;
                    bestScore = score;
                }
            }
            if (bestMove < 0) return false;
            return Submit(new MoveHero { Vertex = bestMove });
        }

        /// <summary>
        /// First hero move (a neighbouring corner) and move count to clear a flag: walk to a corner, or walk along
        /// the hex edge of an intermediate flag (the hero clears the flags it passes).
        /// </summary>
        private static bool HeroRoute(BoardTopology topo, int hero, int target, out int first, out int moves)
        {
            first = -1;
            moves = int.MaxValue;
            if (topo.IsCorner(target))
            {
                var p = topo.FindCornerPath(hero, target);
                if (p == null || p.Length < 2) return false;
                first = p[1];
                moves = p.Length - 1;
                return true;
            }
            int he = topo.HexEdgeOfFlag(target);
            int a = topo.HexEdgeCornerA(he), b = topo.HexEdgeCornerB(he);
            foreach (var (start, end) in new[] { (a, b), (b, a) })
            {
                var p = topo.FindCornerPath(hero, start);
                if (p == null) continue;
                int m = p.Length - 1 + 1;
                if (m < moves)
                {
                    moves = m;
                    first = p.Length > 1 ? p[1] : end;
                }
            }
            return first >= 0;
        }

        /// <summary>A demon matters when it sits on the player's road network, a building or a city.</summary>
        private static bool Relevant(WorldState s, int v)
        {
            if (StrategyApi.PlayerCities(s).Contains(v)) return true;
            if (EconomyApi.BuildingAt(s, v) >= 0) return true;
            foreach (int e in s.Board.Topology.VertexEdges(v)) if (s.Economy.Roads[e]) return true;
            return false;
        }

        // ---------------- Plan ----------------

        private int Have(Ware w) => EconomyApi.StorehouseCount(S, w);

        private bool Exists(BuildingType t) => EconomyApi.FindBuilding(S, t) >= 0;

        private bool Active(BuildingType t) => EconomyApi.FindBuilding(S, t, true) >= 0;

        /// <summary>Wares the current goals still need from the storehouse (cards and trades leave them alone).</summary>
        private readonly int[] _reserve = new int[Wares.Count];

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
            g.Add(CityGoal("city2", 2));
            if (!NoArmy)
            {
                // The iron chain first (the age-end comparisons pay +3 and +5), the food chain right after: the
                // starting rations feed the first ore.
                g.Add(TerrainBuilding("mine", BuildingType.Mine, Terrain.Mountain));
                g.Add(Building("power_plant", BuildingType.PowerPlant, null));
                g.Add(Building("smelter", BuildingType.Smelter, null));
                g.Add(Building("smithy", BuildingType.Smithy, null));
            }
            g.Add(Building("farm", BuildingType.Farm, EconomyTestKit.Touches(s, fields)));
            g.Add(TerrainBuilding("sheepfold", BuildingType.Sheepfold, Terrain.Pasture));
            g.Add(Building("mill", BuildingType.Mill, null));
            g.Add(Building("bakery", BuildingType.Bakery, null));
            g.Add(Building("butcher", BuildingType.Butcher, null));
            g.Add(Building("tavern", BuildingType.Tavern, null));
            if (!NoArmy)
            {
                g.Add(Building("barracks", BuildingType.Barracks, null));
                g.Add(new Goal
                {
                    Name = "units",
                    Done = () => UnitsTrainedOrQueued(S) >= S.Military.Tuning.MaxSquadSize,
                    Cost = () => !Active(BuildingType.Barracks) ? null : new int[Wares.Count],
                    Act = () => Submit(new QueueUnit { BarracksId = EconomyApi.FindBuilding(S, BuildingType.Barracks, true), Count = 1 }),
                });
            }
            else
            {
                g.Add(TerrainBuilding("mine", BuildingType.Mine, Terrain.Mountain));
                g.Add(Building("power_plant", BuildingType.PowerPlant, null));
                g.Add(Building("smelter", BuildingType.Smelter, null));
            }
            g.Add(Building("wonder_site", BuildingType.WonderSite, null));
            g.Add(Stage(1));
            g.Add(new Goal { Name = "cover", Done = () => CoverTarget(S, out _, out _) < 0, Cost = () => CoverCost(S), Act = () => Cover(S) });
            g.Add(CityGoal("city3", 3));
            g.Add(Stage(2));
            if (Completionist) g.Add(Building("market", BuildingType.Market, null));
            _goals = g;
            return g;
        }

        private Goal Building(string name, BuildingType t, Func<int, bool> filter) => new Goal
        {
            Name = name,
            Done = () => Active(t),
            Cost = () => SiteCost(t, filter),
            Act = () => Place(S, t, filter),
        };

        /// <summary>A harvester on a hex of the terrain touching one of your cities (none until such a city exists).</summary>
        private Goal TerrainBuilding(string name, BuildingType t, Terrain terrain) => new Goal
        {
            Name = name,
            Done = () => Active(t),
            Cost = () =>
            {
                var f = CityTerrainFilter(S, terrain);
                return f == null ? null : SiteCost(t, f);
            },
            Act = () => Place(S, t, CityTerrainFilter(S, terrain)),
        };

        private int[] SiteCost(BuildingType t, Func<int, bool> filter)
        {
            if (Exists(t))
            {
                // A site on its way: keep its missing wares, carry on with the next goal.
                var site = S.Economy.Buildings[EconomyApi.FindBuilding(S, t)];
                if (site.State == BuildingState.Site)
                {
                    var def = EconomyApi.Def(S, t);
                    for (int w = 0; w < Wares.Count; w++) _reserve[w] += Math.Max(0, def.Cost[w] - site.Stock[w] - site.Incoming[w]);
                }
                return null;
            }
            int v = EconomyTestKit.ChooseVertex(S, t, filter, Anchor(S, t));
            if (v < 0) return null;
            var c = (int[])EconomyApi.Def(S, t).Cost.Clone();
            var road = EconomyApi.RoadCostFor(S, EconomyTestKit.RoadsToConnect(S, v).Count);
            for (int w = 0; w < Wares.Count; w++) c[w] += road[w];
            return c;
        }

        /// <summary>Flags touching a hex of the terrain that touches one of your cities (the batch only comes there).</summary>
        private static Func<int, bool> CityTerrainFilter(WorldState s, Terrain terrain)
        {
            var topo = s.Board.Topology;
            var hexes = new List<int>();
            foreach (int c in StrategyApi.PlayerCities(s))
                foreach (int h in topo.VertexHexes(c))
                    if (s.Board.HexTerrain[h] == terrain && !hexes.Contains(h)) hexes.Add(h);
            if (hexes.Count == 0) return null;
            return v => topo.VertexHexes(v).Any(h => hexes.Contains(h));
        }

        private Goal Stage(int stage) => new Goal
        {
            Name = "wonder" + stage + "_ordered",
            Done = () => S.Strategy.Wonder.StagesDone >= stage || S.Strategy.Wonder.InProgressStage == stage,
            Cost = () => !Active(BuildingType.WonderSite) || S.Strategy.Wonder.StagesDone != stage - 1 || S.Strategy.Wonder.InProgressStage != 0
                ? null : S.Strategy.Tuning.WonderStages[stage - 1],
            Act = () => Submit(new BuildWonderStage()),
        };

        private Goal CityGoal(string name, int n) => new Goal
        {
            Name = name,
            Done = () => StrategyApi.PlayerCities(S).Count >= n,
            Cost = () =>
            {
                if (!S.Strategy.Started) return null;
                int site = CitySite(S, out var roads);
                if (site < 0) return null;
                var c = (int[])S.Strategy.Tuning.CityCost.Clone();
                var road = EconomyApi.RoadCostFor(S, roads.Count);
                for (int w = 0; w < Wares.Count; w++) c[w] += road[w];
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
            if (!Exists(BuildingType.ClayPit)) return Place(s, BuildingType.ClayPit, EconomyTestKit.Touches(s, hills));
            if (!Exists(BuildingType.Sawmill)) return Place(s, BuildingType.Sawmill, null);
            if (!Exists(BuildingType.CharcoalKiln)) return Place(s, BuildingType.CharcoalKiln, null);
            if (!Exists(BuildingType.BrickKiln)) return Place(s, BuildingType.BrickKiln, null);
            // Commission first: hold its planks and bricks until it is done.
            if (!s.Economy.Commission.Complete)
            {
                _reserve[(int)Ware.Planks] = s.Economy.Tuning.CommissionPlanks;
                _reserve[(int)Ware.Bricks] = s.Economy.Tuning.CommissionBricks;
                return false;
            }
            // Commission done: end the prologue round so Age I (and cities) can begin.
            if (!s.Strategy.Started) return false;

            if (AttackIfReady(s)) return true;

            // Deliveries for the wonder and the iron chain go first: coal is shared by the brick kiln, the power
            // plant and the smelter, and the brick kiln (nearest to the charcoal kiln) would otherwise take it all.
            foreach (var b in s.Economy.Buildings)
            {
                if (b.State == BuildingState.Removed || b.Priority == Priority.High) continue;
                bool wonderBusy = b.Type == BuildingType.WonderSite && (b.State != BuildingState.Active || s.Strategy.Wonder.InProgressStage > 0);
                bool ironChain = b.Type == BuildingType.PowerPlant || b.Type == BuildingType.Smelter || b.Type == BuildingType.Smithy || b.Type == BuildingType.Mine
                                 || (b.Type == BuildingType.Barracks && UnitsTrainedOrQueued(s) > 0);
                if (wonderBusy || ironChain) return Submit(new SetPriority { BuildingId = b.Id, Priority = Priority.High });
            }

            // Bricks stockpiled beyond what is needed: switch the brick kiln off so its coal goes to the smelter.
            int kiln = EconomyApi.FindBuilding(s, BuildingType.BrickKiln, true);
            if (kiln >= 0 && Active(BuildingType.Smelter))
            {
                var kb = s.Economy.Buildings[kiln];
                int bricks = Have(Ware.Bricks);
                if (kb.Enabled && bricks >= 12) return Submit(new ToggleBuilding { BuildingId = kiln, Enabled = false });
                if (!kb.Enabled && bricks < 6) return Submit(new ToggleBuilding { BuildingId = kiln, Enabled = true });
            }

            // Points and shields in hand are worth more than the next building: build them as soon as they are affordable.
            if (BuildPointCards(s)) return true;

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
                bool fits = true;
                for (int w = 0; w < Wares.Count; w++) if (Have((Ware)w) < cost[w] + _reserve[w]) fits = false;
                if (fits) return g.Act();
                for (int w = 0; w < Wares.Count; w++) _reserve[w] += cost[w];
                for (int w = 0; w < Wares.Count; w++)
                    if (Have((Ware)w) < _reserve[w] && TradeFor(s, (Ware)w, _reserve[w])) return true;
                break;
            }
            // Point and shield cards (and other useful cards) from what the goals leave over.
            return BuildCards(s, true) || BuildCards(s, false) || DiscardJunk(s);
        }

        /// <summary>Storehouse count beyond what the current goals need.</summary>
        private int Spare(Ware w) => Have(w) - _reserve[(int)w];

        /// <summary>Build a civic or military card from hand when the storehouse (not just the spare) covers it.</summary>
        private bool BuildPointCards(WorldState s)
        {
            var room = StrategyApi.CitiesWithCardRoom(s);
            if (room.Count == 0) return false;
            foreach (var cv in StrategyApi.Hand(s).OrderByDescending(c => Pref(s, c.CardId)))
            {
                var d = StrategyApi.CardDefOf(s, cv.CardId);
                if (d.Vp <= 0 && d.VpPerCity <= 0 && (d.Shields <= 0 || NoArmy)) continue;
                if (d.CoinCost > s.Player(0).Coin) continue;
                bool fits = true;
                for (int w = 0; w < Wares.Count; w++) if (d.Cost[w] > Have((Ware)w)) fits = false;
                if (fits) return Submit(new BuildCard { CardId = cv.CardId, CityVertex = room[0] });
            }
            return false;
        }

        private bool BuildCards(WorldState s, bool pointsOnly)
        {
            var room = StrategyApi.CitiesWithCardRoom(s);
            if (room.Count == 0) return false;
            foreach (var cv in StrategyApi.Hand(s).OrderByDescending(c => Pref(s, c.CardId)))
            {
                var d = StrategyApi.CardDefOf(s, cv.CardId);
                bool points = d.Vp > 0 || d.VpPerCity > 0 || d.Shields > 0;
                if (pointsOnly && !points) continue;
                if (!Useful(s, d)) continue;
                bool fits = d.CoinCost <= s.Player(0).Coin;
                for (int w = 0; w < Wares.Count; w++) if (d.Cost[w] > Spare((Ware)w)) fits = false;
                if (fits) return Submit(new BuildCard { CardId = cv.CardId, CityVertex = room[0] });
            }
            return false;
        }

        /// <summary>Points and shields always; yield and workshop cards in Ages I-II; lore cards; the rest is sold for coin.</summary>
        private bool Useful(WorldState s, CardDef d)
        {
            if (d.Vp > 0 || d.VpPerCity > 0 || d.Shields > 0) return !NoArmy || d.Shields == 0 || d.Vp > 0;
            switch (d.Family)
            {
                case CardFamily.RawAndCrafted: return s.Strategy.Age <= 2;
                case CardFamily.Lore: return !IgnoreDemons;
                case CardFamily.Commerce: return d.TradeRateBonus > 0 || d.CoinPerRound > 0;
                default: return false;
            }
        }

        private bool DiscardJunk(WorldState s)
        {
            foreach (var cv in StrategyApi.Hand(s))
                if (!Useful(s, StrategyApi.CardDefOf(s, cv.CardId))) return Submit(new DiscardCard { CardId = cv.CardId });
            return false;
        }

        /// <summary>Trade the ware with the most to spare (beyond the goals' own needs and a small floor) for one unit, else buy with coin.</summary>
        private bool TradeFor(WorldState s, Ware want, int target)
        {
            if (Have(want) >= target) return false;
            int rate = StrategyApi.TradeRate(s);
            Ware best = Ware.Logs;
            int bestSpare = 0;
            foreach (var w in Wares.All)
            {
                if (w == want || w == Ware.Rations || w == Ware.Iron || w == Ware.Weapons) continue;
                int floor = Wares.IsRaw(w) ? 0 : 2;
                int spare = Spare(w) - floor;
                if (spare >= rate && spare > bestSpare)
                {
                    best = w;
                    bestSpare = spare;
                }
            }
            if (bestSpare < rate)
            {
                // Coin from discarded cards and tolls buys what the village cannot make in time.
                if (s.Player(0).Coin < StrategyApi.BuyPrice(s)) return false;
                Mark("first_buy");
                return Submit(new Buy { Ware = want, Count = 1 });
            }
            Mark("first_trade");
            return Submit(new Trade { Give = best, Get = want, Count = 1 });
        }

        // ---------------- Buildings and cities ----------------

        /// <summary>Flag of the building a new one trades with most (a mill near the farm, a smelter near the mine...), or -1.</summary>
        private static int Anchor(WorldState s, BuildingType t)
        {
            BuildingType[] near;
            switch (t)
            {
                case BuildingType.Mill: near = new[] { BuildingType.Farm }; break;
                case BuildingType.Bakery: near = new[] { BuildingType.Mill, BuildingType.Farm }; break;
                case BuildingType.Butcher: near = new[] { BuildingType.Sheepfold }; break;
                case BuildingType.Tavern: near = new[] { BuildingType.Butcher, BuildingType.Bakery }; break;
                case BuildingType.Smelter: near = new[] { BuildingType.Mine }; break;
                case BuildingType.Smithy: near = new[] { BuildingType.Smelter }; break;
                case BuildingType.PowerPlant: near = new[] { BuildingType.Smelter, BuildingType.Mine }; break;
                case BuildingType.Barracks: near = new[] { BuildingType.Smithy, BuildingType.Tavern }; break;
                case BuildingType.Mine: near = new[] { BuildingType.Tavern }; break;
                default: return -1;
            }
            foreach (var n in near)
            {
                int id = EconomyApi.FindBuilding(s, n);
                if (id >= 0) return s.Economy.Buildings[id].Vertex;
            }
            return -1;
        }

        private bool Place(WorldState s, BuildingType t, Func<int, bool> filter)
        {
            int v = EconomyTestKit.ChooseVertex(s, t, filter, Anchor(s, t));
            if (v < 0) return false;
            var roads = EconomyTestKit.RoadsToConnect(s, v);
            var cost = (int[])EconomyApi.Def(s, t).Cost.Clone();
            var road = EconomyApi.RoadCostFor(s, roads.Count);
            for (int w = 0; w < Wares.Count; w++)
            {
                int need = cost[w] + road[w];
                if (Have((Ware)w) < need) return TradeFor(s, (Ware)w, need);
            }
            Mark("place_" + t);
            _queue.Enqueue(new PlaceBuilding { Type = t, Vertex = v });
            foreach (int e in roads) _queue.Enqueue(new PlaceRoad { Edge = e });
            return Submit(_queue.Dequeue());
        }

        /// <summary>Longest new road (in segments) the player builds to reach a city corner (two hex edges).</summary>
        private const int MaxCityRoad = 6;

        /// <summary>
        /// Best city corner: legal now, or one that only lacks roads (roads returned in placeable order); -1 if none.
        /// Value: a terrain the village has no city on yet (pasture 9, mountain 8, forest 6, hills 5, fields 3), 1 for
        /// a hex already covered; minus 1 per new road segment.
        /// </summary>
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
                    if (TerrainInfo.RawWare(terr) == null) continue;
                    bool covered = mine.Any(c => topo.VertexHexes(c).Contains(h));
                    bool haveTerrain = mine.Any(c => topo.VertexHexes(c).Any(x => s.Board.HexTerrain[x] == terr));
                    if (covered) val += 1;
                    else if (!haveTerrain) val += terr == Terrain.Pasture ? 9 : terr == Terrain.Mountain ? 8 : terr == Terrain.Forest ? 6 : terr == Terrain.Hills ? 5 : 3;
                    else val += terr == Terrain.Forest ? 4 : terr == Terrain.Hills ? 3 : 2;
                }
                return val;
            }
            int best = -1, bestScore = int.MinValue;
            List<int> bestRoads = null;
            foreach (int v in topo.Corners)
            {
                var r = StrategyApi.CheckCity(s, v);
                List<int> need;
                if (r.Ok || r.Reason == StrategyRefusals.CannotAfford) need = new List<int>();
                else if (r.Reason == StrategyRefusals.NoRoad)
                {
                    var p = EconomyTestKit.PathToNetwork(s, v);
                    if (p == null || p.Length - 1 > MaxCityRoad) continue;
                    need = topo.PathEdges(p).Where(e => !s.Economy.Roads[e]).ToList();
                    need.Reverse();
                }
                else continue;
                int score = Value(v) * 2 - need.Count;
                if (score > bestScore || (score == bestScore && v < best))
                {
                    best = v;
                    bestScore = score;
                    bestRoads = need;
                }
            }
            if (best >= 0) roads = bestRoads;
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
                    else if (terr == Terrain.Hills) t = BuildingType.ClayPit;
                    else if (terr == Terrain.Fields) t = BuildingType.Farm;
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
            return SiteCost(t, f) ?? new int[Wares.Count];
        }

        private bool Cover(WorldState s)
        {
            if (CoverTarget(s, out var t, out var f) < 0) return false;
            Mark("cover_" + t);
            return Place(s, t, f);
        }

        // ---------------- Military ----------------

        private static int UnitsTrainedOrQueued(WorldState s)
        {
            int n = MilitaryApi.Squads(s, PlayerIds.Human).Sum(x => x.Units);
            foreach (var q in s.Military.Queues) n += q.Orders.Count;
            return n;
        }

        private bool AttackIfReady(WorldState s)
        {
            // The completionist attacks the outpost with a full squad once Age III begins: a fallen outpost scores +2,
            // ends the rival's plan and raids, and leaves the rival no guards for the last comparison.
            if (!Completionist || s.Military.Outpost.Destroyed) return false;
            if (s.Strategy.Age < s.Strategy.Tuning.Ages) return false;
            var ready = MilitaryApi.Squads(s, PlayerIds.Human).Where(x => x.Order == OrderKind.None && x.Units >= s.Military.Tuning.MaxSquadSize).ToList();
            if (ready.Count == 0) return false;
            Mark("attack");
            return Submit(new OrderSquad { SquadId = ready[0].Id, Order = OrderKind.Attack, Target = s.Board.RivalOutpostVertex });
        }

        // ---------------- Rounds ----------------

        /// <summary>No forest hex at your cities has batch units left and no woodcutter is mid-cycle.</summary>
        private static bool ForestDrained(WorldState s)
        {
            var topo = s.Board.Topology;
            foreach (int c in StrategyApi.PlayerCities(s))
                foreach (int h in topo.VertexHexes(c))
                    if (s.Board.HexTerrain[h] == Terrain.Forest && s.Economy.HexBatchRemaining[h] > 0 && !s.Economy.HexStopped[h]) return false;
            foreach (var b in s.Economy.Buildings)
                if (b.Type == BuildingType.Woodcutter && b.State == BuildingState.Active && b.WorkProgress > 0) return false;
            return true;
        }

        private bool ShouldEndRound(WorldState s)
        {
            if (s.Economy.RoundStartPending) return false;
            long age = s.Tick - _roundStartTick;
            if (age < MinRoundTicks) return false;
            if (age >= MaxRoundTicks) return true;
            // Logs bind the whole economy: once every forest batch at your cities is felled, a new draw is worth more
            // than waiting (the work in progress carries on into the next round).
            if (s.Strategy.Started && ForestDrained(s)) return true;
            // End the round once this round's draw is used up and nothing is moving any more: sites still waiting
            // for wares that do not exist would only wait (the next draw brings more).
            if (s.Economy.Buildings.Any(b => b.State == BuildingState.Constructing && !b.Blocked)) return false;
            if (s.Military.Queues.Any(q => q.Orders.Count > 0 && q.Orders[0].Supplied && q.Reason == null)) return false;
            if (MilitaryApi.Squads(s, 0).Any(x => x.Order == OrderKind.Attack) && !s.Military.Outpost.Destroyed) return false;
            int store = s.Economy.StorehouseId;
            if (s.Economy.Jobs.Any(j => j.DestId != store && j.Reason == null)) return false;
            var topo = s.Board.Topology;
            foreach (var b in s.Economy.Buildings)
            {
                if (b.State != BuildingState.Active) continue;
                var def = EconomyApi.Def(s, b.Type);
                if (def.Kind == BuildingKind.Harvester)
                {
                    if (b.WorkProgress > 0) return false;
                    bool stalled = b.Reason == Reasons.OutputFull || b.Reason == Reasons.DemonOnRoute || b.Reason == Reasons.NoRoad
                                   || b.Reason == Reasons.InputMissing || b.Reason == Reasons.WaitingWare;
                    foreach (int h in topo.VertexHexes(b.Vertex))
                        if (s.Board.HexTerrain[h] == def.Terrain && s.Economy.HexBatchRemaining[h] > 0 && !stalled && !s.Economy.HexStopped[h]) return false;
                }
                if (def.Kind == BuildingKind.Producer && b.WorkProgress > 0) return false;
            }
            if (s.Economy.Carriers.Any(c => c.HasLoad)) return false;
            return true;
        }
    }
}
