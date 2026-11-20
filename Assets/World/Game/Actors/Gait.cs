using UnityEngine;

namespace World.Game
{
    /// <summary>Limb angles of one frame of a gait (degrees about the pivots' X axes, metres for the body offsets).</summary>
    public struct GaitPose
    {
        public float LegL, LegR, ArmL, ArmR;
        /// <summary>Body height offset (the inverted-pendulum dip of a walk, the flight of a run).</summary>
        public float Bob;
        /// <summary>Forward lean of the body (degrees, positive = forward).</summary>
        public float Pitch;
        /// <summary>Side roll of the hips (degrees).</summary>
        public float Roll;
    }

    /// <summary>
    /// P1 polish: the shared, cheap procedural gait for every primitive figure (the placeholder actor, the board's
    /// collecting villagers, Historic carriers, soldiers). The phase advances by ground covered, not by time, so a
    /// planted foot stays put: a rigid leg of length L swinging +-A degrees covers one step of 2 L sin A while it is on the
    /// ground, which is half a cycle. The amplitude grows with speed (short steps when strolling, long ones when
    /// running); the body dips at double support like an inverted pendulum when walking and lifts in flight when running.
    /// No state of its own: callers keep a phase float.
    /// </summary>
    public static class Gait
    {
        /// <summary>Speeds (m/s, figure scale 1) where the run starts and is complete.</summary>
        public const float RunFrom = 3.6f, RunFull = 5.2f;

        /// <summary>Leg swing amplitude (degrees) for a speed (m/s at figure scale 1).</summary>
        public static float Amplitude(float speed)
        {
            float walk = Mathf.Lerp(16f, 30f, Mathf.Clamp01(speed / 3f));
            return Mathf.Lerp(walk, 44f, RunBlend(speed));
        }

        /// <summary>0 walking, 1 running.</summary>
        public static float RunBlend(float speed) => Mathf.Clamp01((speed - RunFrom) / (RunFull - RunFrom));

        /// <summary>Length of one step (m) for a leg of <paramref name="legLength"/> m at an amplitude.</summary>
        public static float StepLength(float legLength, float ampDeg) => 2f * legLength * Mathf.Sin(ampDeg * Mathf.Deg2Rad);

        /// <summary>Advances a phase (radians) by a distance walked: half a cycle per step, so feet do not slide.</summary>
        public static float Advance(float phase, float distance, float legLength, float speed)
        {
            float step = Mathf.Max(0.05f, StepLength(legLength, Amplitude(speed)));
            phase += Mathf.PI * distance / step;
            if (phase > 1000f) phase -= Mathf.PI * 2f * 150f;
            return phase;
        }

        /// <summary>
        /// The pose at a phase. <paramref name="moving"/> 0..1 fades the whole cycle in and out (0 = standing, legs
        /// together); <paramref name="speed"/> in m/s at figure scale 1.
        /// </summary>
        public static GaitPose Evaluate(float phase, float moving, float speed, float legLength)
        {
            float amp = Amplitude(speed) * moving, run = RunBlend(speed);
            float s = Mathf.Sin(phase);
            var p = new GaitPose();
            p.LegL = s * amp;
            p.LegR = -s * amp;
            // Arms swing against the legs; a runner pumps them harder and holds them a little forward.
            float armAmp = amp * Mathf.Lerp(0.75f, 1.25f, run);
            p.ArmL = -s * armAmp - 18f * run * moving;
            p.ArmR = s * armAmp - 18f * run * moving;
            // Walk: the hip rides over the planted leg (L cos theta), lowest at double support. Run: flight between steps.
            float theta = amp * s * Mathf.Deg2Rad;
            float walkBob = legLength * (Mathf.Cos(theta) - 1f) * 0.6f;
            float runBob = Mathf.Abs(Mathf.Sin(phase)) * 0.09f * moving - 0.05f * moving;
            p.Bob = Mathf.Lerp(walkBob, runBob, run);
            p.Pitch = (3f + 11f * run) * moving;
            p.Roll = Mathf.Sin(phase) * 2.5f * moving * (1f - run);
            return p;
        }

        /// <summary>A stable per-person number 0..1 from a string (idle phases, fidget timing).</summary>
        public static float Seed01(string id)
        {
            unchecked
            {
                uint h = 2166136261u;
                foreach (char c in id ?? "") { h ^= c; h *= 16777619u; }
                h ^= h >> 13; h *= 0x5bd1e995u; h ^= h >> 15;
                return (h & 0xFFFFFF) / (float)0x1000000;
            }
        }
    }
}
