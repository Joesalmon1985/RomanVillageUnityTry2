using System.Linq;
using UnityEngine;
using World.Game;

/// <summary>
/// Puts the running game (Play Mode) into the states used for the map-rework captures.
/// Run an entry, wait a frame, then: unity command capture game_view --output-path Evidence/screenshots/map_<name>.png
///   unity command script run --file AgentScripts/MapCaptures.cs --entry MapCaptures.WholeIsland
/// </summary>
public static class MapCaptures
{
    static WorldBootstrap B => WorldBootstrap.Instance;

    static void Reset()
    {
        DialoguePanel.Close();
        if (Hud.MenuOpen) B.Hud.CloseMenu();
        if (EncounterHost.Instance.IsRunning) EncounterHost.Instance.Abort();
        if (B.AutoPlay.IsRunning) B.AutoPlay.StopAllCoroutines();
        B.Views.SetView(ViewMode.FirstPerson);
        B.Picker.ClearSelection();
    }

    public static string WholeIsland()
    {
        Reset();
        B.Views.SetView(ViewMode.God);
        B.GodCam.FrameIsland();
        return "island size " + B.GodCam.Size.ToString("0") + " focus " + B.GodCam.Focus;
    }

    public static string GodHome()
    {
        Reset();
        B.Views.SetView(ViewMode.God);
        B.GodCam.FrameHome();
        return "home size " + B.GodCam.Size.ToString("0") + " focus " + B.GodCam.Focus;
    }

    public static string Spawn()
    {
        Reset();
        var L = B.Layout;
        B.Avatar.Teleport(L.Spawn + Vector3.up * 0.05f, L.SpawnYaw, 3f);
        return "spawn " + L.Spawn + " yaw " + L.SpawnYaw;
    }

    public static string Interior()
    {
        Reset();
        var w = B.Warehouse;
        w.SetRouteOpen(Route.Bar, true); w.SetRouteOpen(Route.Balcony, true);
        var p = w.transform.TransformPoint(new Vector3(-1.5f, 0f, -2.5f));
        B.Avatar.Teleport(p, w.transform.eulerAngles.y + 180f + 8f, 8f);
        return "inside";
    }

    public static string CloseRoutes()
    {
        B.Warehouse.SetRouteOpen(Route.Bar, false); B.Warehouse.SetRouteOpen(Route.Balcony, false);
        return "closed";
    }

    /// <summary>First person about 80 m from the village, looking back at it.</summary>
    public static string VillageFromHill(float distance = 85f, float yawOffset = 235f)
    {
        Reset();
        var L = B.Layout;
        var dir = BoardSpace.DirOf(BoardSpace.YawOf(L.Out) + yawOffset);
        var p = L.VillageCentre + dir * distance;
        B.Avatar.Teleport(p + Vector3.up * 0.05f, BoardSpace.YawOf(L.VillageCentre - p), -2f);
        return "from " + p + " hex " + BoardSpace.HexAt(p);
    }

    public static string Outpost()
    {
        Reset();
        var o = B.Layout.Outpost;
        var toHome = (B.Layout.Home - o); toHome.y = 0; toHome.Normalize();
        var p = o + toHome * 24f + new Vector3(toHome.z, 0, -toHome.x) * 5f;
        B.Avatar.Teleport(p + Vector3.up * 0.05f, BoardSpace.YawOf(o - p), -8f);
        return "outpost view from " + p;
    }

    public static string Pasture()
    {
        Reset();
        var board = B.Sim.State.Board; var t = board.Topology;
        int best = -1; float bd = float.MaxValue;
        for (int h = 0; h < t.HexCount; h++)
            if (board.HexTerrain[h] == World.Sim.Terrain.Pasture)
            {
                float d = BoardSpace.Flat(BoardSpace.Hex(t, h) - B.Layout.Home);
                if (d < bd) { bd = d; best = h; }
            }
        var c = BoardSpace.Hex(t, best);
        var p = c + BoardSpace.DirOf(215f) * 40f;
        B.Avatar.Teleport(p + Vector3.up * 0.05f, BoardSpace.YawOf(c - p) - 10f, -9f);
        return "pasture hex " + best + " at " + c;
    }

    /// <summary>Runs a registered AutoPlay script from the spawn (watch the console for "[AutoPlay] name OK").</summary>
    public static string RunScript(string name)
    {
        Spawn();
        if (!AutoPlayScripts.TryCreate(name, out var s)) return "unknown " + name;
        B.AutoPlay.Run(s);
        return name + " started";
    }

    public static string AutoPlayState() => "running=" + B.AutoPlay.IsRunning + " failed=" + B.AutoPlay.Failed + " " + B.AutoPlay.FailMessage;

    public static string Fps()
    {
        return Screen.width + "x" + Screen.height + " fps ~ " + (1f / Time.smoothDeltaTime).ToString("0") + "  dt " + Time.smoothDeltaTime.ToString("0.0000");
    }

    public static string Props() => string.Join(", ", TerrainBuilder.PropCounts.Select(kv => kv.Key + " " + kv.Value));
}
