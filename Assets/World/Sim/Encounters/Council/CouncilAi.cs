using System;
using System.Collections.Generic;

namespace World.Sim.Encounters.Council
{
    /// <summary>
    /// AI candidates. They only produce <see cref="ElectionAction"/>s, which go through the same
    /// <see cref="ElectionEngine.Validate"/> as the player's. Random choices use the election's AiRng, never its Rolls.
    /// </summary>
    public static class CouncilAi
    {
        /// <summary>The next action for the active seat under a personality. Never draws from the roll stream.</summary>
        public static ElectionAction Decide(ElectionState e, CouncilPersonality p)
        {
            int seat = e.Active;
            switch (e.Phase)
            {
                case ElectionPhase.Ready:
                    if (!e.HasAdvanced) return ElectionAction.Push(seat);
                    return ShouldBank(e, p) ? ElectionAction.Bank(seat) : ElectionAction.Push(seat);
                case ElectionPhase.Choosing:
                    return ChooseTray(e, p);
                case ElectionPhase.BustPending:
                    // A scripted player always spends a spare petition; an AI candidate never holds one.
                    return e.PetitionAvailable && seat == e.PlayerSeat ? ElectionAction.SpendPetition(seat) : ElectionAction.AcceptBust(seat);
            }
            return ElectionAction.Push(seat);
        }

        static bool ShouldBank(ElectionState e, CouncilPersonality p)
        {
            bool atTop = false;
            for (int i = 0; i < CouncilRules.TrackCount; i++) if (e.Temp[i] == CouncilRules.TrackLengths[i]) atTop = true;
            if (atTop && e.AiRng.NextInt(100) < p.BankAtTopPercent) return true;
            if (e.SittingAdvances < p.MinAdvances) return false;
            if (e.SittingAdvances >= p.MaxAdvances) return true;
            int mine = e.ClaimsOf(e.Active), best = 0;
            for (int s = 0; s < CouncilRules.Seats; s++) if (s != e.Active) best = Math.Max(best, e.ClaimsOf(s));
            double tol = p.BustTolerance;
            if (best > mine) tol += p.TrailingBoost;
            else if (mine > best) tol -= p.LeadingCaution;
            if (e.PetitionAvailable && e.Active == e.PlayerSeat) tol += p.PetitionBoost;
            bool bank = ElectionEngine.BustPercent(e) > tol;
            if (p.Impulse > 0 && e.AiRng.NextInt(100) < p.Impulse) bank = !bank;
            return bank;
        }

        static ElectionAction ChooseTray(ElectionState e, CouncilPersonality p)
        {
            int bestTray = 0, bestSocket = -1;
            double best = double.NegativeInfinity;
            for (int i = 0; i < e.Pending.Count; i++)
            {
                var tray = e.Pending[i];
                if (tray.Mode == TrayMode.Both)
                {
                    double sc = Score(e, p, ElectionEngine.MovesOf(tray, -1));
                    if (p.Noise > 0) sc += p.Noise * e.AiRng.NextInt(1000) / 1000.0;
                    if (sc > best) { best = sc; bestTray = i; bestSocket = -1; }
                }
                else
                {
                    for (int s = 0; s < 2; s++)
                    {
                        if (tray.Sockets[s].IsEmpty) continue;
                        double sc = Score(e, p, ElectionEngine.MovesOf(tray, s));
                        if (p.Noise > 0) sc += p.Noise * e.AiRng.NextInt(1000) / 1000.0;
                        if (sc > best) { best = sc; bestTray = i; bestSocket = s; }
                    }
                }
            }
            return ElectionAction.Choose(e.Active, e.PushId, bestTray, bestSocket);
        }

        static double Score(ElectionState e, CouncilPersonality p, List<KeyValuePair<int, int>> moves)
        {
            double sc = 0;
            foreach (var kv in moves)
            {
                int track = kv.Key, steps = kv.Value, ti = track - 2, len = CouncilRules.TrackLengths[ti];
                bool isNew = e.Temp[ti] == 0;
                int from = isNew ? e.BankedAt(e.Active, track) : e.Temp[ti];
                int to = from + steps;
                double central = 1.0 - Math.Abs(track - 7) / 5.0;
                sc += p.WeightStep * steps
                    + p.WeightProgress * (steps * 10.0 / len)
                    + (to == len ? p.WeightTop : 0)
                    + (isNew ? p.WeightNewCatspaw : 0)
                    + p.WeightCentral * central
                    + p.WeightAdvanced * (to / (double)len);
            }
            return sc;
        }
    }
}
