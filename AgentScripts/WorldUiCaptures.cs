using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using World.Game;

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
            case "info":
                return "screen " + Screen.width + "x" + Screen.height + " safe " + ScreenSafeArea.Current() + " state " + WorldActions.State
                    + " ledger " + (LedgerCard.IsOpen ? LedgerCard.LastRect.ToString() : "closed") + " zones " + ZoneOutlineOverlay.ZonesDrawn + " objects " + ZoneOutlineOverlay.ObjectsDrawn;
            default: return "unknown " + a[0];
        }
    }
}
