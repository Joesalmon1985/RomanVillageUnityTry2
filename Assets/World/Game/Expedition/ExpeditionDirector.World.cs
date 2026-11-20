using System.Collections.Generic;
using UnityEngine;
using World.Sim;
using World.Sim.Campaign;

namespace World.Game
{
    /// <summary>
    /// WU-6: the expedition as things in the world (WORLD_UI_PLAN 2.6). Mira's act offered by tapping the thing (the
    /// lead whose goal is that marker, sent as one AcceptLead on the second tap), a companion's card, Mira's pouch and
    /// her journal book. Reads state; the only command is the one AcceptLead of <see cref="CommitAct"/>.
    /// </summary>
    public sealed partial class ExpeditionDirector
    {
        /// <summary>Mira's pouch and journal book, built on her body (tests).</summary>
        public MiraPouch Pouch { get; private set; }
        public MiraJournalBook Book { get; private set; }

        /// <summary>True while the player is in Mira's company (the sim's flag).</summary>
        public bool PlayerPresent => C != null && C.Party.PlayerPresent;

        private void BuildWorldUi()
        {
            if (Mira == null) return;
            var body = Mira.Actor is PlaceholderActor pa ? pa.BodyPivot : Mira.transform;
            Pouch = MiraPouch.Build(this, body);
            Book = MiraJournalBook.Build(this, body);
            foreach (var m in _mates)
            {
                var card = m.Npc.gameObject.GetComponent<CompanionCard>() ?? m.Npc.gameObject.AddComponent<CompanionCard>();
                card.Dir = this; card.Mate = m;
                WorldZone.Tag(m.Npc.gameObject, WorldZones.CryParty);
            }
            WorldZone.Tag(Mira.gameObject, WorldZones.CryParty);
        }

        /// <summary>The lead whose goal is this marker (Mira's act there: examine, search, take, restore), or null.</summary>
        public LeadView ActFor(MarkerRef at)
        {
            if (C == null || C.Phase != CampaignPhase.Running) return null;
            foreach (var l in CampaignApi.Leads(S)) if (l.Goal == at) return l;
            return null;
        }

        /// <summary>The sim's answer to AcceptLead for this lead now (state read only).</summary>
        public CommandResult CheckAct(int leadId) => CampaignApi.Check(S, new AcceptLead { LeadId = leadId });

        /// <summary>The player takes Mira's advice to go for a lead through the thing: one AcceptLead, as her own question sends.</summary>
        public bool CommitAct(int leadId)
        {
            if (C == null || C.Phase != CampaignPhase.Running) return false;
            AcceptLead(leadId);
            return LastCommand == "AcceptLead";
        }

        /// <summary>"Lead me out" from a companion: Mira says so and the walk to the nearest open way up begins.</summary>
        public bool LeadMeOut()
        {
            if (Interiors.Current == null) return false;
            SayLine(ExpeditionLines.MiraLeadOut);
            return Interiors.LeadOut();
        }

        /// <summary>Mira turns to a thing while its act is offered (null: back to nothing).</summary>
        public void MiraLooksAt(Vector3? point) { if (Mira != null) Mira.PointAt(point); }

        /// <summary>The words of a companion's service (what the sim does with them), our own text.</summary>
        public static string ServiceWords(string service)
        {
            switch (service)
            {
                case CompanionServices.Shield: return "Carries the shield. Covers a retreat after a clash.";
                case CompanionServices.Lamp: return "Carries the lamp. Unseen marks show as a question mark on the map.";
                case CompanionServices.Fire: return "Keeps the kiln order.";
                case CompanionServices.Water: return "Reads the water: floods and the cistern sluice.";
                case CompanionServices.Earth: return "Reads the forest marks.";
                case CompanionServices.Scout: return "Goes ahead to scout.";
                default: return "";
            }
        }

        /// <summary>The crystals Mira carries now, by name (the pouch).</summary>
        public List<string> CarriedNames()
        {
            var list = new List<string>();
            if (C == null) return list;
            for (int e = 0; e < C.Crystals.Length; e++)
            {
                if (C.Crystals[e].Custody != Custody.Carried) continue;
                var cd = C.Definition.Crystal((Element)e);
                list.Add(cd != null && !string.IsNullOrEmpty(cd.Name) ? cd.Name : ((Element)e).ToString());
            }
            return list;
        }

        /// <summary>A short account of where a companion is: with the party, scouting, fallen.</summary>
        public string MateStatus(Mate m)
        {
            if (!m.Alive)
            {
                string where = "";
                foreach (var cv in CampaignApi.Companions(S))
                    if (cv.Id == m.Id && !string.IsNullOrEmpty(cv.FellAt)) { var e = D.Encounter(cv.FellAt); where = e != null && !string.IsNullOrEmpty(e.Name) ? e.Name : cv.FellAt; }
                return "Fallen" + (where.Length > 0 ? " at " + where : "") + ". The party remembers.";
            }
            if (m.Out) return "Alive. Out scouting.";
            var p = C.Party;
            if (p.Mode == PartyMode.AtThreshold) return "Alive. Covering the way.";
            if (p.Mode == PartyMode.Acting) return "Alive. Standing by while Mira works.";
            return "Alive. With the party.";
        }
    }

    // ---------------- the things ----------------

    /// <summary>
    /// Tap a pedestal, stone or marker where Mira has an errand: she turns to it and offers the act ("Ask Mira to take
    /// it?"); the second tap sends the one command (AcceptLead). Where she has none, the card only reads the thing.
    /// </summary>
    public sealed class MiraActThing : MonoBehaviour, IWorldAction, IWorldActionAnchor, IWorldPrimary
    {
        public InteriorThing Thing;
        private GameObject _ring;

        public string Title => Thing != null ? Thing.Title : name;
        private LeadView Lead => ExpeditionDirector.Instance != null && Thing != null ? ExpeditionDirector.Instance.ActFor(Thing.Ref) : null;

        public IEnumerable<string> Lines
        {
            get
            {
                var lines = new List<string>();
                if (Thing != null) lines.AddRange(Thing.Lines);
                var l = Lead;
                if (l != null && !string.IsNullOrEmpty(l.Reason)) lines.Add(ExpeditionLines.Sentence(l.Reason));
                return lines;
            }
        }

        public string PreviewLine
        {
            get
            {
                var l = Lead;
                if (l == null) return null;
                switch (l.Action)
                {
                    case QuestAction.Examine: return "Ask Mira to read it?";
                    case QuestAction.Search: return "Ask Mira to search here?";
                    case QuestAction.Take: return "Ask Mira to take it?";
                    case QuestAction.Restore: return "Ask Mira to set the crystal?";
                    case QuestAction.Talk: return "Ask Mira to talk with them?";
                    default: return "Ask Mira to go there?";
                }
            }
        }

        public Transform Anchor => transform;
        public float KeyReach => 3f;

        public WorldActionCheck Check()
        {
            var d = ExpeditionDirector.Instance;
            var l = Lead;
            if (d == null || l == null) return WorldActionCheck.Disable(null);
            var r = d.CheckAct(l.Id);
            return r.Ok ? WorldActionCheck.Allowed : WorldActionCheck.Refuse(string.IsNullOrEmpty(r.Message) ? "Mira cannot go there now." : r.Message);
        }

        public void ShowPreview(bool on)
        {
            var d = ExpeditionDirector.Instance;
            if (d != null) d.MiraLooksAt(on ? transform.position + Vector3.up * 0.9f : (Vector3?)null);
            if (on && _ring == null)
            {
                _ring = Signs.Prim(PrimitiveType.Cylinder, transform, "Act ring", new Vector3(0, 0.03f, 0), new Vector3(1.9f, 0.015f, 1.9f), MaterialFactory.CreateUnlit(new Color(1f, 0.82f, 0.40f), "Act ring"));
                var col = _ring.GetComponent<Collider>(); if (col != null) Destroy(col);
            }
            if (_ring != null) _ring.SetActive(on);
        }

        public bool Commit()
        {
            var d = ExpeditionDirector.Instance;
            var l = Lead;
            return d != null && l != null && d.CommitAct(l.Id);
        }

        private void OnEnable() => WorldActions.Register(this);
        private void OnDisable() => WorldActions.Unregister(this);
    }

    /// <summary>A companion's card (CRY-PARTY): tap for name, trade, what they do for the party and whether they live; underground a second tap asks them to lead you out.</summary>
    public sealed class CompanionCard : MonoBehaviour, IWorldAction, IWorldActionAnchor, IWorldPrimary
    {
        public ExpeditionDirector Dir;
        public ExpeditionDirector.Mate Mate;

        public string Title => Mate != null ? Mate.Def.Name : name;

        public IEnumerable<string> Lines
        {
            get
            {
                var l = new List<string>();
                if (Mate == null || Dir == null) return l;
                if (!string.IsNullOrEmpty(Mate.Def.Role)) l.Add(Mate.Def.Role.TrimEnd('.') + ".");
                var sv = ExpeditionDirector.ServiceWords(Mate.Def.Service);
                if (sv.Length > 0) l.Add(sv);
                l.Add(Dir.MateStatus(Mate));
                return l;
            }
        }

        public string PreviewLine => "Ask " + (Mate != null ? Mate.Def.Name : "them") + " to lead you out?";
        public Transform Anchor => transform;
        public float KeyReach => 0f;

        public WorldActionCheck Check()
        {
            if (Dir == null || Mate == null || !Mate.Alive || Interiors.Current == null || !Dir.PlayerPresent) return WorldActionCheck.Disable(null);
            return WorldActionCheck.Allowed;
        }

        public void ShowPreview(bool on) { if (on && Dir != null && PlayerWalker.Instance != null) Mate.Npc.FaceTowards(PlayerWalker.Instance.Position); }

        /// <summary>"Lead me out": the walk to the nearest open way up (no sim command; the walker's own steps).</summary>
        public bool Commit() => Dir != null && Dir.LeadMeOut();

        private void OnEnable() => WorldActions.Register(this);
        private void OnDisable() => WorldActions.Unregister(this);
    }

    /// <summary>Mira's pouch on her belt (CRY-PACK): tap for the crystals she carries. There is no other inventory.</summary>
    public sealed class MiraPouch : MonoBehaviour, IInteractable, IInspectable, IWorldPrimary
    {
        private ExpeditionDirector _dir;
        public string Title => "Mira's pouch";
        public string Prompt => Title;

        public IEnumerable<string> Lines
        {
            get
            {
                var names = _dir != null ? _dir.CarriedNames() : new List<string>();
                if (names.Count == 0) return new[] { "Nothing in it now. No crystal is in her keeping." };
                var l = new List<string> { names.Count == 1 ? "She carries one crystal." : "She carries " + names.Count + " crystals." };
                foreach (var n in names) l.Add(n + ".");
                return l;
            }
        }

        public static MiraPouch Build(ExpeditionDirector dir, Transform body)
        {
            var go = new GameObject("Mira's pouch");
            go.transform.SetParent(body, false);
            go.transform.localPosition = new Vector3(0.30f, 0.86f, 0.22f);
            var leather = MaterialFactory.Shared(new Color(0.48f, 0.30f, 0.16f));
            Signs.Prim(PrimitiveType.Sphere, go.transform, "Sack", Vector3.zero, new Vector3(0.22f, 0.26f, 0.20f), leather);
            Signs.Prim(PrimitiveType.Cylinder, go.transform, "Tie", new Vector3(0, 0.13f, 0), new Vector3(0.12f, 0.02f, 0.12f), MaterialFactory.Shared(new Color(0.78f, 0.66f, 0.40f)));
            Signs.Prim(PrimitiveType.Cube, go.transform, "Strap", new Vector3(-0.16f, 0.14f, 0), new Vector3(0.34f, 0.04f, 0.04f), leather, new Vector3(0, 0, 40f));
            LandLook.PickBox(go, Vector3.zero, new Vector3(1.3f, 1.3f, 1.3f)); // P2a: a larger tap volume than the drawing
            var p = go.AddComponent<MiraPouch>();
            p._dir = dir;
            WorldZone.Tag(go, WorldZones.CryPack);
            return p;
        }

        /// <summary>A tap: the ledger names what she carries.</summary>
        public void Interact() => WorldActions.Inspect(this);

        private void OnEnable() => WorldActions.Register(this);
        private void OnDisable() => WorldActions.Unregister(this);
    }

    /// <summary>Mira's journal, a book at her hip (CRY-CAMP): tap for the journal as the map shows it (J does the same).</summary>
    public sealed class MiraJournalBook : MonoBehaviour, IInteractable, IInspectable, IWorldPrimary
    {
        private ExpeditionDirector _dir;
        public string Title => "Mira's journal";
        public string Prompt => Title;
        public IEnumerable<string> Lines => new[] { "What she knows, and where to look next." };

        public static MiraJournalBook Build(ExpeditionDirector dir, Transform body)
        {
            var go = new GameObject("Mira's journal");
            go.transform.SetParent(body, false);
            go.transform.localPosition = new Vector3(-0.32f, 0.92f, 0.16f);
            go.transform.localRotation = Quaternion.Euler(0, 0, 8f);
            Signs.Prim(PrimitiveType.Cube, go.transform, "Cover", Vector3.zero, new Vector3(0.20f, 0.27f, 0.06f), MaterialFactory.Shared(new Color(0.40f, 0.12f, 0.14f)));
            Signs.Prim(PrimitiveType.Cube, go.transform, "Pages", new Vector3(0.012f, 0, 0), new Vector3(0.17f, 0.24f, 0.07f), MaterialFactory.Shared(new Color(0.92f, 0.88f, 0.74f)));
            Signs.Prim(PrimitiveType.Cube, go.transform, "Band", new Vector3(-0.06f, 0, 0.04f), new Vector3(0.025f, 0.28f, 0.01f), MaterialFactory.Shared(new Color(0.85f, 0.70f, 0.30f)));
            LandLook.PickBox(go, Vector3.zero, new Vector3(1.2f, 1.3f, 1.2f)); // P2a
            var b = go.AddComponent<MiraJournalBook>();
            b._dir = dir;
            WorldZone.Tag(go, WorldZones.CryCamp);
            return b;
        }

        /// <summary>A tap: the journal opens.</summary>
        public void Interact() { if (_dir != null && _dir.Journal != null) _dir.Journal.Open(); }

        private void OnEnable() => WorldActions.Register(this);
        private void OnDisable() => WorldActions.Unregister(this);
    }
}
