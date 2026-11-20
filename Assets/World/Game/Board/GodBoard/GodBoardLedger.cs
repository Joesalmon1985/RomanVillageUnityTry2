using System;
using System.Collections.Generic;
using System.Text;
using World.Sim;
using World.Sim.Table;

namespace World.Game
{
    /// <summary>
    /// The texts of the altar ledge's tablets (WU-2): what each object says when it is tapped. Plain functions of a
    /// <see cref="GodBoardViewModel"/>; words and exact counts only (WORLD_UI_PLAN section 3), never hex numbers, dice, number
    /// tokens, roll totals or odds (AB-1). The Clear view HUD reads the same strings through <see cref="LedgerCard.Content"/>.
    /// </summary>
    public static class GodBoardLedger
    {
        private static readonly string[] Singular = { "log", "clay brick", "sheaf", "fleece", "stone" };
        private static readonly string[] Plural = { "logs", "clay bricks", "sheaves", "fleece", "stones" };

        /// <summary>"2 logs, 1 clay brick", or "nothing".</summary>
        public static string Goods(int[] counts)
        {
            if (counts == null) return "nothing";
            var sb = new StringBuilder();
            for (int g = 0; g < Wares.RawCount && g < counts.Length; g++)
            {
                if (counts[g] <= 0) continue;
                if (sb.Length > 0) sb.Append(", ");
                sb.Append(counts[g]).Append(' ').Append(counts[g] == 1 ? Singular[g] : Plural[g]);
            }
            return sb.Length == 0 ? "nothing" : sb.ToString();
        }

        private static LedgerContent C(string title) => new LedgerContent { Title = title };

        public static string Plural1(int n, string one, string many) => n == 1 ? one : many;

        /// <summary>The five offering baskets: your hand with exact counts, the costs and which you can afford (HEX-STORE).</summary>
        public static LedgerContent Baskets(GodBoardViewModel vm)
        {
            var c = C("Offering baskets");
            for (int g = 0; g < Wares.RawCount; g++) c.Lines.Add(char.ToUpperInvariant(Plural[g][0]) + Plural[g].Substring(1) + ": " + vm.Hand[g]);
            c.Lines.Add("In all " + vm.HandSize + " " + Plural1(vm.HandSize, "good", "goods") + " in your hand. These baskets are the hand, not the goods stored in the camps.");
            for (int k = 0; k < 3; k++)
            {
                var kv = vm.Kind[k];
                c.Lines.Add(kv.Name + " costs " + Goods(kv.Cost) + ": " + (kv.CanPay ? "you can pay." : "you lack " + Goods(kv.Missing) + "."));
            }
            return c;
        }

        /// <summary>The builder's bench: the three pieces, cost, pieces left, and what stops you (HEX-BUILD).</summary>
        public static LedgerContent Bench(GodBoardViewModel vm)
        {
            var c = C("The builder's bench");
            for (int k = 0; k < 3; k++)
            {
                var kv = vm.Kind[k];
                c.Lines.Add(kv.Name + ": " + Goods(kv.Cost) + ". " + kv.Left + " of " + kv.Max + " pieces left. " + (kv.Affordable ? "Ready." : BuildRefusal(vm, kv.Kind)));
            }
            return c;
        }

        /// <summary>Why a kind cannot be built now, as one sentence (the builder shakes his head).</summary>
        public static string BuildRefusal(GodBoardViewModel vm, BuildKind kind)
        {
            var kv = vm.KindOf(kind);
            if (vm.Phase != TablePhase.Main) return vm.Phase == TablePhase.Setup ? "Place your first camps first." : "Not yet.";
            if (vm.Paused) return "The board is waiting.";
            if (vm.Current != vm.Human) return "Wait for your turn.";
            if (kv.Left <= 0) return "No " + kv.Name.ToLowerInvariant() + " pieces left.";
            if (!kv.CanPay) return "You lack " + Goods(kv.Missing) + ".";
            return "";
        }

        /// <summary>The banner stakes: points, cards, longest road, soldiers, war, the goal (HEX-AWARDS).</summary>
        public static LedgerContent Standings(GodBoardViewModel vm)
        {
            var c = C("The banner stakes");
            var order = new List<int>();
            if (vm.TurnOrder.Length > 0 && (vm.Phase == TablePhase.Main || vm.Phase == TablePhase.Discard)) order.AddRange(vm.TurnOrder);
            else for (int p = 0; p < vm.PeopleCount; p++) order.Add(p);
            foreach (int p in order)
            {
                string cards = p == vm.Human ? vm.Cards[p] + " goods in hand" : vm.Cards[p] + " goods held";
                c.Lines.Add(vm.NameOf(p) + ": " + vm.Points[p] + " " + Plural1(vm.Points[p], "point", "points") + ", " + cards + ", road " + vm.RoadLength[p] + ", " + vm.Soldiers[p] + " " + Plural1(vm.Soldiers[p], "soldier", "soldiers") + ".");
            }
            c.Lines.Add(vm.LongestHolder >= 0
                ? "Longest road: " + vm.NameOf(vm.LongestHolder) + ", " + vm.RoadLength[vm.LongestHolder] + " roads. It needs " + vm.LongestRoadMin + " at least."
                : "Longest road: nobody yet. It needs " + vm.LongestRoadMin + " roads at least.");
            if (vm.WarPairs.Count == 0) c.Lines.Add("War: none.");
            else
            {
                var sb = new StringBuilder("War: ");
                for (int i = 0; i < vm.WarPairs.Count; i++) { if (i > 0) sb.Append("; "); sb.Append(vm.NameOf(vm.WarPairs[i][0])).Append(" and ").Append(vm.NameOf(vm.WarPairs[i][1])); }
                c.Lines.Add(sb.Append('.').ToString());
            }
            c.Lines.Add("The first to " + vm.WinPoints + " points turns the age.");
            return c;
        }

        /// <summary>The trader: the bank rate and your jetties (HEX-TRADE).</summary>
        public static LedgerContent Trader(GodBoardViewModel vm)
        {
            var c = C("The trader");
            c.Lines.Add("The bank takes " + vm.BankRateBase + " of one good for 1 of another.");
            c.Lines.Add("You hold " + vm.OwnedJetties + " " + Plural1(vm.OwnedJetties, "jetty", "jetties") + "; they are your ports.");
            var sb = new StringBuilder("Your rates: ");
            for (int g = 0; g < Wares.RawCount; g++) { if (g > 0) sb.Append(", "); sb.Append(Plural[g]).Append(' ').Append(vm.BankRate[g]).Append(" for 1"); }
            c.Lines.Add(sb.ToString() + ".");
            return c;
        }

        /// <summary>An envoy's bundle: what it gives, what it asks, its patience (HEX-TRADE).</summary>
        public static LedgerContent Envoy(GodBoardViewModel vm, TradeOffer o)
        {
            var c = C("Envoy of " + vm.NameOf(o.From));
            c.Lines.Add("Offers you " + Goods(o.Give) + ".");
            c.Lines.Add("Asks " + Goods(o.Get) + (TableRules.CanPay(vm.Hand, o.Get) ? "." : ", which you cannot pay."));
            int s = (int)Math.Ceiling(o.TicksLeft / (double)WorldSim.TicksPerSecond);
            c.Lines.Add("Patience: " + s + " " + Plural1(s, "second", "seconds") + ".");
            return c;
        }

        /// <summary>The discard sack: the exact number, the chosen count and the timer (HEX-RAIDER).</summary>
        public static LedgerContent Sack(GodBoardViewModel vm, int chosen)
        {
            var c = C("The sack");
            if (!vm.MustDiscard) { c.Lines.Add("Nothing is owed. The storekeeper's sack is empty."); return c; }
            c.Lines.Add("Sickness: give up " + vm.DiscardOwed + " " + Plural1(vm.DiscardOwed, "good", "goods") + ".");
            c.Lines.Add("In the sack: " + chosen + " of " + vm.DiscardOwed + ".");
            int s = (int)Math.Ceiling(vm.DiscardTicksLeft / (double)WorldSim.TicksPerSecond);
            c.Lines.Add(s + " " + Plural1(s, "second", "seconds") + " left, then the adviser chooses.");
            return c;
        }

        /// <summary>The turn hourglass: the sand in seconds (HEX-END).</summary>
        public static LedgerContent Hourglass(GodBoardViewModel vm)
        {
            var c = C("The hourglass");
            if (vm.Phase == TablePhase.Setup) c.Lines.Add(vm.Current == vm.Human ? "Place your camp and road. The sand waits for you." : "The peoples are placing their camps.");
            else if (vm.CanAct) c.Lines.Add("Sand left: " + vm.SandSeconds + " " + Plural1(vm.SandSeconds, "second", "seconds") + ".");
            else if (vm.Paused) c.Lines.Add("The sand is frozen while you are away.");
            else c.Lines.Add(vm.Current >= 0 ? vm.NameOf(vm.Current) + " have the hourglass." : "The sand rests.");
            return c;
        }

        public static LedgerContent Chronicler(IReadOnlyList<string> lines)
        {
            var c = C("The chronicler");
            int n = Math.Min(7, lines.Count);
            for (int i = 0; i < n; i++) c.Lines.Add(lines[lines.Count - n + i]);
            if (n == 0) c.Lines.Add("Nothing has happened yet.");
            return c;
        }

        public static LedgerContent Prince(bool hintsOn, string line)
        {
            var c = C("The prince");
            c.Lines.Add(!hintsOn ? "He keeps his counsel. Hints are off." : string.IsNullOrEmpty(line) ? "He has no counsel for this moment." : line);
            return c;
        }

        public static LedgerContent Totem(bool on)
        {
            var c = C("The demon-ward totem");
            c.Lines.Add(on ? "The plague overlay is showing on the island." : "The plague overlay is hidden.");
            return c;
        }

        /// <summary>The words that must never appear on the ledge (AB-1): returns the first violation in any tablet or null.</summary>
        public static string FirstViolation(GodBoardViewModel vm, IReadOnlyList<string> feed)
        {
            var all = new List<LedgerContent> { Baskets(vm), Bench(vm), Standings(vm), Trader(vm), Sack(vm, 0), Hourglass(vm), Chronicler(feed), Prince(true, null), Totem(true) };
            foreach (var o in vm.Incoming) all.Add(Envoy(vm, o));
            foreach (var c in all) { var v = LedgerText.Violation(c); if (v != null) return v; }
            return null;
        }
    }
}
