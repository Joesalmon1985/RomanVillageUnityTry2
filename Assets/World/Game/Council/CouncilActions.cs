using System.Collections.Generic;
using System.Text;
using UnityEngine;
using World.Sim;
using World.Sim.Encounters.Council;

namespace World.Game
{
    /// <summary>
    /// A council move that takes two taps (WU-4, owner's Q-8): a proposal (CNC-NOP), the bank with the recorder (CNC-U)
    /// or the way out by the ladder (CNC-EXIT). The first tap previews it in the village and on the ledger card, the
    /// second sends the command once. State is only read here; <see cref="Commit"/> goes through the encounter.
    /// </summary>
    public sealed class CouncilAction : IWorldAction, IScreenAnchored
    {
        public enum Kind { Proposal, Bank, Leave }
        public readonly Kind K;
        public readonly CouncilProposal P;
        readonly CouncilEncounter _enc;
        readonly int _pushId;

        public CouncilAction(CouncilEncounter enc, Kind kind, CouncilProposal p = null, int pushId = 0) { _enc = enc; K = kind; P = p; _pushId = pushId; }

        CouncilScene Scene => _enc.Scene;
        ElectionState E => _enc.E;

        static string Join(List<string> l)
        {
            if (l.Count == 0) return "the gatherings";
            if (l.Count == 1) return l[0];
            return string.Join(", ", l.GetRange(0, l.Count - 1)) + " and " + l[l.Count - 1];
        }

        public string Title => K == Kind.Proposal ? "A proposal" : K == Kind.Bank ? "Settle with the recorder" : "Climb down";

        public IEnumerable<string> Lines
        {
            get
            {
                if (K == Kind.Proposal && P != null)
                {
                    if (P.Steps.Count == 1 && P.Steps[0].Value >= 2) yield return "One representative would step forward twice at the " + CouncilNames.Place(P.Steps[0].Key) + ".";
                    else if (P.Steps.Count == 1 && P.Socket >= 0) yield return "Only the " + CouncilNames.Place(P.Steps[0].Key) + " would move. The other choice is left behind.";
                    else if (P.Steps.Count == 1) yield return "One representative would step forward at the " + CouncilNames.Place(P.Steps[0].Key) + ".";
                    else yield return "Representatives would step forward at the " + CouncilNames.Place(P.Steps[0].Key) + " and the " + CouncilNames.Place(P.Steps[1].Key) + ", both at once.";
                    yield return "The people there are turning to listen.";
                }
                else if (K == Kind.Bank)
                {
                    var l = new List<string>();
                    var e = E; if (e != null) for (int t = 2; t <= 12; t++) if (e.Temp[t - 2] > 0 && e.Claimed[t - 2] < 0) l.Add(CouncilNames.Place(t));
                    yield return "Everyone listening at the " + Join(l) + " would settle with us, for good.";
                    yield return "This ends the turn.";
                }
                else if (K == Kind.Leave)
                {
                    yield return "Your standing is kept. Come back and the council goes on where it stopped.";
                }
            }
        }

        public string PreviewLine => K == Kind.Proposal ? "Put this to the gatherings?" : K == Kind.Bank ? "Settle now?" : "Leave the council?";

        public WorldActionCheck Check()
        {
            var e = E; if (e == null) return WorldActionCheck.Disable("There is no council.");
            bool mine = e.PlayerSeat >= 0 && e.Active == e.PlayerSeat && e.Phase != ElectionPhase.Complete;
            switch (K)
            {
                case Kind.Leave: return WorldActionCheck.Allowed;
                case Kind.Bank:
                    if (!mine) return WorldActionCheck.Disable("It is not your turn.");
                    if (e.Phase == ElectionPhase.Ready && e.HasAdvanced) return WorldActionCheck.Allowed;
                    if (e.Phase == ElectionPhase.Ready) return WorldActionCheck.Refuse("Nothing is won yet. Press on first.");
                    return WorldActionCheck.Disable("Not now.");
                default:
                    if (!mine || e.Phase != ElectionPhase.Choosing || e.PushId != _pushId) return WorldActionCheck.Disable("These proposals have gone.");
                    return WorldActionCheck.Allowed;
            }
        }

        public void ShowPreview(bool on)
        {
            if (Scene == null) return;
            if (!on) { if (SamePreview()) Scene.ClearPreview(); return; }
            Scene.SetPreview(K == Kind.Proposal ? P : null, K == Kind.Bank, K == Kind.Leave);
        }

        bool SamePreview() => (K == Kind.Proposal && Scene.PreviewProposal == P) || (K == Kind.Bank && Scene.PreviewBank) || (K == Kind.Leave && Scene.PreviewLeave);

        public bool Commit()
        {
            return _enc.CommitAction(this);
        }

        /// <summary>The name of the screen target this action sits on.</summary>
        public string TargetName()
        {
            if (K == Kind.Leave) return "ladder";
            if (K == Kind.Bank) return "recorder";
            int k = Scene != null ? Scene.Proposals.IndexOf(P) : -1;
            return "note " + k;
        }

        public bool TryGetScreenRect(out Rect gui)
        {
            gui = default;
            var cam = Scene != null ? Scene.ViewCamera : null; if (cam == null) return false;
            string name = TargetName();
            foreach (var t in Scene.Targets(cam))
                if (t.Name == name) { gui = new Rect(t.Px.x - t.RadiusPx, Screen.height - t.Px.y - t.RadiusPx, t.RadiusPx * 2, t.RadiusPx * 2); return true; }
            return false;
        }
    }

    /// <summary>The recorder's tablet (CNC-STATUS): exact standings by candidate and place, in words and supporter counts (never dice, sums or odds).</summary>
    public sealed class CouncilTablet : IInspectable, IScreenAnchored
    {
        readonly CouncilEncounter _enc;
        public CouncilTablet(CouncilEncounter enc) { _enc = enc; }
        public string Title => "The recorder's tablet";
        public IEnumerable<string> Lines => Standings(_enc.Council);

        /// <summary>Per candidate: claims of three and the places claimed, settled people per place of its total; for the sitting candidate the people listening now; the petition.</summary>
        public static List<string> Standings(SettlementCouncil sc)
        {
            var l = new List<string>(); var e = sc?.Election; if (e == null) return l;
            for (int s = 0; s < 4 && s < sc.Candidates.Count; s++)
            {
                var sb = new StringBuilder();
                sb.Append(sc.Candidates[s].Name); if (s == e.PlayerSeat) sb.Append(" (yours)");
                sb.Append(": claims ").Append(e.ClaimsOf(s)).Append(" of ").Append(CouncilRules.ClaimsToWin);
                var claimed = new List<string>();
                for (int t = 2; t <= 12; t++) if (e.Claimed[t - 2] == s) claimed.Add(CouncilNames.Place(t));
                if (claimed.Count > 0) sb.Append(" (").Append(string.Join(", ", claimed)).Append(")");
                sb.Append('.');
                var settled = new List<string>();
                for (int t = 2; t <= 12; t++) { int b = e.BankedAt(s, t); if (b > 0 && e.Claimed[t - 2] < 0) settled.Add(CouncilNames.Place(t) + " " + b + " of " + CouncilRules.Length(t)); }
                if (settled.Count > 0) sb.Append(" Settled: ").Append(string.Join(", ", settled)).Append('.');
                if (s == e.Active && e.Phase != ElectionPhase.Complete)
                {
                    var temp = new List<string>();
                    for (int t = 2; t <= 12; t++) { int w = e.Temp[t - 2] - e.BankedAt(s, t); if (e.Temp[t - 2] > 0 && e.Claimed[t - 2] < 0 && w > 0) temp.Add(CouncilNames.Place(t) + " " + w + " more"); }
                    if (temp.Count > 0) sb.Append(" Listening now: ").Append(string.Join(", ", temp)).Append('.');
                }
                l.Add(sb.ToString());
            }
            if (e.PlayerSeat >= 0) l.Add(e.PetitionAvailable ? "You hold a petition." : e.PetitionSpent ? "Your petition is spent." : "You hold no petition.");
            return l;
        }

        public bool TryGetScreenRect(out Rect gui)
        {
            gui = default; var sc = _enc.Scene; var cam = sc != null ? sc.ViewCamera : null; if (cam == null) return false;
            foreach (var t in sc.Targets(cam)) if (t.Name == "tablet") { gui = new Rect(t.Px.x - t.RadiusPx, Screen.height - t.Px.y - t.RadiusPx, t.RadiusPx * 2, t.RadiusPx * 2); return true; }
            return false;
        }
    }
}
