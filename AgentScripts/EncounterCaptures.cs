using UnityEngine;
using World.Game;
using World.Sim.Encounters.Ward;
using SimRoute = World.Sim.Encounters.Rhythm.Route;

/// <summary>
/// Puts the running game (Play Mode) into the Milestone 2 capture states. Run an entry, then
///   unity command capture game_view --output-path Evidence/screenshots/<name>.png
///   unity command script run --file AgentScripts/EncounterCaptures.cs --entry EncounterCaptures.DuelSetup
/// </summary>
public static class EncounterCaptures
{
    static WorldBootstrap B => WorldBootstrap.Instance;

    public static string Reset()
    {
        DialoguePanel.Close();
        if (Hud.MenuOpen) B.Hud.CloseMenu();
        if (EncounterHost.Instance.IsRunning) EncounterHost.Instance.Abort();
        var bot = B.GetComponent<WardBot>(); if (bot != null) bot.enabled = false;
        B.Views.SetView(ViewMode.FirstPerson);
        return "reset";
    }

    /// <summary>Stand by Mira, open her dialogue, accept; pick two essences so the setup row shows progress.</summary>
    public static string DuelSetup()
    {
        Reset();
        var mira = Npc.Find(NpcIds.Mira);
        var d = B.Layout.Spawn - mira.transform.position; d.y = 0;
        B.Avatar.Teleport(mira.transform.position + d.normalized * 2.6f, BoardSpace.YawOf(-d), 8f);
        mira.Interact();
        DialoguePanel.Choose(0);
        var enc = WardDuelEncounter.Current;
        if (enc == null) return "no duel";
        enc.AddEssence(Essence.Frost);
        enc.AddEssence(Essence.Flame);
        return "setup, pool " + enc.Pool.Length + ", building " + enc.Building.Count;
    }

    /// <summary>Lets the scripted player cast up to maxCasts times every interval seconds (holds the duel open after).</summary>
    public static string DuelBot(int maxCasts, float interval)
    {
        var bot = WardBot.Attach(B.gameObject);
        bot.MaxCastsPerDuel = maxCasts; bot.CastInterval = interval; bot.AutoReturn = false; bot.FixedWard = null;
        return "bot on";
    }

    public static string DuelState()
    {
        var e = WardDuelEncounter.Current;
        if (e == null) return "no duel";
        return e.Phase + " outcome " + e.LastOutcome + " you " + e.Session.PlayerHistory.Count + " mira " + e.Session.RivalHistory.Count + " elapsed " + e.Session.ElapsedMs +
               " completed " + B.Sim.State.Ward.CompletedDuelsVsMira + " pool " + e.Pool.Length;
    }

    /// <summary>Stand at the decks, accept DJ Sol's party; autoplayer on (route as given: "bar" or "balcony"; skip = block to miss or -1).</summary>
    public static string Party(string route, int skipBlock)
    {
        Reset();
        var w = B.Warehouse;
        B.Avatar.Teleport(w.DecksFrontPosition + Vector3.up * 0.05f, w.DecksYaw + 180f, 0f);
        var host = Npc.Find(NpcIds.Host);
        host.Interact();
        DialoguePanel.Choose(0);
        var r = RhythmPartyEncounter.Current;
        if (r == null) return "no party";
        r.AutoPlayEnabled = true;
        r.AutoRoute = route == "balcony" ? SimRoute.Balcony : SimRoute.Bar;
        r.AutoSkipBlock = skipBlock < 0 ? null : (System.Func<int, bool>)(b => b == skipBlock);
        return "party started, stems " + r.Stems.Source;
    }

    public static string PartyState()
    {
        var r = RhythmPartyEncounter.Current;
        if (r == null) return "no party";
        return r.Phase + " song " + r.SongTime.ToString("0.000") + " beat " + r.Session.SecondsToBeat(r.SongTime).ToString("0.00") + " block " + r.Session.CurrentBlock +
               " judged " + r.CuesJudged + " window " + r.Session.RouteWindow + " route " + r.Session.CurrentRoute + " fallbackClock " + r.Player.UsingRealtimeFallback;
    }

    /// <summary>Freeze the game loop for a clean capture (Time.timeScale does not stop DSP audio; this pauses the editor).</summary>
    public static string Pause(bool on) { UnityEditor.EditorApplication.isPaused = on; return "paused " + on; }

    public static string Return()
    {
        var w = WardDuelEncounter.Current; if (w != null) w.Return();
        var r = RhythmPartyEncounter.Current; if (r != null) r.Return();
        return "return";
    }

    /// <summary>View of an opened route from inside (after a successful phrase).</summary>
    public static string LookInto(string route)
    {
        Reset();
        var w = B.Warehouse;
        if (route == "bar")
        {
            var p = w.transform.TransformPoint(new Vector3(-9.0f, 0, -12.5f));
            var target = w.transform.TransformPoint(new Vector3(-17f, 1.2f, -16.5f));
            B.Avatar.Teleport(p + Vector3.up * 0.05f, BoardSpace.YawOf(target - p), 4f);
        }
        else
        {
            var p = w.transform.TransformPoint(new Vector3(9.3f, 3.65f, -11.5f));
            var target = w.transform.TransformPoint(new Vector3(-2f, 1.0f, -20f));
            B.Avatar.Teleport(p + Vector3.up * 0.05f, BoardSpace.YawOf(target - p), 14f);
        }
        return route + " open " + w.IsRouteOpen(route == "bar" ? Route.Bar : Route.Balcony);
    }

    public static string FirstFive()
    {
        Reset();
        var L = B.Layout;
        B.Avatar.Teleport(L.Spawn + Vector3.up * 0.05f, L.SpawnYaw, 3f);
        AutoPlayScripts.TryCreate("first_five", out var s);
        B.AutoPlay.Run(s);
        return "first_five started";
    }

    public static string AutoPlayState() => "running=" + B.AutoPlay.IsRunning + " failed=" + B.AutoPlay.Failed + " " + B.AutoPlay.FailMessage;
}
