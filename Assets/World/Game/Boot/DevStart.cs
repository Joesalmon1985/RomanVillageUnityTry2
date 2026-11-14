using System;
using System.Collections.Generic;
using UnityEngine;
using World.Sim;
using World.Sim.Eras;

namespace World.Game
{
    /// <summary>
    /// Developer starts (PRD_AGES A.2 <see cref="ScenarioStarts"/>, e.g. <c>dev.historic</c>): builds the start's sim
    /// and reloads the scene on it, so every view is built from that state alone (as a load or a new game does).
    /// From the F1 God View tool "Starts" (hotkey G) or the command line <c>-start &lt;id&gt;</c> (with
    /// <c>-seed &lt;n&gt;</c>). Builds the sim in one frame (dev.historic runs its scripted build: a few seconds).
    /// Views for campaign things do not exist yet (I5, I6); the crystal campaign is started by I8's
    /// <c>CampaignStart</c>, not here. Historic base, 14 Nov 2026; owner: the ages integration (H1 lane), additive.
    /// </summary>
    public static class DevStart
    {
        public const string ToolId = "starts";
        /// <summary>The developer start this scene was built from, or null (a new game or a load).</summary>
        public static string Current { get; private set; }
        /// <summary>Why the last start failed (sentence), or null.</summary>
        public static string LastError { get; private set; }
        /// <summary>The starts that can be built now.</summary>
        public static IReadOnlyList<string> Ids => ScenarioStarts.Ids;

        private static WorldSim s_pending;
        private static string s_pendingId;
        private static bool s_commandLineTaken;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { Current = null; LastError = null; s_pending = null; s_pendingId = null; s_commandLineTaken = false; }

        /// <summary>Builds the start and reloads the scene on it. False (with <see cref="LastError"/>) if it cannot be built.</summary>
        public static bool Begin(string startId, ulong? seed = null)
        {
            var b = WorldBootstrap.Instance;
            ulong sd = seed ?? (b != null ? unchecked((ulong)b.Seed) : 20261113UL);
            var sim = Build(startId, sd);
            if (sim == null) { Hud.Notice(LastError, true); return false; }
            s_pending = sim; s_pendingId = startId;
            if (b != null) UnityEngine.SceneManagement.SceneManager.LoadScene(b.gameObject.scene.name);
            return true;
        }

        /// <summary>
        /// <see cref="WorldBootstrap"/>.Awake: the start's sim for this scene, or null for a new game. The first scene
        /// of a run also honours <c>-start &lt;id&gt;</c>.
        /// </summary>
        public static WorldSim TakePending(ulong seed)
        {
            if (s_pending == null && !s_commandLineTaken)
            {
                s_commandLineTaken = true;
                string id = Arg("-start");
                if (id != null)
                {
                    ulong sd = ulong.TryParse(Arg("-seed"), out var n) ? n : seed;
                    s_pending = Build(id, sd);
                    s_pendingId = s_pending != null ? id : null;
                    if (s_pending == null) Debug.LogError("[DevStart] " + LastError);
                }
            }
            var sim = s_pending;
            Current = sim != null ? s_pendingId : null;
            s_pending = null; s_pendingId = null;
            if (sim != null) Debug.Log("[DevStart] " + Current + ": tick " + sim.State.Tick + ", era " + AgesApi.Current(sim.State) + ", mode " + AgesApi.Mode(sim.State));
            return sim;
        }

        private static WorldSim Build(string id, ulong seed)
        {
            LastError = null;
            try
            {
                float t0 = Time.realtimeSinceStartup;
                var sim = ScenarioStarts.Create(id, seed);
                if (sim == null) { LastError = "There is no developer start called \"" + id + "\"."; return null; }
                Debug.Log("[DevStart] built " + id + " (seed " + seed + ") in " + (Time.realtimeSinceStartup - t0).ToString("0.0") + " s");
                return sim;
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                LastError = "The start \"" + id + "\" could not be built (" + e.Message + ").";
                return null;
            }
        }

        private static string Arg(string name)
        {
            var args = Environment.GetCommandLineArgs();
            int i = Array.IndexOf(args, name);
            return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
        }

        /// <summary>The F1 tool: one button per start (called by <see cref="DevToolsInstaller"/>).</summary>
        public static void RegisterTool()
        {
            GodToolbar.Register(new GodTool
            {
                Id = ToolId, Label = "Starts", Order = 95, Hotkey = KeyCode.G, PanelWidth = 520, DrawPanel = Panel,
                TipText = () => "Developer starts: rebuild the world from a scripted start.",
            });
        }

        private static void Panel(GodTool t, Rect a)
        {
            float x = a.x + UiStyle.Px(14), w = a.width - UiStyle.Px(28), y = a.y + UiStyle.Px(10), bh = UiStyle.Px(40);
            GUI.Label(new Rect(x, y, w, UiStyle.Px(56)), "Rebuild the world from a developer start (same seed). Building takes a few seconds.", UiStyle.Small);
            y += UiStyle.Px(60);
            foreach (var id in Ids)
            {
                if (GUI.Button(new Rect(x, y, w, bh), id + (Current == id ? "  (running)" : ""), UiStyle.Button)) Begin(id);
                y += bh + UiStyle.Px(6);
            }
            var s = WorldBootstrap.Instance != null && WorldBootstrap.Instance.Sim != null ? WorldBootstrap.Instance.Sim.State : null;
            if (s != null)
                GUI.Label(new Rect(x, y + UiStyle.Px(6), w, UiStyle.Px(60)), "Now: " + AgesApi.EraName(AgesApi.Current(s)) + ", " + AgesApi.Mode(s) + (Current != null ? ", start " + Current : ""), UiStyle.Small);
            if (LastError != null)
                GUI.Label(new Rect(x, a.yMax - UiStyle.Px(60), w, UiStyle.Px(56)), "<color=#FF9A80>" + LastError + "</color>", UiStyle.Small);
        }
    }
}
