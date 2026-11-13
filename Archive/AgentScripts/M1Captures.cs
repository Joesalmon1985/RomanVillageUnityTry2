using UnityEngine;
using World.Game;

/// <summary>
/// Puts the running game (Play Mode) into the states used for the Milestone 1 captures.
/// Run each entry, then: unity command capture game_view --output-path Evidence/screenshots/<name>.png
///   unity command script run --file AgentScripts/M1Captures.cs --entry M1Captures.Spawn
/// </summary>
public static class M1Captures
{
    static WorldBootstrap B => WorldBootstrap.Instance;

    static void Reset()
    {
        DialoguePanel.Close();
        if (Hud.MenuOpen) B.Hud.CloseMenu();
        if (EncounterHost.Instance.IsRunning) EncounterHost.Instance.Abort();
        B.Views.SetView(ViewMode.FirstPerson);
        B.Picker.ClearSelection();
    }

    public static string Spawn()
    {
        Reset();
        var L = B.Layout;
        B.Avatar.Teleport(L.Spawn + Vector3.up * 0.05f, L.SpawnYaw, 3f);
        return "spawn";
    }

    public static string GodView()
    {
        Reset();
        Spawn();
        B.Views.SetView(ViewMode.God);
        B.GodCam.SetFocus(Vector3.Lerp(Vector3.zero, B.Layout.Home, 0.45f), 62f);
        B.Picker.Select(VillageBuilder.Storehouse);
        return "god view at " + B.GodCam.Focus;
    }

    public static string Interior()
    {
        Reset();
        var w = B.Warehouse;
        var p = w.transform.TransformPoint(new Vector3(-3f, 0f, -3.5f));
        B.Avatar.Teleport(p, w.transform.eulerAngles.y + 180f + 12f, 6f);
        return "inside";
    }

    public static string Dialogue()
    {
        Reset();
        var mira = Npc.Find(NpcIds.Mira);
        var d = (B.Layout.Spawn - mira.transform.position); d.y = 0;
        var p = mira.transform.position + d.normalized * 3.6f;
        B.Avatar.Teleport(p, BoardSpace.YawOf(-d) + 8f, 9f);
        mira.Interact();
        return "dialogue open: " + DialoguePanel.IsOpen;
    }

    public static string Pause()
    {
        Spawn();
        B.Hud.OpenMenu();
        return "menu";
    }

    public static string OpenRoutes()
    {
        B.Warehouse.SetRouteOpen(Route.Bar, true);
        B.Warehouse.SetRouteOpen(Route.Balcony, true);
        return "routes open";
    }

    public static string Clear() { Reset(); return "clear"; }

    /// <summary>Runs the registered boot_tour script in Play Mode (watch the console for "[AutoPlay] boot_tour OK").</summary>
    public static string BootTour()
    {
        Spawn();
        AutoPlayScripts.TryCreate("boot_tour", out var s);
        B.AutoPlay.Run(s);
        return "boot_tour started";
    }

    public static string AutoPlayState() => "running=" + B.AutoPlay.IsRunning + " failed=" + B.AutoPlay.Failed + " " + B.AutoPlay.FailMessage;

    public static string Fps()
    {
        return Screen.width + "x" + Screen.height + " fps ~ " + (1f / Time.smoothDeltaTime).ToString("0") + "  dt " + Time.smoothDeltaTime.ToString("0.0000");
    }
}

