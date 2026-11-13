using System;
using UnityEngine;
using World.Sim;
using World.Sim.Factions;

namespace World.Game
{
    /// <summary>
    /// Ground press to <c>CastSpell</c> (REFACTOR_SPEC 2.C.2, appendix B): finds the open proposal of the player's people
    /// whose press area holds the point (<c>FactionApi.ProposalAt</c>), checks the player stands within
    /// <c>FactionApi.CastRangeMetres</c> of its target point, and submits the command. A cast plays
    /// <see cref="SpellEffect"/> at the target; a refusal is said in a bubble at the pressed point ("Walk closer to
    /// cast.", or the sim's sentence). Pressing ground without a proposal does nothing. Owner: package P.
    /// </summary>
    public static class SpellCaster
    {
        public const string NoProposal = "no_proposal", TooFar = "too_far";
        public const string TooFarSentence = "Walk closer to cast.";

        /// <summary>Raised on every attempt: proposal id (-1 if none) and the result.</summary>
        public static event Action<int, CommandResult> CastAttempted;

        /// <summary>Last attempt (tests).</summary>
        public static CommandResult LastResult { get; private set; }
        public static int LastProposal { get; private set; } = -1;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { CastAttempted = null; LastResult = default; LastProposal = -1; }

        private static WorldSim Sim => WorldBootstrap.Instance != null ? WorldBootstrap.Instance.Sim : null;

        /// <summary>Distance (m, on the ground) from the player to a proposal's target point; infinity if gone.</summary>
        public static float DistanceTo(int proposalId)
        {
            var sim = Sim; var w = PlayerWalker.Instance;
            if (sim == null || w == null || !FactionApi.ProposalTarget(sim.State, proposalId, out double x, out double z, out _)) return float.PositiveInfinity;
            return BoardSpace.Flat(new Vector3((float)x, 0, (float)z) - w.Position);
        }

        /// <summary>True if the player stands within cast range of the proposal's target point.</summary>
        public static bool InRange(int proposalId)
        {
            var sim = Sim;
            return sim != null && DistanceTo(proposalId) <= FactionApi.CastRangeMetres(sim.State) + 1e-3f;
        }

        public static CommandResult TryCastAt(Vector3 ground)
        {
            var sim = Sim;
            if (sim == null) return Report(-1, CommandResult.Reject(NoProposal, "There is no world."), ground, false);
            int pid = FactionApi.ProposalAt(sim.State, PlayerIds.Human, ground.x, ground.z);
            if (pid < 0) return Report(-1, CommandResult.Reject(NoProposal, "Nothing has been proposed here."), ground, false);
            return TryCast(pid, ground);
        }

        /// <summary>Casts a proposal of the player's people from where the player stands (range checked).</summary>
        public static CommandResult TryCast(int proposalId, Vector3 pressedAt)
        {
            var sim = Sim;
            if (sim == null) return Report(proposalId, CommandResult.Reject(NoProposal, "There is no world."), pressedAt, true);
            if (!InRange(proposalId))
                return Report(proposalId, CommandResult.Reject(TooFar, TooFarSentence), pressedAt, true);
            FactionApi.ProposalTarget(sim.State, proposalId, out double x, out double z, out _);
            var r = sim.Submit(new CastSpell { PlayerId = PlayerIds.Human, ProposalId = proposalId });
            if (r.Ok) SpellEffect.Play(new Vector3((float)x, PlayerWalker.GroundHeight(new Vector3((float)x, 0, (float)z)), (float)z), true);
            return Report(proposalId, r, pressedAt, true);
        }

        /// <summary>F1 tools: cast any proposal (either people) from anywhere, without the range check.</summary>
        public static CommandResult CastAnywhere(int proposalId)
        {
            var sim = Sim;
            var p = sim != null ? FactionApi.GetProposal(sim.State, proposalId) : null;
            if (p == null) return CommandResult.Reject("unknown_proposal", "That proposal is gone.");
            FactionApi.ProposalTarget(sim.State, proposalId, out double x, out double z, out _);
            var r = sim.Submit(new CastSpell { PlayerId = p.Faction, ProposalId = proposalId });
            if (r.Ok) SpellEffect.Play(new Vector3((float)x, PlayerWalker.GroundHeight(new Vector3((float)x, 0, (float)z)), (float)z), true);
            LastResult = r; LastProposal = proposalId;
            CastAttempted?.Invoke(proposalId, r);
            return r;
        }

        private static CommandResult Report(int pid, CommandResult r, Vector3 at, bool say)
        {
            LastResult = r; LastProposal = pid;
            if (!r.Ok && say && !string.IsNullOrEmpty(r.Message)) SpeechBubbles.SayAt(at + Vector3.up * 1.6f, r.Message, 3.5f);
            CastAttempted?.Invoke(pid, r);
            return r;
        }
    }

    /// <summary>
    /// The visible spell: a pillar of light falling onto the target, a widening ring on the ground and sparks rising,
    /// about 1.6 s, made of primitives with the additive glow material. Original visuals only.
    /// </summary>
    public sealed class SpellEffect : MonoBehaviour
    {
        public const float Duration = 1.8f;
        private static readonly Color Gold = new Color(1f, 0.82f, 0.35f, 1f);
        private static readonly Color Blue = new Color(0.55f, 0.85f, 1f, 1f);
        private float _t;
        private Transform _pillar, _ring, _core;
        private Transform[] _sparks;
        private Vector3[] _sparkVel;
        private Material _pillarMat, _ringMat, _sparkMat, _coreMat;
        private Light _light;

        /// <summary>Effects currently playing (tests).</summary>
        public static int Active { get; private set; }
        /// <summary>Total effects started since boot (tests).</summary>
        public static int Played { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { Active = 0; Played = 0; }

        public static SpellEffect Play(Vector3 ground, bool success)
        {
            var go = new GameObject("SpellEffect");
            if (WorldBootstrap.Instance != null) go.transform.SetParent(WorldBootstrap.Instance.transform, false);
            go.transform.position = ground;
            go.layer = 2;
            var e = go.AddComponent<SpellEffect>();
            e.Build(success ? Gold : Blue);
            Active++; Played++;
            return e;
        }

        private void Build(Color c)
        {
            var mb = new MeshBuilder();
            mb.Cylinder(Vector3.zero, 1f, 0.6f, 1f, 20, Color.white, false);
            _pillarMat = MaterialFactory.CreateGlow(c, "SpellPillar");
            _pillar = mb.ToObject("Pillar", transform, _pillarMat).transform;

            var core = new MeshBuilder();
            core.Cylinder(Vector3.zero, 0.35f, 0.35f, 1f, 12, Color.white, false);
            _coreMat = MaterialFactory.CreateGlow(Color.white, "SpellCore");
            _core = core.ToObject("Core", transform, _coreMat).transform;

            var rb = new MeshBuilder();
            const int N = 48;
            for (int i = 0; i < N; i++)
            {
                float a0 = i * Mathf.PI * 2 / N, a1 = (i + 1) * Mathf.PI * 2 / N;
                var o0 = new Vector3(Mathf.Sin(a0), 0, Mathf.Cos(a0)); var o1 = new Vector3(Mathf.Sin(a1), 0, Mathf.Cos(a1));
                rb.Quad(o0 * 0.8f, o0, o1, o1 * 0.8f, Color.white);
                rb.Quad(o0 * 0.8f, o1 * 0.8f, o1, o0, Color.white);
            }
            _ringMat = MaterialFactory.CreateGlow(c, "SpellRing");
            _ring = rb.ToObject("Ring", transform, _ringMat).transform;
            _ring.localPosition = Vector3.up * 0.1f;

            _sparkMat = MaterialFactory.CreateGlow(c, "SpellSpark");
            var sb = new MeshBuilder();
            sb.Box(Vector3.zero, Vector3.one * 0.25f, Color.white, 45f);
            var sparkMesh = sb.ToMesh("Spark");
            _sparks = new Transform[14];
            _sparkVel = new Vector3[_sparks.Length];
            var rnd = new PropRandom(Played * 7919 + 13);
            for (int i = 0; i < _sparks.Length; i++)
            {
                var g = new GameObject("Spark");
                g.transform.SetParent(transform, false);
                g.AddComponent<MeshFilter>().sharedMesh = sparkMesh;
                g.AddComponent<MeshRenderer>().sharedMaterial = _sparkMat;
                float a = rnd.Range(0, Mathf.PI * 2), r = rnd.Range(0.5f, 2.5f);
                g.transform.localPosition = new Vector3(Mathf.Sin(a) * r, 0.2f, Mathf.Cos(a) * r);
                _sparkVel[i] = new Vector3(Mathf.Sin(a) * rnd.Range(0.3f, 1.2f), rnd.Range(2.5f, 6f), Mathf.Cos(a) * rnd.Range(0.3f, 1.2f));
                _sparks[i] = g.transform;
            }
            var lg = new GameObject("Light");
            lg.transform.SetParent(transform, false);
            lg.transform.localPosition = Vector3.up * 3f;
            _light = lg.AddComponent<Light>();
            _light.type = LightType.Point; _light.color = c; _light.range = 18f; _light.intensity = 0f;
            Animate(0f);
        }

        private void Update()
        {
            _t += Time.deltaTime;
            Animate(_t / Duration);
            if (_t >= Duration) Destroy(gameObject);
        }

        private void OnDestroy() { Active = Mathf.Max(0, Active - 1); }

        private void Animate(float u)
        {
            u = Mathf.Clamp01(u);
            // Pillar: drops from the sky in the first 25%, then thins and fades.
            float drop = Mathf.Clamp01(u / 0.25f);
            float height = 40f;
            float fade = 1f - Mathf.Clamp01((u - 0.35f) / 0.65f);
            float width = Mathf.Lerp(2.6f, 0.4f, Mathf.Clamp01((u - 0.2f) / 0.8f));
            _pillar.localScale = new Vector3(width, height * drop, width);
            _pillar.localPosition = Vector3.up * (height * (1f - drop));
            SetAlpha(_pillarMat, 0.55f * fade);
            _core.localScale = new Vector3(width * 0.5f, height * drop, width * 0.5f);
            _core.localPosition = _pillar.localPosition;
            SetAlpha(_coreMat, 0.8f * fade);
            // Ring: widens over the whole effect.
            float rr = Mathf.Lerp(0.5f, 9f, Mathf.Sqrt(u));
            _ring.localScale = new Vector3(rr, 1f, rr);
            SetAlpha(_ringMat, 0.9f * (1f - u));
            for (int i = 0; i < _sparks.Length; i++)
            {
                var v = _sparkVel[i];
                _sparks[i].localPosition += v * Time.deltaTime * (u > 0.15f ? 1f : 0f);
                _sparks[i].Rotate(0, 360f * Time.deltaTime, 0);
            }
            SetAlpha(_sparkMat, fade);
            _light.intensity = 6f * Mathf.Sin(Mathf.Clamp01(u * 1.4f) * Mathf.PI);
        }

        private static void SetAlpha(Material m, float a) { var c = m.color; c.a = a; m.color = c; }
    }
}
