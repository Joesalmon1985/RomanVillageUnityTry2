using System;
using System.Collections.Generic;
using World.Sim.Table;

namespace World.Sim.Historic
{
    /// <summary>
    /// The Historic age of a god board game (PRD_GODBOARD 9): the switch (HI-1, HI-2), castles (HI-3), finite resources and
    /// the peoples' Historic economies (HI-5), run by <see cref="HistoricPlanner"/> for every people (HI-4, HI-6).
    /// Deterministic: no random numbers, every choice is ordered by id.
    /// <para>
    /// Carriers: each people has a number of carriers; a carrier takes one unit from a store, a building's output, to a
    /// site, a workshop or the castle, and is busy for <see cref="HistoricTuning.CarryBaseTicks"/> plus
    /// <see cref="HistoricTuning.CarryPerHexTicks"/> per hex step between the building's hex and the store at the castle.
    /// This is a compact stand-in for the prehistoric logistics (<c>Economy/Logistics.cs</c>), whose camps, plots and main
    /// roads do not exist for the table's settlements (the economy keeps only the four start camps on the god board).
    /// </para>
    /// </summary>
    public static class HistoricRules
    {
        // ---------------------------------------------------------------- the switch

        /// <summary>
        /// HI-1, HI-2, HI-3, HI-5: sets up the Historic age once (does nothing when it already began). The table must have
        /// ended. Raises HistoricBegan, TemplesTornDown and one CastleStageStarted per people.
        /// </summary>
        public static void Begin(WorldState s, int winner, EventBuffer ev)
        {
            if (s == null || s.Historic != null || s.Board == null) return;
            var tu = new HistoricTuning();
            var h = new HistoricState { Tuning = tu, StartTick = s.Tick, Winner = winner, TableClosed = true };
            var t = s.Table;
            int n = s.PeopleCount;
            h.TempleCorners = new int[n];
            for (int p = 0; p < n; p++)
                h.TempleCorners[p] = t != null && p < t.Peoples.Count ? t.Peoples[p].StartCorner
                    : (s.Board.StartCorners != null && p < s.Board.StartCorners.Length ? s.Board.StartCorners[p] : -1);
            h.Hexes = InitHexes(s.Board, tu);
            for (int p = 0; p < n; p++)
            {
                var hp = new HistoricPeople { Id = p, LastWoodTick = s.Tick, NextPlanTick = s.Tick + p * 7 };
                hp.StoreCorner = CastleCorner(s, p);
                hp.Castle.Corner = hp.StoreCorner;
                hp.Carriers = tu.CarriersBase + Holdings(s, p);
                for (int w = 0; w < Wares.Count && w < tu.StartStock.Length; w++)
                {
                    hp.Stock[w] = tu.StartStock[w];
                    h.Ledger.Starting[w] += tu.StartStock[w];
                }
                h.Peoples.Add(hp);
            }
            s.Historic = h;
            ev?.Add(new HistoricBegan { Winner = winner });
            ev?.Add(new TemplesTornDown { Corners = (int[])h.TempleCorners.Clone() });
            foreach (var hp in h.Peoples) ev?.Add(new CastleStageStarted { People = hp.Id, Corner = hp.Castle.Corner, Stage = 0 });
        }

        /// <summary>HI-5: trees from the pips of each Forest hex, rock on Mountain and Hills, clay on Hills, field slots on Fields.</summary>
        public static HexResources[] InitHexes(Board b, HistoricTuning tu)
        {
            int count = b.HexTerrain.Length;
            var list = new HexResources[count];
            for (int i = 0; i < count; i++)
            {
                var terrain = b.HexTerrain[i];
                int pips = BoardGenerator.Pips(b.HexNumber[i]);
                var r = new HexResources { Hex = i, Terrain = terrain, Pips = pips };
                int slots = terrain == Terrain.Forest ? tu.ForestSlots : terrain == Terrain.Meadow ? tu.MeadowSlots : 0;
                r.Trees = new int[slots];
                r.TreeNext = new long[slots];
                if (terrain == Terrain.Forest)
                {
                    int trees = Math.Min(slots, tu.TreesBase + tu.TreesPerPip * pips);
                    for (int k = 0; k < trees; k++) r.Trees[k] = (int)TreeStage.Mature;
                }
                if (terrain == Terrain.Mountain) r.Rock = tu.MountainRockBase + tu.MountainRockPerPip * pips;
                if (terrain == Terrain.Hills)
                {
                    r.Rock = tu.HillsRockBase + tu.HillsRockPerPip * pips;
                    r.Clay = r.ClayCap = tu.ClayBase + tu.ClayPerPip * pips;
                }
                int fields = terrain == Terrain.Fields ? tu.FieldsSlotsBase + pips : terrain == Terrain.Meadow ? tu.MeadowFieldSlots : 0;
                r.Fields = new int[fields];
                r.FieldNext = new long[fields];
                list[i] = r;
            }
            return list;
        }

        /// <summary>HI-3: the people's most productive settlement: highest summed pips of its table corners; ties to the lowest corner. Start corner without any.</summary>
        public static int CastleCorner(WorldState s, int p)
        {
            var t = s.Table;
            var topo = s.Board.Topology;
            int best = -1, bestPips = -1;
            if (t != null)
                for (int c = 0; c < t.CornerOwner.Length; c++)
                {
                    if (t.CornerOwner[c] != p) continue;
                    int pips = BoardGenerator.CornerPips(s.Board, topo, c);
                    if (pips > bestPips) { best = c; bestPips = pips; }
                }
            if (best >= 0) return best;
            if (t != null && p < t.Peoples.Count) return t.Peoples[p].StartCorner;
            return s.Board.StartCorners != null && p < s.Board.StartCorners.Length ? s.Board.StartCorners[p] : 0;
        }

        /// <summary>Settlements count 1 and cities 2 (the table's levels).</summary>
        public static int Holdings(WorldState s, int p)
        {
            var t = s.Table;
            if (t == null) return 1;
            int n = 0;
            for (int c = 0; c < t.CornerOwner.Length; c++) if (t.CornerOwner[c] == p) n += Math.Max(1, t.CornerLevel[c]);
            return n;
        }

        // ---------------------------------------------------------------- geometry

        private static readonly int[] DQ = { 1, 1, 0, -1, -1, 0 };
        private static readonly int[] DR = { 0, -1, -1, 0, 1, 1 };

        /// <summary>The hex's neighbours on the board, ascending id.</summary>
        public static List<int> Neighbours(Board b, int hex)
        {
            var topo = b.Topology;
            var c = topo.Hex(hex);
            var list = new List<int>(6);
            for (int d = 0; d < 6; d++)
            {
                int n = topo.HexIndex(new HexCoord(c.Q + DQ[d], c.R + DR[d]));
                if (n >= 0) list.Add(n);
            }
            list.Sort();
            return list;
        }

        /// <summary>Hex steps from a corner (its nearest touching hex) to a hex.</summary>
        public static int Distance(Board b, int corner, int hex)
        {
            var topo = b.Topology;
            int best = int.MaxValue;
            var target = topo.Hex(hex);
            foreach (int h in topo.VertexHexes(corner)) best = Math.Min(best, topo.Hex(h).DistanceTo(target));
            return best == int.MaxValue ? 0 : best;
        }

        /// <summary>
        /// The hexes a people may build on: every hex touching one of its table corners (or its castle), and their
        /// neighbours. Ascending id. Peoples may share hexes (and compete for their trees).
        /// </summary>
        public static List<int> Territory(WorldState s, int p)
        {
            var b = s.Board;
            var topo = b.Topology;
            var near = new bool[b.HexTerrain.Length];
            var t = s.Table;
            void Touch(int corner) { foreach (int h in topo.VertexHexes(corner)) near[h] = true; }
            if (t != null) for (int c = 0; c < t.CornerOwner.Length; c++) if (t.CornerOwner[c] == p) Touch(c);
            var h0 = s.Historic;
            if (h0 != null && p < h0.Peoples.Count) Touch(h0.Peoples[p].StoreCorner);
            var all = (bool[])near.Clone();
            for (int h = 0; h < near.Length; h++) if (near[h]) foreach (int n in Neighbours(b, h)) all[n] = true;
            var list = new List<int>();
            for (int h = 0; h < all.Length; h++) if (all[h]) list.Add(h);
            return list;
        }

        /// <summary>The hexes a building works: its own, then (woodcutter, forester) its neighbours ascending.</summary>
        public static List<int> Reach(WorldState s, HistoricBuilding bld)
        {
            var list = new List<int> { bld.Hex };
            if (HistoricDefs.Get(bld.Kind).ReachesNeighbours) list.AddRange(Neighbours(s.Board, bld.Hex));
            return list;
        }

        // ---------------------------------------------------------------- the tick

        /// <summary>One Historic tick: resources grow and rot, buildings work, castles rise, carriers carry, the brains plan.</summary>
        public static void Tick(WorldState s, EventBuffer ev)
        {
            var h = s.Historic;
            if (h == null) return;
            TickResources(s, h);
            for (int i = 0; i < h.Buildings.Count; i++) TickBuilding(s, h, h.Buildings[i], ev);
            foreach (var hp in h.Peoples) TickCastle(s, h, hp, ev);
            TickJobs(s, h);
            foreach (var hp in h.Peoples) MakeJobs(s, h, hp);
            foreach (var hp in h.Peoples) TrackWood(s, hp);
            foreach (var hp in h.Peoples)
            {
                if (s.Tick < hp.NextPlanTick) continue;
                hp.NextPlanTick = s.Tick + h.Tuning.PlanIntervalTicks;
                HistoricPlanner.Plan(s, hp.Id, ev);
            }
        }

        private static void TickResources(WorldState s, HistoricState h)
        {
            var tu = h.Tuning;
            long now = s.Tick;
            foreach (var r in h.Hexes)
            {
                for (int k = 0; k < r.Trees.Length; k++)
                {
                    long next = r.TreeNext[k];
                    if (next == 0 || now < next) continue;
                    switch ((TreeStage)r.Trees[k])
                    {
                        case TreeStage.Sapling: r.Trees[k] = (int)TreeStage.Young; r.TreeNext[k] = now + tu.TreeGrowTicks; break;
                        case TreeStage.Young: r.Trees[k] = (int)TreeStage.Mature; r.TreeNext[k] = 0; break;
                        case TreeStage.Stump: r.Trees[k] = (int)TreeStage.Empty; r.TreeNext[k] = 0; break;
                        default: r.TreeNext[k] = 0; break;
                    }
                }
                for (int k = 0; k < r.Fields.Length; k++)
                {
                    if (r.FieldNext[k] == 0 || now < r.FieldNext[k]) continue;
                    if (r.Fields[k] == (int)FieldStage.Sown) r.Fields[k] = (int)FieldStage.Ripe;
                    r.FieldNext[k] = 0;
                }
                if (r.ClayCap > 0 && r.Clay < r.ClayCap)
                {
                    if (r.NextClayTick == 0) r.NextClayTick = now + tu.ClayRefillTicks;
                    else if (now >= r.NextClayTick) { r.Clay++; r.NextClayTick = r.Clay < r.ClayCap ? now + tu.ClayRefillTicks : 0; }
                }
            }
        }

        /// <summary>The first slot of a stage in the building's reach (own hex first), or false.</summary>
        public static bool FindTree(WorldState s, HistoricBuilding b, TreeStage stage, out int hex, out int slot)
        {
            foreach (int x in Reach(s, b))
            {
                var r = s.Historic.Hexes[x];
                for (int k = 0; k < r.Trees.Length; k++)
                    if (r.Trees[k] == (int)stage) { hex = x; slot = k; return true; }
            }
            hex = slot = -1;
            return false;
        }

        private static int FindField(HexResources r, FieldStage stage)
        {
            for (int k = 0; k < r.Fields.Length; k++) if (r.Fields[k] == (int)stage) return k;
            return -1;
        }

        private static void TickBuilding(WorldState s, HistoricState h, HistoricBuilding b, EventBuffer ev)
        {
            var d = HistoricDefs.Get(b.Kind);
            var tu = h.Tuning;
            if (b.State == HistoricBuildingState.Site)
            {
                for (int w = 0; w < Wares.Count; w++) if (b.Delivered[w] < d.Cost[w]) return;
                if (++b.BuildProgress < d.BuildTicks) return;
                b.State = HistoricBuildingState.Active;
                b.Delivered = new int[Wares.Count];
                ev?.Add(new HistoricBuildingFinished { People = b.People, BuildingId = b.Id, Kind = b.Kind, Hex = b.Hex });
                return;
            }
            if (b.State != HistoricBuildingState.Active) return;
            var r = h.Hexes[b.Hex];
            var hp = h.Peoples[b.People];
            if (b.WorkProgress == 0)
            {
                // Start a cycle when there is work, material and room in the output store.
                switch (b.Kind)
                {
                    case HistoricKind.Woodcutter:
                        if (b.Output + tu.WoodPerTree > tu.OutputCap || !FindTree(s, b, TreeStage.Mature, out _, out _)) return;
                        b.WorkMode = 1; break;
                    case HistoricKind.Forester:
                        if (!FindTree(s, b, TreeStage.Empty, out _, out _)) return;
                        b.WorkMode = 1; break;
                    case HistoricKind.Quarry:
                        if (r.Rock <= 0)
                        {
                            b.State = HistoricBuildingState.Exhausted;
                            ev?.Add(new QuarryExhausted { People = b.People, BuildingId = b.Id, Hex = b.Hex });
                            return;
                        }
                        if (b.Output + d.OutputCount > tu.OutputCap) return;
                        b.WorkMode = 1; break;
                    case HistoricKind.ClayPit:
                        if (r.Clay <= 0 || b.Output + d.OutputCount > tu.OutputCap) return;
                        b.WorkMode = 1; break;
                    case HistoricKind.Farm:
                        if (FindField(r, FieldStage.Ripe) >= 0 && b.Output + tu.GrainPerHarvest <= tu.OutputCap) b.WorkMode = 1;
                        else if (FindField(r, FieldStage.Empty) >= 0) b.WorkMode = 2;
                        else return;
                        break;
                    default: // workshops: kiln, sheep farm
                        if (b.Output + d.OutputCount > tu.OutputCap) return;
                        for (int w = 0; w < Wares.Count; w++) if (b.Delivered[w] < d.Inputs[w]) return;
                        for (int w = 0; w < Wares.Count; w++)
                        {
                            b.Delivered[w] -= d.Inputs[w];
                            h.Ledger.Consumed[w] += d.Inputs[w];
                        }
                        b.WorkMode = 1; break;
                }
                b.WorkProgress = 1;
                return;
            }
            if (++b.WorkProgress < d.WorkTicks) return;
            b.WorkProgress = 0;
            int mode = b.WorkMode;
            b.WorkMode = 0;
            switch (b.Kind)
            {
                case HistoricKind.Woodcutter:
                    if (FindTree(s, b, TreeStage.Mature, out int fh, out int fs))
                    {
                        var fr = h.Hexes[fh];
                        fr.Trees[fs] = (int)TreeStage.Stump;
                        fr.TreeNext[fs] = s.Tick + tu.StumpRotTicks;
                        Make(h, b, Ware.Wood, tu.WoodPerTree);
                        hp.TreesFelled++;
                        ev?.Add(new TreeFelled { People = b.People, BuildingId = b.Id, Hex = fh, Slot = fs });
                    }
                    break;
                case HistoricKind.Forester:
                    if (FindTree(s, b, TreeStage.Empty, out int ph, out int ps))
                    {
                        var pr = h.Hexes[ph];
                        pr.Trees[ps] = (int)TreeStage.Sapling;
                        pr.TreeNext[ps] = s.Tick + tu.TreeGrowTicks;
                        hp.TreesPlanted++;
                        ev?.Add(new TreePlanted { People = b.People, BuildingId = b.Id, Hex = ph, Slot = ps });
                    }
                    break;
                case HistoricKind.Quarry:
                    if (r.Rock > 0) { r.Rock--; Make(h, b, Ware.Stone, d.OutputCount); }
                    break;
                case HistoricKind.ClayPit:
                    if (r.Clay > 0) { r.Clay--; Make(h, b, Ware.Clay, d.OutputCount); }
                    break;
                case HistoricKind.Farm:
                    if (mode == 1)
                    {
                        int k = FindField(r, FieldStage.Ripe);
                        if (k >= 0) { r.Fields[k] = (int)FieldStage.Empty; Make(h, b, Ware.Grain, tu.GrainPerHarvest); }
                    }
                    else
                    {
                        int k = FindField(r, FieldStage.Empty);
                        if (k >= 0) { r.Fields[k] = (int)FieldStage.Sown; r.FieldNext[k] = s.Tick + tu.FieldGrowTicks; }
                    }
                    break;
                default:
                    Make(h, b, d.Output, d.OutputCount);
                    break;
            }
        }

        private static void Make(HistoricState h, HistoricBuilding b, Ware w, int n)
        {
            b.Output += n;
            h.Ledger.Produced[(int)w] += n;
        }

        private static void TickCastle(WorldState s, HistoricState h, HistoricPeople hp, EventBuffer ev)
        {
            var c = hp.Castle;
            if (c.StagesDone >= 3) return;
            var cost = h.Tuning.CastleCost[c.StagesDone];
            for (int w = 0; w < Wares.Count; w++) if (c.Delivered[w] < cost[w]) return;
            if (++c.BuildProgress < h.Tuning.CastleStageTicks) return;
            int stage = c.StagesDone;
            c.RaisedTick[stage] = s.Tick;
            c.StagesDone++;
            c.BuildProgress = 0;
            c.Delivered = new int[Wares.Count];
            ev?.Add(new CastleStageRaised { People = hp.Id, Corner = c.Corner, Stage = stage });
            if (c.StagesDone < 3) ev?.Add(new CastleStageStarted { People = hp.Id, Corner = c.Corner, Stage = c.StagesDone });
        }

        /// <summary>Goods still wanted by the castle's current stage (cost - delivered - on the way); zero once it is finished.</summary>
        public static int CastleNeed(HistoricState h, HistoricPeople hp, int w)
        {
            var c = hp.Castle;
            if (c.StagesDone >= 3) return 0;
            return Math.Max(0, h.Tuning.CastleCost[c.StagesDone][w] - c.Delivered[w] - c.Incoming[w]);
        }

        // ---------------------------------------------------------------- carriers

        private static void TickJobs(WorldState s, HistoricState h)
        {
            for (int i = 0; i < h.Jobs.Count; i++)
            {
                var j = h.Jobs[i];
                if (++j.Progress < j.Ticks) continue;
                Arrive(s, h, j);
                h.Jobs.RemoveAt(i);
                i--;
            }
        }

        private static void Arrive(WorldState s, HistoricState h, HistoricJob j)
        {
            int w = (int)j.Ware;
            var hp = h.Peoples[j.People];
            switch (j.To)
            {
                case CarryEnd.Store:
                    hp.Stock[w]++;
                    if (j.Ware == Ware.Wood)
                    {
                        hp.LongestWoodGap = Math.Max(hp.LongestWoodGap, s.Tick - hp.LastWoodTick);
                        hp.LastWoodTick = s.Tick;
                        hp.WoodDelivered++;
                    }
                    break;
                case CarryEnd.Building:
                    var b = h.Buildings[j.ToId];
                    b.Incoming[w]--;
                    b.Delivered[w]++;
                    if (b.State == HistoricBuildingState.Site) h.Ledger.Consumed[w]++;
                    break;
                case CarryEnd.Castle:
                    hp.Castle.Incoming[w]--;
                    hp.Castle.Delivered[w]++;
                    h.Ledger.Consumed[w]++;
                    break;
            }
        }

        private static int Busy(HistoricState h, int p)
        {
            int n = 0;
            foreach (var j in h.Jobs) if (j.People == p) n++;
            return n;
        }

        private static void AddJob(WorldState s, HistoricState h, HistoricPeople hp, Ware w, CarryEnd from, int fromId, CarryEnd to, int toId, int hex)
        {
            int dist = hex >= 0 ? Distance(s.Board, hp.StoreCorner, hex) : 0;
            h.Jobs.Add(new HistoricJob
            {
                Id = h.NextJobId++, People = hp.Id, Ware = w, From = from, FromId = fromId, To = to, ToId = toId,
                Ticks = h.Tuning.CarryBaseTicks + h.Tuning.CarryPerHexTicks * dist,
            });
        }

        /// <summary>
        /// Sends free carriers, in order: outputs to the store; construction goods to sites; inputs to workshops (two
        /// cycles' worth); then the castle, keeping <see cref="HistoricTuning.SiteReserveWood"/> wood back for sites.
        /// </summary>
        private static void MakeJobs(WorldState s, HistoricState h, HistoricPeople hp)
        {
            int free = hp.Carriers - Busy(h, hp.Id);
            if (free <= 0) return;
            var list = h.Buildings;
            for (int i = 0; i < list.Count && free > 0; i++)
            {
                var b = list[i];
                if (b.People != hp.Id) continue;
                while (b.Output > 0 && free > 0)
                {
                    b.Output--;
                    AddJob(s, h, hp, HistoricDefs.Get(b.Kind).Output, CarryEnd.Building, b.Id, CarryEnd.Store, hp.Id, b.Hex);
                    free--;
                }
            }
            for (int i = 0; i < list.Count && free > 0; i++)
            {
                var b = list[i];
                if (b.People != hp.Id || b.State != HistoricBuildingState.Site) continue;
                var cost = HistoricDefs.Get(b.Kind).Cost;
                for (int w = 0; w < Wares.Count && free > 0; w++)
                    while (free > 0 && hp.Stock[w] > 0 && b.Delivered[w] + b.Incoming[w] < cost[w])
                    {
                        hp.Stock[w]--;
                        b.Incoming[w]++;
                        AddJob(s, h, hp, (Ware)w, CarryEnd.Store, hp.Id, CarryEnd.Building, b.Id, b.Hex);
                        free--;
                    }
            }
            for (int i = 0; i < list.Count && free > 0; i++)
            {
                var b = list[i];
                if (b.People != hp.Id || b.State != HistoricBuildingState.Active) continue;
                var inputs = HistoricDefs.Get(b.Kind).Inputs;
                for (int w = 0; w < Wares.Count && free > 0; w++)
                    while (free > 0 && inputs[w] > 0 && hp.Stock[w] > 0 && b.Delivered[w] + b.Incoming[w] < 2 * inputs[w])
                    {
                        hp.Stock[w]--;
                        b.Incoming[w]++;
                        AddJob(s, h, hp, (Ware)w, CarryEnd.Store, hp.Id, CarryEnd.Building, b.Id, b.Hex);
                        free--;
                    }
            }
            for (int w = 0; w < Wares.Count && free > 0; w++)
            {
                int keep = w == (int)Ware.Wood ? h.Tuning.SiteReserveWood : 0;
                while (free > 0 && hp.Stock[w] > keep && CastleNeed(h, hp, w) > 0)
                {
                    hp.Stock[w]--;
                    hp.Castle.Incoming[w]++;
                    AddJob(s, h, hp, (Ware)w, CarryEnd.Store, hp.Id, CarryEnd.Castle, hp.Id, -1);
                    free--;
                }
            }
        }

        private static void TrackWood(WorldState s, HistoricPeople hp)
        {
            if (hp.Stock[(int)Ware.Wood] == 0)
            {
                if (hp.WoodEmptySince < 0) hp.WoodEmptySince = s.Tick;
            }
            else if (hp.WoodEmptySince >= 0)
            {
                hp.LongestWoodEmpty = Math.Max(hp.LongestWoodEmpty, s.Tick - hp.WoodEmptySince);
                hp.WoodEmptySince = -1;
            }
        }

        // ---------------------------------------------------------------- building

        /// <summary>Why a people may not place a Historic site of this kind on this hex (null when it may).</summary>
        public static string SiteRefusal(WorldState s, int p, HistoricKind kind, int hex)
        {
            var h = s.Historic;
            if (h == null) return "not_historic";
            var d = HistoricDefs.Get(kind);
            if (d == null) return "unknown_kind";
            if (hex < 0 || hex >= h.Hexes.Length) return "invalid_site";
            if (!HistoricDefs.Allows(d, h.Hexes[hex].Terrain)) return "wrong_terrain";
            if (!Territory(s, p).Contains(hex)) return "out_of_reach";
            int onHex = 0;
            foreach (var b in h.Buildings) if (b.Hex == hex && b.State != HistoricBuildingState.Exhausted) onHex++;
            if (onHex >= h.Tuning.MaxBuildingsPerHex) return "hex_full";
            return null;
        }

        /// <summary>Places a construction site (no checks beyond <see cref="SiteRefusal"/>); returns its id or -1.</summary>
        public static int PlaceSite(WorldState s, int p, HistoricKind kind, int hex, EventBuffer ev)
        {
            if (SiteRefusal(s, p, kind, hex) != null) return -1;
            var h = s.Historic;
            var b = new HistoricBuilding { Id = h.Buildings.Count, People = p, Kind = kind, Hex = hex, State = HistoricBuildingState.Site, PlacedTick = s.Tick };
            h.Buildings.Add(b);
            ev?.Add(new HistoricSitePlaced { People = p, BuildingId = b.Id, Kind = kind, Hex = hex });
            return b.Id;
        }
    }
}
