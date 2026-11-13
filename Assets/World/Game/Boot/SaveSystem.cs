using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;
using World.Sim;
using World.Sim.Save;

namespace World.Game
{
    /// <summary>
    /// One save slot (F5 save, F9 load, and the pause menu). The file is the sim's <see cref="SaveCodec"/> JSON
    /// (the whole WorldState, including Ward progress) plus the game-layer data that is not in WorldState: the
    /// player's position and facing, the view mode and God View framing, the warehouse routes opened by the party,
    /// Bram's acknowledgement and the objective line.
    /// Loading is the sanctioned load path: the parsed state is handed to <see cref="WorldBootstrap"/> and the scene is
    /// reloaded, so every view, panel and NPC is rebuilt from the loaded state alone (like New game).
    /// Saving is refused during an encounter (the duel and the party are not part of the state) and while an order
    /// given this frame is still queued (it would be lost).
    /// </summary>
    public sealed class SaveSystem : MonoBehaviour
    {
        public const string SlotFileName = "worldmvp_save.json";
        /// <summary>Slot used when the game runs an -autoplay script, so smoke runs never touch the player's save.</summary>
        public const string AutoPlaySlotFileName = "worldmvp_autoplay_save.json";

        public static SaveSystem Instance { get; private set; }
        /// <summary>Tests: a full path that replaces the slot path.</summary>
        public static string PathOverride;
        /// <summary>Tests: a pause reason set on the loaded game's clock before its first tick (so the loaded state can be compared exactly).</summary>
        public static string TestPauseOnLoad;
        public static string SlotPath => PathOverride ?? Path.Combine(Application.persistentDataPath, AutoPlay.CommandLineScript() != null ? AutoPlaySlotFileName : SlotFileName);

        /// <summary>Last save or load message (sentence), for the pause menu and tests.</summary>
        public static string LastMessage { get; private set; }
        public static bool LastWasError { get; private set; }
        /// <summary>Number of completed loads in this process (tests wait for it to change).</summary>
        public static int LoadCount { get; private set; }

        // A parsed save waiting for the next scene load (set by Load, taken by WorldBootstrap.Awake).
        private static SaveModel s_pending;
        private SaveModel _applying;
        private WorldBootstrap _boot;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { s_pending = null; PathOverride = null; TestPauseOnLoad = null; LastMessage = null; LastWasError = false; LoadCount = 0; }

        /// <summary>WorldBootstrap.Awake: the save to start from, or null for a new game.</summary>
        public static SaveModel TakePending()
        {
            var m = s_pending; s_pending = null;
            return m;
        }

        public void Bind(WorldBootstrap boot, SaveModel loaded)
        {
            Instance = this;
            _boot = boot;
            _applying = loaded;
            if (loaded != null && !string.IsNullOrEmpty(TestPauseOnLoad)) boot.Clock.Pause(TestPauseOnLoad);
        }

        private void OnDestroy() { if (Instance == this) Instance = null; }

        private void Start()
        {
            if (_applying == null) return;
            var m = _applying; _applying = null;
            ApplyGameLayer(m);
            LoadCount++;
            Report("Game loaded (" + Describe(m.State) + ").", false);
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.F5)) Save();
            else if (Input.GetKeyDown(KeyCode.F9)) Load();
        }

        // ---------------- save ----------------

        /// <summary>Why saving is not possible right now (sentence), or null.</summary>
        public string WhyCannotSave()
        {
            if (_boot == null || _boot.Sim == null) return "The village is not running.";
            if (EncounterHost.Instance != null && EncounterHost.Instance.IsRunning) return "You can't save during the duel or the party. Finish or leave it first.";
            if (_boot.Sim.PendingCommands.Count > 0) return "Your last order is still being carried out. Try again in a moment.";
            return null;
        }

        public bool Save()
        {
            string why = WhyCannotSave();
            if (why != null) { Report("Not saved: " + why, true); return false; }
            try
            {
                string json = BuildJson();
                string path = SlotPath;
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                string tmp = path + ".tmp";
                File.WriteAllText(tmp, json);
                if (File.Exists(path)) File.Delete(path);
                File.Move(tmp, path);
                Debug.Log("[Save] saved " + json.Length + " chars to " + path + " at tick " + _boot.Sim.State.Tick);
                Report("Game saved (" + Describe(_boot.Sim.State) + "). F9 loads it.", false);
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Save] failed: " + e);
                Report("Not saved: the file could not be written (" + e.Message + ").", true);
                return false;
            }
        }

        /// <summary>The save text for the current game (the sim's codec plus the game-layer extras).</summary>
        public string BuildJson()
        {
            var a = _boot.Avatar;
            var pose = new PlayerPose { X = a.Position.x, Y = a.Position.y, Z = a.Position.z, Yaw = a.Yaw, Pitch = a.Pitch };
            var x = new Dictionary<string, string>
            {
                ["view"] = _boot.Views.CurrentView.ToString(),
                ["god.focus"] = V3(_boot.GodCam.Focus),
                ["god.size"] = F(_boot.GodCam.Size),
                ["objective"] = Hud.Objective ?? "",
                ["seed"] = _boot.Seed.ToString(CultureInfo.InvariantCulture),
            };
            return SaveCodec.ToJson(_boot.Sim.State, pose, x);
        }

        // ---------------- load ----------------

        public bool HasSave => File.Exists(SlotPath);

        /// <summary>Why loading is not possible right now (sentence), or null. Does not read the file.</summary>
        public string WhyCannotLoad()
        {
            if (_boot == null) return "The village is not running.";
            if (EncounterHost.Instance != null && EncounterHost.Instance.IsRunning) return "Leave the duel or the party before loading.";
            if (!HasSave) return "There is no save yet. Press F5 to save.";
            return null;
        }

        /// <summary>
        /// Reads and checks the slot; on success the scene reloads from the saved state (this object is destroyed).
        /// A missing, unreadable, older or newer save is refused with a sentence and the game goes on.
        /// </summary>
        public bool Load()
        {
            string why = WhyCannotLoad();
            if (why != null) { Report("Not loaded: " + why, true); return false; }
            SaveModel m;
            try
            {
                m = SaveCodec.FromJson(File.ReadAllText(SlotPath));
            }
            catch (FormatException e)
            {
                Debug.LogWarning("[Save] load refused: " + e.Message);
                string reason = e.Message.StartsWith("The save could not be read") ? "The save file is damaged or is not a save." : e.Message;
                Report("Not loaded: " + reason + (reason.Contains("older") ? " Start a new game; old saves can't be read." : ""), true);
                return false;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Save] load failed: " + e);
                Report("Not loaded: the save could not be read (" + e.Message + ").", true);
                return false;
            }
            Debug.Log("[Save] loading tick " + m.State.Tick + " from " + SlotPath);
            s_pending = m;
            UnityEngine.SceneManagement.SceneManager.LoadScene(_boot.gameObject.scene.name);
            return true;
        }

        private void ApplyGameLayer(SaveModel m)
        {
            var x = m.Extras;
            if (m.Player != null)
                _boot.Avatar.Teleport(new Vector3((float)m.Player.X, (float)m.Player.Y, (float)m.Player.Z), (float)m.Player.Yaw, (float)m.Player.Pitch);
            if (x.TryGetValue("objective", out var obj) && !string.IsNullOrEmpty(obj)) Hud.SetObjective(obj);
            if (x.TryGetValue("view", out var view) && view == ViewMode.God.ToString()) _boot.Views.SetView(ViewMode.God);
            if (x.TryGetValue("god.focus", out var f) && TryV3(f, out var focus))
            {
                float? size = x.TryGetValue("god.size", out var sz) && float.TryParse(sz, NumberStyles.Float, CultureInfo.InvariantCulture, out var s) ? s : (float?)null;
                _boot.GodCam.SetFocus(focus, size);
            }
        }

        // ---------------- helpers ----------------

        private static string Describe(WorldState s)
        {
            float min = s.Tick / (float)(WorldSim.TicksPerSecond * 60);
            return "game minute " + min.ToString("0.0", CultureInfo.InvariantCulture);
        }

        private static void Report(string text, bool error)
        {
            LastMessage = text; LastWasError = error;
            Hud.Notice(text, error);
            if (error) Debug.Log("[Save] " + text);
        }

        private static string F(float v) => v.ToString("R", CultureInfo.InvariantCulture);
        private static string V3(Vector3 v) => F(v.x) + "," + F(v.y) + "," + F(v.z);
        private static bool TryV3(string s, out Vector3 v)
        {
            v = default;
            var p = s.Split(',');
            if (p.Length != 3) return false;
            if (!float.TryParse(p[0], NumberStyles.Float, CultureInfo.InvariantCulture, out v.x)) return false;
            if (!float.TryParse(p[1], NumberStyles.Float, CultureInfo.InvariantCulture, out v.y)) return false;
            return float.TryParse(p[2], NumberStyles.Float, CultureInfo.InvariantCulture, out v.z);
        }
    }
}
