using System.Collections.Generic;

namespace World.Sim.Encounters.Council
{
    /// <summary>Read helpers for the game layer. Nothing here mutates state.</summary>
    public static class CouncilApi
    {
        public static SettlementCouncil Get(WorldState s, int settlementId) => s == null ? null : CouncilOps.Find(s, settlementId);

        public static CouncilStatus StatusOf(WorldState s, int settlementId) => Get(s, settlementId)?.Status ?? CouncilStatus.None;

        /// <summary>True when an election could be offered for the settlement right now (and none exists yet).</summary>
        public static bool CanOffer(WorldState s, int settlementId, CouncilContent content = null)
        {
            if (Get(s, settlementId) != null) return false;
            return CouncilRoster.IsEligible(s, settlementId, content ?? CouncilContent.BuiltIn, out _);
        }

        /// <summary>The recorded office holder of a settlement, or null.</summary>
        public static OfficeHolder OfficeHolderOf(WorldState s, int settlementId)
        {
            if (s?.Council == null) return null;
            foreach (var o in s.Council.Offices) if (o.Settlement == settlementId) return o;
            return null;
        }

        /// <summary>A dispute line for the council scene: kind is CouncilContent.OpeningLine etc.; candidate is the seat speaking for; winner is the seat that secured it (or -1).</summary>
        public static string DisputeLine(SettlementCouncil sc, int track, string kind, int variant, int candidateSeat, int winnerSeat, CouncilContent content = null)
        {
            content = content ?? CouncilContent.BuiltIn;
            var d = content.Dispute(track);
            var b = sc.Disputes[track - 2];
            string cand = candidateSeat >= 0 ? sc.Candidates[candidateSeat].Name : "";
            string win = winnerSeat >= 0 ? sc.Candidates[winnerSeat].Name : "";
            return CouncilContent.Fill(CouncilContent.Line(d, kind, variant), cand, b.LeadName, b.Prop, sc.Office, win);
        }

        /// <summary>The text of a listening line id.</summary>
        public static ListeningLine ListeningLineOf(string id, CouncilContent content = null) => (content ?? CouncilContent.BuiltIn).ListeningLine(id);

        /// <summary>Banked place of a seat on a track (0 = none).</summary>
        public static int Banked(SettlementCouncil sc, int seat, int track) => sc.Election.BankedAt(seat, track);
    }
}
