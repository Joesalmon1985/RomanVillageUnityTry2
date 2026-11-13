using System.Collections.Generic;
using UnityEngine;
using World.Sim;
using World.Sim.Economy;
using World.Sim.Factions;

namespace World.Game
{
    /// <summary>
    /// The attacker's war camp while an overrun warning runs (REFACTOR_SPEC 2.C.3, 8.4): three tents in its colours, a
    /// war fire, standards and four warriors facing the defender, beside the attacker's camp nearest the defender
    /// (18 m out along the hex edge that points most towards it, 3.5 m to the side, clear of plots and paths), with
    /// the seconds left on a board. It stays up after an overrun.
    /// </summary>
    public sealed class WarCampView : MonoBehaviour, IInspectable
    {
        public int Attacker { get; private set; }
        public int Defender { get; private set; }
        public int AtSettlement { get; private set; }

        private TextMesh _countdown;
        private GameObject _fire;
        private readonly List<LandFigure> _warriors = new List<LandFigure>();

        public string Title => Attacker == PlayerIds.Rival ? "The Ash Clan's war camp" : "Your people's war camp";
        public IEnumerable<string> Lines
        {
            get
            {
                var s = LandText.State;
                if (s == null) yield break;
                var w = FactionApi.Warning(s);
                if (w.Active) yield return w.Sentence;
                else yield return FactionApi.Headline(s);
            }
        }

        /// <summary>The attacker's finished camp nearest any finished camp of the defender (ties lower id), or null.</summary>
        public static Settlement Nearest(WorldState s, int attacker, int defender, out Settlement target)
        {
            Settlement best = null; target = null; float bestD = float.MaxValue;
            var t = s.Board.Topology;
            foreach (var a in s.Economy.Settlements)
            {
                if (a.Faction != attacker || a.State != SettlementState.Active) continue;
                foreach (var d in s.Economy.Settlements)
                {
                    if (d.Faction != defender || d.State != SettlementState.Active) continue;
                    float dist = (BoardSpace.Vertex(t, a.Corner) - BoardSpace.Vertex(t, d.Corner)).sqrMagnitude;
                    if (dist < bestD - 0.01f) { bestD = dist; best = a; target = d; }
                }
            }
            return best;
        }

        public static WarCampView Create(Transform parent, WorldState s, int attacker, int defender)
        {
            var at = Nearest(s, attacker, defender, out var target);
            if (at == null) return null;
            var t = s.Board.Topology;
            var c = BoardSpace.Vertex(t, at.Corner);
            var goal = target != null ? BoardSpace.Vertex(t, target.Corner) : Vector3.zero;
            var toGoal = (goal - c); toGoal.y = 0; toGoal = toGoal.sqrMagnitude > 0 ? toGoal.normalized : Vector3.forward;
            // The hex edge from this corner that points most towards the defender.
            Vector3 dir = toGoal; float bestDot = -2f;
            foreach (int he in t.CornerHexEdges(at.Corner))
            {
                int other = t.HexEdgeCornerA(he) == at.Corner ? t.HexEdgeCornerB(he) : t.HexEdgeCornerA(he);
                var d = BoardSpace.Vertex(t, other) - c; d.y = 0; d.Normalize();
                float dot = Vector3.Dot(d, toGoal);
                if (dot > bestDot) { bestDot = dot; dir = d; }
            }
            var side = Vector3.Cross(Vector3.up, dir);
            if (Vector3.Dot(side, toGoal) < 0) side = -side;
            var go = new GameObject("War camp (" + (attacker == PlayerIds.Rival ? "Ash Clan" : "yours") + ")");
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(c + dir * 18f + side * 3.5f, Quaternion.LookRotation(toGoal, Vector3.up));
            var v = go.AddComponent<WarCampView>();
            v.Attacker = attacker; v.Defender = defender; v.AtSettlement = at.Id;
            LandLook.Part(go.transform, "Tents", LandLook.Cached("war/tents/" + attacker, mb =>
            {
                var pc = LandLook.People(attacker); var tc = LandLook.PeopleTrim(attacker);
                LandShapes.Tent(mb, new Vector3(-3.2f, 0, -1.5f), 1.6f, 3.0f, Color.Lerp(LandLook.Hide, pc, 0.55f));
                LandShapes.Tent(mb, new Vector3(3.2f, 0, -1.5f), 1.6f, 3.0f, Color.Lerp(LandLook.Hide, pc, 0.55f));
                LandShapes.Tent(mb, new Vector3(0, 0, -3.4f), 1.9f, 3.6f, Color.Lerp(LandLook.Hide, tc, 0.5f));
                for (int i = 0; i < 2; i++)
                {
                    var p = new Vector3(i == 0 ? -1.5f : 1.5f, 0, 2.4f);
                    LandShapes.Beam(mb, p, p + Vector3.up * 5.5f, 0.07f, 5, LandLook.DarkWood);
                    mb.Box(p + new Vector3(0, 4.6f, 0.05f), new Vector3(1.4f, 1.6f, 0.06f), pc);
                    mb.Box(p + new Vector3(0, 4.6f, 0.1f), new Vector3(0.6f, 0.6f, 0.04f), tc);
                    mb.Cone(p + Vector3.up * 5.5f, 0.12f, 0.5f, 4, LandLook.Stone);
                }
                for (int i = 0; i < 8; i++) mb.Box(Quaternion.Euler(0, i * 45f, 0) * Vector3.forward * 0.8f + Vector3.up * 0.12f, new Vector3(0.3f, 0.24f, 0.26f), LandLook.Stone, i * 45f);
                mb.Box(new Vector3(0, 1.0f, 3.4f), new Vector3(2.0f, 1.0f, 0.12f), LandLook.DarkWood);
                LandShapes.Beam(mb, new Vector3(-0.8f, 0, 3.4f), new Vector3(-0.8f, 1.0f, 3.4f), 0.05f, 4, LandLook.DarkWood);
                LandShapes.Beam(mb, new Vector3(0.8f, 0, 3.4f), new Vector3(0.8f, 1.0f, 3.4f), 0.05f, 4, LandLook.DarkWood);
            }));
            v._fire = LandLook.Part(go.transform, "WarFire", LandLook.Cached("war/fire", mb => LandShapes.Flames(mb, Vector3.zero, 0.7f, 1.8f)), LandLook.Unlit, false);
            v._countdown = Signs.Text(go.transform, "", new Vector3(0, 1.05f, 3.49f), 0.6f, new Color(1f, 0.85f, 0.6f), 180f);
            v._countdown.name = "Countdown";
            for (int i = 0; i < 4; i++)
            {
                var f = LandFigure.Create(go.transform, "War warrior " + i, attacker, LandFigure.Kind.Warrior);
                f.transform.localPosition = new Vector3(-3f + i * 2f, 0, 5.2f);
                _ = f; v._warriors.Add(f);
            }
            LandLook.PickBox(go, Vector3.up * 1.6f, new Vector3(10f, 3.2f, 10f));
            return v;
        }

        public void Sync(WorldState s)
        {
            var w = FactionApi.Warning(s);
            long secs = (w.TicksLeft + WorldSim.TicksPerSecond - 1) / WorldSim.TicksPerSecond;
            _countdown.text = w.Active ? secs + " s" : s.Factions.GameOver ? "Overrun" : "";
        }

        public void Animate(float now)
        {
            _fire.transform.localScale = new Vector3(1f, 1f + 0.18f * Mathf.Sin(now * 8f) + 0.08f * Mathf.Sin(now * 21f), 1f);
            for (int i = 0; i < _warriors.Count; i++)
            {
                _warriors[i].Body.localRotation = Quaternion.Euler(0, 0, 0);
                _warriors[i].Body.localPosition = new Vector3(0, Mathf.Max(0, Mathf.Sin(now * 4f + i * 1.3f)) * 0.18f, 0);
            }
        }
    }
}
