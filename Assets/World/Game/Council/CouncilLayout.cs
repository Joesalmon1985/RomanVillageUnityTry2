using System.Collections.Generic;
using UnityEngine;
using World.Sim.Encounters.Council;

namespace World.Game
{
    /// <summary>
    /// The council stage geometry, pure and testable. Everything is placed from the brief's normalised safe-area
    /// coordinates (16:9, origin top-left, y down): a screen point is turned into a ray of a virtual forward camera and
    /// intersected with the floor, so a figure's feet and head land exactly in the band the brief gives. The real camera
    /// uses the same virtual projection, so the layout is identical at every aspect ratio (the 16:9 safe area is fitted
    /// and centred in the screen). All positions are in the stage's local frame: x right, z forward (away from the
    /// camera), the camera at (0, CamHeight, 0).
    /// </summary>
    public static class CouncilLayout
    {
        public const float Aspect = 16f / 9f;
        public const float FovV = 40f;
        public const float PitchDeg = 34f;
        /// <summary>Overall size of the stage (1 = camera 7.5 m up). Chosen smaller when the camp has little room.</summary>
        public static float Scale = 1f;
        public static float CamHeight => 7.5f * Scale;
        public static float TanV => Mathf.Tan(FovV * 0.5f * Mathf.Deg2Rad);
        public static float TanH => TanV * Aspect;
        public static Vector3 CamPos => new Vector3(0, CamHeight, 0);
        public static Quaternion CamRot => Quaternion.Euler(PitchDeg, 0, 0);

        // ---- safe area ----

        /// <summary>The 16:9 safe rectangle inside a screen of w x h pixels (top-left origin).</summary>
        public static Rect SafeRect(float w, float h)
        {
            if (w / h >= Aspect) { float sw = h * Aspect; return new Rect((w - sw) * 0.5f, 0, sw, h); }
            float sh = w / Aspect; return new Rect(0, (h - sh) * 0.5f, w, sh);
        }

        /// <summary>Camera vertical field of view that keeps the whole safe area (horizontal extent) on screen.</summary>
        public static float CameraFov(float w, float h)
        {
            float a = w / h;
            return a >= Aspect ? FovV : 2f * Mathf.Atan(TanV * Aspect / a) * Mathf.Rad2Deg;
        }

        /// <summary>Safe-area fractions (u right, v down) of a screen point in pixels (origin bottom-left, Unity).</summary>
        public static Vector2 ToSafe(Vector2 screenBL, float w, float h)
        {
            var r = SafeRect(w, h);
            return new Vector2((screenBL.x - r.x) / r.width, (h - screenBL.y - r.y) / r.height);
        }

        /// <summary>Pixel rect (top-left origin, IMGUI style) of a normalised safe rectangle.</summary>
        public static Rect ToPixels(Rect safe, float w, float h)
        {
            var r = SafeRect(w, h);
            return new Rect(r.x + safe.x * r.width, r.y + safe.y * r.height, safe.width * r.width, safe.height * r.height);
        }

        // ---- the hit targets (normalised safe-area rectangles, x/y/width/height as fractions) ----

        static Rect R(float x0, float y0, float x1, float y1) => new Rect(x0 / 100f, y0 / 100f, (x1 - x0) / 100f, (y1 - y0) / 100f);

        public static Rect Bay(int seat) => R(6 + 22 * seat, 0, 28 + 22 * seat, 10);
        public static Rect LeadBay(int track) => R(6 + 8 * (track - 2), 10, 14 + 8 * (track - 2), 22);
        public static Rect Tray(int i) => R(24 + 18 * i, 62, 42 + 18 * i, 88);
        /// <summary>One socket of a tray: side by side, 0 = left.</summary>
        public static Rect Socket(int i, int s) => R(25 + 18 * i + 8 * s, 66, 33 + 18 * i + 8 * s, 84);
        public static readonly Rect Hand = R(10, 62, 24, 84);
        public static readonly Rect Candidate = R(0, 58, 10, 92);
        public static readonly Rect Seal = R(80, 62, 89, 88);
        public static readonly Rect Petition = R(90, 62, 98, 88);
        public static readonly Rect Lantern = R(1, 92, 11, 100);
        public static readonly Rect Table = R(24, 58, 78, 92);
        public static readonly Rect Rows = R(6, 22, 94, 58);
        // left view
        public static readonly Rect SlabSame = R(0, 60, 50, 92);
        public static readonly Rect SlabDifferent = R(50, 60, 100, 92);
        public static readonly Rect Aide = R(0, 14, 25, 60);
        public static readonly Rect Speaker = R(25, 14, 75, 60);
        public static readonly Rect NextSpeaker = R(75, 14, 100, 60);
        public static readonly Rect SpeakerBubble = R(25, 14, 75, 26);
        public static readonly Rect RightLantern = R(88, 24, 98, 44);

        /// <summary>Every tappable target of the forward view, by name (for tests and hit testing).</summary>
        public static List<KeyValuePair<string, Rect>> ForwardTargets()
        {
            var l = new List<KeyValuePair<string, Rect>>();
            for (int i = 0; i < 3; i++) { l.Add(new KeyValuePair<string, Rect>("socket " + i + "a", Socket(i, 0))); l.Add(new KeyValuePair<string, Rect>("socket " + i + "b", Socket(i, 1))); }
            l.Add(new KeyValuePair<string, Rect>("hand", Hand));
            l.Add(new KeyValuePair<string, Rect>("seal", Seal));
            l.Add(new KeyValuePair<string, Rect>("petition", Petition));
            l.Add(new KeyValuePair<string, Rect>("lantern", Lantern));
            return l;
        }

        public static List<KeyValuePair<string, Rect>> LeftTargets()
        {
            return new List<KeyValuePair<string, Rect>>
            {
                new KeyValuePair<string, Rect>("same", SlabSame),
                new KeyValuePair<string, Rect>("different", SlabDifferent),
            };
        }

        // ---- forward projection ----

        public static Vector3 Ray(float u, float v)
        {
            var d = new Vector3((u - 0.5f) * 2f * TanH, (0.5f - v) * 2f * TanV, 1f);
            return CamRot * d.normalized;
        }

        /// <summary>Point on the plane y = height seen at safe-area (u, v). Null when the ray does not come down to it.</summary>
        public static Vector3 OnFloor(float u, float v, float height = 0f)
        {
            var d = Ray(u, v);
            if (d.y >= -1e-4f) return new Vector3((u - 0.5f) * 40f, height, 80f);
            float t = (height - CamHeight) / d.y;
            return CamPos + d * t;
        }

        public static Vector2 Project(Vector3 p)
        {
            var pc = Quaternion.Inverse(CamRot) * (p - CamPos);
            if (pc.z < 0.01f) return new Vector2(-9, -9);
            return new Vector2(0.5f + pc.x / pc.z / (2f * TanH), 0.5f - pc.y / pc.z / (2f * TanV));
        }

        /// <summary>The height of an upright figure standing at feet whose top projects at safe-area v = vTop.</summary>
        public static float HeightFor(Vector3 feet, float vTop)
        {
            float lo = 0.01f, hi = 40f;
            for (int i = 0; i < 40; i++)
            {
                float mid = (lo + hi) * 0.5f;
                if (Project(feet + Vector3.up * mid).y > vTop) lo = mid; else hi = mid;
            }
            return (lo + hi) * 0.5f;
        }

        /// <summary>A point seen at (u, v) lying at the given distance along the camera's view axis (billboard plane).</summary>
        public static Vector3 OnPlane(float u, float v, float depth)
        {
            var q = CamRot;
            var local = new Vector3((u - 0.5f) * 2f * TanH * depth, (0.5f - v) * 2f * TanV * depth, depth);
            return CamPos + q * local;
        }

        public static float DepthOf(Vector3 p) => (Quaternion.Inverse(CamRot) * (p - CamPos)).z;

        // ---- places on the tracks ----

        /// <summary>Row place centre in safe coordinates. Place 1 is nearest the table (lowest), the last place by the lead.</summary>
        public static Vector2 PlaceUV(int track, int place)
        {
            int len = CouncilRules.Length(track);
            float u = (6f + 8f * (track - 2) + 4f) / 100f;
            float v = (22f + (len - place + 0.5f) * 2.5f) / 100f;
            return new Vector2(u, v);
        }

        public static Vector3 PlacePos(int track, int place)
        {
            var uv = PlaceUV(track, place);
            return OnFloor(uv.x, uv.y);
        }

        /// <summary>The standing spot of a lead: feet at the foot of the lead band.</summary>
        public static Vector3 LeadFeet(int track) => OnFloor((6f + 8f * (track - 2) + 4f) / 100f, 0.22f);

        /// <summary>Ground cover needed (local x, z) for the stage; used to look for a clear site.</summary>
        public static List<Vector3> FootprintSamples(float step)
        {
            var l = new List<Vector3>();
            float[] vs = { 0.0f, 0.2f, 0.4f, 0.6f, 0.8f, 1.0f };
            foreach (var v in vs)
                for (float u = 0.0f; u <= 1.0001f; u += 0.25f)
                    l.Add(OnFloor(u, Mathf.Min(v, 0.999f)));
            return l;
        }

        /// <summary>The track colour tint a lead stands in (soft, so props read).</summary>
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
