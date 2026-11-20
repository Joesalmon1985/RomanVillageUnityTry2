using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;
using World.Game;
using World.Sim;

/// <summary>
/// P1 polish (people) capture driver. Reads commands from Temp/p1_cmd.txt (separated by ';') and runs them in play mode:
///   godboard | historic | day | size W H | spawn | crowd | cam TARGET DIST HEIGHT SIDE | nocam | collectors N |
///   shot NAME | seq NAME N SECONDS | info
/// TARGET: npc:ID | ring | builder | keeper | collector | carrier | crowd. The capture camera follows the target every
/// frame (Camera.onPreCull) and draws over the player's camera. "seq" saves N frames SECONDS apart (real time).
/// Captures go to Evidence/polish/ (local, never committed).
/// Run: unity command script run --file AgentScripts/PeopleCaptures.cs --entry PeopleCaptures.Run
/// </summary>
public static class PeopleCaptures
{
    public const string Folder = "Evidence/polish";
    static Camera _cam; static Func<Transform> _target; static float _dist = 4f, _height = 1.6f, _side = 0f, _lookH = 1.0f;
    static bool _hooked;

    public static string Run()
    {
        string path = Path.Combine(Directory.GetCurrentDirectory(), "Temp", "p1_cmd.txt");
        if (!File.Exists(path)) return "no Temp/p1_cmd.txt";
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

    static Transform Nearest(IEnumerable<Npc> list, Func<Npc, bool> ok)
    {
        var p = PlayerWalker.Instance != null ? PlayerWalker.Instance.Position : Vector3.zero;
        Transform best = null; float bd = float.MaxValue;
        foreach (var n in list) if (n != null && ok(n)) { float d = (n.transform.position - p).sqrMagnitude; if (d < bd) { bd = d; best = n.transform; } }
        return best;
    }

    static string Do(string cmd)
    {
        var a = cmd.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        var ci = System.Globalization.CultureInfo.InvariantCulture;
        var boot = WorldBootstrap.Instance;
        switch (a[0])
        {
            case "godboard": return "godboard " + DevStart.Begin(GodBoardStart.Id, 7UL) + " " + DevStart.LastError;
            case "historic": return "historic " + DevStart.Begin(GodBoardStart.HistoricId, 7UL) + " " + DevStart.LastError;
            case "day": if (DayNight.Instance != null) DayNight.Instance.SetHour(a.Length > 1 ? float.Parse(a[1], ci) : 11f); return "day";
            case "size":
                {
                    int w = int.Parse(a[1]), h = int.Parse(a[2]);
                    PlayModeWindow.SetCustomRenderingResolution((uint)w, (uint)h, "P1 " + w + "x" + h);
                    ScreenSafeArea.Override = a.Length > 3 && a[3] == "notch" ? ScreenSafeArea.SimulatedNotch(w, h) : (Rect?)null;
                    PlayModeWindow.GetRenderingResolution(out uint rw, out uint rh);
                    return "size " + rw + "x" + rh;
                }
            case "spawn":
                {
                    var layout = new CampLayout(boot.Sim.State.Board);
                    if (PrinceDirector.Instance != null) PrinceDirector.Instance.enabled = false;
                    WorldActions.Cancel(); GroundWalk.ClearMarker();
                    foreach (int h in SpeechBubbles.OpenHandles()) SpeechBubbles.Close(h);
                    PlayerWalker.Instance.StopAuto();
                    PlayerWalker.Instance.Teleport(layout.Spawn, layout.SpawnHeading);
                    return "spawn";
                }
            case "crowd":
                {
                    // Sends the five nearest free villagers past a point 7 m in front of the player (side to side), for a walking crowd.
                    var w = PlayerWalker.Instance; var fwd = BoardSpace.DirOf(w.Yaw); var right = Vector3.Cross(Vector3.up, fwd);
                    var centre = w.Position + fwd * 7f;
                    var pick = new List<Npc>();
                    foreach (var n in Npc.All) if (n != null && !n.Directed && n.Id != NpcIds.Mira) pick.Add(n);
                    pick.Sort((x, y) => (x.transform.position - centre).sqrMagnitude.CompareTo((y.transform.position - centre).sqrMagnitude));
                    int k = 0;
                    foreach (var n in pick)
                    {
                        if (k >= 5) break;
                        var from = centre - right * (6f + k * 1.3f) + fwd * (k % 2 == 0 ? -1f : 1.2f) * (k * 0.5f);
                        from.y = PlayerWalker.GroundHeight(from);
                        n.PlaceAt(from, BoardSpace.YawOf(right));
                        n.WalkSpeed = k == 4 ? 7f : Npc.StrollSpeed * (0.8f + 0.1f * k);
                        n.WalkTo(centre + right * (9f - k) + fwd * (k - 2) * 0.8f);
                        k++;
                    }
                    _crowd = pick.Count > 0 ? pick[0].transform : null;
                    return "crowd " + k;
                }
            case "cam":
                {
                    string t = a[1];
                    _dist = a.Length > 2 ? float.Parse(a[2], ci) : 4f; _height = a.Length > 3 ? float.Parse(a[3], ci) : 1.6f; _side = a.Length > 4 ? float.Parse(a[4], ci) : 0f;
                    _lookH = a.Length > 5 ? float.Parse(a[5], ci) : 1.0f;
                    _target = Resolve(t);
                    if (_target == null || _target() == null) return "cam: no target " + t;
                    if (_cam == null)
                    {
                        _cam = new GameObject("P1 capture camera").AddComponent<Camera>();
                        var main = PlayerWalker.Instance != null ? PlayerWalker.Instance.Camera : Camera.main;
                        if (main != null) { _cam.CopyFrom(main); }
                        _cam.depth = 100; _cam.fieldOfView = 50f; _cam.nearClipPlane = 0.1f; _cam.farClipPlane = 4000f;
                    }
                    if (!_hooked) { Camera.onPreCull += Follow; _hooked = true; }
                    Follow(_cam);
                    return "cam " + t + " at " + _target().position;
                }
            case "nocam": if (_cam != null) UnityEngine.Object.Destroy(_cam.gameObject); _cam = null; _target = null; return "nocam";
            case "collectors":
                {
                    // Visual only: sends N collecting villagers from the corner nearest the player to a producing hex (reflection on the private spawn).
                    var d = GodBoardDirector.Instance; if (d == null || d.Collectors == null) return "no collectors";
                    var s = boot.Sim.State; var topo = s.Board.Topology; var p = PlayerWalker.Instance.Position;
                    int bestHex = -1; float bd = float.MaxValue;
                    for (int h = 0; h < topo.HexCount; h++)
                    {
                        if (!TerrainInfo.RawWare(s.Board.HexTerrain[h]).HasValue) continue;
                        float dd = (BoardSpace.Hex(topo, h) - p).sqrMagnitude; if (dd < bd) { bd = dd; bestHex = h; }
                    }
                    var corners = topo.HexCorners(bestHex);
                    var spawn = typeof(CollectWalkers).GetMethod("Spawn", BindingFlags.NonPublic | BindingFlags.Instance);
                    int n = int.Parse(a[1]);
                    int ci2 = 0; foreach (int c in corners) { ci2 = c; break; }
                    for (int i = 0; i < n; i++) spawn.Invoke(d.Collectors, new object[] { ci2, bestHex, 0, TerrainInfo.RawWare(s.Board.HexTerrain[bestHex]).Value, i % 3 });
                    return "collectors " + n + " hex " + bestHex + " corner " + ci2;
                }
            case "shot":
                {
                    Directory.CreateDirectory(Folder);
                    string file = Path.Combine(Folder, a[1] + ".png");
                    ScreenCapture.CaptureScreenshot(file);
                    return "shot " + file;
                }
            case "seq":
                {
                    Directory.CreateDirectory(Folder);
                    string name = a[1]; int n = int.Parse(a[2]); double step = double.Parse(a[3], ci);
                    double t0 = EditorApplication.timeSinceStartup + 0.05; int k = 0;
                    EditorApplication.CallbackFunction f = null;
                    f = () =>
                    {
                        if (EditorApplication.timeSinceStartup < t0 + k * step) return;
                        ScreenCapture.CaptureScreenshot(Path.Combine(Folder, name + "_" + k + ".png"));
                        k++;
                        if (k >= n) EditorApplication.update -= f;
                    };
                    EditorApplication.update += f;
                    return "seq " + name + " x" + n;
                }
            case "info":
                {
                    var sb = new StringBuilder();
                    sb.Append("npcs " + Npc.All.Count);
                    var h = HistoricDirector.Instance; if (h != null) sb.Append(" carriers " + h.WalkersShown + " swing " + h.CarrierLegSwing.ToString("F1"));
                    var d = GodBoardDirector.Instance; if (d != null && d.Collectors != null) sb.Append(" collectors " + d.Collectors.Active.Count);
                    sb.Append(" screen " + Screen.width + "x" + Screen.height);
                    return sb.ToString();
                }
        }
        return "unknown " + cmd;
    }

    static Transform _crowd;

    static Func<Transform> Resolve(string t)
    {
        if (t.StartsWith("npc:")) { string id = t.Substring(4); return () => { var n = Npc.Find(id); return n != null ? n.transform : null; }; }
        switch (t)
        {
            case "ring": return () => DuelRing.Instance != null ? DuelRing.Instance.Sparrers[0].transform : null;
            case "builder": return () => TempleForecourt.Instance != null ? TempleForecourt.Instance.Builders[0].transform : null;
            case "keeper": return () => TempleForecourt.Instance != null ? TempleForecourt.Instance.Keeper.transform : null;
            case "crowd": return () => _crowd;
            case "collector":
                return () =>
                {
                    var d = GodBoardDirector.Instance; if (d == null || d.Collectors == null) return null;
                    foreach (var w in d.Collectors.Active) if (w.Go != null && w.Go.activeSelf) return w.Go.transform;
                    return null;
                };
            case "carrier":
                return () =>
                {
                    var h = HistoricDirector.Instance; if (h == null) return null;
                    foreach (Transform c in h.transform.GetComponentsInChildren<Transform>()) if (c.name == "Walker" && c.gameObject.activeInHierarchy) return c;
                    return null;
                };
        }
        return null;
    }

    static void Follow(Camera c)
    {
        if (c != _cam || _cam == null || _target == null) return;
        var t = _target(); if (t == null) return;
        float s = Mathf.Max(0.01f, t.lossyScale.y);
        var fwd = t.forward; fwd.y = 0; if (fwd.sqrMagnitude < 1e-4f) fwd = Vector3.forward; fwd.Normalize();
        var right = Vector3.Cross(Vector3.up, fwd);
        var look = t.position + Vector3.up * _lookH * s;
        _cam.transform.position = t.position + (fwd * Mathf.Cos(_side * Mathf.Deg2Rad) + right * Mathf.Sin(_side * Mathf.Deg2Rad)) * _dist * s + Vector3.up * _height * s;
        _cam.transform.LookAt(look);
    }
}
