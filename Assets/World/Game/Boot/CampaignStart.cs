using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;
using World.Sim;
using World.Sim.Campaign;
using World.Sim.Eras;

namespace World.Game
{
    /// <summary>
    /// Starts the crystal campaign (PRD_CRYSTALS 8.1, package I8): the developer start <c>dev.historic</c>, then
    /// <c>StartCampaign</c> with a shipped pack. From the F1 tool "Campaign" (<see cref="CampaignDevTool"/>) or the
    /// command line <c>-start dev.historic -campaign authored_v1</c>. A normal launch never starts it (the prince's
    /// handover scene that leads into it is a later package). Commands only.
    /// </summary>
    public static class CampaignStart
    {
        public const string DefaultPack = "authored_v1";
        /// <summary>The pack waiting for the next scene (set by <see cref="Begin"/> before the reload).</summary>
        private static string s_pendingPack;
        private static bool s_commandLineTaken, s_hooked;
        /// <summary>Why the last start failed (sentence), or null.</summary>
        public static string LastError { get; private set; }

        private static WorldBootstrap B => WorldBootstrap.Instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { s_pendingPack = null; s_commandLineTaken = false; LastError = null; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Hook()
        {
            if (!s_hooked) { SceneManager.sceneLoaded += (_, __) => Kick(); s_hooked = true; }
            Kick(); // the first scene of a run is already loaded
        }

        private static void Kick()
        {
            string pack = s_pendingPack;
            s_pendingPack = null;
            if (pack == null && !s_commandLineTaken)
            {
                s_commandLineTaken = true;
                pack = Arg("-campaign");
            }
            if (pack == null) return;
            var host = new GameObject("CampaignStart (once)");
            host.AddComponent<Runner>().Pack = pack;
        }

        /// <summary>
        /// Starts the campaign on <paramref name="pack"/>: at once on a running <c>dev.historic</c> world without a
        /// campaign, else after the scene is rebuilt on <c>dev.historic</c>.
        /// </summary>
        public static bool Begin(string pack = DefaultPack)
        {
            LastError = null;
            if (B != null && B.Sim != null && DevStart.Current == ScenarioStarts.DevHistoric && B.Sim.State.Campaign == null)
                return StartNow(pack);
            s_pendingPack = pack;
            if (!DevStart.Begin(ScenarioStarts.DevHistoric)) { s_pendingPack = null; LastError = DevStart.LastError; return false; }
            return true;
        }

        /// <summary>Sends StartCampaign with the pack (applied at the next tick). False with <see cref="LastError"/> when refused.</summary>
        public static bool StartNow(string pack)
        {
            if (B == null || B.Sim == null) { LastError = "The world is not running."; return false; }
            if (B.Sim.State.Campaign != null) { LastError = "A campaign is already running."; return false; }
            CampaignDefinition d;
            try { d = CampaignLoader.LoadDirectory(Path.Combine(Application.streamingAssetsPath, "world", "campaigns", pack)); }
            catch (Exception e) { LastError = "The pack \"" + pack + "\" could not be read (" + e.Message + ")."; Debug.LogError("[Campaign] " + LastError); return false; }
            var r = B.Sim.Submit(new StartCampaign { Definition = d });
            if (!r.Ok) { LastError = "StartCampaign refused: " + r.Message; Debug.LogError("[Campaign] " + LastError); return false; }
            Debug.Log("[Campaign] StartCampaign " + d.Id + " (" + pack + ") on " + (DevStart.Current ?? "this world") + " at tick " + B.Sim.State.Tick);
            return true;
        }

        private static string Arg(string name)
        {
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++) if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase)) return args[i + 1];
            return null;
        }

        /// <summary>Waits for the scene's world, then starts the campaign once (a dev.historic world only).</summary>
        private sealed class Runner : MonoBehaviour
        {
            public string Pack;
            private IEnumerator Start()
            {
                float until = Time.realtimeSinceStartup + 60f;
                while ((B == null || B.Sim == null) && Time.realtimeSinceStartup < until) yield return null;
                yield return null;
                if (B != null && B.Sim != null && B.Sim.State.Campaign == null)
                {
                    if (DevStart.Current != ScenarioStarts.DevHistoric) Debug.LogWarning("[Campaign] -campaign needs -start dev.historic; the campaign was not started.");
                    else StartNow(Pack);
                }
                Destroy(gameObject);
            }
        }
    }
}
