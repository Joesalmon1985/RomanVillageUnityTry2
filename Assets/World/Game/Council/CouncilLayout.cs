using System.Collections.Generic;
using UnityEngine;
using World.Sim.Encounters.Council;

namespace World.Game
{
    /// <summary>
    /// Constants and small pure helpers of the council village view: the 16:9 safe area of the listening bench (the
    /// left view is still placed from safe-area fractions), hit-target maths, emblem colours and the lead tunics. The
    /// village itself (places, people, camera) is placed in world metres by <see cref="CouncilSite"/> and
    /// <see cref="CouncilScene"/>.
    /// </summary>
    public static class CouncilLayout
    {
        public const float Aspect = 16f / 9f;
        public const float FovV = 40f;
        /// <summary>Kept for the bench set (1 = normal size).</summary>
        public static float Scale = 1f;
        public static float TanV => Mathf.Tan(FovV * 0.5f * Mathf.Deg2Rad);
        public static float TanH => TanV * Aspect;

        /// <summary>The 16:9 safe rectangle inside a screen of w x h pixels (top-left origin).</summary>
        public static Rect SafeRect(float w, float h)
        {
            if (w / h >= Aspect) { float sw = h * Aspect; return new Rect((w - sw) * 0.5f, 0, sw, h); }
            float sh = w / Aspect; return new Rect(0, (h - sh) * 0.5f, w, sh);
        }

        /// <summary>Camera vertical field of view of the bench view, keeping the 16:9 safe area on screen.</summary>
        public static float CameraFov(float w, float h)
        {
            float a = w / h;
            return a >= Aspect ? FovV : 2f * Mathf.Atan(TanV * Aspect / a) * Mathf.Rad2Deg;
        }

        public static Vector2 ToSafe(Vector2 screenBL, float w, float h)
        {
            var r = SafeRect(w, h);
            return new Vector2((screenBL.x - r.x) / r.width, (h - screenBL.y - r.y) / r.height);
        }

        public static Rect ToPixels(Rect safe, float w, float h)
        {
            var r = SafeRect(w, h);
            return new Rect(r.x + safe.x * r.width, r.y + safe.y * r.height, safe.width * r.width, safe.height * r.height);
        }

        static Rect R(float x0, float y0, float x1, float y1) => new Rect(x0 / 100f, y0 / 100f, (x1 - x0) / 100f, (y1 - y0) / 100f);

        // left view (the listening bench)
        public static readonly Rect SlabSame = R(0, 60, 50, 92);
        public static readonly Rect SlabDifferent = R(50, 60, 100, 92);
        public static readonly Rect RightLantern = R(88, 24, 98, 44);

        public static List<KeyValuePair<string, Rect>> LeftTargets()
        {
            return new List<KeyValuePair<string, Rect>>
            {
                new KeyValuePair<string, Rect>("same", SlabSame),
                new KeyValuePair<string, Rect>("different", SlabDifferent),
            };
        }

        /// <summary>The tunic of a lead of a track (soft, so figures read at a distance).</summary>
        public static Color LeadTunic(int track)
        {
            var c = new[] {
                new Color(0.35f, 0.55f, 0.65f), new Color(0.62f, 0.50f, 0.35f), new Color(0.35f, 0.58f, 0.30f), new Color(0.68f, 0.42f, 0.25f),
                new Color(0.55f, 0.40f, 0.62f), new Color(0.72f, 0.62f, 0.30f), new Color(0.48f, 0.48f, 0.52f), new Color(0.75f, 0.68f, 0.52f),
                new Color(0.30f, 0.50f, 0.70f), new Color(0.58f, 0.34f, 0.30f), new Color(0.80f, 0.78f, 0.70f) };
            return c[(track - 2) % c.Length];
        }

        public static Color EmblemColor(string colour)
        {
            switch (colour)
            {
                case "green": return new Color(0.25f, 0.80f, 0.30f);
                case "red": return new Color(0.92f, 0.22f, 0.20f);
                case "blue": return new Color(0.28f, 0.50f, 0.98f);
                case "gold": return new Color(1.00f, 0.82f, 0.15f);
                default: return Color.white;
            }
        }

        public static readonly string[] EmblemColours = { "green", "red", "blue", "gold" };
        public static readonly string[] EmblemShapes = { "circle", "triangle", "square", "diamond" };
    }
}
