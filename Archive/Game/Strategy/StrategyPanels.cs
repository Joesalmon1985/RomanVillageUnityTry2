using System.Collections.Generic;
using UnityEngine;
using World.Sim;
using World.Sim.Economy;
using World.Sim.Military;
using World.Sim.Strategy;

namespace World.Game
{
    /// <summary>
    /// Panel bodies shared by the God View tools (left column) and the centred <see cref="StrategyWindow"/> (draft modal,
    /// first-person NPCs). Each draws with <see cref="Flow"/> from the state and acts only through <see cref="StrategyUi"/>.
    /// All numbers, names and costs come from the sim's helpers (StrategyApi, MilitaryApi, EconomyApi).
    /// </summary>
    public static class StrategyPanels
    {
        public enum Kind { Draft, Cards, Market, Wonder, Army, Cities, Hero }

        public static string TitleOf(Kind k)
        {
            switch (k)
            {
                case Kind.Draft: return "Draft";
                case Kind.Cards: return "Cards";
                case Kind.Market: return "Market";
                case Kind.Wonder: return "Wonder";
                case Kind.Army: return "Barracks and squads";
                case Kind.Cities: return "Cities";
                default: return "Hero";
            }
        }

        public static void Draw(Kind k, Flow f, WorldState s)
        {
            switch (k)
            {
                case Kind.Draft: Draft(f, s); break;
                case Kind.Cards: Cards(f, s); break;
                case Kind.Market: Market(f, s); break;
                case Kind.Wonder: Wonder(f, s); break;
                case Kind.Army: Army(f, s); break;
                case Kind.Cities: Cities(f, s); break;
                case Kind.Hero: Hero(f, s); break;
            }
        }

        private static readonly Color Dim = new Color(1f, 1f, 1f, 0.12f);

        // ---------------- cards ----------------

        private static float CardBox(Flow f, CardView c, string extra)
        {
            string body = "<b>" + c.Name + "</b>   " + StrategyUi.FamilyTag(c) + "   <color=#BBB0A0>Age " + StrategyApi.Roman(c.Age) + "</color>\n"
                + c.EffectText + "\n<color=#D8C890>Cost: " + c.CostText + "</color>" + (string.IsNullOrEmpty(extra) ? "" : "\n" + extra);
            float pad = UiStyle.Px(14);
            float h = Flow.TextStyle.CalcHeight(new GUIContent(body), f.W - pad * 2) + UiStyle.Px(10);
            var r = f.Box(h, StrategyUi.FamilyColor(c));
            GUI.Label(new Rect(r.x + pad, r.y + UiStyle.Px(5), r.width - pad * 2, h), body, Flow.TextStyle);
            f.Y += h + UiStyle.Px(4);
            return h;
        }

        public static void Draft(Flow f, WorldState s)
        {
            var st = s.Strategy;
            var t = st.Tuning;
            if (!StrategyApi.DraftPending(s))
            {
                f.Text("No pick is waiting. Each age deals you a hand; you take one card at the start of each round (" + t.DraftPicks + " picks an age).");
                NextHand(f, s);
                Cards(f, s);
                return;
            }
            f.Text("<b>Age " + StrategyApi.Roman(st.DraftAge) + " draft, pick " + st.DraftStep + " of " + t.DraftPicks + ".</b> Take one card into your hand; the rival takes one from its hand at the same time. Then the hands swap and the next pick comes at the start of the next round; the last card is discarded. The clock waits for you.");
            var hand = StrategyApi.DraftHand(s);
            foreach (var c in hand)
            {
                string afford = c.Affordable ? "<color=#8CE08C>You could build it now.</color>" : "<color=#FFB080>Short of " + c.MissingText + " to build it now.</color>";
                CardBox(f, c, afford);
                if (f.Button("Take " + c.Name)) StrategyUi.Pick(c.CardId);
                f.Space(4);
            }
            RivalPicks(f, s);
            f.Line(Dim);
            f.Head("Your hand");
            HandList(f, s);
        }

        /// <summary>The passed hand waiting for the next round's pick (DraftNextStep), shown read-only.</summary>
        private static void NextHand(Flow f, WorldState s)
        {
            if (s.Strategy.DraftNextStep <= 0) return;
            var hand = StrategyApi.DraftHand(s);
            if (hand.Count == 0) return;
            var names = new List<string>();
            foreach (var c in hand) names.Add("<color=" + c.Colour + ">" + c.Name + "</color>");
            f.Text("Next round's pick (" + s.Strategy.DraftNextStep + " of " + s.Strategy.Tuning.DraftPicks + ") will be from: " + string.Join(", ", names) + ".", true);
        }

        private static void RivalPicks(Flow f, WorldState s)
        {
            var taken = new List<string>();
            foreach (int id in s.Strategy.Sides[PlayerIds.Rival].Hand) taken.Add(StrategyApi.DescribeCard(s, id).Name);
            foreach (var b in s.Strategy.Sides[PlayerIds.Rival].Built) taken.Add(StrategyApi.DescribeCard(s, b.CardId).Name + " (built)");
            f.Text(taken.Count == 0 ? "The rival has taken no cards yet." : "The rival has taken: " + string.Join(", ", taken) + ".", true);
        }

        private static int _cityPick = -1;

        private static void HandList(Flow f, WorldState s)
        {
            var hand = StrategyApi.Hand(s);
            var room = StrategyApi.CitiesWithCardRoom(s);
            if (hand.Count == 0) { f.Text("Your hand is empty. Cards come from the draft at the start of each age.", true); return; }
            if (room.Count == 0) f.Text("<color=#FFB080>Every city holds " + s.Strategy.Tuning.MaxCardsPerCity + " cards: found a city to build more.</color>", true);
            else if (room.Count > 1)
            {
                if (!room.Contains(_cityPick)) _cityPick = room[0];
                var labels = new List<string>();
                foreach (int v in room) labels.Add(Short(StrategyUi.SiteName(s, v)) + " (" + StrategyApi.CardsAtCity(s, PlayerIds.Human, v).Count + "/" + s.Strategy.Tuning.MaxCardsPerCity + ")");
                f.Text("Build at:", true);
                int hit = f.Buttons(labels, null, null, room.IndexOf(_cityPick), 32f);
                if (hit >= 0) _cityPick = room[hit];
            }
            else _cityPick = room[0];
            foreach (var c in hand)
            {
                CardBox(f, c, c.Affordable ? null : "<color=#FFB080>Short of " + c.MissingText + ".</color>");
                bool can = c.Affordable && room.Count > 0;
                int hit = f.Buttons(new[] { "Build", "Discard (+" + s.Strategy.Tuning.DiscardCoin + " coin)" }, new[] { can, true },
                    new[] { can ? "Pay " + c.CostText + " from the storehouse and build it at " + StrategyUi.SiteName(s, _cityPick) + "." : (room.Count == 0 ? "No city has room." : "Short of " + c.MissingText + "."), "Gives up the card for coin." });
                if (hit == 0) StrategyUi.BuildCard(c.CardId, _cityPick);
                else if (hit == 1) StrategyUi.DiscardCard(c.CardId);
                f.Space(4);
            }
        }

        private static string Short(string site) => site.StartsWith("the ") ? char.ToUpper(site[4]) + site.Substring(5) : char.ToUpper(site[0]) + site.Substring(1);

        public static void Cards(Flow f, WorldState s)
        {
            if (StrategyApi.DraftPending(s))
            {
                f.Text("<color=#FFD070><b>A draft is waiting.</b></color> Pick a card first; End Round is refused until you do.");
                if (f.Button("Open the draft")) StrategyWindow.Open(Kind.Draft);
            }
            f.Head("Your hand");
            HandList(f, s);
            f.Line(Dim);
            f.Head("Built cards");
            var cities = StrategyApi.PlayerCities(s);
            bool any = false;
            foreach (int v in cities)
            {
                var built = StrategyApi.CardsAtCity(s, PlayerIds.Human, v);
                if (built.Count == 0) continue;
                any = true;
                f.Text("<b>" + Short(StrategyUi.SiteName(s, v)) + "</b> (" + built.Count + "/" + s.Strategy.Tuning.MaxCardsPerCity + ")", true);
                foreach (var c in built) f.Text("<color=" + c.Colour + ">■</color> <b>" + c.Name + "</b>: " + c.EffectText, true, UiStyle.Px(14));
            }
            if (!any) f.Text("None yet. Built cards stay for the whole game; each city holds " + s.Strategy.Tuning.MaxCardsPerCity + ".", true);
        }

        // ---------------- market ----------------

        private static Ware _give = Ware.Clay, _get = Ware.Planks;

        public static void Market(Flow f, WorldState s)
        {
            int rate = StrategyApi.TradeRate(s), per = StrategyApi.SellWaresPerCoin(s), price = StrategyApi.BuyPrice(s);
            int coin = s.Player(PlayerIds.Human).Coin;
            f.Text(StrategyApi.MarketText(s) + "  Coin: <b>" + coin + "</b>.");
            f.Head("Trade " + rate + " : 1");
            f.Text("Give:", true);
            WarePicker(f, s, ref _give, "give");
            f.Text("Get:", true);
            WarePicker(f, s, ref _get, "get");
            int have = EconomyApi.StorehouseCount(s, _give), haveGet = EconomyApi.StorehouseCount(s, _get);
            bool can = _give != _get && have >= rate;
            f.Text("You have " + have + " " + EconomyApi.WareName(_give) + " and " + haveGet + " " + EconomyApi.WareName(_get) + ". "
                + (can ? "After the trade: " + (have - rate) + " " + EconomyApi.WareName(_give) + ", " + (haveGet + 1) + " " + EconomyApi.WareName(_get) + "."
                       : _give == _get ? "Pick two different goods." : "<color=#FFB080>You need " + rate + " " + EconomyApi.WareName(_give) + " to trade.</color>"), true);
            if (f.Button("Trade " + rate + " " + EconomyApi.WareName(_give) + " for 1 " + EconomyApi.WareName(_get), can,
                    _give == _get ? "Pick two different goods." : have < rate ? "You need " + rate + " " + EconomyApi.WareName(_give) + "." : null))
                StrategyUi.Trade(_give, _get, 1);
            f.Line(Dim);
            f.Head("Sell and buy");
            foreach (var w in Wares.All)
            {
                int n = EconomyApi.StorehouseCount(s, w);
                f.Text(EconomyLook.WareTag(w) + "  <b>" + n + "</b> in the storehouse", true);
                int hit = f.Buttons(new[] { "Sell " + per + " (+1 coin)", "Buy 1 (" + price + " coin)" }, new[] { n >= per, coin >= price }, null, -1, 30f);
                if (hit == 0) StrategyUi.Sell(w, per);
                else if (hit == 1) StrategyUi.Buy(w, 1);
            }
        }

        private static void WarePicker(Flow f, WorldState s, ref Ware sel, string key)
        {
            const int perRow = 5;
            var all = Wares.All;
            for (int i = 0; i < all.Length; i += perRow)
            {
                var labels = new List<string>();
                int on = -1;
                for (int j = i; j < i + perRow && j < all.Length; j++)
                {
                    labels.Add(EconomyApi.WareTitle(all[j]) + " " + EconomyApi.StorehouseCount(s, all[j]));
                    if (all[j] == sel) on = j - i;
                }
                int hit = f.Buttons(labels, null, null, on, 30f);
                if (hit >= 0) sel = all[i + hit];
            }
        }

        // ---------------- wonder ----------------

        public static void Wonder(Flow f, WorldState s)
        {
            var w = StrategyApi.Wonder(s);
            f.Text("<b>" + w.Status + "</b>");
            for (int i = 0; i < w.StageCount; i++)
            {
                string cost = EconomyApi.CostText(s.Strategy.Tuning.WonderStages[i]);
                string state = i < w.StagesDone ? "<color=#8CE08C>done</color>" : w.InProgressStage == i + 1 ? "<color=#FFD070>being supplied</color>" : "not started";
                f.Text("Stage " + (i + 1) + ": " + cost + " = <b>" + w.StageVp[i] + " points</b>  (" + state + ")", true);
            }
            if (w.Lines != null && w.Lines.Length > 0 && w.InProgressStage > 0) f.Text("Delivered: " + string.Join(", ", w.Lines));
            if (w.BuildingId < 0 || EconomyApi.FindBuilding(s, BuildingType.WonderSite, true) < 0)
            {
                bool planned = EconomyApi.FindBuilding(s, BuildingType.WonderSite) >= 0;
                if (!planned && f.Button("Plan a wonder site (Build tool)")) { StrategyWindow.Close(); EconomyGodTools.OpenBuildPlans(BuildingType.WonderSite); }
                return;
            }
            var check = GodCommands.Check(new BuildWonderStage());
            if (f.Button("Build stage " + (w.StagesDone + 1), w.CanOrder && check.Ok, check.Ok ? "Carriers bring the goods; the stage completes on delivery." : check.Message))
                StrategyUi.OrderWonderStage();
        }

        // ---------------- army ----------------

        /// <summary>Squad awaiting a target click (Army tool in God View); -1 none.</summary>
        public static int ArmedSquad = -1;
        public static OrderKind ArmedOrder;

        public static void Army(Flow f, WorldState s)
        {
            var pw = MilitaryApi.Power(s);
            f.Head("Power");
            f.Text(pw.Sentence + (pw.FuelledPlants > 0 ? "" : " Power plants burn coal only while something needs power."), true);
            f.Head("Barracks");
            int bid = StrategyUi.FirstBarracks(s);
            if (bid < 0)
            {
                f.Text("No barracks yet. Build one (Build tool); soldiers then cost " + MilitaryApi.UnitCostText(s) + ".", true);
                if (f.Button("Plan a barracks (Build tool)")) { StrategyWindow.Close(); EconomyGodTools.OpenBuildPlans(BuildingType.Barracks); }
            }
            else
            {
                var info = MilitaryApi.DescribeBarracks(s, bid);
                bool powered = MilitaryApi.IsPowered(s, bid);
                f.Text("<b>" + info.Status + "</b>");
                f.Text("Queued " + info.Queued + "   " + (info.Queued > 0 ? (info.HeadSupplied ? "supplied, training " + info.ProgressPercent + "%" : "waiting for " + (info.WaitingFor.Length == 0 ? "-" : string.Join(" and ", info.WaitingFor))) : "")
                    + "   power: " + (powered ? "<color=#8CE08C>on</color>" : "<color=#FF8070>off</color>") + "\n" + info.CostText + "; training takes " + (info.TrainingTicks / WorldSim.TicksPerSecond) + " s.", true);
                if (info.Queued > 0)
                {
                    var bar = new Rect(f.X, f.Y, f.W, UiStyle.Px(10));
                    GUI.DrawTexture(bar, UiStyle.SolidTexture(new Color(0, 0, 0, 0.5f)));
                    GUI.DrawTexture(new Rect(bar.x, bar.y, bar.width * info.ProgressPercent / 100f, bar.height), UiStyle.SolidTexture(new Color(0.4f, 0.9f, 0.4f)));
                    f.Y += UiStyle.Px(16);
                }
                var check = GodCommands.Check(new QueueUnit { BarracksId = bid, Count = 1 });
                if (f.Button("Queue a soldier", check.Ok, check.Ok ? "The barracks asks for " + MilitaryApi.UnitCostText(s) + "." : check.Message)) StrategyUi.QueueUnit(bid, 1);
            }
            f.Head("Shields");
            var sh = MilitaryApi.ShieldSummary(s);
            f.Text(sh.Sentence, true);
            foreach (var c in StrategyApi.Comparisons(s))
                f.Text("Age " + StrategyApi.Roman(c.Age) + ": you " + c.PlayerShields + " vs rival " + c.RivalShields + ", " + c.Result.ToString().ToLowerInvariant()
                    + " (" + (c.PlayerVp >= 0 ? "+" : "") + c.PlayerVp + " for you, " + (c.RivalVp >= 0 ? "+" : "") + c.RivalVp + " for the rival)", true, UiStyle.Px(14));
            f.Head("Squads");
            var op = MilitaryApi.Outpost(s);
            f.Text(op.Status, true);
            var mine = MilitaryApi.Squads(s, PlayerIds.Human);
            if (mine.Count == 0) f.Text("No squads yet: trained soldiers form one squad per barracks.", true);
            foreach (var q in mine)
            {
                f.Text("<b>Squad " + q.Id + "</b>: " + q.Units + " soldiers, " + q.TotalHp + "/" + q.MaxHp + " health, at " + StrategyUi.SiteName(s, q.At) + ". " + q.OrderText, true);
                bool armed = ArmedSquad == q.Id;
                int hit = f.Buttons(new[] { "Attack outpost", "Defend home", armed ? "Click a flag..." : "Order to a flag", "Cancel" },
                    new[] { !op.Destroyed, true, true, q.Order != OrderKind.None },
                    new[] { "Walk to the rival outpost and fight its guards, then the outpost.", "Walk home and hold the village flag.", "Then click a flag in God View: Attack.", "Stop at the next flag." },
                    armed ? 2 : -1, 32f);
                if (hit == 0) StrategyUi.OrderSquad(q.Id, OrderKind.Attack, s.Board.RivalOutpostVertex);
                else if (hit == 1) StrategyUi.OrderSquad(q.Id, OrderKind.Defend, s.Board.HomeCityVertex);
                else if (hit == 2) { ArmedSquad = armed ? -1 : q.Id; ArmedOrder = OrderKind.Attack; StrategyGodTools.OpenArmyTargeting(); }
                else if (hit == 3) StrategyUi.CancelOrder(q.Id);
            }
            var raids = MilitaryApi.RaidedFlags(s);
            if (raids.Count > 0) f.Text("<color=#FF8070>Rival raiders hold " + StrategyUi.SiteList(s, raids) + ": send a squad there (Attack) to drive them off.</color>", true);
        }

        // ---------------- cities ----------------

        public static void Cities(Flow f, WorldState s)
        {
            f.Text("A city stands on a hex corner joined to your roads, at least two hex edges from any other city. Cost: <b>" + StrategyApi.CityCostText(s)
                + "</b>. Each city is worth " + s.Strategy.Tuning.VpPerCity + " points, sends batches from its hexes, holds " + s.Strategy.Tuning.MaxCardsPerCity
                + " cards and extends your land.", true);
            if (!s.Strategy.Started) { f.Text("<color=#FFB080>Cities open with Age I (after the commission).</color>"); }
            var tool = GodToolbar.Find(StrategyGodTools.CityId) as CityTool;
            if (tool != null && tool.HoverCorner >= 0)
            {
                var r = tool.HoverResult;
                f.Text((r.Ok ? "<color=#8CE08C>" : "<color=#FF8070>") + StrategyUi.SiteName(s, tool.HoverCorner) + ": " + (r.Ok ? "you can found a city here." : r.Message) + "</color>");
            }
            f.Head("Suggested sites");
            int shown = 0;
            foreach (int v in s.Board.SuggestedCitySites)
            {
                if (shown >= 8) break;
                var r = StrategyApi.CheckCity(s, v);
                if (r.Reason == StrategyRefusals.Occupied) continue;
                shown++;
                string verdict = r.Ok ? "<color=#8CE08C>ready</color>" : "<color=#FFB080>" + r.Message + "</color>";
                f.Text(Short(StrategyUi.SiteName(s, v)) + ": " + verdict, true);
                int hit = f.Buttons(new[] { "Show", "Found here" }, new[] { true, r.Ok }, null, -1, 30f);
                if (hit == 0 && WorldBootstrap.Instance.GodCam != null) WorldBootstrap.Instance.GodCam.SetFocus(BoardSpace.Vertex(v), 110f);
                else if (hit == 1) StrategyUi.FoundCity(v);
            }
        }

        // ---------------- hero ----------------

        public static void Hero(Flow f, WorldState s)
        {
            var st = StrategyApi.Status(s);
            f.Text("<b>The hero has " + st.HeroMovesLeft + " moves left this round</b> (refilled each round). Each move walks one hex edge to a neighbouring corner and clears every demon on the flags it passes and the corner it reaches.");
            f.Text("The hero is at " + StrategyUi.SiteName(s, st.HeroVertex) + ". Green corners are where it can go; click one.", true);
            var fc = StrategyApi.Forecast(s);
            f.Head("Infection");
            f.Text(fc.Sentence, true);
            if (fc.Discard != null && fc.Discard.Length > 0) f.Text("Discard pile (drawn already, safe until a reshuffle): " + StrategyUi.SiteList(s, fc.Discard, 6) + ".", true);
            f.Head("Demons");
            var demons = StrategyApi.Demons(s);
            if (demons.Count == 0) f.Text("No demons on the board.", true);
            int cap = StrategyApi.DemonCap(s);
            var topo = s.Board.Topology;
            demons.Sort((a, b) => b.Value != a.Value ? b.Value.CompareTo(a.Value) : a.Key.CompareTo(b.Key));
            int n = 0;
            foreach (var kv in demons)
            {
                if (n++ >= 10) break;
                string danger = kv.Value >= cap ? "  <color=#FF6050><b>at the cap: one more outbreaks</b></color>" : "";
                f.Text(kv.Value + " on " + StrategyUi.SiteName(s, kv.Key) + danger, true);
                if (f.Button("Show", true, null, 28f) && WorldBootstrap.Instance.GodCam != null) WorldBootstrap.Instance.GodCam.SetFocus(BoardSpace.Vertex(kv.Key), 80f);
            }
        }
    }
}
