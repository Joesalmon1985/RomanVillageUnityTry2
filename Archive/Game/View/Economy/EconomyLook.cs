using UnityEngine;
using World.Sim;
using World.Sim.Economy;

namespace World.Game
{
    /// <summary>How a building's badge reads: working, idle, waiting, blocked (stalled hard), switched off, or a site.</summary>
    public enum EconomyBadge { Working, Idle, Waiting, Blocked, Off, Site }

    /// <summary>
    /// Colours and short words shared by the economy views and the God View panels. Names, colours and sentences
    /// come from the sim's helpers (<see cref="EconomyApi"/>); this class only turns them into Unity colours and
    /// one- or two-word label text.
    /// </summary>
    public static class EconomyLook
    {
        /// <summary>
        /// The quartermaster's six commission buildings. Mirrors the sim's internal <c>EconomyLogic.CommissionTypes</c>
        /// (not public; see Docs/ECONOMY_UI.md, "Needed from the sim").
        /// </summary>
        public static readonly BuildingType[] CommissionTypes =
        {
            BuildingType.Forester, BuildingType.Woodcutter, BuildingType.Sawmill,
            BuildingType.ClayPit, BuildingType.CharcoalKiln, BuildingType.BrickKiln,
        };

        private static Color[] _wareColours;

        /// <summary>Unity colour of a ware, parsed once from <see cref="EconomyApi.WareColour"/>.</summary>
        public static Color WareColor(Ware w)
        {
            if (_wareColours == null)
            {
                _wareColours = new Color[Wares.Count];
                for (int i = 0; i < Wares.Count; i++)
                    _wareColours[i] = ColorUtility.TryParseHtmlString(EconomyApi.WareColour((Ware)i), out var c) ? c : Color.magenta;
            }
            int k = (int)w;
            return k >= 0 && k < _wareColours.Length ? _wareColours[k] : Color.magenta;
        }

        /// <summary>A ware colour that reads as text on the dark panels (very dark wares such as coal are lifted).</summary>
        public static Color WareTextColor(Ware w)
        {
            var c = WareColor(w);
            float l = 0.3f * c.r + 0.59f * c.g + 0.11f * c.b;
            return l < 0.45f ? Color.Lerp(c, Color.white, 0.55f) : c;
        }

        public static string WareName(Ware w) => EconomyApi.WareName(w);
        public static string WareTitle(Ware w) => EconomyApi.WareTitle(w);
        public static string Hex(Color c) => "#" + ColorUtility.ToHtmlStringRGB(c);

        /// <summary>"<color=#..>Planks</color>" for rich text.</summary>
        public static string WareTag(Ware w) => "<color=" + Hex(WareTextColor(w)) + ">" + WareTitle(w) + "</color>";

        public static Color BadgeColor(EconomyBadge b)
        {
            switch (b)
            {
                case EconomyBadge.Working: return new Color(0.35f, 0.92f, 0.35f);
                case EconomyBadge.Idle: return new Color(0.55f, 0.85f, 0.95f);
                case EconomyBadge.Waiting: return new Color(1.00f, 0.72f, 0.18f);
                case EconomyBadge.Blocked: return new Color(1.00f, 0.25f, 0.20f);
                case EconomyBadge.Off: return new Color(0.55f, 0.55f, 0.55f);
                default: return new Color(0.95f, 0.85f, 0.40f);
            }
        }

        /// <summary>True when the robber stops every hex of the building's terrain that its flag touches.</summary>
        public static bool RobbedHarvester(WorldState s, Building b)
        {
            var def = EconomyApi.Def(s, b.Type);
            if (def.Kind != BuildingKind.Harvester || s.Economy.HexStopped == null) return false;
            bool any = false;
            foreach (int h in s.Board.Topology.VertexHexes(b.Vertex))
            {
                if (s.Board.HexTerrain[h] != def.Terrain) continue;
                if (h >= s.Economy.HexStopped.Length || !s.Economy.HexStopped[h]) return false;
                any = true;
            }
            return any;
        }

        /// <summary>Badge from the state: Blocked (a demon or raid on its flag) and HexStopped (robber) count as blocked.</summary>
        public static EconomyBadge BadgeOf(WorldState s, Building b)
        {
            var def = EconomyApi.Def(s, b.Type);
            if (b.Blocked) return EconomyBadge.Blocked;
            if (b.State == BuildingState.Site || b.State == BuildingState.Constructing)
                return b.Reason == Reasons.NoRoad || b.Reason == Reasons.DemonOnRoute ? EconomyBadge.Blocked
                     : b.Reason != null ? EconomyBadge.Waiting : EconomyBadge.Site;
            if (!b.Enabled) return EconomyBadge.Off;
            if (RobbedHarvester(s, b)) return EconomyBadge.Blocked;
            switch (b.Reason)
            {
                case null:
                    return b.WorkProgress > 0 || def.Kind == BuildingKind.Forester || def.Kind == BuildingKind.Store
                           || (def.Kind == BuildingKind.PowerPlant && b.FuelLeft > 0) ? EconomyBadge.Working : EconomyBadge.Idle;
                case Reasons.NoRoad:
                case Reasons.DemonOnRoute:
                case Reasons.NoPower:
                    return EconomyBadge.Blocked;
                case Reasons.OutputFull: return EconomyBadge.Idle; // stock target reached: normal, not a problem
                case Reasons.Disabled: return EconomyBadge.Off;
                default: return EconomyBadge.Waiting;
            }
        }

        /// <summary>Two or three words for a floating label ("working", "waiting for logs", "no road").</summary>
        public static string ShortStatus(WorldState s, Building b)
        {
            var def = EconomyApi.Def(s, b.Type);
            string ware = b.ReasonWare >= 0 ? WareName((Ware)b.ReasonWare) : null;
            if (b.Blocked) return "demon on its flag";
            if (b.State == BuildingState.Constructing && b.Reason == null) return "building " + Percent(b.BuildProgress, def.BuildTicks);
            if (!b.Enabled && b.State == BuildingState.Active) return "switched off";
            if (b.State == BuildingState.Active && RobbedHarvester(s, b)) return "hex stopped by demons";
            switch (b.Reason)
            {
                case null:
                    if (b.State == BuildingState.Site) return "materials arriving";
                    if (b.WorkProgress > 0) return "working";
                    if (def.Kind == BuildingKind.PowerPlant && b.FuelLeft > 0) return "supplying power";
                    return def.Kind == BuildingKind.Forester ? "tending the forest" : "ready";
                case Reasons.NoRoad: return "no road";
                case Reasons.NoCarrier: return "no free carrier";
                case Reasons.WaitingWare: return ware != null ? "waiting for " + ware : "waiting for goods";
                case Reasons.DemonOnRoute: return "demon on the route";
                case Reasons.OutputFull: return "output full";
                case Reasons.InputMissing: return ware != null ? "needs " + ware : "input missing";
                case Reasons.NoPower: return "no power";
                case Reasons.NoBatch: return "no batch this round";
                case Reasons.Disabled: return "switched off";
                default: return b.Reason.Replace('_', ' ');
            }
        }

        public static string Percent(int a, int b) => (100 * a / Mathf.Max(1, b)) + "%";

        /// <summary>The open delivery job with this id, or null.</summary>
        public static DeliveryJob Job(WorldState s, int id)
        {
            foreach (var j in s.Economy.Jobs) if (j.Id == id) return j;
            return null;
        }

        /// <summary>True for Site and Constructing.</summary>
        public static bool IsSite(Building b) => b.State == BuildingState.Site || b.State == BuildingState.Constructing;
    }
}
