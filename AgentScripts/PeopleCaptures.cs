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
///   shot NAME | seq NAME N SECONDS | info | ringview DIST HEIGHT | soldier | soldierdie | ask
/// TARGET: npc:ID | ring | builder | keeper | collector | carrier | crowd | soldier.
/// P3: "ringview" frames the duel ring from a spectator's spot just outside the fence on the side square to the
/// sparring line, above the fence, looking at the two sparrers' midpoint (P1's orbit camera ended up behind the wall).
/// "soldier" stands a lone soldier view 5 m in front of the player; "soldierdie" kills it (the death sequence).
/// "ask" makes the healer show the map table (as when the player asks how bad it is). The capture camera follows the target every
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
                    _ring = false;
                    if (_target == null || _target() == null) { KillCams(); _cam = null; return "cam: no target " + t; }
                    MakeCam();
                    return "cam " + t + " at " + _target().position;
                }
            case "nocam": KillCams(); _cam = null; _target = null; _ring = false; return "nocam";
            case "ringview":
                {
                    _dist = a.Length > 1 ? float.Parse(a[1], ci) : DuelRing.FenceRadius + 1.5f; _height = a.Length > 2 ? float.Parse(a[2], ci) : 2.6f;
                    _side = a.Length > 3 ? float.Parse(a[3], ci) : 0f; // degrees round the ring from the player's side
                    _ring = true; _target = () => DuelRing.Instance != null ? DuelRing.Instance.transform : null;
                    MakeCam(); return "ringview";
                }
            case "soldier":
                {
                    var w = PlayerWalker.Instance; var fwd = BoardSpace.DirOf(w.Yaw);
                    foreach (var o in UnityEngine.Object.FindObjectsByType<SoldierView>(FindObjectsSortMode.None)) if (o.name.StartsWith("P3 ")) UnityEngine.Object.DestroyImmediate(o.gameObject);
                    var v = SoldierView.Create(null, new World.Sim.War.Soldier { Id = 4, People = 0 }, LandLook.People(0), World.Sim.War.SoldierLook.Warrior);
                    v.name = "P3 soldier";
                    var at = w.Position + fwd * 5f; at.y = PlayerWalker.GroundHeight(at);
                    v.SnapTo(at); v.Target = at; v.FaceYaw = w.Yaw + 180f; v.transform.rotation = Quaternion.Euler(0, w.Yaw + 120f, 0);
                    _soldier = v;
                    return "soldier at " + at;
                }
            case "soldierdie": { var v = FindSoldier(); if (v == null) return "no soldier"; v.Die(); return "soldierdie"; }
            case "ask":
                {
                    var l = HealersLodge.Instance; if (l == null || l.Plague == null) return "no lodge";
                    l.ShowTo(l.Plague.TableCentre); return "ask";
                }
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
            case "ringnear":
                {
                    var sb = new StringBuilder(); var c = DuelRing.Instance.Frame.O;
                    foreach (var r in UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
                    {
                        if (!r.enabled || !r.gameObject.activeInHierarchy) continue;
                        var b = r.bounds; var q = b.ClosestPoint(new Vector3(c.x, b.center.y, c.z));
                        if ((new Vector2(q.x - c.x, q.z - c.z)).magnitude < 3f && b.size.magnitude < 60f)
                        {
                            var t = r.transform; string path = t.name; while (t.parent != null && path.Length < 120) { t = t.parent; path = t.name + "/" + path; }
                            sb.Append(path + " size " + b.size.ToString("F1") + "; ");
                        }
                    }
                    return sb.ToString();
                }
            case "info":
                {
                    var sb = new StringBuilder();
                    sb.Append("npcs " + Npc.All.Count);
                    var h = HistoricDirector.Instance; if (h != null) sb.Append(" carriers " + h.WalkersShown + " swing " + h.CarrierLegSwing.ToString("F1"));
                    var d = GodBoardDirector.Instance; if (d != null && d.Collectors != null) sb.Append(" collectors " + d.Collectors.Active.Count);
                    sb.Append(" screen " + Screen.width + "x" + Screen.height);
                    var ring = DuelRing.Instance; if (ring != null) sb.Append(" ring O " + ring.Frame.O + " yaw " + ring.Frame.Yaw + " s0 " + ring.Sparrers[0].transform.position + " s1 " + ring.Sparrers[1].transform.position);
                    foreach (var c in Camera.allCameras) if (c.name == "P1 capture camera") sb.Append(" cam " + c.transform.position);
                    return sb.ToString();
                }
        }
        return "unknown " + cmd;
    }

    static Transform _crowd;
    static bool _ring;
    static SoldierView _soldier;

    static SoldierView FindSoldier()
    {
        if (_soldier != null) return _soldier;
        foreach (var o in UnityEngine.Object.FindObjectsByType<SoldierView>(FindObjectsSortMode.None)) if (o.name.StartsWith("P3 ")) return o;
        return null;
    }

    static void MakeCam()
    {
        KillCams(); _cam = null; // each script run is a new assembly: cameras of earlier runs are found by name
        _cam = new GameObject("P1 capture camera").AddComponent<Camera>();
        var main = PlayerWalker.Instance != null ? PlayerWalker.Instance.Camera : Camera.main;
        if (main != null) { _cam.CopyFrom(main); }
        _cam.depth = 100; _cam.fieldOfView = 50f; _cam.nearClipPlane = 0.1f; _cam.farClipPlane = 4000f;
        if (!_hooked) { Camera.onPreCull += Follow; _hooked = true; }
        Follow(_cam);
    }

    static void KillCams()
    {
        foreach (var c in Camera.allCameras) if (c != null && c.name == "P1 capture camera") UnityEngine.Object.DestroyImmediate(c.gameObject);
    }

    static Func<Transform> Resolve(string t)
    {
        if (t.StartsWith("npc:")) { string id = t.Substring(4); return () => { var n = Npc.Find(id); return n != null ? n.transform : null; }; }
        switch (t)
        {
            case "ring": return () => DuelRing.Instance != null ? DuelRing.Instance.Sparrers[0].transform : null;
            case "builder": return () => TempleForecourt.Instance != null ? TempleForecourt.Instance.Builders[0].transform : null;
            case "keeper": return () => TempleForecourt.Instance != null ? TempleForecourt.Instance.Keeper.transform : null;
            case "crowd": return () => _crowd;
            case "soldier": return () => { var v = FindSoldier(); return v != null ? v.transform : null; };
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
        if (_ring && DuelRing.Instance != null && DuelRing.Instance.Sparrers != null && DuelRing.Instance.Sparrers.Length >= 2)
        {
            // Spectator's view: square to the line between the sparrers, just outside the fence, above it.
            var r = DuelRing.Instance; var p0 = r.Sparrers[0].transform.position; var p1 = r.Sparrers[1].transform.position;
            var mid = (p0 + p1) * 0.5f; var line = p1 - p0; line.y = 0; if (line.sqrMagnitude < 1e-3f) line = Vector3.right;
            var perp = Vector3.Cross(Vector3.up, line.normalized);
            var centre = r.Frame.O; // the ring root sits at the hub origin; its frame is the ring
            // the side nearer the player's spawn side (towards the camera that the player would stand on)
            var pp = PlayerWalker.Instance != null ? PlayerWalker.Instance.Position : centre + perp;
            if (Vector3.Dot(pp - centre, perp) < 0f) perp = -perp;
            perp = Quaternion.Euler(0, _side, 0) * perp;
            var at = centre + perp * _dist; at.y = mid.y + _height;
            _cam.transform.position = at;
            _cam.transform.LookAt(mid + Vector3.up * 0.9f);
            return;
        }
        float s = Mathf.Max(0.01f, t.lossyScale.y);
        var fwd = t.forward; fwd.y = 0; if (fwd.sqrMagnitude < 1e-4f) fwd = Vector3.forward; fwd.Normalize();
        var right = Vector3.Cross(Vector3.up, fwd);
        var look = t.position + Vector3.up * _lookH * s;
        _cam.transform.position = t.position + (fwd * Mathf.Cos(_side * Mathf.Deg2Rad) + right * Mathf.Sin(_side * Mathf.Deg2Rad)) * _dist * s + Vector3.up * _height * s;
        _cam.transform.LookAt(look);
    }
}
