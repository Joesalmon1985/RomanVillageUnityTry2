using System.Collections.Generic;
using World.Sim.War;

namespace World.Sim.Table
{
    /// <summary>
    /// Developer set-up for the packaged AutoPlay smoke scripts (PRD_GODBOARD 12.3), kept in the sim assembly so the Game
    /// layer never writes state. Each call only prepares a situation (cards in hand, an AI offer, a points lead, an
    /// ungarrisoned camp); the smoke scripts then play it out through the real commands and rules. Never called by the game.
    /// </summary>
    public static class SmokeSetup
    {
        /// <summary>Tops the people's hand up to at least <paramref name="total"/> cards (adds grain).</summary>
        public static void TopUpHand(WorldState s, int people, int total)
        {
            var h = s.Table.Peoples[people].Hand;
            int n = TableRules.HandSize(h);
            if (n < total) h[2] += total - n;
        }

        /// <summary>Sets one pile of the people's hand.</summary>
        public static void SetCards(WorldState s, int people, int good, int count) { s.Table.Peoples[people].Hand[good] = count; }

        /// <summary>An open offer from an AI seat to the human: one of <paramref name="give"/> for one of <paramref name="get"/>; the AI is given the card it offers.</summary>
        public static int AddOffer(WorldState s, int from, int to, int give, int get)
        {
            var t = s.Table;
            if (t.Peoples[from].Hand[give] < 1) t.Peoples[from].Hand[give] = 1;
            var o = new TradeOffer { Id = t.NextOfferId++, From = from, To = to, TicksLeft = 150, Status = OfferStatus.Open };
            o.Give[give] = 1; o.Get[get] = 1;
            t.Offers.Add(o);
            return o.Id;
        }

        /// <summary>Free settlements for a people (the crystal grant path) so its points lead the table. Returns the corners placed.</summary>
        public static List<int> GrantSettlements(WorldState s, int people, int count)
        {
            var list = new List<int>();
            for (int i = 0; i < count; i++)
            {
                int c = TableGrants.BestSettlementCorner(s, people);
                if (c < 0 || !TableGrants.GrantSettlement(s, people, c, null)) break;
                list.Add(c);
            }
            return list;
        }

        /// <summary>
        /// A trained soldier standing at the people's home camp (what its lodge turns out in time). Home garrisons of one never
        /// leave, so a raid needs one more. Returns the soldier, or null when the people has no home camp.
        /// </summary>
        public static Soldier AddSoldier(WorldState s, int people)
        {
            var w = s.War;
            if (w == null) return null;
            int corner = -1;
            foreach (var st in w.Settlements) if (st.People == people && st.IsHome) { corner = st.Corner; break; }
            if (corner < 0) return null;
            var x = new Soldier { Id = w.NextSoldierId++, People = people, BaseAttack = 14, Corner = corner, Node = 0 };
            x.Hp = WarApi.MaxHealth(x);
            w.Soldiers.Add(x);
            return x;
        }

        /// <summary>Sends the people's soldiers standing at the corner away (the camp is left unguarded). Returns how many.</summary>
        public static int RemoveGarrison(WorldState s, int people, int corner)
        {
            return s.War == null ? 0 : s.War.Soldiers.RemoveAll(x => x.People == people && x.Corner == corner);
        }
    }
}
