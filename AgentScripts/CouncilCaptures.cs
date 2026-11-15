using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using World.Game;
using World.Sim;
using World.Sim.Encounters.Council;

/// <summary>
/// Capture and inspection driver of the council village (Docs/COUNCIL_UI.md). Reads commands from Temp/c_cmd.txt
/// (separated by ';') and runs them against the World scene in play mode:
///   open | stage | tap NAME | left | right | hour H | info | ai SECONDS | pause | resume | targets | site | focus T1,T2 | wait
/// Run: unity command script run --file AgentScripts/CouncilCaptures.cs --entry CouncilCaptures.Run
/// Screenshots: unity command capture game_view --output-path Evidence/screenshots/NAME.png
/// </summary>
public static class CouncilCaptures
{
    public static string Run()
    {
        string path = Path.Combine(Directory.GetCurrentDirectory(), "Temp", "c_cmd.txt");
        if (!File.Exists(path)) return "no Temp/c_cmd.txt";
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
        var boot = WorldBootstrap.Instance;
        int home = CouncilDirector.Instance != null ? CouncilDirector.Instance.HomeSettlement : 0;
        var enc = CouncilEncounter.Current;
        var ci = System.Globalization.CultureInfo.InvariantCulture;
        switch (a[0])
        {
            case "open": EditorSceneManager.OpenScene("Assets/Scenes/World.unity", OpenSceneMode.Single); return "opened";
            case "stage":
                CouncilDirector.Disabled = true;
                CouncilFixture.StageExample(boot.Sim.State, home, 0);
                if (boot.Views != null) boot.Views.SetView(ViewMode.FirstPerson);
                return "started " + EncounterDirectory.TryStart(CouncilEncounter.Id, null, home);
            case "tap": return "tap " + enc.TapTarget(cmd.Substring(4));
            case "left": return "left " + enc.TurnLeft();
            case "right": return "right " + enc.TurnRight();
            case "hour": DayNight.Instance.SetHour(float.Parse(a[1], ci)); return "hour";
            case "pause": boot.Clock.Pause("CouncilCapture"); return "paused";
            case "resume": boot.Clock.Resume("CouncilCapture"); return "resumed";
            case "ai": CouncilEncounter.AiDelay = float.Parse(a[1], ci); return "aidelay " + CouncilEncounter.AiDelay;
            case "focus":
                { var l = new System.Collections.Generic.List<int>(); foreach (var t in a[1].Split(',')) l.Add(int.Parse(t)); enc.FocusOn(l, 600f); return "focus"; }
            case "targets":
                {
                    var sb = new StringBuilder("targets: ");
                    foreach (var t in enc.Scene.Targets(enc.Scene.ViewCamera)) sb.Append(t.Name + "@" + t.Px.x.ToString("0") + "," + t.Px.y.ToString("0") + " r" + t.RadiusPx.ToString("0") + "; ");
                    return sb.ToString();
                }
            case "size":
                {
                    int w = int.Parse(a[1]), h = int.Parse(a[2]);
                    var asm = typeof(Editor).Assembly;
                    var sizesType = asm.GetType("UnityEditor.GameViewSizes");
                    var single = typeof(ScriptableSingleton<>).MakeGenericType(sizesType);
                    var inst = single.GetProperty("instance").GetValue(null);
                    var group = sizesType.GetMethod("GetGroup").Invoke(inst, new object[] { (int)GameViewSizeGroupType.Standalone });
                    var gvs = asm.GetType("UnityEditor.GameViewSize");
                    var gvt = asm.GetType("UnityEditor.GameViewSizeType");
                    var ctor = gvs.GetConstructor(new[] { gvt, typeof(int), typeof(int), typeof(string) });
                    var ns = ctor.Invoke(new object[] { 1, w, h, "council_" + w + "x" + h });
                    group.GetType().GetMethod("AddCustomSize").Invoke(group, new[] { ns });
                    int idx = (int)group.GetType().GetMethod("GetTotalCount").Invoke(group, null) - 1;
                    var gv = EditorWindow.GetWindow(asm.GetType("UnityEditor.GameView"));
                    gv.GetType().GetProperty("selectedSizeIndex", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic).SetValue(gv, idx);
                    return "size " + w + "x" + h + " idx " + idx;
                }
            case "near":
                {
                    var sb = new StringBuilder("near tower: ");
                    var tp = CouncilScene.Current.Site.Position;
                    foreach (var pa in UnityEngine.Object.FindObjectsByType<PlaceholderActor>(FindObjectsSortMode.None))
                        if ((pa.transform.position - tp).magnitude < 16f) sb.Append(pa.name + " " + pa.transform.position.ToString("0") + " seated " + pa.Seated + "; ");
                    return sb.ToString();
                }
            case "site":
                { var st = CouncilScene.Current != null ? CouncilScene.Current.Site : CouncilSite.Find(boot.Sim.State); return "bench " + (CouncilScene.Current != null ? CouncilScene.Current.Bench.Origin.ToString() : "-") + " site tower " + st.Position + " yaw " + st.Yaw + " " + st.Why; }
            case "info":
                {
                    var sc = CouncilApi.Get(boot.Sim.State, home);
                    var s = CouncilScene.Current;
                    string counts = "";
                    if (s != null) for (int t = 2; t <= 12; t++) { s.Counts(t, out int st, out int li, out int un); counts += t + ":" + st + "/" + li + "/" + un + " "; }
                    return "status " + (sc != null ? sc.Status + " phase " + sc.Election?.Phase + " active " + sc.Election?.Active + " temp " + (sc.Election != null ? string.Join(",", sc.Election.Temp) : "") : "none")
                        + "; enc " + (enc != null ? enc.View + " acc " + enc.Accepted + " ref " + enc.Refused + " " + enc.LastRefusal + " fov " + enc.CamFov.ToString("0") + " yaw " + enc.CamYaw.ToString("0") + " pitch " + enc.CamPitch.ToString("0") : "none")
                        + "; settled/listening/undecided " + counts + "; screen " + Screen.width + "x" + Screen.height;
                }
        }
        return "unknown " + cmd;
    }
}
