using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using World.Game;
using World.Sim;

/// <summary>
/// Capture and inspection driver of the lighting pass (package L, Docs/LIGHTING.md). Reads a line of commands from
/// Temp/l_cmd.txt (separated by ';') and runs them against the World scene in play mode:
///   open | hour H | grow TICKS MINE THEIRS | step N | pause | resume | at PLACE | tp X Z YAW PITCH | god | fp |
///   scan | shot NAME | info | dance | stopdance
/// Run: unity command script run --file AgentScripts/LightCaptures.cs --entry LightCaptures.Run
/// Shots are rendered from the active camera at 1920x1080 into Evidence/screenshots (git-ignored).
/// </summary>
public static class LightCaptures
{
    public static string Run()
    {
        string path = Path.Combine(Directory.GetCurrentDirectory(), "Temp", "l_cmd.txt");
        if (!File.Exists(path)) return "no Temp/l_cmd.txt";
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

    private static float F(string s) => float.Parse(s, System.Globalization.CultureInfo.InvariantCulture);

    private static string Do(string cmd)
    {
        var a = cmd.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        var boot = WorldBootstrap.Instance;
        switch (a[0])
        {
            case "open":
                EditorSceneManager.OpenScene("Assets/Scenes/World.unity", OpenSceneMode.Single);
                return "opened World";
            case "hour":
                DayNight.Instance.SetHour(F(a[1]));
                return "hour " + DayNight.Instance.Hour.ToString("0.00");
            case "pause": boot.Clock.Pause("LightCapture"); return "paused";
            case "resume": boot.Clock.Resume("LightCapture"); return "resumed";
            case "step":
                for (int i = 0; i < int.Parse(a[1]); i++) boot.Clock.StepOnce();
                return "tick " + boot.Sim.State.Tick;
            case "grow":
            {
                var t = Type.GetType("World.Game.Tests.LandLateGame, World.Game.Tests");
                if (t == null) return "LandLateGame not found";
                var m = t.GetMethods().First(x => x.Name == "Grow" && x.GetParameters().Length == 5);
                Action step = () => boot.Clock.StepOnce();
                m.Invoke(null, new object[] { boot.Sim, int.Parse(a[1]), new[] { int.Parse(a[2]), int.Parse(a[3]) }, true, step });
                if (LightDecorator.Instance != null) LightDecorator.Instance.ScanNow();
                return "grown to tick " + boot.Sim.State.Tick;
            }
            case "scan": LightDecorator.Instance.ScanNow(); return "scanned";
            case "god": boot.Views.SetView(ViewMode.God); return "god";
            case "fp": boot.Views.SetView(ViewMode.FirstPerson); return "fp";
            case "tp":
                boot.Avatar.Teleport(new Vector3(F(a[1]), 0.05f, F(a[2])), F(a[3]), a.Length > 4 ? F(a[4]) : 6f);
                return "tp";
            case "at": return At(boot, a[1], a.Length > 2 ? F(a[2]) : 0f);
            case "look": boot.Avatar.SetLook(F(a[1]), F(a[2])); return "look";
            case "moon":
            {
                var md = DayNight.Instance.State.MoonDir;
                boot.Avatar.SetLook(BoardSpace.YawOf(md), a.Length > 1 ? F(a[1]) : -8f);
                return "look at moon";
            }
            case "sun":
            {
                var sd = DayNight.Instance.State.SunDir;
                boot.Avatar.SetLook(BoardSpace.YawOf(sd), a.Length > 1 ? F(a[1]) : -8f);
                return "look at sun";
            }
            case "shot": return Shot(a[1]);
            case "info": return Info(boot);
            case "dance":
            {
                bool ok = EncounterDirectory.TryStart(EncounterDirectory.RhythmParty, Npc.Find(NpcIds.Host));
                var e = EncounterHost.Instance.Current as RhythmPartyEncounter;
                if (e != null) e.AutoPlayEnabled = true;
                return "dance started " + ok;
            }
            case "stopdance": EncounterHost.Instance.Abort(); return "dance aborted";
            case "lapse": DayNight.DevTimeLapse = F(a[1]); return "lapse " + a[1];
            default: return "unknown " + a[0];
        }
    }

    private static void Look(WorldBootstrap boot, Vector3 from, Vector3 at, float pitch = 6f)
    {
        var d = at - from; d.y = 0;
        boot.Avatar.Teleport(new Vector3(from.x, 0.05f, from.z), BoardSpace.YawOf(d), pitch);
    }

    private static string At(WorldBootstrap boot, string place, float arg)
    {
        var s = boot.Sim.State;
        var layout = new CampLayout(s.Board);
        var views = LandViews.Instance;
        var t = s.Board.Topology;
        switch (place)
        {
            case "spawn":
                boot.Avatar.Teleport(WalkLattice.ToWorld(layout.Spawn) + Vector3.up * 0.05f, WalkLattice.Yaw(layout.SpawnHeading), 6f);
                return "at spawn";
            case "hearth":
            {
                var from = layout.Hearth + (WalkLattice.ToWorld(layout.Spawn) - layout.Hearth).normalized * (arg > 0 ? arg : 13f);
                Look(boot, from, layout.Hearth, 8f);
                return "at hearth";
            }
            case "mira":
            {
                var face = BoardSpace.DirOf(layout.MiraYaw);
                Look(boot, layout.MiraSpot + face * (arg > 0 ? arg : 6f), layout.MiraSpot, 4f);
                return "at mira";
            }
            case "dance":
                boot.Avatar.Teleport(layout.DancerSpot + Vector3.up * 0.05f - layout.Forward * arg, layout.DancerYaw, 15f);
                return "at dance";
            case "rival":
            {
                var c = BoardSpace.Vertex(t, s.Board.StartCorners[1]);
                var dir = (layout.Hearth - c); dir.y = 0; dir.Normalize();
                Look(boot, c + Quaternion.Euler(0, 25f, 0) * dir * (arg > 0 ? arg : 15f), c, 8f);
                return "at rival";
            }
            case "mark":
            {
                var mv = views.Markers.OrderByDescending(m => m.IsTop).FirstOrDefault();
                if (mv == null) return "no marker";
                var from = mv.Target + (layout.Hearth - mv.Target).normalized * (arg > 0 ? arg : 16f);
                if ((layout.Hearth - mv.Target).sqrMagnitude < 4f) from = mv.Target + Vector3.forward * 16f;
                Look(boot, from, mv.Target, 8f);
                return "at mark " + mv.name + " top=" + mv.IsTop + " r=" + mv.PressRadius;
            }
            case "forest":
            {
                int best = -1; float bd = 1e9f;
                for (int h = 0; h < t.HexCount; h++)
                    if (s.Board.HexTerrain[h] == World.Sim.Terrain.Forest)
                    {
                        float d = (BoardSpace.Hex(t, h) - layout.Hearth).magnitude;
                        if (d < bd) { bd = d; best = h; }
                    }
                var c = BoardSpace.Hex(t, best);
                var from = c + (layout.Hearth - c).normalized * (arg > 0 ? arg : 15f);
                Look(boot, from, c + Vector3.up, -4f);
                return "at forest hex " + best + " number " + s.Board.HexNumber[best];
            }
            case "pasture":
            {
                int best = -1; float bd = 1e9f;
                for (int h = 0; h < t.HexCount; h++)
                    if (s.Board.HexTerrain[h] == World.Sim.Terrain.Pasture)
                    {
                        float d = (BoardSpace.Hex(t, h) - layout.Hearth).magnitude;
                        if (d < bd) { bd = d; best = h; }
                    }
                var c = BoardSpace.Hex(t, best);
                Look(boot, c + (layout.Hearth - c).normalized * (arg > 0 ? arg : 13f), c, 6f);
                return "at pasture hex " + best;
            }
            case "demon":
            {
                var dv = views.DemonViews.FirstOrDefault();
                if (dv == null) return "no demons";
                var p = dv.transform.position;
                Look(boot, p + (layout.Hearth - p + Vector3.forward * 0.1f).normalized * (arg > 0 ? arg : 14f), p, 6f);
                return "at demons " + dv.name;
            }
            case "building":
            {
                var bv = views.Buildings.Where(b => b.Faction == 0 && b.ShownState == World.Sim.Economy.BuildingState.Active)
                    .OrderBy(b => (b.transform.position - layout.Hearth).sqrMagnitude).Skip((int)arg).FirstOrDefault();
                if (bv == null) return "no building";
                var p = bv.transform.position;
                Look(boot, p + bv.transform.forward * 11f + bv.transform.right * 3f, p, 6f);
                return "at building " + bv.name;
            }
            case "carrier":
            {
                var cv = views.Carriers.OrderByDescending(c => c.Figure.HasLoad).ThenBy(c => (c.transform.position - layout.Hearth).sqrMagnitude).FirstOrDefault();
                if (cv == null) return "no carrier";
                var p = cv.transform.position;
                Look(boot, p + (layout.Hearth - p + Vector3.right * 0.1f).normalized * -6f, p, 8f);
                return "at carrier " + cv.name + " load=" + cv.Figure.HasLoad;
            }
            case "warcamp":
            {
                if (views.WarCamp == null) return "no war camp";
                var p = views.WarCamp.transform.position;
                Look(boot, p + views.WarCamp.transform.forward * 14f, p, 6f);
                return "at war camp";
            }
        }
        return "unknown place " + place;
    }

    private static string Shot(string name)
    {
        var cam = ViewSwitcher.Instance != null ? ViewSwitcher.Instance.ActiveCamera : Camera.main;
        int w = 1920, h = 1080;
        var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
        var prev = cam.targetTexture;
        cam.targetTexture = rt;
        cam.Render();
        cam.targetTexture = prev;
        RenderTexture.active = rt;
        var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
        tex.Apply();
        RenderTexture.active = null;
        string dir = Path.Combine(Directory.GetCurrentDirectory(), "Evidence", "screenshots");
        Directory.CreateDirectory(dir);
        string file = Path.Combine(dir, name + ".png");
        File.WriteAllBytes(file, tex.EncodeToPNG());
        // Mean luminance and the share of near-black and near-white pixels: a quick check on "never black, never white-out".
        var px = tex.GetPixels32();
        double sum = 0; int black = 0, white = 0;
        for (int i = 0; i < px.Length; i += 7)
        {
            float l = (0.2126f * px[i].r + 0.7152f * px[i].g + 0.0722f * px[i].b) / 255f;
            sum += l; if (l < 0.02f) black++; if (l > 0.95f) white++;
        }
        int n = (px.Length + 6) / 7;
        UnityEngine.Object.DestroyImmediate(tex); rt.Release(); UnityEngine.Object.DestroyImmediate(rt);
        return "shot " + name + " lum=" + (sum / n).ToString("0.000") + " black=" + (100.0 * black / n).ToString("0.0") + "% white=" + (100.0 * white / n).ToString("0.0") + "%";
    }

    private static string Info(WorldBootstrap boot)
    {
        var dn = DayNight.Instance; var wl = WorldLights.Instance;
        int pixel = 0, vertex = 0, enabled = 0;
        foreach (var l in UnityEngine.Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
        {
            if (!l.enabled || !l.gameObject.activeInHierarchy || l.intensity <= 0f) continue;
            enabled++;
            if (l.renderMode == LightRenderMode.ForceVertex) vertex++; else pixel++;
        }
        var s = boot.Sim.State;
        return "tick " + s.Tick + " hour " + dn.Hour.ToString("0.00") + " night " + dn.Night.ToString("0.00") + " sunEl " + dn.State.SunElevation.ToString("0.0")
            + " lights: registered " + WorldLights.Registered + " enabled " + enabled + " (pixel-capable " + pixel + ", vertex " + vertex + ") budget px " + wl.PixelLightsNow + " vx " + wl.VertexLightsNow
            + " camps " + LightDecorator.Instance.CampsLit + " buildings " + LightDecorator.Instance.BuildingsLit
            + " markers " + LandViews.Instance.Markers.Count() + " demons " + LandViews.Instance.DemonViews.Count() + " carriers " + LandViews.Instance.Carriers.Count()
            + " settlements " + s.Economy.Settlements.Count + " buildingsInState " + s.Economy.Buildings.Count
            + " | frame ms " + (Time.smoothDeltaTime * 1000f).ToString("0.00") + " fps " + (1f / Mathf.Max(1e-5f, Time.smoothDeltaTime)).ToString("0")
            + " UnityStats frameTime ms " + (UnityStats.frameTime * 1000f).ToString("0.00") + " renderTime ms " + (UnityStats.renderTime * 1000f).ToString("0.00")
            + " batches " + UnityStats.batches + " setPass " + UnityStats.setPassCalls + " tris " + UnityStats.triangles + " screen " + UnityStats.screenRes;
    }
}
