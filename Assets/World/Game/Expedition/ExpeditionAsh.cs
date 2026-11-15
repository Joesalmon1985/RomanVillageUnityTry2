using System.Collections.Generic;
using UnityEngine;
using World.Sim;
using World.Sim.Campaign;

namespace World.Game
{
    /// <summary>
    /// The Ash expedition in the world (package I6, PRD 5.3): three runners in the Ash Clan's red on the surface,
    /// marching from the Ash home to the site they are after, searching it, or carrying a seized crystal (the runner in
    /// the middle holds it, glowing) to the place of its altar. They are drawn only when the player is near enough to
    /// see them (<see cref="SeeDistance"/>): what the player otherwise knows of the rival is the reports
    /// (<see cref="CampaignApi.RivalView"/>), which the director shows as cues and the map shows with their age. The
    /// position is read from the rival's state and plan; nothing is written.
    /// </summary>
    public sealed class ExpeditionAsh : MonoBehaviour
    {
        /// <summary>The runners are drawn when the player is within this many metres of them.</summary>
        public static float SeeDistance = 170f;

        private ExpeditionDirector _dir;
        private WorldBootstrap _boot;
        private readonly List<LandFigure> _runners = new List<LandFigure>();
        private GameObject _carried;
        private Element _carriedElement = (Element)(-1);
        private Vector3 _lastPos;

        /// <summary>True while the runners are drawn (the player is near them on the surface).</summary>
        public bool Visible { get; private set; }
        /// <summary>True when the drawn runners carry a crystal.</summary>
        public bool CarryingShown => Visible && _carried != null && _carried.activeSelf;
        /// <summary>Where the expedition is on the surface now (valid when <see cref="OnSurface"/>).</summary>
        public Vector3 Position { get; private set; }
        public bool OnSurface { get; private set; }
        /// <summary>The last report that reached the party, or null.</summary>
        public RivalReport LastReport { get; private set; }
        public int ReportsSeen { get; private set; }

        public static ExpeditionAsh Create(ExpeditionDirector dir, WorldBootstrap boot, Transform parent)
        {
            var go = new GameObject("Ash expedition");
            go.transform.SetParent(parent, false);
            var a = go.AddComponent<ExpeditionAsh>();
            a._dir = dir; a._boot = boot;
            for (int i = 0; i < 3; i++)
            {
                var f = LandFigure.Create(go.transform, "Ash runner " + i, PlayerIds.Rival, i == 0 ? LandFigure.Kind.Warrior : LandFigure.Kind.Villager);
                f.gameObject.SetActive(false);
                a._runners.Add(f);
            }
            var known = CampaignApi.RivalView(boot.Sim.State);
            if (known.Reports.Count > 0) a.LastReport = known.Reports[known.Reports.Count - 1];
            return a;
        }

        public void OnReported(RivalReported rr)
        {
            ReportsSeen++;
            var known = CampaignApi.RivalView(_boot.Sim.State);
            if (known.Reports.Count > 0) LastReport = known.Reports[known.Reports.Count - 1];
        }

        private Vector3 LocationPoint(CampaignDefinition d, WorldState s, string locationId)
        {
            var loc = d.Location(locationId);
            if (loc != null && CampaignPlaces.TryAnchorPoint(d, loc, s.Board, out double x, out double z)) return new Vector3((float)x, 0, (float)z);
            return Vector3.zero;
        }

        /// <summary>The expedition's place on the surface from its state: false while it is paused out of sight, done, or has no step.</summary>
        public bool TryPosition(out Vector3 pos, out bool carrying, out Element crystal)
        {
            pos = default; carrying = false; crystal = Element.Earth;
            var s = _boot.Sim.State; var c = s.Campaign;
            if (c == null || c.Phase != CampaignPhase.Running || c.Rival == null) return false;
            var plan = c.RivalPlan ?? c.Definition.Rival?.Steps;
            if (plan == null || c.Rival.Step < 0 || c.Rival.Step >= plan.Count) return false;
            var step = plan[c.Rival.Step];
            var cd = c.Definition.Crystal(step.Crystal);
            if (cd == null) return false;
            crystal = cd.Element;
            var t = s.Board.Topology;
            var ashHome = BoardSpace.Vertex(t, s.Board.StartCorners[s.Board.StartCorners.Length > 1 ? PlayerIds.Rival : 0]);
            var site = LocationPoint(c.Definition, s, cd.Site.Location);
            var altar = LocationPoint(c.Definition, s, step.Altar.Location);
            // Each march starts where the last step ended: the Ash home for the first, the last altar's place after that.
            var from = ashHome;
            if (c.Rival.Step > 0) from = LocationPoint(c.Definition, s, plan[c.Rival.Step - 1].Altar.Location);
            var phase = c.Rival.Phase == RivalPhase.Paused ? c.RivalResumePhase : c.Rival.Phase;
            switch (phase)
            {
                case RivalPhase.Marching:
                    pos = Vector3.Lerp(from, site, step.MarchCost > 0 ? Mathf.Clamp01(c.Rival.StepProgress / (float)step.MarchCost) : 1f);
                    return true;
                case RivalPhase.Searching:
                    pos = site + new Vector3(4f, 0, 3f);
                    return true;
                case RivalPhase.Carrying:
                    pos = Vector3.Lerp(site, altar, step.CarryCost > 0 ? Mathf.Clamp01(c.Rival.StepProgress / (float)step.CarryCost) : 1f);
                    carrying = c.Crystal(cd.Element).Custody == Custody.RivalTransit;
                    return true;
                default: return false;
            }
        }

        private void Update()
        {
            if (_boot == null || _boot.Sim == null) return;
            var walker = PlayerWalker.Instance;
            bool has = TryPosition(out var pos, out bool carrying, out var element);
            OnSurface = has;
            if (has) Position = pos;
            bool near = has && walker != null && _dir != null && _dir.PlayerSpace.Length == 0 && BoardSpace.Flat(walker.Position - pos) <= SeeDistance;
            if (near != Visible)
            {
                Visible = near;
                foreach (var f in _runners) f.gameObject.SetActive(near);
                _lastPos = pos;
            }
            if (!near) { if (_carried != null) _carried.SetActive(false); return; }
            var dir = pos - _lastPos; dir.y = 0;
            bool moving = dir.sqrMagnitude > 1e-6f;
            float yaw = moving ? BoardSpace.YawOf(dir) : _runners[0].transform.eulerAngles.y;
            _lastPos = pos;
            var fwd = BoardSpace.DirOf(yaw); var right = new Vector3(fwd.z, 0, -fwd.x);
            for (int i = 0; i < _runners.Count; i++)
            {
                var p = ExpeditionSpace.Ground(pos - fwd * (i * 1.7f) + right * ((i % 2 == 0 ? 0.5f : -0.5f)));
                _runners[i].Pose = moving ? FigurePose.Walk : FigurePose.Work;
                _runners[i].Place(p, yaw);
                _runners[i].Animate(Time.time * 1.6f);
            }
            if (carrying)
            {
                if (_carried == null || _carriedElement != element)
                {
                    if (_carried != null) Destroy(_carried);
                    _carried = ExpeditionSites.MakeCrystal(_runners[1].Body, "Seized crystal", ExpeditionSites.ElementColour(_boot.Sim.State.Campaign.Definition, element), 0.34f, false);
                    _carried.transform.localPosition = new Vector3(0, 2.0f, 0.1f);
                    _carriedElement = element;
                }
                _carried.SetActive(true);
            }
            else if (_carried != null) _carried.SetActive(false);
        }
    }
}
