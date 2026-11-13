using System.Collections.Generic;
using UnityEngine;
using World.Sim;
using World.Sim.Demons;
using World.Sim.Factions;

namespace World.Game
{
    /// <summary>
    /// The "cast here" ground mark of an open proposal of your people (REFACTOR_SPEC 2.C.2, 8.4): a pulsing glowing
    /// ring with inward-pointing runes over the exact press area of <see cref="FactionApi.ProposalTarget"/> (a 6 m
    /// wide strip along the hex edge for roads), and, on the top proposal, a light pillar seen from afar. The top one
    /// is brighter. Exposed to the prince and spell packages through <see cref="LandViews.ProposalMarker"/>.
    /// </summary>
    public sealed class ProposalMarkerView : MonoBehaviour, IInspectable
    {
        public int ProposalId { get; private set; }
        public bool IsTop { get; private set; }
        public bool IsStrip { get; private set; }
        public float PressRadius { get; private set; }
        /// <summary>The press point (proposal target) on the ground.</summary>
        public Vector3 Target { get; private set; }

        /// <summary>Within this distance of the viewer the top proposal's beam is hidden.</summary>
        public const float PillarHideMetres = 22f;
        public bool PillarShown => _pillar != null && _pillar.activeSelf;
        private bool _near;
        private Renderer[] _glow;
        private GameObject _pillar, _runes;
        private MaterialPropertyBlock _mpb;

        public string Title => LandText.State != null ? FactionApi.ProposalTitle(LandText.State, ProposalId) : "Proposal";
        public IEnumerable<string> Lines
        {
            get
            {
                var s = LandText.State;
                var p = s != null ? FactionApi.GetProposal(s, ProposalId) : null;
                if (p == null) yield break;
                yield return p.Reason;
                yield return "Stand within " + FactionApi.CastRangeMetres(s).ToString("0") + " m and press the glowing ground to cast.";
            }
        }

        public static ProposalMarkerView Create(Transform parent, WorldState s, Proposal p)
        {
            FactionApi.ProposalTarget(s, p.Id, out double x, out double z, out double r);
            var go = new GameObject("Proposal " + p.Id + " " + p.Kind);
            go.transform.SetParent(parent, false);
            var v = go.AddComponent<ProposalMarkerView>();
            v.ProposalId = p.Id;
            v.Target = LandLook.Ground(x, z);
            v.PressRadius = (float)r;
            v._mpb = new MaterialPropertyBlock();
            go.transform.position = v.Target;
            var t = s.Board.Topology;
            bool strip = p.Kind == ProposalKind.Road || (p.Kind == ProposalKind.Banish && p.DemonSiteKind == DemonSiteKind.HexEdge);
            v.IsStrip = strip;
            var col = p.Kind == ProposalKind.Banish ? new Color(0.55f, 0.85f, 1f) : new Color(1f, 0.82f, 0.35f);
            string ck = p.Kind == ProposalKind.Banish ? "b" : "g";
            GameObject ring;
            if (strip)
            {
                int he = p.Kind == ProposalKind.Road ? p.HexEdge : p.DemonSiteId;
                BoardSpace.HexEdgeEnds(t, he, out var a, out var b);
                go.transform.rotation = Quaternion.LookRotation((b - a).normalized, Vector3.up);
                float half = (b - a).magnitude / 2f - 3f, w = (float)FactionApi.RoadStripHalfWidth;
                ring = LandLook.Part(go.transform, "Strip", LandLook.Cached("prop/strip/" + ck + "/" + Mathf.RoundToInt(half), mb =>
                {
                    var c = col;
                    foreach (float sx in new[] { -w, w })
                        mb.Strip(new Vector3(sx, 0.12f, -half), new Vector3(sx, 0.12f, half), 0.55f, c);
                    mb.Strip(new Vector3(-w, 0.12f, -half), new Vector3(w, 0.12f, -half), 0.55f, c);
                    mb.Strip(new Vector3(-w, 0.12f, half), new Vector3(w, 0.12f, half), 0.55f, c);
                    for (float zz = -half + 3f; zz < half - 1f; zz += 6f) Chevron(mb, new Vector3(0, 0.13f, zz), 1.1f, c * 0.8f);
                }), LandLook.Additive, false);
            }
            else
            {
                float rr = (float)r;
                ring = LandLook.Part(go.transform, "Ring", LandLook.Cached("prop/ring/" + ck + "/" + rr, mb =>
                {
                    LandShapes.Ring(mb, new Vector3(0, 0.12f, 0), rr, 0.45f, 40, col);
                    LandShapes.Ring(mb, new Vector3(0, 0.12f, 0), rr * 0.55f, 0.25f, 32, col * 0.7f);
                    LandShapes.Disc(mb, new Vector3(0, 0.11f, 0), rr * 0.95f, 32, col * 0.12f);
                }), LandLook.Additive, false);
                v._runes = LandLook.Part(go.transform, "Runes", LandLook.Cached("prop/runes/" + ck + "/" + rr, mb =>
                {
                    for (int i = 0; i < 8; i++)
                    {
                        var q = Quaternion.Euler(0, i * 45f, 0);
                        var p0 = q * new Vector3(0, 0.14f, rr * 0.92f);
                        // Arrowhead pointing in.
                        mb.Tri(p0 + q * new Vector3(-0.5f, 0, 0), p0 + q * new Vector3(0, 0, -0.9f), p0 + q * new Vector3(0.5f, 0, 0), col);
                        mb.Tri(p0 + q * new Vector3(0.5f, 0, 0), p0 + q * new Vector3(0, 0, -0.9f), p0 + q * new Vector3(-0.5f, 0, 0), col);
                    }
                }), LandLook.Additive, false);
            }
            v._pillar = LandLook.Part(go.transform, "Pillar", LandLook.Cached("prop/pillar2/" + ck, mb =>
            {
                // A slim, soft beam: seen from afar, never hiding the plot or the people on it.
                LandShapes.FadeBeam(mb, Vector3.zero, 0.22f, 0.10f, 26f, 8, col * 0.10f);
                LandShapes.FadeBeam(mb, Vector3.zero, 0.07f, 0.03f, 34f, 6, col * 0.16f);
            }), LandLook.Additive, false);
            var rends = new List<Renderer> { ring.GetComponent<Renderer>(), v._pillar.GetComponent<Renderer>() };
            if (v._runes != null) rends.Add(v._runes.GetComponent<Renderer>());
            v._glow = rends.ToArray();
            // A thin trigger on the ground (5 cm high), so a hit on it is a hit on the ground of the press area.
            LandLook.PickBox(go, Vector3.up * 0.05f, strip ? new Vector3(6f, 0.1f, 50f) : new Vector3((float)r * 2, 0.1f, (float)r * 2));
            return v;
        }

        private static void Chevron(MeshBuilder mb, Vector3 c, float s, Color col)
        {
            mb.Tri(c + new Vector3(-s, 0, -s * 0.5f), c + new Vector3(0, 0, s * 0.5f), c + new Vector3(0, 0, s * 0.1f), col);
            mb.Tri(c + new Vector3(0, 0, s * 0.1f), c + new Vector3(0, 0, s * 0.5f), c + new Vector3(s, 0, -s * 0.5f), col);
        }

        public void SetTop(bool top)
        {
            IsTop = top;
            _pillar.SetActive(top && !_near);
        }

        public void Animate(float now)
        {
            // The beam is a beacon for finding the spot from afar; close by, the ring alone marks the ground.
            bool near = BoardSpace.Flat(LandViews.Eye - Target) < PillarHideMetres;
            if (near != _near) { _near = near; _pillar.SetActive(IsTop && !_near); }
            float pulse = 0.5f + 0.5f * Mathf.Sin(now * 3.2f + ProposalId);
            float k = IsTop ? 0.55f + 0.35f * pulse : 0.25f + 0.2f * pulse;
            _mpb.SetColor("_Color", new Color(k, k, k, 1f));
            foreach (var r in _glow) r.SetPropertyBlock(_mpb);
            if (_runes != null) _runes.transform.localRotation = Quaternion.Euler(0, now * 20f, 0);
            if (_runes != null) _runes.transform.localScale = Vector3.one * (1f - 0.06f * pulse);
        }
    }
}
