using System.Collections.Generic;
using UnityEngine;
using World.Sim;
using World.Sim.Military;
using World.Sim.Strategy;

namespace World.Game
{
    /// <summary>
    /// Strategy and military God View tools in the toolbar gaps: Market (62, T), Cards (65, C), City (70, F),
    /// Hero (72, J), Army (75, N: barracks, shields, squads and their orders), Wonder (80, O).
    /// </summary>
    public static class StrategyGodTools
    {
        public const string MarketId = "market", CardsId = "cards", CityId = "city", HeroId = "hero", ArmyId = "army", WonderId = "wonder";

        private static WorldState S => WorldBootstrap.Instance.Sim.State;

        public static void RegisterDefaults()
        {
            GodToolbar.Register(PanelTool(MarketId, "Market", 62, KeyCode.T, StrategyPanels.Kind.Market, 640f,
                () => "Trade goods " + StrategyApi.TradeRate(S) + " : 1, sell for coin or buy with coin.", null));
            GodToolbar.Register(PanelTool(CardsId, "Cards", 65, KeyCode.C, StrategyPanels.Kind.Cards, 640f,
                () => "Your hand (build or discard) and the cards built at your cities.",
                () => StrategyApi.DraftPending(S) ? "<color=#FFD070>(draft)</color>" : S.Strategy.Sides[PlayerIds.Human].Hand.Count > 0 ? "(" + S.Strategy.Sides[PlayerIds.Human].Hand.Count + ")" : null));
            GodToolbar.Register(new CityTool());
            GodToolbar.Register(new HeroTool());
            GodToolbar.Register(new ArmyTool());
            GodToolbar.Register(PanelTool(WonderId, "Wonder", 80, KeyCode.O, StrategyPanels.Kind.Wonder, 560f,
                () => StrategyApi.Wonder(S).Status, null));
        }

        public static GodTool PanelTool(string id, string label, int order, KeyCode key, StrategyPanels.Kind kind, float width, System.Func<string> tip, System.Func<string> badge) => new GodTool
        {
            Id = id, Label = label, Order = order, Hotkey = key, PanelWidth = width, TipText = tip, BadgeText = badge,
            DrawPanel = (t, area) =>
            {
                var f = Flow.Begin(id, area);
                f.Title(StrategyPanels.TitleOf(kind));
                StrategyPanels.Draw(kind, f, S);
                f.End();
            },
        };

        /// <summary>Opens the Army tool in God View so the armed squad order takes the next flag click.</summary>
        public static void OpenArmyTargeting()
        {
            var b = WorldBootstrap.Instance;
            if (StrategyWindow.IsOpen) StrategyWindow.Close();
            if (b.Views.CurrentView != ViewMode.God) b.Views.SetView(ViewMode.God);
            GodToolbar.Instance?.Activate(ArmyId);
        }

        public static CityTool City => GodToolbar.Find(CityId) as CityTool;
        public static HeroTool Hero => GodToolbar.Find(HeroId) as HeroTool;
    }

    /// <summary>Found City: legal corners glow (green: affordable now, amber: only the cost is missing); hover shows the sim's verdict.</summary>
    public sealed class CityTool : GodTool
    {
        public int HoverCorner { get; private set; } = -1;
        public CommandResult HoverResult { get; private set; }
        public List<int> Legal { get; } = new List<int>();
        public List<int> LegalIgnoringCost { get; } = new List<int>();
        private float _refreshAt;
        private int _shownHover = -2;

        public CityTool()
        {
            Id = StrategyGodTools.CityId; Label = "City"; Order = 70; Hotkey = KeyCode.F; CapturesClicks = true; PanelWidth = 560f;
            TipText = () => "Found a city on a glowing hex corner (" + StrategyApi.CityCostText(WorldBootstrap.Instance.Sim.State) + ").";
        }

        public override bool HasPanel => true;
        public override void Activate() { _refreshAt = 0; _shownHover = -2; base.Activate(); }
        public override void Deactivate() { GodOverlay.Hide(); HoverCorner = -1; base.Deactivate(); }

        public void Refresh()
        {
            var s = WorldBootstrap.Instance.Sim.State;
            _refreshAt = Time.unscaledTime + 0.5f;
            Legal.Clear(); LegalIgnoringCost.Clear();
            Legal.AddRange(StrategyApi.LegalCitySites(s));
            LegalIgnoringCost.AddRange(StrategyApi.LegalCitySites(s, true));
            _shownHover = -2;
        }

        public CommandResult TryFound(int corner) { var r = StrategyUi.FoundCity(corner); _refreshAt = 0; return r; }

        public override void Update()
        {
            if (Time.unscaledTime >= _refreshAt) Refresh();
            int c = GodToolbar.MouseOverUi() ? -1 : BoardPicker.CornerUnderMouse();
            if (c != HoverCorner) { HoverCorner = c; HoverResult = c >= 0 ? StrategyApi.CheckCity(WorldBootstrap.Instance.Sim.State, c) : default; }
            if (_shownHover != HoverCorner)
            {
                _shownHover = HoverCorner;
                var items = new List<GodOverlay.Item>();
                foreach (int v in LegalIgnoringCost)
                    items.Add(new GodOverlay.Item(v, Legal.Contains(v) ? GodOverlay.Valid : new Color(1f, 0.7f, 0.15f), 7f));
                if (HoverCorner >= 0) items.Add(new GodOverlay.Item(HoverCorner, HoverResult.Ok ? GodOverlay.Hover : GodOverlay.Invalid, 9f));
                GodOverlay.ShowVertices(items);
            }
        }

        public override void WorldClick() { if (HoverCorner >= 0) TryFound(HoverCorner); }

        public override void Overlay()
        {
            if (HoverCorner < 0) return;
            var s = WorldBootstrap.Instance.Sim.State;
            GodToolbar.Tooltip(HoverResult.Ok ? "<b>Found a city at " + StrategyUi.SiteName(s, HoverCorner) + "</b> for " + StrategyApi.CityCostText(s) + ". Click."
                                              : "<color=#FF9A80>" + HoverResult.Message + "</color>");
        }

        public override void Panel(Rect area)
        {
            var f = Flow.Begin(Id, area);
            f.Title("Found a city");
            StrategyPanels.Cities(f, WorldBootstrap.Instance.Sim.State);
            f.End();
        }
    }

    /// <summary>Move Hero: the neighbouring corners glow; click one. Shows moves left and the demons.</summary>
    public sealed class HeroTool : GodTool
    {
        public int HoverCorner { get; private set; } = -1;
        public List<int> Targets { get; } = new List<int>();
        private long _sig = long.MinValue;
        private int _shownHover = -2;

        public HeroTool()
        {
            Id = StrategyGodTools.HeroId; Label = "Hero"; Order = 72; Hotkey = KeyCode.J; CapturesClicks = true; PanelWidth = 560f;
            TipText = () => "Move the hero one hex edge at a time; it clears demons where it walks.";
            BadgeText = () =>
            {
                var s = WorldBootstrap.Instance.Sim.State;
                return s.Strategy.Started ? "(" + s.Strategy.HeroMovesLeft + ")" : null;
            };
        }

        public override bool HasPanel => true;
        public override void Activate() { _sig = long.MinValue; _shownHover = -2; base.Activate(); }
        public override void Deactivate() { GodOverlay.Hide(); HoverCorner = -1; base.Deactivate(); }

        public void Refresh()
        {
            var s = WorldBootstrap.Instance.Sim.State;
            long sig = s.Strategy.HeroVertex * 1000L + s.Strategy.HeroMovesLeft * 10 + (s.Strategy.GameOver ? 1 : 0);
            if (sig == _sig) return;
            _sig = sig;
            Targets.Clear();
            Targets.AddRange(StrategyApi.HeroMoveTargets(s));
            _shownHover = -2;
        }

        public CommandResult TryMove(int corner) => StrategyUi.MoveHero(corner);

        public override void Update()
        {
            Refresh();
            int c = GodToolbar.MouseOverUi() ? -1 : BoardPicker.CornerUnderMouse();
            HoverCorner = c;
            if (_shownHover != HoverCorner)
            {
                _shownHover = HoverCorner;
                var items = new List<GodOverlay.Item>();
                foreach (int v in Targets) items.Add(new GodOverlay.Item(v, v == HoverCorner ? GodOverlay.Hover : GodOverlay.Valid, v == HoverCorner ? 9f : 7f));
                if (HoverCorner >= 0 && !Targets.Contains(HoverCorner)) items.Add(new GodOverlay.Item(HoverCorner, GodOverlay.Invalid, 7f));
                GodOverlay.ShowVertices(items);
            }
        }

        public override void WorldClick() { if (HoverCorner >= 0) TryMove(HoverCorner); }

        public override void Overlay()
        {
            if (HoverCorner < 0) return;
            var s = WorldBootstrap.Instance.Sim.State;
            if (Targets.Contains(HoverCorner))
            {
                int he = s.Board.Topology.HexEdgeBetween(s.Strategy.HeroVertex, HoverCorner);
                int demons = 0;
                if (he >= 0) foreach (int v in s.Board.Topology.HexEdgeFlags(he)) if (v != s.Strategy.HeroVertex) demons += StrategyApi.DemonsAt(s, v);
                GodToolbar.Tooltip("<b>Walk to " + StrategyUi.SiteName(s, HoverCorner) + "</b>: clears " + demons + (demons == 1 ? " demon" : " demons") + " on the way. " + s.Strategy.HeroMovesLeft + " moves left.");
            }
            else
            {
                var r = GodCommands.Check(new MoveHero { Vertex = HoverCorner });
                GodToolbar.Tooltip("<color=#FF9A80>" + (r.Ok ? "Pick a glowing corner." : r.Message) + "</color>");
            }
        }

        public override void Panel(Rect area)
        {
            var f = Flow.Begin(Id, area);
            f.Title("Hero");
            StrategyPanels.Hero(f, WorldBootstrap.Instance.Sim.State);
            f.End();
        }
    }

    /// <summary>Barracks, shields and squads. While a squad order is armed, a click on a flag sends it there (Attack).</summary>
    public sealed class ArmyTool : GodTool
    {
        public int HoverFlag { get; private set; } = -1;

        public ArmyTool()
        {
            Id = StrategyGodTools.ArmyId; Label = "Army"; Order = 75; Hotkey = KeyCode.N; PanelWidth = 600f;
            TipText = () => MilitaryApi.ShieldSummary(WorldBootstrap.Instance.Sim.State).Sentence;
            BadgeText = () =>
            {
                var s = WorldBootstrap.Instance.Sim.State;
                int bid = StrategyUi.FirstBarracks(s, true);
                if (bid >= 0 && !MilitaryApi.IsPowered(s, bid)) return "<color=#FF8070>!</color>";
                int n = 0;
                foreach (var q in s.Military.Squads) if (q.Alive && q.Owner == PlayerIds.Human) n += q.Units.Count;
                return n > 0 ? "(" + n + ")" : null;
            };
        }

        public override bool HasPanel => true;
        public override void Deactivate() { StrategyPanels.ArmedSquad = -1; GodOverlay.Hide(); base.Deactivate(); }

        public override void Update()
        {
            CapturesClicks = StrategyPanels.ArmedSquad >= 0;
            HoverFlag = CapturesClicks && !GodToolbar.MouseOverUi() ? BoardPicker.VertexUnderMouse() : -1;
            if (CapturesClicks)
                GodOverlay.ShowVertices(HoverFlag >= 0 ? new[] { new GodOverlay.Item(HoverFlag, GodOverlay.Hover, 7f) } : new GodOverlay.Item[0]);
        }

        public override void WorldClick()
        {
            if (StrategyPanels.ArmedSquad < 0 || HoverFlag < 0) return;
            var r = StrategyUi.OrderSquad(StrategyPanels.ArmedSquad, StrategyPanels.ArmedOrder, HoverFlag);
            if (r.Ok) { StrategyPanels.ArmedSquad = -1; GodOverlay.Hide(); }
        }

        public override void Overlay()
        {
            if (StrategyPanels.ArmedSquad < 0) return;
            var s = WorldBootstrap.Instance.Sim.State;
            GodToolbar.Tooltip(HoverFlag >= 0 ? "Send squad " + StrategyPanels.ArmedSquad + " to attack " + StrategyUi.SiteName(s, HoverFlag) + ". Click." : "Click a flag to send squad " + StrategyPanels.ArmedSquad + " there. Right click cancels.");
        }

        public override void Panel(Rect area)
        {
            var f = Flow.Begin(Id, area);
            f.Title("Army");
            StrategyPanels.Army(f, WorldBootstrap.Instance.Sim.State);
            f.End();
        }
    }
}
