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
            case "info":
                return "screen " + Screen.width + "x" + Screen.height + " safe " + ScreenSafeArea.Current() + " state " + WorldActions.State
                    + " ledger " + (LedgerCard.IsOpen ? LedgerCard.LastRect.ToString() : "closed") + " zones " + ZoneOutlineOverlay.ZonesDrawn + " objects " + ZoneOutlineOverlay.ObjectsDrawn;
            default: return "unknown " + a[0];
        }
    }
}
