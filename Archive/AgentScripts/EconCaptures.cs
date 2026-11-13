using System.Linq;
using UnityEngine;
using World.Game;
using World.Sim;
using World.Sim.Economy;

/// <summary>
/// Puts the running game (Play Mode) into the road-economy capture states. Run an entry, wait a frame, then
///   unity command capture game_view --output-path Evidence/screenshots/econ_<name>.png
///   unity command script run --file AgentScripts/EconCaptures.cs --entry EconCaptures.GodHome
/// Commands go through GodCommands (the same path as the tools). Fast-forwarding uses GameClock.StepOnce so the
/// views see every tick.
/// </summary>
public static class EconCaptures
{
    static WorldBootstrap B => WorldBootstrap.Instance;
    static WorldState S => B.Sim.State;

    static void Reset(bool keepView = false)
    {
        DialoguePanel.Close();
        if (Hud.MenuOpen) B.Hud.CloseMenu();
        if (EncounterHost.Instance.IsRunning) EncounterHost.Instance.Abort();
        if (!keepView) B.Views.SetView(ViewMode.FirstPerson);
        B.Picker.ClearSelection();
        B.Avatar.Teleport(B.Layout.Spawn + Vector3.up * 0.05f, B.Layout.SpawnYaw, 3f);
        if (GodToolbar.Instance != null) GodToolbar.Instance.Activate(GodToolbar.SelectId);
    }

    public static string Pause() { B.Clock.Pause("Capture"); return "paused"; }
    public static string Resume() { B.Clock.Resume("Capture"); return "running"; }

    public static string Step(int ticks)
    {
        for (int i = 0; i < ticks; i++) B.Clock.StepOnce();
        return "tick " + S.Tick;
    }

    public static string Status()
    {
        var v = EconomyViews.Instance;
        var e = S.Economy;
        int loaded = e.Carriers.Count(c => c.HasLoad);
        return "tick " + S.Tick + " round " + e.Round + " buildings " + e.Buildings.Count(b => b.State != BuildingState.Removed)
            + " roads " + e.Roads.Count(r => r) + " carriers " + e.Carriers.Count + " (loaded " + loaded + ")"
            + " views: b" + v.BuildingViewCount + " r" + v.RoadViewCount + " c" + v.CarrierViewCount + " f" + v.FlagViewCount + " batch" + v.BatchMarkerCount
            + " commission " + EconomyText.CommissionProgress(S) + (e.Commission.Complete ? " COMPLETE" : "")
            + " fps~" + (1f / Mathf.Max(1e-4f, Time.smoothDeltaTime)).ToString("0");
    }

    public static string GodHome(float size = 115f)
    {
        Reset();
        B.Views.SetView(ViewMode.God);
        B.GodCam.SetFocus(B.Layout.VillageCentre, size);
        return Status();
    }

    public static string GodAt(float x, float z, float size)
    {
        Reset(true);
        B.Views.SetView(ViewMode.God);
        B.GodCam.SetFocus(new Vector3(x, 0, z), size);
        return "focus " + x + "," + z;
    }

    /// <summary>Plays the commission through the God View tools at speed (same driver as the AutoPlay script).</summary>
    public static string FastCommission(int maxTicks = 4000)
    {
        var d = new CommissionDriver(B);
        int n = 0;
        while (!S.Economy.Commission.Complete && n < maxTicks && d.Failure == null)
        {
            d.Act();
            B.Clock.StepOnce();
            n++;
        }
        return (d.Failure ?? "ok") + " after " + n + " ticks; " + Status();
    }

    /// <summary>Runs the commission driver until the given tick (for a mid-commission scene).</summary>
    public static string CommissionUntil(int tick)
    {
        var d = new CommissionDriver(B);
        while (S.Tick < tick && d.Failure == null && !S.Economy.Commission.Complete) { d.Act(); B.Clock.StepOnce(); }
        return (d.Failure ?? "ok") + "; " + Status();
    }

    /// <summary>Builds the late-game economy (LateGame driver: about 30+ buildings, about 50 carriers).</summary>
    public static string LateGame(int maxTicks = 20000)
    {
        var d = new LateGameDriver(B);
        int n = 0;
        while (!d.Done && n < maxTicks && d.Failure == null) { d.Act(); B.Clock.StepOnce(); n++; }
        return (d.Failure ?? "ok") + " after " + n + " ticks; " + Status();
    }

    /// <summary>First person standing beside a road, a loaded carrier walking towards the camera (outside the village).</summary>
    public static string FirstPersonCarrier()
    {
        Reset();
        B.Clock.Pause("Capture");
        var v = EconomyViews.Instance;
        var home = B.Layout.Home;
        CarrierView best = null;
        foreach (var c in v.Carriers)
        {
            var sc = S.Economy.Carriers[c.CarrierId];
            if (!c.HasLoad || sc.CurrentEdge < 0 || sc.Progress > S.Economy.Tuning.TicksPerEdge / 2) continue;
            if (BoardSpace.Flat(BoardSpace.EdgeMid(sc.CurrentEdge) - home) < 22f) continue;
            best = c; break;
        }
        if (best == null) return "no loaded carrier on a road outside the village";
        var sim = S.Economy.Carriers[best.CarrierId];
        var a = BoardSpace.Vertex(sim.FromVertex); var b = BoardSpace.Vertex(sim.ToVertex);
        var dir = (b - a).normalized; var side = new Vector3(dir.z, 0, -dir.x);
        float u = sim.Progress / (float)S.Economy.Tuning.TicksPerEdge;
        var eye = Vector3.Lerp(a, b, Mathf.Min(1f, u + 0.5f)) + side * 3.2f;
        for (int i = 0; i < 3; i++) B.Clock.StepOnce();
        B.Avatar.Teleport(eye + Vector3.up * 0.05f, BoardSpace.YawOf(best.transform.position - eye), 0f);
        B.Avatar.LookAt(best.CratePosition - Vector3.up * 0.6f);
        return "carrier " + best.CarrierId + " with " + best.Load + " edge " + sim.CurrentEdge;
    }
    /// <summary>God View close on a loaded carrier mid-road, with it selected.</summary>
    public static string GodCarrier(float size = 22f)
    {
        Reset();
        var v = EconomyViews.Instance;
        B.Clock.Pause("Capture");
        var best = v.Carriers.Where(c => c.HasLoad && S.Economy.Carriers[c.CarrierId].CurrentEdge >= 0)
            .OrderByDescending(c => BoardSpace.Flat(c.transform.position - B.Layout.Home) > 25f ? 1 : 0).FirstOrDefault();
        if (best == null) return "no loaded carrier on a road";
        B.Views.SetView(ViewMode.God);
        B.GodCam.SetFocus(best.transform.position, size);
        B.Picker.Select(best);
        return "carrier " + best.CarrierId + " " + best.Load;
    }

    public static string BuildMenu(string type = "BrickKiln")
    {
        Reset();
        B.Views.SetView(ViewMode.God);
        B.GodCam.SetFocus(B.Layout.Home, 75f);
        var t = (BuildTool)GodToolbar.Instance.Activate(BuildTool.ToolId);
        t.SelectType((BuildingType)System.Enum.Parse(typeof(BuildingType), type));
        t.Refresh();
        var legal = t.LegalFlags;
        if (legal.Count > 0) t.ForceHover(legal[legal.Count / 2]);
        return "legal " + legal.Count;
    }

    public static string BuildRefusal(string type = "ClayPit")
    {
        Reset();
        B.Views.SetView(ViewMode.God);
        B.GodCam.SetFocus(B.Layout.VillageCentre, 115f);
        var t = (BuildTool)GodToolbar.Instance.Activate(BuildTool.ToolId);
        t.SelectType((BuildingType)System.Enum.Parse(typeof(BuildingType), type));
        for (int v = 0; v < S.Board.Topology.VertexCount; v++)
            if (EconomyApi.IsInTerritory(S, PlayerIds.Human, v) && !EconomyApi.CheckPlacement(S, PlayerIds.Human, t.Selected, v).Ok && EconomyApi.BuildingAt(S, v) < 0)
            { t.ForceHover(v); return "hover " + v + ": " + EconomyApi.CheckPlacement(S, PlayerIds.Human, t.Selected, v).Message; }
        return "no refusal found";
    }

    public static string RoadTool()
    {
        Reset();
        B.Views.SetView(ViewMode.God);
        B.GodCam.SetFocus(B.Layout.VillageCentre, 115f);
        GodToolbar.Instance.Activate(global::World.Game.RoadTool.ToolId);
        return "road tool";
    }

    /// <summary>Selects a stalled site (prefers one waiting for wares or a road) in God View, close up.</summary>
    public static string StalledSite()
    {
        Reset();
        var v = EconomyViews.Instance;
        var site = v.Buildings.Where(b => b.IsSite).OrderBy(b => S.Economy.Buildings[b.BuildingId].Reason == null ? 1 : 0).FirstOrDefault();
        if (site == null) return "no site";
        B.Views.SetView(ViewMode.God);
        B.GodCam.SetFocus(site.transform.position, 30f);
        B.Picker.Select(site);
        return site.Title + ": " + EconomyApi.WaitingFor(S, site.BuildingId).Sentence;
    }

    /// <summary>Plans a site with no road to it (it stalls on no_road), for the stalled-site capture.</summary>
    public static string PlanUnconnectedSite(string type = "Mill")
    {
        var t = (BuildingType)System.Enum.Parse(typeof(BuildingType), type);
        var legal = EconomyApi.LegalBuildFlags(S, t);
        var topo = S.Board.Topology;
        foreach (int f in legal.OrderByDescending(f => topo.Distance(f, S.Board.HomeCityVertex)))
        {
            bool touches = false;
            foreach (int e in topo.VertexEdges(f)) if (S.Economy.Roads[e]) touches = true;
            if (touches) continue;
            var r = GodCommands.PlaceBuilding(t, f);
            return r.Ok ? "planned " + t + " at " + f : r.Message;
        }
        return "no flag";
    }

    public static string Problems()
    {
        Reset();
        B.Views.SetView(ViewMode.God);
        GodToolbar.Instance.Activate(ProblemsTool.ToolId);
        return EconomyGodTools.Problems.Current.Count + " problems";
    }

    public static string Wares()
    {
        Reset();
        B.Views.SetView(ViewMode.God);
        GodToolbar.Instance.Activate(WaresTool.ToolId);
        return "wares";
    }

    public static string Building(string type)
    {
        Reset();
        var t = (BuildingType)System.Enum.Parse(typeof(BuildingType), type);
        var v = EconomyViews.Instance.Buildings.FirstOrDefault(b => b.Type == t);
        if (v == null) return "none";
        B.Views.SetView(ViewMode.God);
        B.GodCam.SetFocus(v.transform.position, 20f);
        B.Picker.Select(v);
        return v.Title;
    }

    /// <summary>First person in front of a building's sign, looking at the building.</summary>
    public static string FirstPersonBuilding(string type)
    {
        Reset();
        var t = (BuildingType)System.Enum.Parse(typeof(BuildingType), type);
        var v = EconomyViews.Instance.Buildings.FirstOrDefault(b => b.Type == t);
        if (v == null) return "none";
        var flag = v.FlagPosition;
        var side = Vector3.Cross(Vector3.up, v.Plot.Into);
        var eye = flag - v.Plot.Into * 7f + side * 3f;
        B.Avatar.Teleport(eye + Vector3.up * 0.05f, BoardSpace.YawOf(v.transform.position - eye), 4f);
        return v.Title;
    }

    public static string TalkToBram()
    {
        Reset();
        var qm = Npc.Find(NpcIds.Quartermaster);
        B.Avatar.Teleport(qm.transform.position + qm.transform.forward * 2.5f, BoardSpace.YawOf(-qm.transform.forward), 5f);
        qm.Interact();
        return string.Join(" | ", DialoguePanel.Instance.CurrentLines);
    }

    /// <summary>Resumes the clock and measures fps (AutoPlay MeasureFps lines in the console) in God View and first person.</summary>
    public static string MeasurePerf()
    {
        Reset();
        B.Clock.Resume("Capture");
        var L = B.Layout;
        var s = new AutoPlayScript("econ_perf");
        s.Call(() => { B.Views.SetView(ViewMode.God); B.GodCam.SetFocus(L.VillageCentre, 115f); })
         .Wait(1f).MeasureFps("late game god view home (size 115)", 5f)
         .Call(() => B.GodCam.SetFocus(L.VillageCentre, 45f)).Wait(0.5f).MeasureFps("late game god view close (size 45)", 4f)
         .Call(() => B.GodCam.FrameIsland()).Wait(0.5f).MeasureFps("late game god view whole island", 4f)
         .Call(() => { B.Views.SetView(ViewMode.FirstPerson); B.Avatar.Teleport(L.Spawn + Vector3.up * 0.05f, BoardSpace.YawOf(L.Home - L.Spawn) + 160f, 3f); })
         .Wait(1f).MeasureFps("late game first person square", 4f)
         .Log("[AutoPlay] econ_perf done " + Status());
        B.AutoPlay.Run(s);
        return "measuring";
    }

    /// <summary>Names of renderers within r metres (ground distance) of the selected object (debugging captures).</summary>
    public static string NearSelected(float r = 3f)
    {
        var sel = B.Picker.Selected as Component;
        if (sel == null) return "nothing selected";
        var p = sel.transform.position;
        var names = new System.Collections.Generic.List<string>();
        foreach (var rd in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
        {
            if (!rd.enabled || !rd.gameObject.activeInHierarchy) continue;
            if (BoardSpace.Flat(rd.bounds.center - p) > r) continue;
            var t = rd.transform; string path = t.name;
            while (t.parent != null && path.Length < 120) { t = t.parent; path = t.name + "/" + path; }
            names.Add(path);
        }
        return "beacons " + BeaconRegistry.All.Count + "; " + string.Join(" | ", names.Take(40));
    }

    /// <summary>All 20 building models in a labelled 5 x 4 grid on an empty meadow hex (a view check, not game content).</summary>
    public static string ModelGallery(bool god = true)
    {
        Reset();
        var board = S.Board; var topo = board.Topology;
        int hex = -1; float bd = float.MaxValue;
        for (int h = 0; h < topo.HexCount; h++)
            if (board.HexTerrain[h] == World.Sim.Terrain.Meadow)
            {
                float d = BoardSpace.Flat(BoardSpace.Hex(topo, h) - B.Layout.Home);
                if (d > 120f && d < bd) { bd = d; hex = h; }
            }
        var old = GameObject.Find("ModelGallery");
        if (old != null) Object.Destroy(old);
        var root = new GameObject("ModelGallery").transform;
        var c = BoardSpace.Hex(topo, hex);
        int i = 0;
        foreach (BuildingType t in System.Enum.GetValues(typeof(BuildingType)))
        {
            var p = c + new Vector3(((i % 5) - 2) * 15f, 0.02f, (1.5f - i / 5) * 15f);
            var holder = new GameObject(t.ToString()).transform;
            holder.SetParent(root, false);
            holder.position = p;
            holder.rotation = Quaternion.Euler(0, 180f, 0);
            var m = BuildingModels.Build(t, holder);
            if (m.Glow != null) m.Glow.enabled = true;
            float half = BuildingPlots.HalfFor(EconomyApi.Def(S, t).Size);
            var mb = new MeshBuilder();
            TerrainBuilder.Ring(mb, p + Vector3.up * 0.05f, half * 1.41f, 0.15f, 24, Color.white);
            mb.ToObject("Footprint", holder, MaterialFactory.CreateUnlit(Color.white));
            var tm = Signs.FlatText(root, EconomyApi.Def(S, t).Name, p + new Vector3(0, 0.3f, -half - 2.2f), 1.4f, Color.white);
            i++;
        }
        if (god) { B.Views.SetView(ViewMode.God); B.GodCam.SetFocus(c, 42f); }
        else
        {
            var eye = c + new Vector3(0, 0, -48f);
            B.Avatar.Teleport(eye, 0f, -8f);
        }
        return "gallery on hex " + hex + " at " + c;
    }

    public static string Describe()
    {
        var s = S;
        var sb = new System.Text.StringBuilder();
        foreach (var b in s.Economy.Buildings) sb.Append(b.Type).Append(':').Append(b.State).Append(b.Reason != null ? "(" + b.Reason + ")" : "").Append(' ');
        sb.Append(" | outcome ").Append(s.Strategy.Outcome).Append(' ').Append(s.Strategy.OutcomeReason).Append(" breaches ").Append(s.Strategy.Breaches)
          .Append(" cities ").Append(s.Economy.Cities.Count).Append(" age ").Append(s.Strategy.Age).Append(" draft ").Append(World.Sim.Strategy.StrategyApi.DraftPending(s))
          .Append(" farm flag ").Append(CommissionDriver.ChooseVertex(s, BuildingType.Farm, null))
          .Append(" vp ").Append(World.Sim.Strategy.StrategyApi.ScoreText(s, 0)).Append(" rival ").Append(World.Sim.Strategy.StrategyApi.ScoreText(s, 1));
        return sb.ToString();
    }

    public static string Fps() =>Screen.width + "x" + Screen.height + " fps ~ " + (1f / Time.smoothDeltaTime).ToString("0");
}

/// <summary>
/// Builds a late-game economy for performance captures through commands only (GodCommands): plays the commission,
/// picks draft cards, moves the hero onto demons near the network, founds cities at the nearest legal corners and
/// places buildings (the full chain, then more of the busy ones) until about 32 buildings and 50 carriers stand.
/// Ends the round when it has nothing affordable to do.
/// </summary>
public sealed class LateGameDriver
{
    private readonly WorldBootstrap _b;
    private readonly CommissionDriver _commission;
    private readonly System.Collections.Generic.Queue<System.Func<CommandResult>> _queue = new System.Collections.Generic.Queue<System.Func<CommandResult>>();
    private long _roundStart;
    private int _round;
    public string Failure { get; private set; }
    public int TargetBuildings = 32, TargetCarriers = 50;

    private static readonly BuildingType[] Plan =
    {
        BuildingType.Farm, BuildingType.Mill, BuildingType.Bakery, BuildingType.Sheepfold, BuildingType.Butcher, BuildingType.Tavern,
        BuildingType.Mine, BuildingType.PowerPlant, BuildingType.Smelter, BuildingType.Smithy, BuildingType.Market, BuildingType.Barracks,
        BuildingType.Woodcutter, BuildingType.Sawmill, BuildingType.ClayPit, BuildingType.BrickKiln, BuildingType.CharcoalKiln,
        BuildingType.Woodcutter, BuildingType.Farm, BuildingType.Sawmill, BuildingType.WonderSite, BuildingType.Forester,
        BuildingType.ClayPit, BuildingType.Mill, BuildingType.Woodcutter, BuildingType.Bakery, BuildingType.Sawmill, BuildingType.Farm,
    };

    public LateGameDriver(WorldBootstrap b) { _b = b; _commission = new CommissionDriver(b); }
    private WorldState S => _b.Sim.State;
    private int Live => S.Economy.Buildings.Count(x => x.State != BuildingState.Removed);
    public bool Done => Live >= TargetBuildings && S.Economy.Carriers.Count >= TargetCarriers;

    private int Count(BuildingType t) => S.Economy.Buildings.Count(x => x.Type == t && x.State != BuildingState.Removed);

    public void Act()
    {
        var s = S;
        if (Failure != null || _b.Sim.PendingCommands.Count > 0) return;
        if (s.Economy.Round != _round) { _round = s.Economy.Round; _roundStart = s.Tick; }
        if (!s.Economy.Commission.Complete) { _commission.Act(); if (_commission.Failure != null) Failure = _commission.Failure; return; }
        bool over = s.Strategy != null && s.Strategy.GameOver;
        if (over) { if (_queue.Count > 0) { Run(_queue.Dequeue()()); return; } PlanNext(); return; }
        if (World.Sim.Strategy.StrategyApi.DraftPending(s))
        {
            var hand = s.Strategy.Sides[0].DraftHand;
            if (hand.Count > 0) { Run(GodCommands.Submit(new World.Sim.Strategy.DraftPick { CardId = hand[0] }, "picked")); return; }
        }
        if (_queue.Count > 0) { Run(_queue.Dequeue()()); return; }
        if (Hero()) return;
        if (PlanNext()) return;
        if (s.Tick - _roundStart > 300 && !s.Economy.RoundStartPending && GodCommands.CheckEndRound().Ok)
            Run(GodCommands.EndRound());
    }

    private void Run(CommandResult r) { if (!r.Ok && r.Reason != EconomyRefusals.NotEnoughWares) Failure = r.Reason + ": " + r.Message; }

    private bool Hero()
    {
        var s = S;
        if (s.Strategy == null || !s.Strategy.Started || s.Strategy.HeroMovesLeft <= 0) return false;
        var topo = s.Board.Topology;
        int hero = s.Strategy.HeroVertex, best = -1, bestLen = int.MaxValue;
        foreach (var kv in World.Sim.Strategy.StrategyApi.Demons(s))
        {
            int corner = topo.IsCorner(kv.Key) ? kv.Key : topo.HexEdgeCornerA(topo.HexEdgeOfFlag(kv.Key));
            var p = topo.FindCornerPath(hero, corner);
            if (p == null) continue;
            int len = p.Length < 2 ? 1 : p.Length;
            if (len < bestLen) { bestLen = len; best = p.Length >= 2 ? p[1] : (topo.IsCorner(kv.Key) ? -1 : topo.HexEdgeCornerB(topo.HexEdgeOfFlag(kv.Key))); }
        }
        if (best < 0) return false;
        var r = GodCommands.Submit(new World.Sim.Strategy.MoveHero { Vertex = best }, "hero moved", false);
        return r.Ok;
    }

    private bool CanAfford(int[] cost)
    {
        for (int w = 0; w < Wares.Count; w++) if (cost[w] > 0 && EconomyApi.StorehouseCount(S, (Ware)w) < cost[w]) return false;
        return true;
    }

    private long _nextPlanTick;

    private bool PlanNext()
    {
        var s = S;
        if (Live >= TargetBuildings || s.Tick < _nextPlanTick) return false;
        _nextPlanTick = s.Tick + 15;
        // Land first: more cities mean more flags for buildings.
        if (s.Strategy != null && s.Strategy.Started && !s.Strategy.GameOver && s.Economy.Cities.Count(c => c.Owner == PlayerIds.Human) < 4 && FoundCity(null)) return true;
        // Wanted types in plan order (counting duplicates); place the first one that has a flag and is affordable.
        var wanted = new System.Collections.Generic.Dictionary<BuildingType, int>();
        bool needsLand = false;
        foreach (var type in Plan)
        {
            wanted[type] = (wanted.TryGetValue(type, out int n) ? n : 0) + 1;
            if (Count(type) >= wanted[type] + (EconomyLook.CommissionTypes.Contains(type) ? 1 : 0)) continue;
            var def = EconomyApi.Def(s, type);
            int v = CommissionDriver.ChooseVertex(s, type, null);
            if (v < 0) { needsLand = true; continue; }
            var roads = CommissionDriver.RoadsToConnect(s, v);
            var cost = (int[])def.Cost.Clone();
            var rc = EconomyApi.RoadCostFor(s, roads.Count);
            for (int w = 0; w < Wares.Count; w++) cost[w] += rc[w];
            if (!CanAfford(cost)) continue;
            var t = type;
            _queue.Enqueue(() => GodCommands.PlaceBuilding(t, v));
            foreach (int e in roads) _queue.Enqueue(() => GodCommands.PlaceRoad(e));
            Run(_queue.Dequeue()());
            return true;
        }
        return needsLand && FoundCity(null);
    }
    private bool FoundCity(World.Sim.Terrain? terrain)
    {
        var s = S;
        if (s.Strategy == null || !s.Strategy.Started || s.Strategy.GameOver) return false;
        var topo = s.Board.Topology;
        int best = -1; System.Collections.Generic.List<int> bestRoads = null;
        foreach (int c in s.Board.SuggestedCitySites)
        {
            var verdict = World.Sim.Strategy.StrategyApi.CheckCity(s, c);
            if (!verdict.Ok && verdict.Reason != "no_road" && verdict.Reason != "cannot_afford") continue;
            if (terrain.HasValue && !topo.VertexHexes(c).Any(h => s.Board.HexTerrain[h] == terrain.Value)) continue;
            var roads = CommissionDriver.RoadsToConnect(s, c);
            if (bestRoads == null || roads.Count < bestRoads.Count) { best = c; bestRoads = roads; }
        }
        if (best < 0) return terrain.HasValue && FoundCity(null);
        var cost = (int[])s.Strategy.Tuning.CityCost.Clone();
        var rc = EconomyApi.RoadCostFor(s, bestRoads.Count);
        for (int w = 0; w < Wares.Count; w++) cost[w] += rc[w];
        if (!CanAfford(cost)) return false;
        int site = best;
        bestRoads.Reverse(); // build outwards from the network
        foreach (int e in bestRoads) _queue.Enqueue(() => GodCommands.PlaceRoad(e));
        _queue.Enqueue(() => GodCommands.Submit(new World.Sim.Strategy.FoundCity { Vertex = site }, "city founded"));
        Run(_queue.Dequeue()());
        return true;
    }
}
