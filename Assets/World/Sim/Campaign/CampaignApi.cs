using System;
using System.Collections.Generic;

namespace World.Sim.Campaign
{
    /// <summary>
    /// Read helpers of the campaign (A.5). Static, null-safe, never mutate: with no campaign every helper returns false,
    /// null, an empty list or "" as its doc says. Views are fresh snapshots. The game reads these and sends commands.
    /// </summary>
    public static class CampaignApi
    {
        private static CampaignState Run(WorldState s) => s?.Campaign?.Definition != null ? s.Campaign : null;

        /// <summary>A campaign exists and is Running (not won, not lost).</summary>
        public static bool IsRunning(WorldState s) => Run(s)?.Phase == CampaignPhase.Running;

        /// <summary>The truth about a crystal (for views of sites, altars, towers). Null with no campaign.</summary>
        public static CrystalView Crystal(WorldState s, Element e)
        {
            var c = Run(s);
            var st = c?.Crystal(e);
            if (st == null) return null;
            var d = c.Definition.Crystal(e);
            return new CrystalView
            {
                Element = e, Id = d?.Id ?? "", Name = d?.Name ?? "", Custody = st.Custody, At = st.At, SinceTick = st.SinceTick,
                Tower = d?.Tower ?? "",
            };
        }

        /// <summary>What the party thinks of a crystal, with Stale and the "where to look" sentence. Null with no campaign.</summary>
        public static BeliefView Belief(WorldState s, Element e)
        {
            var c = Run(s);
            var b = c?.BeliefOf(e);
            if (b == null) return null;
            bool stale = s.Tick - b.AsOfTick > c.Definition.Tuning.StaleTicks;
            return new BeliefView
            {
                Element = e, Custody = b.Custody, At = b.At, AsOfTick = b.AsOfTick, Source = b.Source, Stale = stale,
                WhereToLook = stale ? WhereToLook(c, e, b) : "",
            };
        }

        // 5.4: the last known place, then the scout if she lives, then the wizard.
        private static string WhereToLook(CampaignState c, Element e, Belief b)
        {
            var d = c.Definition;
            string name = d.Crystal(e)?.Name ?? e.ToString();
            string place = CampaignTokens.PlaceName(d, b.At);
            string text = place.Length > 0 ? "The last word put " + name + " at " + place + "." : "Nobody has word of " + name + ".";
            var scout = ScoutCompanion(c);
            if (scout != null) return text + " " + scout.Name + " could go and look.";
            var wizard = d.Role("wizard");
            return wizard != null && !string.IsNullOrEmpty(wizard.Name) ? text + " " + wizard.Name + " may know more." : text;
        }

        /// <summary>The living companion with the scout service, null when none.</summary>
        internal static CompanionDef ScoutCompanion(CampaignState c)
        {
            if (c?.Definition == null) return null;
            foreach (var k in c.Definition.Companions)
                if (k.Service == CompanionServices.Scout && (c.Companion(k.Id)?.Alive ?? false)) return k;
            return null;
        }

        /// <summary>Mira's lead queue in rank order (at most QueueLength). Empty with no campaign.</summary>
        public static IReadOnlyList<LeadView> Leads(WorldState s)
        {
            var c = Run(s);
            var list = new List<LeadView>();
            if (c?.Leads == null) return list;
            foreach (var l in c.Leads)
            {
                var d = c.Definition.Lead(l.DefId);
                list.Add(new LeadView
                {
                    Id = l.Id, DefId = l.DefId, Title = d?.Title ?? "", Reason = d != null ? LineOf(c, d.ReasonLine) ?? "" : "",
                    Score = l.Score, Rank = l.Rank, Crystal = d?.Crystal ?? "", Goal = d?.Goal?.At ?? default,
                    Action = d?.Goal?.Action ?? QuestAction.Arrive,
                });
            }
            list.Sort((a, b) => a.Rank != b.Rank ? a.Rank.CompareTo(b.Rank) : a.Id.CompareTo(b.Id));
            return list;
        }

        /// <summary>The best lead, null when the queue is empty.</summary>
        public static LeadView TopLead(WorldState s)
        {
            var l = Leads(s);
            return l.Count > 0 ? l[0] : null;
        }

        /// <summary>Mode, place, lead, choice, encounter. Null with no campaign.</summary>
        public static PartyView Party(WorldState s)
        {
            var p = Run(s)?.Party;
            if (p == null) return null;
            return new PartyView
            {
                Mode = p.Mode, At = p.At, PlayerPresent = p.PlayerPresent, LeadId = p.LeadId, ChoiceId = p.ChoiceId ?? "",
                EncounterId = p.EncounterId ?? "", ModeSinceTick = p.ModeSinceTick,
                To = p.Mode == PartyMode.Travelling && p.Leg != null ? p.Leg.To : default,
            };
        }

        /// <summary>
        /// Where the party is between ticks: true while travelling, with t in [0, 1] along the leg (tickFraction in [0, 1)
        /// is the time since the last tick). False otherwise, with from = to = the party's place and t = 0.
        /// </summary>
        public static bool PartyPosition(WorldState s, double tickFraction, out MarkerRef from, out MarkerRef to, out double t)
        {
            var p = Run(s)?.Party;
            from = to = p?.At ?? default;
            t = 0;
            if (p == null || p.Mode != PartyMode.Travelling || p.Leg == null) return false;
            from = p.Leg.From;
            to = p.Leg.To;
            double frac = double.IsNaN(tickFraction) ? 0 : Math.Max(0, Math.Min(1, tickFraction));
            t = p.Leg.Ticks <= 0 ? 1 : Math.Max(0, Math.Min(1, (p.Leg.Progress + frac) / p.Leg.Ticks));
            return true;
        }

        /// <summary>The interior path of the current leg as i,j pairs; empty on the surface or when not travelling.</summary>
        public static int[] LegCells(WorldState s)
        {
            var p = Run(s)?.Party;
            if (p == null || p.Mode != PartyMode.Travelling || p.Leg?.Cells == null) return new int[0];
            return (int[])p.Leg.Cells.Clone();
        }

        /// <summary>The six companions in volunteer order, alive or not. Empty with no campaign.</summary>
        public static IReadOnlyList<CompanionView> Companions(WorldState s)
        {
            var c = Run(s);
            var list = new List<CompanionView>();
            if (c == null) return list;
            foreach (var d in c.Definition.Companions)
            {
                var k = c.Companion(d.Id);
                list.Add(new CompanionView
                {
                    Id = d.Id, Name = d.Name, Role = d.Role, Line = d.Line, Service = d.Service, ActorId = d.ActorId,
                    VolunteerOrder = d.VolunteerOrder, Alive = k?.Alive ?? false, FellAt = k?.FellAt ?? "", FellTick = k?.FellTick ?? -1,
                });
            }
            list.Sort((a, b) => a.VolunteerOrder != b.VolunteerOrder ? a.VolunteerOrder.CompareTo(b.VolunteerOrder) : string.CompareOrdinal(a.Id, b.Id));
            return list;
        }

        /// <summary>The first living companion in volunteer order (who covers the next retreat); null when none.</summary>
        public static CompanionView NextVolunteer(WorldState s) => NextVolunteerOf(Run(s), s);

        internal static CompanionView NextVolunteerOf(CampaignState c, WorldState s = null)
        {
            if (c?.Definition == null) return null;
            CompanionDef best = null;
            foreach (var d in c.Definition.Companions)
            {
                if (!(c.Companion(d.Id)?.Alive ?? false)) continue;
                if (best == null || d.VolunteerOrder < best.VolunteerOrder
                    || d.VolunteerOrder == best.VolunteerOrder && string.CompareOrdinal(d.Id, best.Id) < 0) best = d;
            }
            if (best == null) return null;
            return new CompanionView
            {
                Id = best.Id, Name = best.Name, Role = best.Role, Line = best.Line, Service = best.Service, ActorId = best.ActorId,
                VolunteerOrder = best.VolunteerOrder, Alive = true, FellAt = "", FellTick = -1,
            };
        }

        /// <summary>The pending attempt, null when none.</summary>
        public static AttemptView Attempt(WorldState s)
        {
            var c = Run(s);
            var a = c?.Attempt;
            if (a == null) return null;
            var e = c.Definition.Encounter(a.EncounterId);
            return new AttemptView
            {
                Id = a.Id, EncounterId = a.EncounterId, Volunteer = a.Volunteer ?? "", Seed = a.Seed, Restarts = a.Restarts,
                CommittedTick = a.CommittedTick, Profile = e?.Profile ?? "", Kind = e?.Kind ?? EncounterKind.Lethal,
            };
        }

        /// <summary>The rival as the party knows it: reports that have reached it, never the truth.</summary>
        public static RivalKnown RivalView(WorldState s)
        {
            var c = Run(s);
            var k = new RivalKnown();
            if (c?.Reports == null) return k;
            foreach (var r in c.Reports)
            {
                if (r.DueTick > s.Tick) continue;
                k.Reports.Add(new RivalReport { Crystal = r.Crystal, Kind = r.Kind, At = r.At, Tick = r.Tick, DueTick = r.DueTick });
                if (r.DueTick > k.LastReportTick) k.LastReportTick = r.DueTick;
            }
            return k;
        }

        /// <summary>The journal, oldest first (copies).</summary>
        public static IReadOnlyList<JournalEntry> Journal(WorldState s)
        {
            var c = Run(s);
            var list = new List<JournalEntry>();
            if (c?.Journal == null) return list;
            foreach (var j in c.Journal) list.Add(new JournalEntry { Tick = j.Tick, Text = j.Text, Place = j.Place });
            return list;
        }

        /// <summary>One sentence for the map and the journal: where Mira is and what she is doing ("" with no campaign).</summary>
        public static string WhereIsMira(WorldState s)
        {
            var c = Run(s);
            var p = c?.Party;
            if (p == null) return "";
            var d = c.Definition;
            string here = CampaignTokens.PlaceName(d, p.At);
            if (here.Length == 0) here = "the camp";
            switch (p.Mode)
            {
                case PartyMode.Travelling:
                {
                    string to = p.Leg != null ? CampaignTokens.PlaceName(d, p.Leg.To) : "";
                    return to.Length > 0 ? "Mira is on her way to " + to + "." : "Mira is on the move.";
                }
                case PartyMode.WaitingForPlayer: return "Mira is waiting for you at " + here + ".";
                case PartyMode.AtThreshold: return "Mira stands ready at " + here + ".";
                case PartyMode.InEncounter: return "Mira is fighting at " + here + ".";
                default: return "Mira is at " + here + ".";
            }
        }

        /// <summary>A line with tokens filled and guards applied; null when guarded out, its speaker is dead, or unknown.</summary>
        public static string Line(WorldState s, string lineId) => LineOf(Run(s), lineId);

        internal static string LineOf(CampaignState c, string lineId)
        {
            var d = c?.Definition;
            var l = d?.Line(lineId);
            if (l == null) return null;
            var speaker = c.Companion(l.Speaker);
            if (speaker != null && !speaker.Alive) return null;
            if (!CampaignGuards.AllHold(c, l.Guards)) return null;
            return CampaignTokens.Fill(c, l.Text ?? "");
        }

        /// <summary>Every line of this speaker on this topic that may be said now, filled, in definition order.</summary>
        public static IReadOnlyList<string> LinesFor(WorldState s, string speaker, string topic)
        {
            var c = Run(s);
            var list = new List<string>();
            if (c == null) return list;
            foreach (var l in c.Definition.Lines)
            {
                if (!string.Equals(l.Speaker, speaker, StringComparison.Ordinal)) continue;
                if (!string.Equals(l.Topic ?? "", topic ?? "", StringComparison.Ordinal)) continue;
                var text = LineOf(c, l.Id);
                if (text != null) list.Add(text);
            }
            return list;
        }

        /// <summary>A connection is open: it exists and its OpenWhen fact is known (or it has none).</summary>
        public static bool IsOpen(WorldState s, string connectionId)
        {
            var c = Run(s);
            var con = c?.Definition.Connection(connectionId);
            if (con == null) return false;
            return string.IsNullOrEmpty(con.OpenWhen) || c.Knows(con.OpenWhen);
        }

        /// <summary>
        /// The Low Causeway is passable: the tide is out for TideDry ticks at the start of every TideCycle, counted from the
        /// campaign's start (5.11). False with no campaign.
        /// </summary>
        public static bool CausewayDry(WorldState s)
        {
            var c = Run(s);
            if (c == null) return false;
            var t = c.Definition.Tuning;
            if (t.TideCycle <= 0) return true;
            long phase = (s.Tick - c.StartTick) % t.TideCycle;
            if (phase < 0) phase += t.TideCycle;
            return phase < t.TideDry;
        }

        /// <summary>Validates a campaign command without queuing (the same check the handlers make). Never mutates.</summary>
        public static CommandResult Check(WorldState s, Command c) => CampaignRuntime.Check(s, c);

        /// <summary>
        /// The record for later ages (N-6): ending, definition, seed, restoration order, crystals ever on an altar, survivors
        /// and the fallen with place, tick and cause. Null with no campaign. Valid at any time; final after CampaignEnded.
        /// </summary>
        public static CampaignRecord Record(WorldState s)
        {
            var c = Run(s);
            if (c == null) return null;
            var d = c.Definition;
            var r = new CampaignRecord
            {
                EndingId = c.EndingId ?? "", DefinitionId = d.Id ?? "", Seed = d.Seed,
                RestoredOrder = new List<Element>(c.RestoredOrder ?? new List<Element>()),
                EverOnAltar = new List<Element>(c.EverOnAltar ?? new List<Element>()),
            };
            foreach (var v in Companions(s)) if (v.Alive) r.Survivors.Add(v.Id);
            var fallen = new List<CompanionState>();
            foreach (var k in c.Companions) if (!k.Alive) fallen.Add(k);
            fallen.Sort((a, b) => a.FellTick != b.FellTick ? a.FellTick.CompareTo(b.FellTick) : string.CompareOrdinal(a.Id, b.Id));
            foreach (var k in fallen)
            {
                var e = d.Encounter(k.FellAt);
                string place = e != null ? CampaignTokens.PlaceName(d, e.Threshold) : "";
                r.Fallen.Add(new FallenRecord
                {
                    Id = k.Id, Name = d.Companion(k.Id)?.Name ?? k.Id, EncounterId = k.FellAt ?? "", Place = place, Tick = k.FellTick,
                    Cause = k.Cause,
                });
            }
            return r;
        }
    }
}
