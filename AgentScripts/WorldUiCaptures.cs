using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using World.Game;
using World.Sim;
using World.Sim.Table;
using World.Sim.Encounters.Ward;
using World.Sim.Encounters.Council;

/// <summary>
/// WU-0 capture driver for the world-first UI (Docs/WORLD_UI_PLAN.md, P-6): the three ratios with a simulated notch
/// safe area, the zone outline on and off, the altar preview and its ledger. Reads commands from Temp/wu_cmd.txt
/// (separated by ';') and runs them in play mode:
///   godboard | size W H | notch on|off | overlay on|off | wireframe ID|auto | altar [METRES] | cancel | hour H |
///   pause | resume | shot NAME | check NAME W H | info
/// "size" sets the game view's rendering resolution and the simulated notch for it (ScreenSafeArea.Override).
/// "shot" saves Evidence/worldui/NAME.png at the end of the frame; "check" (a later call) reads the PNG header and
/// says whether its size really is W x H (the editor has saved 1280 x 720 before).
/// Run: unity command script run --file AgentScripts/WorldUiCaptures.cs --entry WorldUiCaptures.Run
/// Captures stay local (Evidence/ is never committed).
/// </summary>
public static class WorldUiCaptures
{
    public const string Folder = "Evidence/worldui";
    static WardRival _solver; static int _solved;

    public static string Run()
    {
        string path = Path.Combine(Directory.GetCurrentDirectory(), "Temp", "wu_cmd.txt");
        if (!File.Exists(path)) return "no Temp/wu_cmd.txt";
        var sb = new StringBuilder();
        foreach (string raw in File.ReadAllText(path).Split(';'))
        {
            string cmd = raw.Trim();
            if (cmd.Length == 0) continue;
            try { sb.Append(Do(cmd)).Append(" | "); }
            catch (Exception e) { sb.Append(cmd + " FAILED: " + e.GetType().Name + " " + e.Message + " | "); }
        }
        return sb.ToString();
    }

    static string Do(string cmd)
    {
        var a = cmd.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        var ci = System.Globalization.CultureInfo.InvariantCulture;
        var boot = WorldBootstrap.Instance;
        switch (a[0])
        {
            case "godboard": return "godboard " + DevStart.Begin(GodBoardStart.Id, 7UL) + " " + DevStart.LastError;
            case "size":
                {
                    int w = int.Parse(a[1]), h = int.Parse(a[2]);
                    PlayModeWindow.SetCustomRenderingResolution((uint)w, (uint)h, "WorldUi " + w + "x" + h);
                    ScreenSafeArea.Override = ScreenSafeArea.SimulatedNotch(w, h);
                    PlayModeWindow.GetRenderingResolution(out uint rw, out uint rh);
                    return "size " + rw + "x" + rh + " safe " + ScreenSafeArea.Override;
                }
            case "notch":
                ScreenSafeArea.Override = a[1] == "on" ? ScreenSafeArea.SimulatedNotch(Screen.width, Screen.height) : (Rect?)null;
                return "notch " + a[1];
            case "overlay": ZoneOutlineTool.Enabled = a[1] == "on"; return "overlay " + ZoneOutlineTool.Enabled;
            case "wireframe": ZoneOutlineTool.Wireframe = a[1] == "auto" ? null : a[1]; return "wireframe " + a[1];
            case "altar":
                {
                    var d = GodBoardDirector.Instance;
                    if (d == null || d.OwnTemple == null) return "no altar";
                    float m = a.Length > 1 ? float.Parse(a[1], ci) : 10f;
                    var spot = d.OwnTemple.AltarSpot.position;
                    var w = PlayerWalker.Instance; w.StopAuto();
                    var dir = w.Position - spot; dir.y = 0; if (dir.sqrMagnitude < 1) dir = Vector3.forward;
                    var feet = spot + dir.normalized * m; feet.y = PlayerWalker.GroundHeight(feet);
                    w.Teleport(feet, BoardSpace.YawOf(spot - feet), 10f);
                    WorldActions.Cancel();
                    var altar = d.OwnTemple.GetComponentInChildren<TempleAltar>();
                    var r = WorldActions.Tap(altar);
                    return "altar " + r + " " + WorldActions.State;
                }
            case "cancel": WorldActions.Cancel(); return "cancelled";
            case "shotafter":
                {
                    double at = EditorApplication.timeSinceStartup + double.Parse(a[1], ci);
                    string file = Path.Combine(Folder, a[2] + ".png");
                    Directory.CreateDirectory(Folder);
                    EditorApplication.CallbackFunction f = null;
                    f = () => { if (EditorApplication.timeSinceStartup < at) return; EditorApplication.update -= f; ScreenCapture.CaptureScreenshot(file); };
                    EditorApplication.update += f;
                    return "shotafter " + a[1] + "s -> " + file;
                }
            // ---- WU-1b: the hub's views ----
            case "hubturn":
                {
                    // hubturn K: the spawn cell with the spawn heading turned K sixths (negative = left); the prince's guide is off.
                    var layout = new CampLayout(boot.Sim.State.Board);
                    if (PrinceDirector.Instance != null) PrinceDirector.Instance.enabled = false;
                    WorldActions.Cancel(); GroundWalk.ClearMarker();
                    foreach (int h in SpeechBubbles.OpenHandles()) SpeechBubbles.Close(h);
                    PlayerWalker.Instance.StopAuto();
                    PlayerWalker.Instance.Teleport(layout.Spawn, ((layout.SpawnHeading + int.Parse(a[1])) % 6 + 6) % 6);
                    return "hubturn " + a[1] + " heading " + PlayerWalker.Instance.Heading;
                }
            case "tapground":
                {
                    // tapground D: tap plain ground D metres ahead on the screen (a real raycast tap).
                    var w = PlayerWalker.Instance; var cam = w.Camera;
                    float dist = a.Length > 1 ? float.Parse(a[1], ci) : 9f;
                    foreach (var off in new[] { 0f, 2f, -2f, 4f, -4f })
                    {
                        var p = w.Position + BoardSpace.DirOf(w.Yaw) * dist + new Vector3(off, 0, 0); p.y = BoardSpace.GroundY;
                        var sp = cam.WorldToScreenPoint(p);
                        if (sp.z <= 0 || !PointerInput.EmptyScene(sp)) continue;
                        var o = PointerInput.TapAt(sp);
                        return "tapground " + o + " goal " + GroundWalk.Goal + " at " + sp;
                    }
                    return "tapground: no free ground";
                }
            case "entry":
                {
                    // entry ring|pen|bench|sol: preview that entry's ledger.
                    WorldActions.Cancel();
                    IWorldAction act = null;
                    switch (a[1])
                    {
                        case "ring": act = DuelRing.Instance.GetComponentInChildren<RingTap>(); break;
                        case "pen": act = TempleForecourt.Instance.Offer; break;
                        case "bench": act = CouncilSteward.Instance.Entry; break;
                        case "sol": act = SolGuide.Instance.Entry; break;
                    }
                    return "entry " + a[1] + " " + (act != null ? WorldActions.Tap(act).ToString() : "none") + " " + WorldActions.State;
                }
            case "lookat":
                {
                    // lookat ring|pen|bench|sol DIST: stand DIST metres from that entry, looking at it.
                    var w = PlayerWalker.Instance; w.StopAuto();
                    Vector3 at; switch (a[1]) { case "ring": at = DuelRing.Instance.Frame.O; break; case "pen": at = TempleForecourt.Instance.Offer.transform.position; break; case "bench": at = CouncilSteward.Instance.Entry.transform.position; break; default: at = SolGuide.Instance.Entry.transform.position; break; }
                    float m = a.Length > 2 ? float.Parse(a[2], ci) : 10f;
                    var from = WalkLattice.ToCell(at + (new Vector3(0, 0, 0) + (WorldBootstrap.Instance.transform.position - at).normalized * 0f) + Vector3.back * m);
                    var feet = WalkLattice.ToWorld(from);
                    w.Teleport(from, WalkLattice.HeadingOf(BoardSpace.YawOf(at - feet)));
                    return "lookat " + a[1] + " cell " + from;
                }
            // ---- WU-2: the altar ledge scenarios
            case "open1":
                {
                    var d = GodBoardDirector.Instance; GodBoardDirector.RiseSeconds = 0.3f;
                    boot.Clock.Pause("WorldUiCapture");
                    bool ok = d.TrySacrifice(true); for (int i = 0; i < 3; i++) boot.Clock.StepOnce();
                    return "open1 " + ok + " " + d.View;
                }
            case "open2":
                {
                    var d = GodBoardDirector.Instance; var t = boot.Sim.State.Table;
                    if (d.View != BoardView.Board) return "not on the board: " + d.View;
                    WorldActions.Machine.MinConfirmSeconds = 0f; WorldActions.Machine.GuardSeconds = 0f;
                    d.IsSpotOffered(BuildKind.Road, 0); // refreshes the marks
                    if (t.SetupCorner < 0)
                    {
                        if (d.AdviseCorner < 0) return "no advised corner yet (view " + d.View + ")";
                        d.TapAtNow(d.ScreenOfCorner(d.AdviseCorner)); boot.Clock.StepOnce(); boot.Clock.StepOnce();
                        d.IsSpotOffered(BuildKind.Road, 0);
                    }
                    if (t.Phase == TablePhase.Setup && d.EdgeSpots.Count > 0) d.TapAtNow(d.ScreenOfEdge(d.EdgeSpots[0])); boot.Clock.StepOnce(); boot.Clock.StepOnce();
                    int g = 0; while (!(t.Phase == TablePhase.Main && t.Current == 0 && !t.Paused) && g++ < 20000) boot.Clock.StepOnce();
                    return "open2 phase " + t.Phase + " current " + t.Current;
                }
            case "tstate": { var t = boot.Sim.State.Table; var d = GodBoardDirector.Instance; return "phase " + t.Phase + " cur " + t.Current + " paused " + t.Paused + " view " + d.View + " myturn " + d.MyTurn + " corners " + d.CornerSpots.Count; }
            case "hand":
                {
                    var h = boot.Sim.State.Table.Peoples[0].Hand;
                    for (int i = 0; i < 5; i++) h[i] = int.Parse(a[1 + i]);
                    return "hand set";
                }
            case "buildmode":
                {
                    var d = GodBoardDirector.Instance; d.SetBuildMode(a[1] == "road" ? BuildKind.Road : a[1] == "camp" ? BuildKind.Settlement : BuildKind.City);
                    return "buildmode " + d.BuildMode;
                }
            case "preview":
                {
                    var d = GodBoardDirector.Instance; d.FreshModel();
                    if (d.EdgeSpots.Count == 0 && d.CornerSpots.Count == 0) return "no spots";
                    string r = a.Length > 1 && a[1] == "camp" && d.CornerSpots.Count > 0 ? d.TapAt(d.ScreenOfCorner(d.CornerSpots[0])) : d.EdgeSpots.Count > 0 ? d.TapAt(d.ScreenOfEdge(d.EdgeSpots[0])) : d.TapAt(d.ScreenOfCorner(d.CornerSpots[0]));
                    return "preview " + r;
                }
            case "tapledge":
                {
                    var hud = GodBoardDirector.Instance.GetComponent<GodBoardHud>();
                    return "tapledge " + a[1] + " " + hud.TapLedge(a[1]);
                }
            case "mat":
                {
                    var hud = GodBoardDirector.Instance.GetComponent<GodBoardHud>();
                    hud.TapLedge("trader"); hud.TapLedge("basket:0"); hud.SelectBankGet(4);
                    return "mat " + hud.MatOpen;
                }
            case "envoy":
                {
                    var t = boot.Sim.State.Table;
                    var o1 = new TradeOffer { Id = t.NextOfferId++, From = 1, To = 0, TicksLeft = 110, Status = OfferStatus.Open }; o1.Give[1] = 1; o1.Get[3] = 1;
                    var o2 = new TradeOffer { Id = t.NextOfferId++, From = 2, To = 0, TicksLeft = 40, Status = OfferStatus.Open }; o2.Give[2] = 2; o2.Get[4] = 1;
                    t.Offers.Add(o1); t.Offers.Add(o2);
                    return "envoys " + o1.Id + "," + o2.Id;
                }
            case "discard":
                {
                    var t = boot.Sim.State.Table; var h = t.Peoples[0].Hand;
                    h[0] = 4; h[1] = 3; h[2] = 2; h[3] = 3; h[4] = 2;
                    t.Phase = TablePhase.Discard; t.DiscardOwed = new int[t.PeopleCount]; t.DiscardOwed[0] = 7; t.DiscardTicksLeft = 90;
                    var hud = GodBoardDirector.Instance.GetComponent<GodBoardHud>();
                    return "discard " + hud.DiscardVisible;
                }
            case "fill":
                {
                    var hud = GodBoardDirector.Instance.GetComponent<GodBoardHud>();
                    for (int g = 0; g < 5; g++) hud.AdjustDiscard(g, 1 + 0 * g);
                    hud.AdjustDiscard(0, 1); hud.AdjustDiscard(1, 1);
                    return "sack " + hud.DiscardChosen;
                }
            case "epidemic":
                {
                    var d = GodBoardDirector.Instance;
                    d.ShowMoment("epidemic", "The gods are angry.", "Sickness runs through the camps.", 6f);
                    return "epidemic moment";
                }
            case "gameover":
                {
                    TableEngine eng = null;
                    foreach (var sys in boot.Sim.Systems) if (sys is TableEngine te) eng = te;
                    return "gameover " + (eng != null && eng.IslandFalls(boot.Sim.State, new EventBuffer()));
                }
            case "clearview": GodBoardSettings.Override = a[1] == "on"; return "clearview " + a[1];
            case "ledge":
                {
                    var hud = GodBoardDirector.Instance.GetComponent<GodBoardHud>();
                    var sb = new StringBuilder("band " + hud.LedgeBand + " safe " + ScreenSafeArea.Current() + " rects " + hud.LedgeRects.Count);
                    foreach (var kv in hud.LedgeRects) if (kv.Key == "basket:0" || kv.Key == "hourglass" || kv.Key == "lamb") sb.Append(" " + kv.Key + "=" + kv.Value);
                    sb.Append(" minside " + WorldHitTarget.MinSide(Screen.height));
                    return sb.ToString();
                }
            // ---- WU-4: the council
            case "council":
                {
                    int home = CouncilDirector.Instance != null ? CouncilDirector.Instance.HomeSettlement : 0;
                    CouncilDirector.Disabled = true;
                    CouncilFixture.StageExample(boot.Sim.State, home, 0);
                    CouncilEncounter.AiDelay = 600f;
                    if (boot.Views != null) boot.Views.SetView(ViewMode.FirstPerson);
                    return "council " + EncounterDirectory.TryStart(CouncilEncounter.Id, null, home);
                }
            case "ctap": return "ctap " + CouncilEncounter.Current.TapTarget(cmd.Substring(5)) + " state " + WorldActions.State;
            case "ctapnow": return "ctapnow " + CouncilEncounter.Current.TapTargetNow(cmd.Substring(8));
            case "cadv":
                {
                    var enc = CouncilEncounter.Current; var sc = enc.Council; var e = sc.Election;
                    if (e.Phase == ElectionPhase.Ready) enc.Do(new CouncilPush { Settlement = enc.SettlementId, ExpectedSeq = e.ActionSeq });
                    else if (e.Phase == ElectionPhase.Choosing)
                    {
                        var p = CouncilScene.BuildProposals(e)[a.Length > 1 ? int.Parse(a[1]) : 0];
                        enc.Do(new CouncilChoose { Settlement = enc.SettlementId, PushId = e.PushId, Tray = p.Tray, Socket = e.Pending[p.Tray].Mode == TrayMode.Both ? -1 : p.Socket, ExpectedSeq = e.ActionSeq });
                    }
                    return "cadv phase " + e.Phase;
                }
            case "cbank": { var enc = CouncilEncounter.Current; var e = enc.E; var r = enc.Do(new CouncilBank { Settlement = enc.SettlementId, ExpectedSeq = e.ActionSeq }); return "cbank " + r.Ok + " " + r.Reason; }
            case "cbust":
                {
                    var enc = CouncilEncounter.Current; var e = enc.E; int b0 = e.Busts;
                    for (int i = 0; i < 400 && e.Busts == b0; i++)
                    {
                        if (e.Phase == ElectionPhase.Ready) enc.Do(new CouncilPush { Settlement = enc.SettlementId, ExpectedSeq = e.ActionSeq });
                        else if (e.Phase == ElectionPhase.Choosing) { var p = CouncilScene.BuildProposals(e)[0]; enc.Do(new CouncilChoose { Settlement = enc.SettlementId, PushId = e.PushId, Tray = p.Tray, Socket = e.Pending[p.Tray].Mode == TrayMode.Both ? -1 : p.Socket, ExpectedSeq = e.ActionSeq }); }
                        else if (e.Phase == ElectionPhase.BustPending) enc.Do(new CouncilAcceptBust { Settlement = enc.SettlementId, ExpectedSeq = e.ActionSeq });
                        else break;
                    }
                    return "cbust busts " + e.Busts + " active " + e.Active + " phase " + e.Phase;
                }
            case "cinfo":
                {
                    var enc = CouncilEncounter.Current; var e = enc.E; var sc = enc.Scene;
                    var sb = new StringBuilder("council phase " + e.Phase + " active " + e.Active + " push " + sc.CamPush.ToString("0.0") + " state " + WorldActions.State + " ghosts " + sc.GhostCount + " targets: ");
                    foreach (var t in sc.Targets(sc.ViewCamera)) sb.Append(t.Name + "@" + t.Px.x.ToString("0") + "," + t.Px.y.ToString("0") + " ");
                    return sb.ToString();
                }
            case "cpole":
                {
                    var cam = ViewSwitcher.Instance != null ? ViewSwitcher.Instance.ActiveCamera : Camera.main;
                    var ray = cam.ScreenPointToRay(new Vector3(float.Parse(a[1], ci) * Screen.width, float.Parse(a[2], ci) * Screen.height, 0));
                    var sb = new StringBuilder("cam " + cam.transform.position + " hits: ");
                    foreach (var r in UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
                    {
                        if (!r.enabled || !r.gameObject.activeInHierarchy) continue;
                        if (!r.bounds.IntersectRay(ray, out float d) || d > 40f) continue;
                        var path = r.name; var t = r.transform.parent; for (int i = 0; i < 4 && t != null; i++, t = t.parent) path = t.name + "/" + path;
                        sb.Append(path + " d" + d.ToString("0.0") + " size" + r.bounds.size.ToString("0.0") + " | ");
                    }
                    return sb.ToString();
                }
            case "ccloser": CouncilScene.ForceCloser = a[1] == "on" ? true : a[1] == "off" ? false : (bool?)null; return "closer " + a[1];
            case "cleave": CouncilEncounter.Current.Return(); return "left";
            case "hour": DayNight.Instance.SetHour(float.Parse(a[1], ci)); return "hour";
            case "pause": boot.Clock.Pause("WorldUiCapture"); return "paused";
            case "resume": boot.Clock.Resume("WorldUiCapture"); return "resumed";
            case "shot":
                {
                    Directory.CreateDirectory(Folder);
                    string file = Path.Combine(Folder, a[1] + ".png");
                    ScreenCapture.CaptureScreenshot(file);
                    return "shot " + file + " (screen " + Screen.width + "x" + Screen.height + ")";
                }
            case "check":
                {
                    string file = Path.Combine(Folder, a[1] + ".png");
                    if (!File.Exists(file)) return "check " + a[1] + ": missing";
                    var head = new byte[24];
                    using (var f = File.OpenRead(file)) f.Read(head, 0, 24);
                    int pw = (head[16] << 24) | (head[17] << 16) | (head[18] << 8) | head[19];
                    int ph = (head[20] << 24) | (head[21] << 16) | (head[22] << 8) | head[23];
                    bool ok = pw == int.Parse(a[2]) && ph == int.Parse(a[3]);
                    return "check " + a[1] + " " + pw + "x" + ph + (ok ? " OK" : " WRONG SIZE");
                }
            case "where":
                {
                    var w = PlayerWalker.Instance; var d = GodBoardDirector.Instance;
                    var cam = ViewSwitcher.Instance != null ? ViewSwitcher.Instance.ActiveCamera : Camera.main;
                    var spot = d.OwnTemple.AltarSpot.position;
                    return "walker " + w.Position + " yaw " + w.Yaw + " auto " + w.IsAutoWalking + " cam " + cam.transform.position + " fwd " + cam.transform.forward + " altar " + spot + " screen " + cam.WorldToScreenPoint(spot);
                }
            // ---- WU-6: the crystal dungeon ----
            case "devhist": return "devhist " + DevStart.Begin(World.Sim.Eras.ScenarioStarts.DevHistoric) + " " + DevStart.LastError;
            case "campaign":
                {
                    var def = ExpeditionDev.LoadPack(ExpeditionDev.AuthoredPack);
                    if (a.Length > 1 && a[1] == "root")
                    {
                        var root = def.Leads.Find(x => x.Id == "earth_root");
                        root.Requires.Clear(); root.Priority = 1000;
                        foreach (var loc in def.Locations) foreach (var mk in loc.Markers) if (mk.Id == "stair") mk.OpenWhen = "";
                        foreach (var cn in def.Connections) if (cn.OpenWhen == "earth.stair_open") cn.OpenWhen = "";
                    }
                    var r = ExpeditionDev.StartCampaign(def);
                    return "campaign " + r.Ok + " " + r.Message;
                }
            case "speed": Time.timeScale = float.Parse(a[1], ci); return "speed " + Time.timeScale;
            case "facts": Interiors.FactsOverride = a[1] == "on" ? (Func<string, bool>)(f => true) : null; return "facts " + a[1];
            case "stones": FootprintStones.Mode = (FootprintStones.StoneMode)Enum.Parse(typeof(FootprintStones.StoneMode), a[1], true); return "stones " + FootprintStones.Mode;
            case "inside":
                {
                    // a[1] marker; a[2] heading (optional: the longest open run)
                    var c = InteriorScripts.CellOf(a[1]);
                    int best = 0, bestRun = -1;
                    string id = null; InteriorSpace.TryLocal(c, out id, out _, out _);
                    var m = Interiors.Get(id);
                    for (int h = 0; h < 6; h++) { int run = 0; var n = c; while (m.Floor.Contains(n + WalkLattice.Dir(h)) && run < 20) { n = n + WalkLattice.Dir(h); run++; } if (run > bestRun) { bestRun = run; best = h; } }
                    int heading = a.Length > 2 ? int.Parse(a[2]) : best;
                    PlayerWalker.Instance.StopAuto(); PlayerWalker.Instance.Teleport(c, heading);
                    return "inside " + a[1] + " heading " + heading + " run " + bestRun;
                }
            case "before":
                {
                    // a[1] marker; a[2] steps: stand that many cells from it, facing it
                    var c = InteriorScripts.CellOf(a[1]); int steps = int.Parse(a[2]);
                    InteriorSpace.TryLocal(c, out var id, out _, out _);
                    var m = Interiors.Get(id);
                    for (int h = 0; h < 6; h++)
                    {
                        bool ok = true; var n = c;
                        for (int k = 0; k < steps; k++) { n = n + WalkLattice.Dir(h); if (!m.Floor.Contains(n)) { ok = false; break; } }
                        if (!ok) continue;
                        PlayerWalker.Instance.StopAuto(); PlayerWalker.Instance.Teleport(n, h + 3);
                        return "before " + a[1] + " heading " + ((h + 3) % 6);
                    }
                    return "before: no open line";
                }
            case "tapact": { var t = InteriorScripts.Thing(a[1]).GetComponent<MiraActThing>(); WorldActions.Cancel(); return "tapact " + PointerInput.TapObject(t) + " " + WorldActions.State; }
            case "tapmate":
                {
                    var mate = ExpeditionDirector.Instance.MateOf(a[1]); WorldActions.Cancel();
                    return "tapmate " + PointerInput.TapObject(mate.Npc.GetComponent<CompanionCard>()) + " " + WorldActions.State;
                }
            case "facemate":
                {
                    var mate = ExpeditionDirector.Instance.MateOf(a[1]);
                    float m = a.Length > 2 ? float.Parse(a[2], ci) : 4f;
                    var w = PlayerWalker.Instance; w.StopAuto();
                    var at = mate.Npc.transform.position; var dir = w.Position - at; dir.y = 0; if (dir.sqrMagnitude < 1) dir = Vector3.back;
                    var feet = at + dir.normalized * m; feet.y = PlayerWalker.GroundHeight(feet);
                    w.Teleport(feet, BoardSpace.YawOf(at - feet), 6f);
                    return "facemate " + mate.Def.Name + " at " + at;
                }
            case "facemira":
                {
                    var d = ExpeditionDirector.Instance; float m = a.Length > 1 ? float.Parse(a[1], ci) : 2.5f;
                    var w = PlayerWalker.Instance; w.StopAuto();
                    var at = d.Mira.transform.position; var dir = w.Position - at; dir.y = 0; if (dir.sqrMagnitude < 1) dir = Vector3.back;
                    var feet = at + dir.normalized * m; feet.y = PlayerWalker.GroundHeight(feet);
                    w.Teleport(feet, BoardSpace.YawOf(at - feet), 0f);
                    return "facemira at " + at;
                }
            case "tappouch": WorldActions.Cancel(); return "tappouch " + PointerInput.TapObject(ExpeditionDirector.Instance.Pouch);
            case "tapbook": WorldActions.Cancel(); return "tapbook " + PointerInput.TapObject(ExpeditionDirector.Instance.Book);
            case "closemap": if (MapScreen.IsOpen) MapScreen.Close(); return "closemap";
            case "cry":
                return "cry stones " + (FootprintStones.Instance != null && FootprintStones.Instance.Visible) + " interior " + Interiors.Current + " state " + WorldActions.State + " map " + MapScreen.IsOpen
                    + " ledger " + (LedgerCard.IsOpen ? string.Join(" / ", LedgerCard.Content.AllText()) : "closed") + " party " + (ExpeditionDirector.Instance != null ? ExpeditionDirector.Instance.Phase.ToString() : "-");
            // ---- WU-7: the duel (duelstart practice|friendly [fast]; duel tap TARGET [i] | add NAME | back i | lock | cast | solve | clear on|off | info)
            case "duelstart":
                {
                    bool fast = a.Length > 2 && a[2] == "fast";
                    WardDuelEncounter.TuningOverride = new WardTuning
                    {
                        PlayerRecoveryMs = 0, MiraFirstCastMinMs = fast ? 200 : 600000, MiraFirstCastMaxMs = fast ? 300 : 600000,
                        MiraThinkMinMs = fast ? 250 : 600000, MiraThinkMaxMs = fast ? 350 : 600000,
                    };
                    _solver = null; _solved = 0;
                    if (a.Length > 1 && a[1] == "friendly") return "duelstart " + CampaignDuel.StartRequest(CampaignDuel.Friendly(WardOpponents.FriendlyOrrin, "Orrin", "orrin"));
                    return "duelstart " + EncounterDirectory.TryStart(EncounterDirectory.WardMira, Npc.Find(NpcIds.Mira));
                }
            case "duel":
                {
                    var e = WardDuelEncounter.Current;
                    if (e == null) return "no duel";
                    WorldActions.Machine.Cancel();
                    switch (a[1])
                    {
                        case "tap":
                            {
                                var t = (DuelTarget)Enum.Parse(typeof(DuelTarget), a[2]); int i = a.Length > 3 ? int.Parse(a[3]) : 0;
                                var p = e.Props.ScreenPointOf(t, i);
                                if (!p.HasValue) return "duel tap " + t + " not offered";
                                return "duel tap " + t + " -> " + e.TapAt(p.Value) + " state " + e.Props.Machine.State;
                            }
                        case "add": return "duel add " + e.AddEssence((Essence)Enum.Parse(typeof(Essence), a[2]));
                        case "back": return "duel back " + e.RemoveAt(int.Parse(a[2]));
                        case "lock": return "duel lock " + e.LockWard();
                        case "cast": { var r = e.Cast(); return "duel cast " + (r != null && r.Ok); }
                        case "solve":
                            {
                                if (_solver == null) { _solver = new WardRival(new WardTuning { Loci = e.Tuning.Loci }, e.Pool, new Rng(5)); _solved = 0; }
                                int n = a.Length > 2 ? int.Parse(a[2]) : 1, done = 0;
                                while (done < n && e.Phase == WardDuelEncounter.Screen.Active && e.Session.PlayerCastsLeft > 0 && e.Session.MsUntilPlayerMayCast == 0)
                                {
                                    var h = e.Session.PlayerHistory;
                                    while (_solved < h.Count) { var x = h[_solved++]; _solver.Observe(x.Weave, x.Fractures, x.Echoes); }
                                    foreach (var w in _solver.NextCast()) e.AddEssence((Essence)w);
                                    e.Cast(); done++;
                                }
                                return "duel solve cast " + done + " history " + e.Session.PlayerHistory.Count + " phase " + e.Phase;
                            }
                        case "close": e.Props.Machine.Cancel(); return "duel close";
                        case "dbg":
                            {
                                var sb2 = new StringBuilder();
                                foreach (var r in e.Props.GetComponentsInChildren<Renderer>(true))
                                    if (r.name.StartsWith("Bead 0") || r.name.StartsWith("Peg 0.0")) sb2.Append(r.name + " act " + r.gameObject.activeInHierarchy + " en " + r.enabled + " mat " + (r.sharedMaterial != null ? r.sharedMaterial.name + " " + r.sharedMaterial.color : "null") + " shader " + (r.sharedMaterial != null ? r.sharedMaterial.shader.name : "") + " pos " + r.transform.position + " bounds " + r.bounds.size + " verts " + r.GetComponent<MeshFilter>().sharedMesh.vertexCount + "; ");
                                return sb2.ToString();
                            }
                        case "clear": GodBoardSettings.Override = a[2] == "on"; return "clear view " + GodBoardSettings.ClearView;
                        case "ledger": return LedgerCard.IsOpen ? string.Join(" / ", LedgerCard.Content.AllText()) + " rect " + LedgerCard.LastRect : "ledger closed";
                    }
                    return "duel " + e.Phase + " building " + e.Building.Count + " history " + e.Session.PlayerHistory.Count + "/" + e.Session.RivalHistory.Count + " left " + e.Session.PlayerCastsLeft + "/" + e.Session.RivalCastsLeft
                        + " state " + e.Props.Machine.State + " rig " + e.Props.RigVisible + " screen " + Screen.width + "x" + Screen.height + " safe " + ScreenSafeArea.Current();
                }
            case "info":
                return "screen " + Screen.width + "x" + Screen.height + " safe " + ScreenSafeArea.Current() + " state " + WorldActions.State
                    + " ledger " + (LedgerCard.IsOpen ? LedgerCard.LastRect.ToString() : "closed") + " zones " + ZoneOutlineOverlay.ZonesDrawn + " objects " + ZoneOutlineOverlay.ObjectsDrawn;
            default: return "unknown " + a[0];
        }
    }
}
