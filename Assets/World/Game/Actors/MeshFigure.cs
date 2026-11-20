using System.Collections.Generic;
using UnityEngine;

namespace World.Game
{
    /// <summary>
    /// P1 polish: a jointed figure from merged-mesh parts (a torso with head and hat, two legs, two arms on pivots) for the
    /// cheap crowds that are not <see cref="PlaceholderActor"/>s: the board's collecting villagers and Historic's carriers,
    /// woodcutters and foresters. Driven by its owner, which passes the ground it covered each frame, so the
    /// <see cref="Gait"/> cycle keeps the feet planted. Not a MonoBehaviour (no Update of its own); five transforms per figure.
    /// Local space: feet at y = 0, front +Z, figure scale 1 (the root may be scaled; <see cref="Scale"/> converts distance).
    /// </summary>
    public sealed class MeshFigure
    {
        public enum Hold { Swing, Load, Work }

        public Transform Root, Body, LegL, LegR, ArmL, ArmR, KneeL, KneeR;
        public MeshFilter TorsoFilter, LegLFilter, LegRFilter, ArmLFilter, ArmRFilter, ShinLFilter, ShinRFilter;
        public float Phase, Speed, Moving;
        /// <summary>Hip height at figure scale 1 (the leg length used by the gait).</summary>
        public float LegLength = 0.8f;
        /// <summary>World units per figure unit (the root's scale).</summary>
        public float Scale = 1f;
        /// <summary>
        /// P3: the fastest gait shown (figure units per second; default no limit). Above it the legs keep this walking
        /// cadence and stride while the root covers more ground (Historic carriers keep the sim's trip timing but walk).
        /// </summary>
        public float MaxGaitSpeed = float.PositiveInfinity;
        /// <summary>Per-figure 0..1 (idle sway and work timing differ).</summary>
        public float Seed;
        /// <summary>Test probe: the angle between the legs this frame.</summary>
        public float LegSwing { get; private set; }
        /// <summary>P3 test probe: the larger knee bend this frame (degrees).</summary>
        public float KneeBend { get; private set; }

        /// <summary>Builds the five parts under a new root (meshes shared; parts on <paramref name="layer"/>).</summary>
        public static MeshFigure Build(string name, Transform parent, Material mat, Mesh torso, Mesh leg, Mesh arm,
            float hipY, float hipX, float shoulderY, float shoulderX, int layer)
        {
            var f = new MeshFigure { LegLength = hipY };
            var root = new GameObject(name); root.layer = layer;
            root.transform.SetParent(parent, false);
            f.Root = root.transform;
            f.Body = Part("Body", f.Root, Vector3.zero, layer, null, null, out _);
            f.TorsoFilter = PartMesh(f.Body, "Torso", torso, mat, layer);
            // P3: knees. The leg mesh is split at half the hip height into a thigh and a shin (on a knee pivot).
            Split(leg, hipY * 0.5f, out var thigh, out var shin);
            f.LegL = Part("LegL", f.Body, new Vector3(-hipX, hipY, 0), layer, thigh, mat, out f.LegLFilter);
            f.LegR = Part("LegR", f.Body, new Vector3(hipX, hipY, 0), layer, thigh, mat, out f.LegRFilter);
            f.KneeL = Part("KneeL", f.LegL, new Vector3(0, -hipY * 0.5f, 0), layer, shin, mat, out f.ShinLFilter);
            f.KneeR = Part("KneeR", f.LegR, new Vector3(0, -hipY * 0.5f, 0), layer, shin, mat, out f.ShinRFilter);
            f.ArmL = Part("ArmL", f.Body, new Vector3(-shoulderX, shoulderY, 0), layer, arm, mat, out f.ArmLFilter);
            f.ArmR = Part("ArmR", f.Body, new Vector3(shoulderX, shoulderY, 0), layer, arm, mat, out f.ArmRFilter);
            return f;
        }

        private static Transform Part(string name, Transform parent, Vector3 pos, int layer, Mesh m, Material mat, out MeshFilter mf)
        {
            var t = new GameObject(name).transform; t.gameObject.layer = layer;
            t.SetParent(parent, false); t.localPosition = pos;
            mf = m != null ? PartMesh(t, "Mesh", m, mat, layer) : null;
            return t;
        }

        private static MeshFilter PartMesh(Transform parent, string name, Mesh m, Material mat, int layer)
        {
            var go = new GameObject(name); go.layer = layer;
            go.transform.SetParent(parent, false);
            var mf = go.AddComponent<MeshFilter>(); mf.sharedMesh = m;
            go.AddComponent<MeshRenderer>().sharedMaterial = mat;
            return mf;
        }

        private static readonly Dictionary<Mesh, KeyValuePair<Mesh, Mesh>> _split = new Dictionary<Mesh, KeyValuePair<Mesh, Mesh>>();

        /// <summary>
        /// Splits a leg mesh (hanging down from y = 0) at y = -<paramref name="knee"/>: the thigh keeps every vertex above
        /// the knee (those below are pulled up to it), the shin every vertex below (those above pulled down), moved up so
        /// its pivot is the knee. Exact for the box legs used here; cached per source mesh.
        /// </summary>
        public static void Split(Mesh leg, float knee, out Mesh thigh, out Mesh shin)
        {
            if (_split.TryGetValue(leg, out var kv) && kv.Key != null && kv.Value != null) { thigh = kv.Key; shin = kv.Value; return; }
            var v = leg.vertices; var tv = new Vector3[v.Length]; var sv = new Vector3[v.Length];
            for (int i = 0; i < v.Length; i++)
            {
                tv[i] = new Vector3(v[i].x, Mathf.Max(v[i].y, -knee), v[i].z);
                sv[i] = new Vector3(v[i].x, Mathf.Min(v[i].y, -knee) + knee, v[i].z);
            }
            thigh = Object.Instantiate(leg); thigh.name = leg.name + " thigh"; thigh.vertices = tv; thigh.RecalculateBounds();
            shin = Object.Instantiate(leg); shin.name = leg.name + " shin"; shin.vertices = sv; shin.RecalculateBounds();
            _split[leg] = new KeyValuePair<Mesh, Mesh>(thigh, shin);
        }

        /// <summary>Swaps the meshes (pooled figures reused for another people or load).</summary>
        public void SetMeshes(Mesh torso, Mesh leg, Mesh arm)
        {
            Split(leg, LegLength * 0.5f, out var thigh, out var shin);
            TorsoFilter.sharedMesh = torso; LegLFilter.sharedMesh = thigh; LegRFilter.sharedMesh = thigh;
            ShinLFilter.sharedMesh = shin; ShinRFilter.sharedMesh = shin;
            ArmLFilter.sharedMesh = arm; ArmRFilter.sharedMesh = arm;
        }

        /// <summary>
        /// One frame: <paramref name="distance"/> is the ground covered (world units) since the last call. Load holds the
        /// arms up and forward round a carried load; Work swings both arms (chopping, digging, gathering) on the spot.
        /// </summary>
        public void Step(float dt, float distance, Hold hold, float clock)
        {
            if (dt <= 0f) return;
            float d = distance / Mathf.Max(0.001f, Scale);
            float sp = d / dt;
            if (d > 4f) { sp = 0f; d = 0f; } // a jump (pooled reuse, snap) is not a step
            if (sp > MaxGaitSpeed) { sp = MaxGaitSpeed; d = MaxGaitSpeed * dt; }
            Speed = Mathf.Lerp(Speed, sp, 1f - Mathf.Exp(-10f * dt));
            Moving = Mathf.MoveTowards(Moving, Mathf.Clamp01(Speed / 0.5f), dt * 5f);
            float gs = Mathf.Max(Speed, 0.8f);
            Phase = Gait.Advance(Phase, d, LegLength, gs);
            if (Moving < 0.01f) Phase = Mathf.Round(Phase / Mathf.PI) * Mathf.PI;
            var g = Gait.Evaluate(Phase, Moving, gs, LegLength);
            float armL = g.ArmL, armR = g.ArmR, armZ = 6f, pitch = g.Pitch, still = 1f - Moving;
            float t = clock + Seed * 23f;
            float sway = Mathf.Sin(t * 0.6f) * 2f * still;
            if (hold == Hold.Load) { armL = armR = -62f; armZ = -14f; pitch += 3f; }
            else if (hold == Hold.Work)
            {
                float k = Mathf.Sin(t * 7f);
                armL = armR = -70f + k * 55f; armZ = -8f; pitch += 16f + k * 8f;
            }
            Body.localPosition = new Vector3(0, g.Bob, 0);
            Body.localRotation = Quaternion.Euler(pitch, 0, g.Roll + sway);
            float back = -pitch, side = -(g.Roll + sway);
            LegL.localRotation = Quaternion.Euler(-g.LegL + back, 0, side);
            LegR.localRotation = Quaternion.Euler(-g.LegR + back, 0, side);
            ArmL.localRotation = Quaternion.Euler(armL, 0, -armZ);
            ArmR.localRotation = Quaternion.Euler(armR, 0, armZ);
            LegSwing = Mathf.Abs(g.LegL - g.LegR);
            // The knee bends while its leg swings forward (the foot clears the ground); a worker crouches a little.
            float c = Mathf.Cos(Phase), knee = Mathf.Lerp(36f, 72f, Gait.RunBlend(gs)) * Moving, crouch = hold == Hold.Work ? 14f : 3f;
            float kl = Mathf.Max(0f, c) * knee + crouch, kr = Mathf.Max(0f, -c) * knee + crouch;
            KneeL.localRotation = Quaternion.Euler(kl, 0, 0); KneeR.localRotation = Quaternion.Euler(kr, 0, 0);
            KneeBend = Mathf.Max(kl, kr);
        }
    }
}
