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
/// Capture and inspection driver of the council (Docs/COUNCIL_UI.md). Reads commands from Temp/c_cmd.txt (separated
/// by ';') and runs them against the World scene in play mode:
///   open | stage | ex2 | tap NAME | left | right | hour H | info | listen | ai N | pause | resume
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
            case "hour": DayNight.Instance.SetHour(float.Parse(a[1], System.Globalization.CultureInfo.InvariantCulture)); return "hour";
            case "pause": boot.Clock.Pause("CouncilCapture"); return "paused";
            case "resume": boot.Clock.Resume("CouncilCapture"); return "resumed";
            case "ai": CouncilEncounter.AiDelay = float.Parse(a[1], System.Globalization.CultureInfo.InvariantCulture); return "aidelay " + CouncilEncounter.AiDelay;
            case "clear":
                {
                    var board = boot.Sim.State.Board; var lay = new CampLayout(board);
                    var res = new System.Collections.Generic.List<(float r, Vector3 p)>();
                    for (float x = -90; x <= 90; x += 2f) for (float z = -90; z <= 90; z += 2f)
                    {
                        var p = lay.Hearth + new Vector3(x, 0, z);
                        if (CampLayout.Obstruction(board, p, 0.5f) != null) continue;
                        float r = 0.5f; while (r < 14f && CampLayout.Obstruction(board, p, r + 0.5f) == null) r += 0.5f;
                        res.Add((r, p));
                    }
                    res.Sort((m, n) => n.r.CompareTo(m.r));
                    var sb = new StringBuilder("hearth " + lay.Hearth + " free spots " + res.Count + ": ");
                    for (int i = 0; i < Mathf.Min(12, res.Count); i++) sb.Append("r" + res[i].r + "@" + (res[i].p - lay.Hearth) + " d" + (res[i].p - lay.Hearth).magnitude.ToString("0") + "; ");
                    return sb.ToString();
                }
            case "ground":
                {
                    var sc2 = CouncilScene.Current; var sb = new StringBuilder("root y " + sc2.Root.position.y + ": ");
                    for (float u = 0f; u <= 1f; u += 0.25f) for (float v = 0.1f; v <= 1f; v += 0.3f)
                    {
                        var w = sc2.Root.TransformPoint(CouncilLayout.OnFloor(u, v));
                        var hits = Physics.RaycastAll(w + Vector3.up * 50f, Vector3.down, 100f);
                        string names = ""; foreach (var h in hits) names += h.collider.name + "@" + h.point.y.ToString("0.00") + " ";
                        sb.Append("(" + u + "," + v + ") " + names + "; ");
                    }
                    return sb.ToString();
                }
            case "near":
                {
                    var cam = Camera.main; var sb = new StringBuilder("cam " + cam.transform.position + ": ");
                    foreach (var r in UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
                    {
                        var b = r.bounds; if (b.Contains(cam.transform.position) || Vector3.Distance(b.ClosestPoint(cam.transform.position), cam.transform.position) < 3f) if (!r.transform.root.name.StartsWith("WorldBootstrapx") && b.size.x > 0.45f && b.size.x < 200f)
                            sb.Append(r.transform.root.name + "/" + r.transform.parent?.name + "/" + r.name + " " + b.size.ToString("0.0") + "; ");
                    }
                    return sb.ToString();
                }
            case "ray":
                {
                    var cam = Camera.main; var ray = cam.ScreenPointToRay(new Vector3(float.Parse(a[1]) * Screen.width, float.Parse(a[2]) * Screen.height, 0));
                    var sb = new StringBuilder("ray: ");
                    foreach (var r in UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
                        if (r.enabled && r.bounds.IntersectRay(ray, out float d) && d < 9f) sb.Append(r.transform.root.name + "/" + (r.transform.parent != null ? r.transform.parent.name : "") + "/" + r.name + " d" + d.ToString("0.0") + " " + r.bounds.size.ToString("0.0") + "; ");
                    return sb.ToString();
                }
            case "site":
                { var st = CouncilSite.Find(boot.Sim.State); var lay = new CampLayout(boot.Sim.State.Board); return "site " + st.Position + " d" + (st.Position - lay.Hearth).magnitude.ToString("0") + " scale " + st.Scale + " " + st.Why; }
            case "hide":
                { int n = 0; foreach (var go in UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsSortMode.None)) if (go.name == cmd.Substring(5)) { go.gameObject.SetActive(false); n++; } return "hid " + n; }
            case "wherecam":
                { var c = Camera.main; var sc3 = CouncilScene.Current; return "cam local " + sc3.Root.InverseTransformPoint(c.transform.position) + " bench origin " + sc3.Bench.Origin + " fov " + c.fieldOfView; }
            case "cams":
                { var sb = new StringBuilder(); foreach (var c in Camera.allCameras) sb.Append(c.name + " depth " + c.depth + " mask " + c.cullingMask + " rect " + c.rect + " clear " + c.clearFlags + " parent " + (c.transform.parent != null ? c.transform.parent.name : "-") + "; "); return sb.ToString(); }
            case "info":
                {
                    var sc = CouncilApi.Get(boot.Sim.State, home);
                    var s = CouncilScene.Current;
                    return "site " + (s != null ? s.Site.Position + " yaw " + s.Site.Yaw + " scale " + s.Site.Scale + " " + s.Site.Why : "none")
                        + "; status " + (sc != null ? sc.Status + " phase " + sc.Election?.Phase + " active " + sc.Election?.Active + " temp " + (sc.Election != null ? string.Join(",", sc.Election.Temp) : "") : "none")
                        + "; enc " + (enc != null ? enc.View + " acc " + enc.Accepted + " ref " + enc.Refused + " " + enc.LastRefusal : "none")
                        + "; screen " + Screen.width + "x" + Screen.height;
                }
        }
        return "unknown " + cmd;
    }
}
