using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using World.Game;

/// <summary>
/// Opening vista capture and probe driver (local captures only, Evidence/vista/). Reads commands from Temp/vista_cmd.txt separated by ';'
/// and runs them in play mode:
///   size W H | notch on|off | info | tap ZONE | region X Y (fractions) | shot NAME | shotafter SECONDS NAME | regions NAME |
///   leave | back | hour H | cancel | welcome off | swing PIXELS
/// "regions NAME" writes a small PNG of the region map (one colour per place) beside the screenshot for checking.
/// Run: unity command script run --file AgentScripts/VistaDrive.cs --entry VistaDrive.Run
/// </summary>
public static class VistaDrive
{
    public const string Folder = "Evidence/vista";

    public static string Run()
    {
        string path = Path.Combine(Directory.GetCurrentDirectory(), "Temp", "vista_cmd.txt");
        if (!File.Exists(path)) return "no Temp/vista_cmd.txt";
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
        var v = OpeningVista.Instance;
        Directory.CreateDirectory(Folder);
        switch (a[0])
        {
            case "size":
                {
                    int w = int.Parse(a[1]), h = int.Parse(a[2]);
                    PlayModeWindow.SetCustomRenderingResolution((uint)w, (uint)h, "Vista " + w + "x" + h);
                    ScreenSafeArea.Override = null;
                    PlayModeWindow.GetRenderingResolution(out uint rw, out uint rh);
                    return "size " + rw + "x" + rh;
                }
            case "notch":
                ScreenSafeArea.Override = a[1] == "on" ? ScreenSafeArea.SimulatedNotch(Screen.width, Screen.height) : (Rect?)null;
                return "notch " + a[1];
            case "info":
                {
                    if (v == null) return "no vista";
                    var sb = new StringBuilder("bearing " + v.Bearing + " pitch " + v.Pitch + " state " + v.State + " screen " + Screen.width + "x" + Screen.height + " cam " + v.CameraPosition + " focus " + v.Focus + " | ");
                    foreach (var p in v.Places)
                    {
                        var r = v.ScreenRect(p); var c = v.ScreenCentre(p);
                        sb.Append(p.Zone + " c(" + (c.x / Screen.width).ToString("0.00") + "," + (c.y / Screen.height).ToString("0.00") + ") r[" + (r.xMin / Screen.width).ToString("0.00") + "-" + (r.xMax / Screen.width).ToString("0.00") + " x " + (r.yMin / Screen.height).ToString("0.00") + "-" + (r.yMax / Screen.height).ToString("0.00") + "] at " + p.Centre + "; ");
                    }
                    var w = PlayerWalker.Instance;
                    sb.Append(" walker " + (w != null ? w.Cell + " h" + w.Heading + " locked " + w.IsLocked : "none"));
                    return sb.ToString();
                }
            case "tap":
                {
                    var p = v.Find(a[1]);
                    var c = v.ScreenCentre(p);
                    var o = PointerInput.TapAt(c);
                    return "tap " + a[1] + " -> " + o + " state " + WorldActions.State + " vista " + v.State;
                }
            case "region":
                {
                    var pt = new Vector2(float.Parse(a[1], ci) * Screen.width, float.Parse(a[2], ci) * Screen.height);
                    var o = PointerInput.TapAt(pt);
                    return "region " + a[1] + "," + a[2] + " -> " + (v.RegionAt(pt) != null ? v.RegionAt(pt).Zone : "none") + " " + o + " vista " + v.State;
                }
            case "shot":
                {
                    string file = Path.Combine(Folder, a[1] + ".png");
                    ScreenCapture.CaptureScreenshot(file);
                    return "shot " + file;
                }
            case "shotafter":
                {
                    double at = EditorApplication.timeSinceStartup + double.Parse(a[1], ci);
                    string file = Path.Combine(Folder, a[2] + ".png");
                    EditorApplication.CallbackFunction f = null;
                    f = () => { if (EditorApplication.timeSinceStartup < at) return; EditorApplication.update -= f; ScreenCapture.CaptureScreenshot(file); };
                    EditorApplication.update += f;
                    return "shotafter " + a[1] + "s -> " + file;
                }
            case "regions":
                {
                    int W = 211, H = Mathf.Max(1, Mathf.RoundToInt(211f * Screen.height / Screen.width));
                    var tex = new Texture2D(W, H);
                    Color[] cols = { new Color(1, .85f, .2f), new Color(.3f, .5f, 1), new Color(1, .4f, .8f), new Color(.2f, .9f, .8f), new Color(.3f, .9f, .3f), new Color(1, .2f, .2f) };
                    for (int y = 0; y < H; y++)
                        for (int x = 0; x < W; x++)
                        {
                            var p = v.RegionAt(new Vector2((x + .5f) / W * Screen.width, (y + .5f) / H * Screen.height));
                            int i = p != null ? IndexOf(v, p) : -1;
                            tex.SetPixel(x, y, i >= 0 ? cols[i % cols.Length] : Color.black);
                        }
                    foreach (var p in v.Places) { var c = v.ScreenCentre(p); int cx = (int)(c.x / Screen.width * W), cy = (int)(c.y / Screen.height * H); for (int dx = -1; dx <= 1; dx++) for (int dy = -1; dy <= 1; dy++) tex.SetPixel(Mathf.Clamp(cx + dx, 0, W - 1), Mathf.Clamp(cy + dy, 0, H - 1), Color.black); }
                    File.WriteAllBytes(Path.Combine(Folder, a[1] + ".png"), tex.EncodeToPNG());
                    var sb = new StringBuilder("regions " + a[1] + ": ");
                    for (int i = 0; i < v.Places.Count; i++) sb.Append(i + "=" + v.Places[i].Zone + " ");
                    return sb.ToString();
                }
            case "pose": v.ForcePose(float.Parse(a[1], ci), float.Parse(a[2], ci)); return "pose " + a[1] + " " + a[2];
            case "choose": { var sb = new StringBuilder(); string r = v.ChooseYaw(float.Parse(a[1], ci), sb); return "choose " + r + " :: " + sb; }
            case "leave": v.Leave(); return "left";
            case "abort": if (EncounterHost.Instance != null && EncounterHost.Instance.IsRunning) EncounterHost.Instance.Abort(); foreach (int h in SpeechBubbles.OpenHandles()) SpeechBubbles.Close(h); if (DialoguePanel.IsOpen) DialoguePanel.Close(); WorldActions.Cancel(); return "aborted";
            case "boardleave": GodBoardDirector.Instance.Leave(); return "board leave";
            case "back": return "back " + OpeningVista.TryReturn();
            case "open": v.Open(true); return "open";
            case "cancel": WorldActions.Cancel(); return "cancelled";
            case "hour": DayNight.Instance.SetHour(float.Parse(a[1], ci)); return "hour";
            case "swing": v.Swing(float.Parse(a[1], ci)); return "swing";
            case "esc": return "esc " + OpeningVista.TryReturn();
            case "state":
                {
                    var w = PlayerWalker.Instance;
                    return "vista " + (v != null ? v.State.ToString() : "-") + " last " + (v != null && v.LastArrived != null ? v.LastArrived.Zone : "-") + " encounter " + (EncounterHost.Instance != null && EncounterHost.Instance.IsRunning)
                        + " board " + (GodBoardDirector.Instance != null ? GodBoardDirector.Instance.View.ToString() : "-") + " actions " + WorldActions.State + " walker " + (w != null ? w.Cell + " locked " + w.IsLocked : "")
                        + " paused " + WorldBootstrap.Instance.Clock.IsPaused + " tick " + WorldBootstrap.Instance.Sim.State.Tick + " guide " + (PrinceDirector.Instance != null ? PrinceDirector.Instance.Guide.ToString() : "-")
                        + " welcoming " + (v != null && v.Welcoming) + " pointed " + (v != null ? string.Join(",", v.Pointed) : "");
                }
        }
        return "unknown " + cmd;
    }

    static int IndexOf(OpeningVista v, VistaPlace p) { for (int i = 0; i < v.Places.Count; i++) if (v.Places[i] == p) return i; return -1; }
}
