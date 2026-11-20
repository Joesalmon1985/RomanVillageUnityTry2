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

        public Transform Root, Body, LegL, LegR, ArmL, ArmR;
        public MeshFilter TorsoFilter, LegLFilter, LegRFilter, ArmLFilter, ArmRFilter;
        public float Phase, Speed, Moving;
        /// <summary>Hip height at figure scale 1 (the leg length used by the gait).</summary>
        public float LegLength = 0.8f;
        /// <summary>World units per figure unit (the root's scale).</summary>
        public float Scale = 1f;
        /// <summary>Per-figure 0..1 (idle sway and work timing differ).</summary>
        public float Seed;
        /// <summary>Test probe: the angle between the legs this frame.</summary>
        public float LegSwing { get; private set; }

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
            f.LegL = Part("LegL", f.Body, new Vector3(-hipX, hipY, 0), layer, leg, mat, out f.LegLFilter);
            f.LegR = Part("LegR", f.Body, new Vector3(hipX, hipY, 0), layer, leg, mat, out f.LegRFilter);
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

        /// <summary>Swaps the meshes (pooled figures reused for another people or load).</summary>
        public void SetMeshes(Mesh torso, Mesh leg, Mesh arm)
        {
            TorsoFilter.sharedMesh = torso; LegLFilter.sharedMesh = leg; LegRFilter.sharedMesh = leg;
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
        }
    }
}
