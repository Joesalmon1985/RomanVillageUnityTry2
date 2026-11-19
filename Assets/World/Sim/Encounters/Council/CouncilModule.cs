using System;
using System.Collections.Generic;
using World.Sim.Eras;

namespace World.Sim.Encounters.Council
{
    /// <summary>
    /// Registers the council commands on a sim (no per-tick system: an election advances only by commands).
    /// Idempotent. A loaded save needs the same line in SaveCodec.CreateSim.
    /// </summary>
    public static class CouncilModule
    {
        public static void Register(WorldSim sim, CouncilContent content = null)
        {
            if (sim.HasHandler(typeof(CouncilOffer))) return;
            var ctx = new CouncilContext { Content = content ?? CouncilContent.BuiltIn };
            sim.RegisterHandler(new OfferHandler(ctx));
            sim.RegisterHandler(new RespondHandler(ctx));
            sim.RegisterHandler(new ActionHandler<CouncilPush>());
            sim.RegisterHandler(new ActionHandler<CouncilChoose>());
            sim.RegisterHandler(new ActionHandler<CouncilBank>());
            sim.RegisterHandler(new ActionHandler<CouncilAcceptBust>());
            sim.RegisterHandler(new ActionHandler<CouncilSpendPetition>());
            sim.RegisterHandler(new AiStepHandler(ctx));
            sim.RegisterHandler(new StartListeningHandler(ctx));
            sim.RegisterHandler(new AnswerListeningHandler());
            sim.RegisterHandler(new AbandonListeningHandler());
            sim.RegisterHandler(new SkipListeningHandler());
            sim.RegisterHandler(new LeaveHandler());
            sim.RegisterHandler(new ResumeHandler());
        }
    }

    /// <summary>Builds the offer (candidates, disputes bound to residents) and starts the election. Deterministic.</summary>
    public static class CouncilSetup
    {
        public static SettlementCouncil Offer(WorldState s, int settlementId, CouncilContent content)
        {
            int no = ++s.Council.ElectionsOffered;
            var era = CouncilRoster.EraOf(s);
            var residents = CouncilRoster.Build(s, settlementId, content);
            var standing = CouncilRoster.WouldStand(residents);
            standing.Sort((a, b) =>
            {
                int c = CouncilRoster.Hash(s, settlementId, a.Key, "cand" + no).CompareTo(CouncilRoster.Hash(s, settlementId, b.Key, "cand" + no));
                return c != 0 ? c : string.CompareOrdinal(a.Key, b.Key);
            });
            var sc = new SettlementCouncil
            {
                SettlementId = settlementId,
                Status = CouncilStatus.Offered,
                ElectionNo = no,
                Seed = CouncilRoster.Hash(s, settlementId, "election", "seed" + no),
                Era = era,
                Office = content.Office(era),
            };
            var taken = new HashSet<string>();
            for (int seat = 0; seat < CouncilRules.Seats; seat++)
            {
                var r = standing[seat];
                taken.Add(r.Key);
                sc.Candidates.Add(new CandidateInfo
                {
                    Seat = seat, Name = r.Name, Occupation = r.Occupation, OccupationId = r.OccupationId,
                    ResidentKey = r.Key, Emblem = content.Emblems[seat].Id,
                });
            }
            sc.AskerSeat = new Rng(sc.Seed).Fork("asker").NextInt(CouncilRules.Seats);
            BindDisputes(s, sc, residents, taken, content);
            return sc;
        }

        static void BindDisputes(WorldState s, SettlementCouncil sc, List<Resident> residents, HashSet<string> taken, CouncilContent content)
        {
            var bindings = new DisputeBinding[CouncilRules.TrackCount];
            var usedNames = new HashSet<string>();
            foreach (var cand in sc.Candidates) usedNames.Add(cand.Name);
            foreach (var r in residents) if (taken.Contains(r.Key)) usedNames.Add(r.Name);
            // Two passes: occupation-specific leads first, so a wildcard role never steals the matching resident.
            for (int pass = 0; pass < 2; pass++)
            {
                for (int t = CouncilRules.FirstTrack; t <= CouncilRules.LastTrack; t++)
                {
                    var d = content.Dispute(t);
                    bool wildcard = Array.IndexOf(d.Occupations, "*") >= 0;
                    if (wildcard != (pass == 1)) continue;
                    Resident pick = null;
                    ulong bestHash = 0;
                    foreach (var r in residents)
                    {
                        if (!r.Living || !r.Eligible || taken.Contains(r.Key)) continue;
                        if (!wildcard && Array.IndexOf(d.Occupations, r.OccupationId) < 0) continue;
                        ulong h = CouncilRoster.Hash(s, sc.SettlementId, r.Key, "lead" + t + "#" + sc.ElectionNo);
                        if (pick == null || h < bestHash) { pick = r; bestHash = h; }
                    }
                    var b = new DisputeBinding { Track = t, DisputeId = d.Id, Prop = content.Prop(d, sc.Era) };
                    if (pick != null)
                    {
                        taken.Add(pick.Key);
                        b.LeadName = pick.Name; b.LeadResidentKey = pick.Key; b.LeadTitle = pick.Occupation; b.IsDelegate = false;
                    }
                    else
                    {
                        var names = content.Fallbacks.DelegateNames;
                        int idx = (int)(CouncilRoster.Hash(s, sc.SettlementId, "delegate" + t, "dname" + sc.ElectionNo) % (ulong)names.Count);
                        int guard = 0;
                        while (usedNames.Contains(names[idx]) && guard++ < names.Count) idx = (idx + 1) % names.Count;
                        b.LeadName = names[idx]; b.LeadResidentKey = null; b.LeadTitle = d.FallbackTitle ?? content.Fallbacks.DelegateTitle; b.IsDelegate = true;
                        usedNames.Add(b.LeadName);
                    }
                    bindings[t - 2] = b;
                }
            }
            sc.Disputes.AddRange(bindings);
        }

        /// <summary>Turns an offered council into a running election. Personalities are dealt to seats from the election's seed.</summary>
        public static void Start(SettlementCouncil sc, bool help, CouncilContent content)
        {
            var ids = new List<string>();
            for (int i = 0; i < CouncilRules.Seats; i++) ids.Add(content.Personalities[i].Id);
            new Rng(sc.Seed).Fork("personalities").Shuffle(ids);
            sc.Election = ElectionEngine.Create(sc.Seed, help ? sc.AskerSeat : -1, ids.ToArray(), content.Listening);
            sc.PlayerHelps = help;
            sc.Status = CouncilStatus.InProgress;
            sc.Present = true;
        }
    }
}
