using System.Collections.Generic;
using UnityEngine;
using World.Sim;
using World.Sim.Economy;

namespace World.Game
{
    /// <summary>
    /// Package H1-5: reads the building and settlement views of <see cref="LandViews"/> and dresses them from state
    /// (grade, upgrade, town, owner) with <see cref="BuildingDress"/> and <see cref="SettlementDress"/>. It reconciles
    /// after every sync, after a rebuild and a few times a second; events only trigger the one-off dust and cheer.
    /// </summary>
    public sealed class GradeViews : MonoBehaviour
    {
        public static GradeViews Instance { get; private set; }
        public int DustBursts { get; private set; }
        private int _rebuildSeen = -1;
        private float _next;
        private LandViews _views;

        public static GradeViews Create(LandViews views)
        {
            var go = new GameObject("Grade views");
            go.transform.SetParent(views.transform, false);
            var g = go.AddComponent<GradeViews>();
            g._views = views;
            Instance = g;
            views.Synced += g.OnSynced;
            return g;
        }

        private void OnDestroy()
        {
            if (_views != null) _views.Synced -= OnSynced;
            if (Instance == this) Instance = null;
        }

        private void OnSynced(IReadOnlyList<SimEvent> events)
        {
            Reconcile();
            if (events == null) return;
            foreach (var e in events)
            {
                if (e is BuildingUpgraded bu)
                {
                    var v = _views.Building(bu.BuildingId);
                    if (v != null) { GradeFx.Burst(v.transform, v.Faction, 1f); DustBursts++; }
                }
                else if (e is TownUpgraded tu)
                {
                    var v = _views.Settlement(tu.SettlementId);
                    if (v != null) { GradeFx.Burst(v.transform, v.Faction, 2f); DustBursts++; }
                }
            }
        }

        private void LateUpdate()
        {
            if (_views == null) return;
            float now = Time.unscaledTime;
            if (_views.RebuildCount != _rebuildSeen || now >= _next) { _next = now + 0.25f; Reconcile(); }
            foreach (var b in _views.Buildings) { if (b == null) continue; var d = b.GetComponent<BuildingDress>(); if (d != null) d.Animate(now); }
        }

        /// <summary>Dresses every view from the current state (tests call it after changing the world).</summary>
        public void Reconcile()
        {
            var s = LandText.State;
            if (s == null || _views == null) return;
            _rebuildSeen = _views.RebuildCount;
            foreach (var bv in _views.Buildings)
            {
                if (bv == null || bv.BuildingId < 0 || bv.BuildingId >= s.Economy.Buildings.Count) continue;
                var d = bv.GetComponent<BuildingDress>();
                if (d == null) d = bv.gameObject.AddComponent<BuildingDress>();
                d.Apply(s, bv, s.Economy.Buildings[bv.BuildingId]);
            }
            foreach (var sv in _views.Settlements)
            {
                if (sv == null || sv.SettlementId < 0 || sv.SettlementId >= s.Economy.Settlements.Count) continue;
                var d = sv.GetComponent<SettlementDress>();
                if (d == null) d = sv.gameObject.AddComponent<SettlementDress>();
                d.Apply(s, sv, s.Economy.Settlements[sv.SettlementId]);
            }
        }

        /// <summary>Sorted description of every dressed view: the proof that a rebuilt scene equals the incremental one.</summary>
        public List<string> Snapshot()
        {
            var list = new List<string>();
            foreach (var bv in _views.Buildings) { if (bv == null) continue; var d = bv.GetComponent<BuildingDress>(); if (d != null) list.Add(d.Describe()); }
            foreach (var sv in _views.Settlements) { if (sv == null) continue; var d = sv.GetComponent<SettlementDress>(); if (d != null) list.Add(d.Describe()); }
            list.Sort(System.StringComparer.Ordinal);
            return list;
        }
    }

    /// <summary>Wording of the grade lines of the inspect label (the upgrade sentences are the sim's own).</summary>
    public static class GradeText
    {
        public static string Name(Grade g) => g == Grade.Historic ? "Historic" : g == Grade.Modern ? "Modern" : "Prehistoric";

        public static string BuildingLine(Building b)
        {
            if (b.State != BuildingState.Active) return null;
            var next = (Grade)((int)b.Grade + 1);
            if (b.Upgrading) return Name(b.Grade) + " grade, being improved to " + Name(next) + ".";
            return Name(b.Grade) + " grade.";
        }
    }

    /// <summary>The tap label of a building: its own lines and a grade line (the sim says what an upgrade still needs).</summary>
    public sealed class GradeInspect : MonoBehaviour, IInspectable
    {
        public BuildingView View;
        public string Title => View != null ? View.Title + GradeSuffix() : "";
        public IEnumerable<string> Lines
        {
            get
            {
                if (View == null) yield break;
                foreach (var l in View.Lines) yield return l;
                var s = LandText.State;
                if (s == null || View.BuildingId >= s.Economy.Buildings.Count) yield break;
                string g = GradeText.BuildingLine(s.Economy.Buildings[View.BuildingId]);
                if (g != null) yield return g;
            }
        }
        private string GradeSuffix()
        {
            var s = LandText.State;
            if (s == null || View.BuildingId >= s.Economy.Buildings.Count) return "";
            var b = s.Economy.Buildings[View.BuildingId];
            return b.State == BuildingState.Active && b.Grade >= Grade.Historic ? " (" + GradeText.Name(b.Grade) + ")" : "";
        }
    }

    /// <summary>The tap label of a camp: its own lines and whether it is a town.</summary>
    public sealed class SettlementInspect : MonoBehaviour, IInspectable
    {
        public SettlementView View;
        public string Title => View != null ? View.Title : "";
        public IEnumerable<string> Lines
        {
            get
            {
                if (View == null) yield break;
                foreach (var l in View.Lines) yield return l;
                var s = LandText.State;
                if (s == null || View.SettlementId >= s.Economy.Settlements.Count) yield break;
                var st = s.Economy.Settlements[View.SettlementId];
                if (st.State != SettlementState.Active) yield break;
                if (st.IsTown) yield return "A town: its outer plots are open for building.";
                else if (st.IsCity) yield return "A city. It can become a town.";
            }
        }
    }

    /// <summary>One-off dust and cheer when an upgrade completes.</summary>
    public sealed class GradeFx : MonoBehaviour
    {
        const float Life = 2.6f;
        private float _t;
        private readonly List<Transform> _puffs = new List<Transform>(), _sparks = new List<Transform>();
        private readonly List<Vector3> _vel = new List<Vector3>();
        private readonly List<LandFigure> _cheer = new List<LandFigure>();

        public static GradeFx Burst(Transform at, int faction, float size)
        {
            var go = new GameObject("Upgrade dust and cheer");
            go.transform.SetParent(at, false);
            var fx = go.AddComponent<GradeFx>();
            var puff = LandLook.Cached("gfx/puff", mb => mb.Box(Vector3.zero, new Vector3(1f, 0.8f, 1f), new Color(0.78f, 0.72f, 0.62f), 20f));
            var spark = LandLook.Cached("gfx/spark", mb => mb.Box(Vector3.zero, new Vector3(0.14f, 0.14f, 0.14f), Color.white, 30f));
            int n = Mathf.RoundToInt(12 * size);
            for (int i = 0; i < n; i++)
            {
                var p = LandLook.Part(go.transform, "Dust", puff, null, false).transform;
                float a = i * 360f / n;
                p.localPosition = Quaternion.Euler(0, a, 0) * Vector3.forward * (1.0f + 1.2f * size) + Vector3.up * 0.2f;
                p.localScale = Vector3.one * 0.3f;
                fx._puffs.Add(p);
                fx._vel.Add(Quaternion.Euler(0, a, 0) * Vector3.forward * (1.2f + 0.4f * size) + Vector3.up * 0.9f);
            }
            var tc = LandLook.PeopleTrim(faction);
            for (int i = 0; i < 10; i++)
            {
                var sp = LandLook.Part(go.transform, "Cheer spark", spark, LandLook.Unlit, false).transform;
                sp.localPosition = new Vector3(Mathf.Sin(i * 1.7f) * 1.2f, 1.2f, 2.6f + Mathf.Cos(i * 2.3f) * 0.8f);
                fx._sparks.Add(sp);
            }
            for (int i = 0; i < 3; i++)
            {
                var f = LandFigure.Create(go.transform, "Cheering", faction);
                f.transform.localPosition = new Vector3(-1.1f + i * 1.1f, 0, 3.3f);
                f.transform.localRotation = Quaternion.Euler(0, 180f, 0);
                fx._cheer.Add(f);
            }
            return fx;
        }

        private void Update()
        {
            _t += Time.deltaTime;
            float u = Mathf.Clamp01(_t / Life);
            for (int i = 0; i < _puffs.Count; i++)
            {
                var p = _puffs[i];
                p.localPosition += _vel[i] * Time.deltaTime * (1f - u);
                p.localScale = Vector3.one * (0.3f + 1.6f * Mathf.Sin(u * Mathf.PI * 0.5f)) * (1f - u * u);
            }
            for (int i = 0; i < _sparks.Count; i++)
            {
                var sp = _sparks[i];
                sp.localPosition += new Vector3(Mathf.Sin(i * 3.1f) * 0.5f, 2.2f - 5f * u, Mathf.Cos(i * 1.9f) * 0.5f) * Time.deltaTime;
                sp.localRotation = Quaternion.Euler(_t * 200f, _t * 150f, 0);
                sp.localScale = Vector3.one * (1f - u * u);
            }
            for (int i = 0; i < _cheer.Count; i++)
            {
                var f = _cheer[i];
                f.Animate(Time.time);
                var lp = f.transform.localPosition; lp.y = Mathf.Abs(Mathf.Sin(_t * 7f + i)) * 0.45f * (1f - u);
                f.transform.localPosition = lp;
            }
            if (_t >= Life) Destroy(gameObject);
        }
    }
}
