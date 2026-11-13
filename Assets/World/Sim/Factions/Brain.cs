using System;
using System.Collections.Generic;
using World.Sim.Demons;
using World.Sim.Economy;

namespace World.Sim.Factions
{
    /// <summary>
    /// The prince's brain (REFACTOR_SPEC 6): one set of rules for both peoples. Keeps a queue of at most QueueLength
    /// open proposals per faction (6.1). S-0 FIRST VERSION of the candidates (6.2): the opening, banish, improved
    /// collectors, workshops, city upgrade and warrior lodge. Not yet: expansion (roads towards and a settlement at the
    /// expansion target), the scarcest-ware bonus. Queue mechanics, speakers, declines and the rival's auto-cast are
    /// as specified.
    /// </summary>
    internal static class Brain
    {
        public const string OpeningReason = "A woodcutter's hut brings in twice the wood of the log pile, and wood builds everything.";

        private struct Candidate
        {
            public ProposalKind Kind;
            public int BuildingType, Settlement, Plot, HexEdge, Corner, DemonSiteId;
            public DemonSiteKind DemonSiteKind;
            public int Score;
            public string Reason, Speaker, Key;
        }

        /// <summary>The faction's starting settlement, or null.</summary>
        private static Settlement StartSettlement(WorldState s, int faction)
        {
            var starts = s.Board?.StartCorners;
            if (starts == null || faction < 0 || faction >= starts.Length) return null;
            var st = EconomyLogic.SettlementAt(s, starts[faction]);
            return st != null && st.Faction == faction ? st : null;
        }

        /// <summary>The opening plot: Forest sector, slot 1 of the faction's starting settlement (631 for the player on the default board), or -1.</summary>
        public static int OpeningPlot(WorldState s, int faction)
        {
            var st = StartSettlement(s, faction);
            if (st == null) return -1;
            var t = s.Board.Topology;
            for (int sec = 0; sec < 3; sec++)
            {
                int hex = PlotLayout.SectorHex(t, st.Corner, sec);
                if (hex >= 0 && s.Board.HexTerrain[hex] == Terrain.Forest) return PlotLayout.PlotId(st.Corner, sec, 1);
            }
            return -1;
        }

        public static string OpeningKey(WorldState s, int faction)
        {
            int plot = OpeningPlot(s, faction);
            return plot < 0 ? null : Proposal.MakeKey(ProposalKind.Building, (int)BuildingType.WoodcutterHut, plot, -1, -1, DemonSiteKind.Corner, -1);
        }

        /// <summary>One planning pass for a faction (6.1). ev may be null (initial plan: no events).</summary>
        public static void Plan(WorldState s, int faction, EventBuffer ev)
        {
            var f = s.Factions;
            if (f == null || f.GameOver || s.Economy == null) return;
            var tu = f.Tuning;

            var cands = Candidates(s, faction);
            var declined = new HashSet<string>();
            foreach (var d in f.Declined) if (d.Faction == faction && d.UntilTick > s.Tick) declined.Add(d.Key);
            var best = new Dictionary<string, Candidate>();
            foreach (var c in cands)
            {
                if (declined.Contains(c.Key)) continue;
                if (!best.TryGetValue(c.Key, out var old) || c.Score > old.Score) best[c.Key] = c;
            }
            var list = new List<Candidate>(best.Values);
            list.Sort((a, b) => a.Score != b.Score ? b.Score.CompareTo(a.Score) : string.CompareOrdinal(a.Key, b.Key));
            if (list.Count > tu.QueueLength) list.RemoveRange(tu.QueueLength, list.Count - tu.QueueLength);

            var oldTop = FactionLogic.Top(f, faction);
            var open = new Dictionary<string, Proposal>();
            foreach (var p in f.Proposals) if (p.Faction == faction) open[p.Key] = p;

            var kept = new List<Proposal>();
            var added = new List<Proposal>();
            for (int i = 0; i < list.Count; i++)
            {
                var c = list[i];
                if (open.TryGetValue(c.Key, out var p)) open.Remove(c.Key);
                else
                {
                    p = new Proposal
                    {
                        Id = f.NextProposalId++, Faction = faction, Kind = c.Kind, BuildingType = c.BuildingType,
                        Settlement = c.Settlement, Plot = c.Plot, HexEdge = c.HexEdge, Corner = c.Corner,
                        DemonSiteKind = c.DemonSiteKind, DemonSiteId = c.DemonSiteId, CreatedTick = s.Tick,
                    };
                    added.Add(p);
                }
                p.Score = c.Score;
                p.Reason = c.Reason;
                p.Speaker = c.Speaker;
                p.Rank = i;
                kept.Add(p);
            }

            // Open proposals not kept expire (ascending id).
            var expired = new List<Proposal>(open.Values);
            expired.Sort((a, b) => a.Id.CompareTo(b.Id));

            var all = new List<Proposal>();
            foreach (var p in f.Proposals) if (p.Faction != faction) all.Add(p);
            all.AddRange(kept);
            all.Sort((a, b) => a.Faction != b.Faction ? a.Faction.CompareTo(b.Faction) : a.Rank.CompareTo(b.Rank));
            f.Proposals = all;

            if (ev != null)
            {
                foreach (var p in expired) ev.Add(new ProposalRemoved { ProposalId = p.Id, Faction = faction, Why = "expired" });
                foreach (var p in added) ev.Add(new ProposalAdded { ProposalId = p.Id, Faction = faction, Rank = p.Rank });
            }
            var newTop = kept.Count > 0 ? kept[0] : null;
            if (newTop != null && newTop != oldTop)
            {
                newTop.TopSinceTick = s.Tick;
                ev?.Add(new ProposalsReordered { Faction = faction, TopProposalId = newTop.Id });
            }
        }

        // ---- candidates (6.2, first version) ----

        private static List<Candidate> Candidates(WorldState s, int faction)
        {
            var list = new List<Candidate>();
            var e = s.Economy;
            var f = s.Factions;
            bool defending = f.Warning.Active && f.Warning.Defender == faction;
            // Pressing an advantage: while this people is the stronger, strength builds come first.
            // (only against a people whose strength has not changed for PressIdleTicks).
            int press = 0;
            if (f.Factions.Count == PlayerIds.Count)
            {
                var me = f.Factions[faction];
                var other = f.Factions[1 - faction];
                if (me.Strength >= other.Strength + f.Tuning.PressAdvantageLead && s.Tick - other.StrengthChangedTick >= f.Tuning.PressIdleTicks)
                    press = f.Tuning.PressAdvantageBonus;
            }

            // Banish own demon sites (ignores the open-site limit).
            if (s.Demons != null)
            {
                foreach (var site in DemonApi.DemonSites(s))
                {
                    if (site.Faction != faction) continue;
                    int n = site.Count;
                    list.Add(Make(ProposalKind.Banish, -1, -1, -1, -1, -1, site.Kind, site.Id, (site.Kind == DemonSiteKind.Corner ? f.Tuning.BanishCornerScore : f.Tuning.BanishEdgeScore) + 10 * n,
                        n + (n == 1 ? " demon sits on " : " demons sit on ") + DemonApi.SiteName(s, site.Kind, site.Id) + ": " + site.Effect + ".",
                        Speakers.Prince));
                }
            }

            if (OpenSites(s, faction) >= f.Tuning.MaxOpenSites) return list;

            // The opening.
            if (!f.OpeningDone[faction])
            {
                int plot = OpeningPlot(s, faction);
                var st = StartSettlement(s, faction);
                if (plot >= 0 && st != null && EconomyLogic.CheckBuilding(s, faction, BuildingType.WoodcutterHut, plot).Ok
                    && Affordable(s, st.Id, BuildingDefs.Get(BuildingType.WoodcutterHut).Cost))
                    list.Add(Make(ProposalKind.Building, (int)BuildingType.WoodcutterHut, st.Id, plot, -1, -1, DemonSiteKind.Corner, -1, 95,
                        OpeningReason, Speakers.Prince));
            }

            var t = s.Board.Topology;
            var collects = new bool[TerrainInfo.Count];
            var buildingsPer = new Dictionary<int, int>();
            bool hasCity = false;
            foreach (var b in e.Buildings)
            {
                if (b.Faction != faction || EconomyLogic.IsGone(b)) continue;
                var def = BuildingDefs.Get(b.Type);
                if (def.IsCollector && b.State == BuildingState.Active) collects[(int)def.Terrain] = true;
                buildingsPer.TryGetValue(b.Settlement, out int k);
                buildingsPer[b.Settlement] = k + 1;
            }
            foreach (var st in e.Settlements) if (st.Faction == faction && st.State == SettlementState.Active && st.IsCity) hasCity = true;

            // Improved collectors (+10 for the scarcest raw ware of the next expansion).
            int scarce = ScarcestRaw(s, faction, collects);
            foreach (var st in e.Settlements)
            {
                if (st.Faction != faction || st.State != SettlementState.Active) continue;
                for (int sec = 0; sec < 3; sec++)
                {
                    int hex = PlotLayout.SectorHex(t, st.Corner, sec);
                    if (hex < 0) continue;
                    var terr = s.Board.HexTerrain[hex];
                    var type = BuildingDefs.ImprovedFor(terr);
                    if (!BuildingDefs.IsValid(type) || HasImproved(s, st.Id, hex)) continue;
                    var def = BuildingDefs.Get(type);
                    if (!Affordable(s, st.Id, def.Cost)) continue;
                    int plot = -1;
                    foreach (int slot in new[] { 1, 0, 2 })
                    {
                        int p = PlotLayout.PlotId(st.Corner, sec, slot);
                        if (EconomyLogic.CheckBuilding(s, faction, type, p).Ok) { plot = p; break; }
                    }
                    if (plot < 0) continue;
                    int number = s.Board.HexNumber[hex];
                    string speaker = type == BuildingType.WoodcutterHut && f.OpeningDone[faction] ? Speakers.Sawyer : Speakers.Prince;
                    var rawWare = TerrainInfo.RawWare(terr).Value;
                    list.Add(Make(ProposalKind.Building, (int)type, st.Id, plot, -1, -1, DemonSiteKind.Corner, -1,
                        40 + 2 * EconomyApi.Pips(number) + ((int)rawWare == scarce ? 10 : 0),
                        def.Name + " doubles the " + Wares.DisplayName(TerrainInfo.RawWare(terr).Value) + " we take from "
                        + TerrainInfo.DisplayName(terr) + " " + number + ".", speaker));
                }
            }

            // Workshops.
            AddWorkshop(s, faction, list, BuildingType.Sawmill, Ware.Wood, collects[(int)Terrain.Forest], 45);
            AddWorkshop(s, faction, list, BuildingType.Kiln, Ware.Clay, collects[(int)Terrain.Hills], 44);
            AddWorkshop(s, faction, list, BuildingType.Stonecutter, Ware.Stone, collects[(int)Terrain.Mountain], 42);
            AddWorkshop(s, faction, list, BuildingType.Bakery, Ware.Grain, collects[(int)Terrain.Fields], 41);
            AddWorkshop(s, faction, list, BuildingType.Smokehouse, Ware.Sheep, collects[(int)Terrain.Pasture], 40);
            AddWorkshop(s, faction, list, BuildingType.WarriorLodge, Ware.Bread, hasCity, f.Tuning.LodgeScore + press + (defending ? 50 : 0), defending);

            // City upgrade: the settlement with the most buildings (ties lower corner).
            Settlement cityTarget = null;
            int most = -1;
            foreach (var st in e.Settlements)
            {
                if (st.Faction != faction || st.State != SettlementState.Active || st.IsCity) continue;
                buildingsPer.TryGetValue(st.Id, out int n);
                if (n > most || (n == most && st.Corner < cityTarget.Corner)) { most = n; cityTarget = st; }
            }
            if (cityTarget != null && EconomyLogic.CheckCity(s, faction, cityTarget.Corner).Ok && Affordable(s, cityTarget.Id, e.Tuning.CityCost))
            {
                string reason = "A city gives us three more plots and counts double against " + Enemy(faction) + ".";
                list.Add(Make(ProposalKind.City, -1, cityTarget.Id, -1, -1, cityTarget.Corner, DemonSiteKind.Corner, -1, f.Tuning.CityScore + press + (defending ? 50 : 0),
                    defending ? reason + Gathering(faction) : reason, Speakers.Prince));
            }

            AddExpansion(s, faction, list, collects, defending, press);
            return list;
        }

        /// <summary>The other people as the brain names it in reasons.</summary>
        public static string Enemy(int faction) => faction == PlayerIds.Human ? "the Ash Clan" : "the valley people";

        private static string Gathering(int faction) =>
            " " + (faction == PlayerIds.Human ? "The Ash Clan is" : "The valley people are") + " gathering: this keeps us standing.";

        // ---- expansion (6.2) ----

        /// <summary>Corners of the faction's network: finished settlement corners and ends of its built roads.</summary>
        private static bool[] Network(WorldState s, int faction)
        {
            var t = s.Board.Topology;
            var net = new bool[t.CornerCount];
            foreach (var st in s.Economy.Settlements)
                if (st.Faction == faction && st.State == SettlementState.Active) net[st.Corner] = true;
            foreach (var r in s.Economy.Roads)
            {
                if (r.Faction != faction || r.State != RoadState.Built) continue;
                net[t.HexEdgeCornerA(r.HexEdge)] = true;
                net[t.HexEdgeCornerB(r.HexEdge)] = true;
            }
            return net;
        }

        /// <summary>An edge a road of this faction could take: no road on it, neither end holding another people's camp.</summary>
        private static bool OpenEdge(WorldState s, int faction, int e)
        {
            if (EconomyLogic.RoadOnEdge(s, e) != null) return false;
            var t = s.Board.Topology;
            var a = EconomyLogic.SettlementAt(s, t.HexEdgeCornerA(e));
            var b = EconomyLogic.SettlementAt(s, t.HexEdgeCornerB(e));
            return (a == null || a.Faction == faction) && (b == null || b.Faction == faction);
        }

        /// <summary>Hex-edge distance from the network over open edges (0 on the network, -1 beyond reach).</summary>
        private static int[] NetworkDistance(WorldState s, int faction, bool[] net, int reach)
        {
            var t = s.Board.Topology;
            var dist = new int[t.CornerCount];
            var frontier = new List<int>();
            for (int c = 0; c < dist.Length; c++)
            {
                dist[c] = net[c] ? 0 : -1;
                if (net[c]) frontier.Add(c);
            }
            for (int d = 1; d <= reach && frontier.Count > 0; d++)
            {
                var next = new List<int>();
                foreach (int c in frontier)
                    foreach (int e in t.CornerHexEdges(c))
                    {
                        if (!OpenEdge(s, faction, e)) continue;
                        int o = t.HexEdgeCornerA(e) == c ? t.HexEdgeCornerB(e) : t.HexEdgeCornerA(e);
                        if (dist[o] >= 0) continue;
                        dist[o] = d;
                        next.Add(o);
                    }
                frontier = next;
            }
            return dist;
        }

        /// <summary>A corner where a camp may stand (ignoring the road connection): interior, free, spaced.</summary>
        private static bool SettlementSpot(WorldState s, int corner)
        {
            var t = s.Board.Topology;
            if (!PlotLayout.IsSettlementCorner(t, corner) || EconomyLogic.SettlementAt(s, corner) != null) return false;
            foreach (int n in t.CornerNeighbours(corner)) if (EconomyLogic.SettlementAt(s, n) != null) return false;
            return true;
        }

        /// <summary>Value of a corner as the next camp (6.2), before the road penalty.</summary>
        public static int CornerValue(WorldState s, int corner, bool[] collects)
        {
            var t = s.Board.Topology;
            int v = 0;
            var terrains = new List<Terrain>();
            foreach (int h in t.VertexHexes(corner))
            {
                var terr = s.Board.HexTerrain[h];
                if (terr == Terrain.Meadow) continue;
                v += EconomyApi.Pips(s.Board.HexNumber[h]);
                if (terrains.Contains(terr)) continue;
                terrains.Add(terr);
                v += 3;
                if (!collects[(int)terr]) v += 6;
            }
            return v;
        }

        /// <summary>The expansion target (best corner within reach, ties lower id), its value after the road penalty, and distances.</summary>
        public static int ExpansionTarget(WorldState s, int faction, bool[] collects, out int value, out int[] dist)
        {
            var t = s.Board.Topology;
            var net = Network(s, faction);
            dist = NetworkDistance(s, faction, net, s.Factions.Tuning.ExpansionReachEdges);
            int best = -1;
            value = int.MinValue;
            for (int c = 0; c < t.CornerCount; c++)
            {
                if (dist[c] < 0 || !SettlementSpot(s, c)) continue;
                int v = CornerValue(s, c, collects) - 4 * dist[c];
                if (v > value) { value = v; best = c; }
            }
            return best;
        }

        private static void AddExpansion(WorldState s, int faction, List<Candidate> list, bool[] collects, bool defending, int press)
        {
            var t = s.Board.Topology;
            int target = ExpansionTarget(s, faction, collects, out int value, out var dist);
            if (target < 0) return;
            string where = "the " + EconomyApi.CornerTerrains(s, target) + " corner";
            if (dist[target] == 0)
            {
                if (!EconomyLogic.CheckSettlement(s, faction, target).Ok) return;
                int src = EconomyLogic.NearestConnectedSettlement(s, faction, new[] { target });
                if (src < 0 || !Affordable(s, src, s.Economy.Tuning.SettlementCost)) return;
                string reason = "A camp at " + where + " gives us " + WaresOf(s, target) + ".";
                list.Add(Make(ProposalKind.Settlement, -1, -1, -1, -1, target, DemonSiteKind.Corner, -1,
                    60 + value / 2 + s.Factions.Tuning.ExpansionBonus + press + (defending ? 50 : 0), defending ? reason + Gathering(faction) : reason, Speakers.Prince));
                return;
            }
            // The next edge of the shortest path from the network to the target (ties by lower corner ids).
            var back = new int[t.CornerCount];
            for (int i = 0; i < back.Length; i++) back[i] = -1;
            back[target] = 0;
            var frontier = new List<int> { target };
            int start = -1;
            while (frontier.Count > 0 && start < 0)
            {
                var next = new List<int>();
                foreach (int c in frontier)
                    foreach (int e in t.CornerHexEdges(c))
                    {
                        if (!OpenEdge(s, faction, e)) continue;
                        int o = t.HexEdgeCornerA(e) == c ? t.HexEdgeCornerB(e) : t.HexEdgeCornerA(e);
                        if (back[o] >= 0) continue;
                        back[o] = back[c] + 1;
                        next.Add(o);
                    }
                next.Sort();
                foreach (int o in next) if (dist[o] == 0) { start = o; break; }
                frontier = next;
            }
            if (start < 0) return;
            var options = new List<int>();
            foreach (int e in t.CornerHexEdges(start))
            {
                if (!OpenEdge(s, faction, e)) continue;
                int o = t.HexEdgeCornerA(e) == start ? t.HexEdgeCornerB(e) : t.HexEdgeCornerA(e);
                if (back[o] >= 0 && back[o] == back[start] - 1) options.Add(o);
            }
            if (options.Count == 0) return;
            options.Sort();
            int edge = t.HexEdgeBetween(start, options[0]);
            if (!EconomyLogic.CheckRoad(s, faction, edge).Ok) return;
            int source = EconomyLogic.NearestConnectedSettlement(s, faction, new[] { start, options[0] });
            if (source < 0 || !Affordable(s, source, s.Economy.Tuning.RoadCost)) return;
            list.Add(Make(ProposalKind.Road, -1, -1, -1, edge, -1, DemonSiteKind.Corner, -1, 50 + value / 2 + s.Factions.Tuning.ExpansionBonus + press,
                "A road towards " + where + " lets us found a camp there.", Speakers.Merchant));
        }

        private static string WaresOf(WorldState s, int corner)
        {
            var names = new List<string>();
            var hexes = new List<int>(s.Board.Topology.VertexHexes(corner));
            hexes.Sort();
            foreach (int h in hexes)
            {
                var w = TerrainInfo.RawWare(s.Board.HexTerrain[h]);
                if (w == null) continue;
                string n = Wares.DisplayName(w.Value);
                if (!names.Contains(n)) names.Add(n);
            }
            if (names.Count == 0) return "room to grow";
            if (names.Count == 1) return names[0];
            return string.Join(", ", names.GetRange(0, names.Count - 1)) + " and " + names[names.Count - 1];
        }

        /// <summary>True when an active workshop or lodge of the faction takes this ware.</summary>
        private static bool WorkshopInput(WorldState s, int faction, int w)
        {
            foreach (var b in s.Economy.Buildings)
                if (b.Faction == faction && b.State == BuildingState.Active && BuildingDefs.Get(b.Type).Inputs[w] > 0) return true;
            return false;
        }

        /// <summary>
        /// The scarcest raw ware for the faction's next goal (a road and a camp, and the inputs of its workshops): of the raw
        /// wares that goal needs, the one with the least unreserved stock in the faction's settlements (ties lower id). -1 when none.
        /// </summary>
        public static int ScarcestRaw(WorldState s, int faction, bool[] collects)
        {
            var tu = s.Economy.Tuning;
            int best = -1, bestStock = int.MaxValue;
            for (int w = 0; w < Wares.RawCount; w++)
            {
                if (tu.SettlementCost[w] + tu.RoadCost[w] <= 0 && !WorkshopInput(s, faction, w)) continue;
                int stock = 0;
                foreach (var st in s.Economy.Settlements)
                    if (st.Faction == faction && st.State == SettlementState.Active) stock += st.Stock[w] - st.Reserved[w];
                if (stock < bestStock) { bestStock = stock; best = w; }
            }
            return best;
        }


        private static void AddWorkshop(WorldState s, int faction, List<Candidate> list, BuildingType type, Ware firstInput, bool unlocked, int score,
            bool defending = false)
        {
            if (!unlocked) return;
            var e = s.Economy;
            var def = BuildingDefs.Get(type);
            // One of each workshop per people (shipments feed it from the other camps); a second lodge only when the
            // first is full of warriors.
            int lodgesFree = 0, same = 0;
            foreach (var b in e.Buildings)
            {
                if (b.Faction != faction || b.Type != type || EconomyLogic.IsGone(b)) continue;
                same++;
                if (type == BuildingType.WarriorLodge && ProductionSystem.WarriorsAt(s, b.Id) < e.Tuning.WarriorsPerLodge) lodgesFree++;
            }
            if (type == BuildingType.WarriorLodge ? lodgesFree > 0 : same > 0) return;
            // The settlement that collects its first input (lowest id), else the lowest id with a free plot.
            Settlement target = null;
            var terr = TerrainInfo.TerrainOf(firstInput);
            foreach (var st in e.Settlements)
            {
                if (st.Faction != faction || st.State != SettlementState.Active || FreePlot(s, faction, type, st) < 0) continue;
                if (Wares.IsRaw(firstInput) && CollectsAt(s, st.Id, terr)) { target = st; break; }
            }
            if (target == null)
                foreach (var st in e.Settlements)
                    if (st.Faction == faction && st.State == SettlementState.Active && FreePlot(s, faction, type, st) >= 0) { target = st; break; }
            if (target == null || !Affordable(s, target.Id, def.Cost)) return;
            int plot = FreePlot(s, faction, type, target);
            string reason = defending ? def.Purpose + Gathering(faction) : def.Purpose;
            string speaker = type == BuildingType.Sawmill ? Speakers.Sawyer : Speakers.Prince;
            // Surplus bonus: a pile of the first input that nothing uses makes its workshop more urgent.
            var tu = s.Factions.Tuning;
            int have = 0;
            foreach (var st in e.Settlements)
                if (st.Faction == faction && st.State == SettlementState.Active) have += st.Stock[(int)firstInput] - st.Reserved[(int)firstInput];
            score += Math.Min(tu.WorkshopSurplusCap, tu.WorkshopSurplusPerUnit * Math.Max(0, have - tu.WorkshopSurplusFloor));
            list.Add(Make(ProposalKind.Building, (int)type, target.Id, plot, -1, -1, DemonSiteKind.Corner, -1, score, reason, speaker));
        }

        /// <summary>The lowest legal plot id for a building type in a settlement, or -1.</summary>
        private static int FreePlot(WorldState s, int faction, BuildingType type, Settlement st)
        {
            foreach (int p in EconomyApi.PlotsOf(s, st.Id))
                if (EconomyLogic.CheckBuilding(s, faction, type, p).Ok) return p;
            return -1;
        }

        private static bool CollectsAt(WorldState s, int settlementId, Terrain terr)
        {
            foreach (var b in s.Economy.Buildings)
            {
                if (b.Settlement != settlementId || b.State != BuildingState.Active) continue;
                var def = BuildingDefs.Get(b.Type);
                if (def.IsCollector && def.Terrain == terr) return true;
            }
            return false;
        }

        private static bool HasImproved(WorldState s, int settlementId, int hex)
        {
            foreach (var b in s.Economy.Buildings)
            {
                if (b.Settlement != settlementId || b.Hex != hex || EconomyLogic.IsGone(b)) continue;
                if (BuildingDefs.Get(b.Type).Class == BuildingClass.ImprovedCollector) return true;
            }
            return false;
        }

        /// <summary>Unfinished sites of a faction (buildings, roads, settlements, city upgrades).</summary>
        public static int OpenSites(WorldState s, int faction)
        {
            var e = s.Economy;
            int n = 0;
            foreach (var b in e.Buildings)
                if (b.Faction == faction && (b.State == BuildingState.Site || b.State == BuildingState.Constructing)) n++;
            foreach (var r in e.Roads)
                if (r.Faction == faction && (r.State == RoadState.Site || r.State == RoadState.Constructing)) n++;
            foreach (var st in e.Settlements)
            {
                if (st.Faction != faction) continue;
                if (st.State == SettlementState.Site) n++;
                if (st.State == SettlementState.Active && st.Upgrade != UpgradeState.None) n++;
            }
            return n;
        }

        /// <summary>The cost is covered by the unreserved stock of the settlements connected to this one by the faction's roads.</summary>
        public static bool Affordable(WorldState s, int settlementId, int[] cost)
        {
            var have = new int[Wares.Count];
            foreach (int id in EconomyLogic.ConnectedSettlements(s, settlementId))
            {
                var st = s.Economy.Settlements[id];
                for (int w = 0; w < Wares.Count; w++) have[w] += st.Stock[w] - st.Reserved[w];
            }
            for (int w = 0; w < cost.Length && w < Wares.Count; w++) if (have[w] < cost[w]) return false;
            return true;
        }

        private static Candidate Make(ProposalKind kind, int type, int settlement, int plot, int hexEdge, int corner,
            DemonSiteKind siteKind, int siteId, int score, string reason, string speaker) => new Candidate
            {
                Kind = kind, BuildingType = type, Settlement = settlement, Plot = plot, HexEdge = hexEdge, Corner = corner,
                DemonSiteKind = siteKind, DemonSiteId = siteId, Score = score, Reason = reason, Speaker = speaker,
                Key = Proposal.MakeKey(kind, type, plot, hexEdge, corner, siteKind, siteId),
            };
    }

    /// <summary>Brain slot (1150): prunes expired declines, plans each faction on its tick, and auto-casts for the rival (6.4).</summary>
    internal sealed class BrainSystem : ISimSystem
    {
        public void Tick(WorldState s, EventBuffer ev)
        {
            var f = s.Factions;
            if (f == null || f.GameOver) return;
            f.Declined.RemoveAll(d => d.UntilTick <= s.Tick);
            int interval = Math.Max(1, f.Tuning.PlanIntervalTicks);
            foreach (var fa in f.Factions)
            {
                int offset = fa.Id == PlayerIds.Human ? 0 : interval / 2;
                if (s.Tick % interval != offset) continue;
                Brain.Plan(s, fa.Id, ev);
                fa.NextPlanTick = s.Tick + interval;
            }
            foreach (var fa in f.Factions)
            {
                if (!fa.Auto) continue;
                var top = FactionLogic.Top(f, fa.Id);
                if (top == null || top.TopSinceTick < 0 || s.Tick - top.TopSinceTick < f.Tuning.RivalCastDelayTicks) continue;
                if (FactionLogic.CheckAction(s, top).Ok) FactionLogic.Cast(s, top, ev);
                else FactionLogic.Remove(s, top, "expired", ev);
            }
        }
    }
}
