using System.Collections.Generic;
using UnityEngine;

namespace World.Game
{
    /// <summary>
    /// Tags a world object with the wireframe zone it serves (WORLD_UI_PLAN section 2: <c>HUB-TEMPLE</c>,
    /// <c>HEX-STORE</c>, ...). Read by tests and by the editor-only zone outline (F1 "Zones"); it draws nothing itself
    /// and changes no behaviour, so it stays in builds.
    /// </summary>
    public sealed class WorldZone : MonoBehaviour
    {
        public string ZoneId;

        private static readonly List<WorldZone> _all = new List<WorldZone>();
        public static IReadOnlyList<WorldZone> All => _all;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => _all.Clear();

        private void OnEnable() { if (!_all.Contains(this)) _all.Add(this); }
        private void OnDisable() => _all.Remove(this);

        public static WorldZone Tag(GameObject go, string zoneId)
        {
            var z = go.GetComponent<WorldZone>() ?? go.AddComponent<WorldZone>();
            z.ZoneId = zoneId;
            return z;
        }

        /// <summary>The wireframe of a zone id ("HUB", "HEX", ...): the part before the first dash.</summary>
        public static string WireframeOf(string zoneId)
        {
            if (string.IsNullOrEmpty(zoneId)) return null;
            int i = zoneId.IndexOf('-');
            return i > 0 ? zoneId.Substring(0, i) : zoneId;
        }
    }

    /// <summary>
    /// The zone ids of WORLD_UI_PLAN section 2 and where each wireframe puts its zone, as a rectangle normalised to
    /// the wireframe's play frame (0..1, top-left origin). Measured from the owner's six layout diagrams; only the
    /// rectangles and our ids are kept here (no colours, no labels from the diagrams). The duel has no wireframe.
    /// </summary>
    public static class WorldZones
    {
        public const string Hub = "HUB", Hex = "HEX", Plague = "PLG", Council = "CNC", Rhythm = "RHY", Crystal = "CRY", Duel = "DUEL";
        public static readonly string[] Wireframes = { Hub, Hex, Plague, Council, Rhythm, Crystal, Duel };

        public const string HubTemple = "HUB-TEMPLE", HubCouncil = "HUB-COUNCIL", HubRhythm = "HUB-RHYTHM", HubHealer = "HUB-HEALER",
            HubCave = "HUB-CAVE", HubDuel = "HUB-DUEL", HubStart = "HUB-START", HubPrince = "HUB-PRINCE", HubMap = "HUB-MAP";
        public const string HexBoard = "HEX-BOARD", HexTurn = "HEX-TURN", HexStore = "HEX-STORE", HexDev = "HEX-DEV", HexAwards = "HEX-AWARDS",
            HexBuild = "HEX-BUILD", HexTrade = "HEX-TRADE", HexRaider = "HEX-RAIDER", HexEnd = "HEX-END", HexFeed = "HEX-FEED",
            HexLeave = "HEX-LEAVE", HexPlague = "HEX-PLAGUE";
        public const string PlgMap = "PLG-MAP", PlgActions = "PLG-ACTIONS", PlgDanger = "PLG-DANGER", PlgCures = "PLG-CURES", PlgDeck = "PLG-DECK",
            PlgRoles = "PLG-ROLES", PlgHands = "PLG-HANDS", PlgCity = "PLG-CITY", PlgPhase = "PLG-PHASE";
        public const string CncAF = "CNC-AF", CncGL = "CNC-GL", CncM = "CNC-M", CncQRS = "CNC-QRS", CncNOP = "CNC-NOP", CncT = "CNC-T",
            CncU = "CNC-U", CncStatus = "CNC-STATUS", CncVZ = "CNC-VZ", CncExit = "CNC-EXIT";
        public const string RhyStage = "RHY-STAGE", RhyPad1 = "RHY-PAD1", RhyPad2 = "RHY-PAD2", RhyPad3 = "RHY-PAD3", RhyPad4 = "RHY-PAD4",
            RhyFeedback = "RHY-FEEDBACK", RhyProgress = "RHY-PROGRESS", RhyTune = "RHY-TUNE", RhySelect = "RHY-SELECT", RhyResult = "RHY-RESULT", RhyPause = "RHY-PAUSE";
        public const string CryView = "CRY-VIEW", CryMove = "CRY-MOVE", CryCombat = "CRY-COMBAT", CryInteract = "CRY-INTERACT", CrySpell = "CRY-SPELL",
            CryParty = "CRY-PARTY", CryPack = "CRY-PACK", CryCamp = "CRY-CAMP";
        public const string DuelStage = "DUEL-STAGE", DuelWeave = "DUEL-WEAVE", DuelState = "DUEL-STATE", DuelEnd = "DUEL-END";

        // The diagrams' play frame, in their own pixels: x 20..1346, y 116..964.
        private const float FX = 20f, FY = 116f, FW = 1326f, FH = 848f;
        private static Rect R(float x0, float y0, float x1, float y1) => new Rect((x0 - FX) / FW, (y0 - FY) / FH, (x1 - x0) / FW, (y1 - y0) / FH);

        /// <summary>Zone id to its normalised wireframe rectangle (zones of ours that the diagrams lack have none).</summary>
        public static readonly Dictionary<string, Rect> Rects = new Dictionary<string, Rect>
        {
            { HubTemple, R(72, 208, 416, 448) }, { HubCouncil, R(471, 204, 948, 468) }, { HubRhythm, R(1010, 205, 1298, 598) },
            { HubDuel, R(72, 509, 417, 729) }, { HubHealer, R(468, 528, 708, 732) }, { HubCave, R(756, 532, 1003, 735) }, { HubStart, R(457, 782, 908, 901) },

            { HexBoard, R(45, 160, 911, 740) }, { HexTurn, R(932, 161, 1319, 304) }, { HexStore, R(931, 318, 1318, 475) }, { HexDev, R(932, 487, 1319, 611) },
            { HexAwards, R(933, 627, 1320, 741) }, { HexBuild, R(45, 758, 328, 931) }, { HexTrade, R(343, 758, 620, 931) }, { HexRaider, R(637, 758, 943, 931) },
            { HexEnd, R(962, 758, 1319, 931) },

            { PlgMap, R(44, 162, 957, 748) }, { PlgActions, R(978, 161, 1314, 294) }, { PlgDanger, R(978, 311, 1314, 443) }, { PlgCures, R(978, 460, 1314, 589) },
            { PlgDeck, R(978, 606, 1314, 747) }, { PlgRoles, R(45, 764, 342, 931) }, { PlgHands, R(359, 764, 669, 931) }, { PlgCity, R(686, 764, 1003, 931) },
            { PlgPhase, R(1020, 764, 1314, 931) },

            { CncAF, R(44, 153, 1076, 441) }, { CncGL, R(45, 460, 1078, 609) }, { CncM, R(44, 630, 358, 751) }, { CncQRS, R(371, 628, 716, 751) },
            { CncStatus, R(732, 628, 1077, 752) }, { CncVZ, R(1094, 153, 1329, 753) }, { CncNOP, R(44, 769, 682, 930) }, { CncT, R(699, 770, 880, 930) },
            { CncU, R(899, 770, 1079, 930) }, { CncExit, R(1094, 769, 1329, 930) },

            { RhyStage, R(46, 160, 976, 670) }, { RhyPad1, R(63, 498, 269, 643) }, { RhyPad2, R(288, 498, 494, 643) }, { RhyPad3, R(513, 498, 719, 643) },
            { RhyPad4, R(738, 498, 944, 643) }, { RhyFeedback, R(998, 161, 1317, 311) }, { RhyProgress, R(998, 329, 1317, 480) }, { RhyTune, R(998, 499, 1317, 669) },
            { RhySelect, R(45, 697, 447, 932) }, { RhyResult, R(467, 697, 861, 932) }, { RhyPause, R(881, 697, 1318, 932) },

            { CryMove, R(40, 160, 305, 516) }, { CryView, R(324, 160, 1059, 678) }, { CryCombat, R(1080, 160, 1320, 330) }, { CryInteract, R(1080, 342, 1320, 511) },
            { CrySpell, R(1080, 522, 1320, 677) }, { CryParty, R(40, 696, 530, 931) }, { CryPack, R(545, 696, 920, 931) }, { CryCamp, R(938, 696, 1321, 931) },
        };

        /// <summary>All ids of section 2 (with and without a wireframe rectangle).</summary>
        public static IEnumerable<string> AllIds()
        {
            foreach (var f in typeof(WorldZones).GetFields())
                if (f.IsLiteral && f.FieldType == typeof(string) && ((string)f.GetValue(null)).Contains("-")) yield return (string)f.GetValue(null);
        }

        /// <summary>A normalised wireframe rectangle placed on a screen area (GUI coordinates).</summary>
        public static Rect OnScreen(Rect normalised, Rect area) =>
            new Rect(area.x + normalised.x * area.width, area.y + normalised.y * area.height, normalised.width * area.width, normalised.height * area.height);
    }
}
