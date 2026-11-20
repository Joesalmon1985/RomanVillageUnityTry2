using UnityEngine;

namespace World.Game
{
    /// <summary>
    /// WU-5: the world objects the rhythm game reads through, built from original primitives while a dance runs (the
    /// encounter makes and destroys them; <see cref="DanceCircle"/> itself is not touched): a performer behind each drum
    /// whose pose follows the cue's family, four block torches, the tuning bell and the log seat marker. Also the
    /// screen-space hit tests (drum shapes, Sol, seat, bell, torches), each at least 7 % of the screen height wide.
    /// Presentation only: it holds no rules and sends no command.
    /// </summary>
    public sealed class DanceStageProps : MonoBehaviour
    {
        public enum Pose { Idle, Kick, Snare, Bass }
        public enum TorchState { Unlit, Current, Kept, Missed }
        public enum Target { None, Sol, Seat, Bell, Torch }

        public const float MinHitFraction = 0.07f;
        public const int TorchCount = 4;

        public DanceCircle Circle { get; private set; }
        public Vector3 SeatPoint { get; private set; }
        public Vector3 BellPoint { get; private set; }
        public Vector3 SolPoint { get; set; }
        public float BellSwing { get; private set; }
        public int BellRings { get; private set; }
        public int Strikes { get; private set; }

        private sealed class Performer
        {
            public Transform Root, Body, Head, ArmR, ArmL, LegR, LegL, Stick;
            public Pose Pose, Shown = Pose.Idle;
            public float Blend, Dip;
            public Vector3 Home;
        }

        private readonly Performer[] _perf = new Performer[5];
        private readonly Transform[] _torch = new Transform[TorchCount];
        private readonly Material[] _flameMat = new Material[TorchCount];
        private readonly Transform[] _flame = new Transform[TorchCount];
        private readonly TorchState[] _torchState = new TorchState[TorchCount];
        private readonly Vector3[] _torchPoint = new Vector3[TorchCount];
        private Transform _bell;
        private float _bellSwingV;

        public static DanceStageProps Build(Transform parent, DanceCircle circle)
        {
            var go = new GameObject("DanceStageProps");
            go.transform.SetParent(parent, false);
            var p = go.AddComponent<DanceStageProps>();
            p.Circle = circle;
            p.BuildAll();
            return p;
        }

        // ------------------------------------------------------------------ build

        private static GameObject Prim(PrimitiveType t, string name, Transform parent, Color c, Vector3 localPos, Vector3 scale)
        {
            var g = GameObject.CreatePrimitive(t);
            g.name = name;
            var col = g.GetComponent<Collider>(); if (col != null) Destroy(col);
            g.transform.SetParent(parent, false);
            g.transform.localPosition = localPos;
            g.transform.localScale = scale;
            g.GetComponent<Renderer>().sharedMaterial = MaterialFactory.CreateUnlit(Color.Lerp(c, Color.white, 0.12f) * 1.15f, name);
            return g;
        }

        private void BuildAll()
        {
            var lay = Circle.Layout;
            // Seat marker: the log seat at the left of the far arc, where a dancer rests; a worn hide and a small totem mark it.
            SeatPoint = lay.CirclePoint(-56f, CampLayout.SeatRadius + 0.05f) + Vector3.up * 0.6f;
            var seat = new GameObject("Log seat marker").transform;
            seat.SetParent(transform, false);
            seat.position = lay.CirclePoint(-56f, CampLayout.SeatRadius + 0.05f);
            Prim(PrimitiveType.Cube, "Hide", seat, new Color(0.62f, 0.45f, 0.30f), new Vector3(0, 0.42f, 0), new Vector3(1.1f, 0.06f, 0.5f));
            Prim(PrimitiveType.Cylinder, "Totem", seat, new Color(0.45f, 0.33f, 0.22f), new Vector3(0, 0.95f, 0.45f), new Vector3(0.12f, 0.5f, 0.12f));
            Prim(PrimitiveType.Sphere, "Totem head", seat, new Color(0.80f, 0.68f, 0.45f), new Vector3(0, 1.55f, 0.45f), new Vector3(0.3f, 0.3f, 0.3f));

            SolPoint = lay.DrummerSpot + Vector3.up * 1.4f;

            // Tuning bell on a post, beside Sol's big drum.
            var bellRoot = new GameObject("Tuning bell").transform;
            bellRoot.SetParent(transform, false);
            bellRoot.position = lay.CirclePoint(34f, 5.9f);
            BellPoint = bellRoot.position + Vector3.up * 1.5f;
            Prim(PrimitiveType.Cylinder, "Post", bellRoot, new Color(0.40f, 0.29f, 0.19f), new Vector3(0, 1.0f, 0), new Vector3(0.14f, 1.0f, 0.14f));
            Prim(PrimitiveType.Cube, "Beam", bellRoot, new Color(0.40f, 0.29f, 0.19f), new Vector3(0, 1.95f, 0), new Vector3(0.9f, 0.1f, 0.1f));
            _bell = new GameObject("Bell swing").transform;
            _bell.SetParent(bellRoot, false);
            _bell.localPosition = new Vector3(0, 1.9f, 0);
            Prim(PrimitiveType.Sphere, "Bell", _bell, new Color(0.80f, 0.58f, 0.22f), new Vector3(0, -0.28f, 0), new Vector3(0.45f, 0.5f, 0.45f));
            Prim(PrimitiveType.Sphere, "Clapper", _bell, new Color(0.35f, 0.25f, 0.15f), new Vector3(0, -0.55f, 0), new Vector3(0.12f, 0.12f, 0.12f));

            // Four block torches along the far edge of the stage.
            float[] ang = { -38f, -26f, -14f, -2f };
            for (int i = 0; i < TorchCount; i++)
            {
                var t = new GameObject("Block torch " + (i + 1)).transform;
                t.SetParent(transform, false);
                t.position = lay.CirclePoint(ang[i], 7.9f);
                Prim(PrimitiveType.Cylinder, "Pole", t, new Color(0.38f, 0.27f, 0.18f), new Vector3(0, 0.95f, 0), new Vector3(0.12f, 0.95f, 0.12f));
                Prim(PrimitiveType.Cylinder, "Bowl", t, new Color(0.25f, 0.19f, 0.14f), new Vector3(0, 1.92f, 0), new Vector3(0.34f, 0.08f, 0.34f));
                var fl = Prim(PrimitiveType.Sphere, "Flame", t, Color.white, new Vector3(0, 2.2f, 0), new Vector3(0.3f, 0.5f, 0.3f));
                _flameMat[i] = MaterialFactory.CreateUnlit(new Color(1f, 0.6f, 0.2f), "Torch flame " + (i + 1));
                fl.GetComponent<Renderer>().sharedMaterial = _flameMat[i];
                fl.SetActive(false);
                _flame[i] = fl.transform;
                _torch[i] = t;
                _torchPoint[i] = t.position + Vector3.up * 1.7f;
            }

            // A performer behind each drum, towards the fire, facing the dancer.
            for (int p = 1; p <= 4; p++)
            {
                var pf = new Performer();
                var drum = lay.DrumPosition(p);
                var towardFire = (lay.DanceCircleCentre - drum); towardFire.y = 0; towardFire.Normalize();
                pf.Home = drum + towardFire * 1.15f;
                pf.Root = new GameObject("Performer " + p).transform;
                pf.Root.SetParent(transform, false);
                pf.Root.position = pf.Home;
                pf.Root.rotation = Quaternion.LookRotation(-towardFire, Vector3.up);
                var skin = new Color(0.72f, 0.52f, 0.38f);
                var cloth = p % 2 == 0 ? new Color(0.62f, 0.34f, 0.20f) : new Color(0.28f, 0.42f, 0.50f);
                pf.LegR = new GameObject("LegR").transform; pf.LegR.SetParent(pf.Root, false); pf.LegR.localPosition = new Vector3(0.11f, 0.85f, 0);
                pf.LegL = new GameObject("LegL").transform; pf.LegL.SetParent(pf.Root, false); pf.LegL.localPosition = new Vector3(-0.11f, 0.85f, 0);
                Prim(PrimitiveType.Capsule, "Leg", pf.LegR, cloth * 0.8f, new Vector3(0, -0.4f, 0), new Vector3(0.16f, 0.42f, 0.16f));
                Prim(PrimitiveType.Capsule, "Leg", pf.LegL, cloth * 0.8f, new Vector3(0, -0.4f, 0), new Vector3(0.16f, 0.42f, 0.16f));
                pf.Body = new GameObject("Body").transform; pf.Body.SetParent(pf.Root, false); pf.Body.localPosition = new Vector3(0, 0.85f, 0);
                Prim(PrimitiveType.Capsule, "Torso", pf.Body, cloth, new Vector3(0, 0.32f, 0), new Vector3(0.38f, 0.38f, 0.26f));
                pf.Head = Prim(PrimitiveType.Sphere, "Head", pf.Body, skin, new Vector3(0, 0.83f, 0), new Vector3(0.26f, 0.28f, 0.26f)).transform;
                pf.ArmR = new GameObject("ArmR").transform; pf.ArmR.SetParent(pf.Body, false); pf.ArmR.localPosition = new Vector3(0.24f, 0.62f, 0);
                pf.ArmL = new GameObject("ArmL").transform; pf.ArmL.SetParent(pf.Body, false); pf.ArmL.localPosition = new Vector3(-0.24f, 0.62f, 0);
                Prim(PrimitiveType.Capsule, "Arm", pf.ArmR, skin, new Vector3(0, -0.26f, 0), new Vector3(0.1f, 0.3f, 0.1f));
                Prim(PrimitiveType.Capsule, "Arm", pf.ArmL, skin, new Vector3(0, -0.26f, 0), new Vector3(0.1f, 0.3f, 0.1f));
                pf.Stick = Prim(PrimitiveType.Cylinder, "Stick", pf.ArmR, new Color(0.75f, 0.62f, 0.40f), new Vector3(0, -0.6f, 0.12f), new Vector3(0.04f, 0.26f, 0.04f)).transform;
                _perf[p] = pf;
            }
            ApplyPoses(1f);
        }

        // ------------------------------------------------------------------ state

        /// <summary>The pose performer <paramref name="pad"/> is asked to hold (the target, not the blended one).</summary>
        public Pose PoseOf(int pad) => pad >= 1 && pad <= 4 ? _perf[pad].Pose : Pose.Idle;

        /// <summary>Pose from a cue family ("kick" = foot braced, "snare" = stick raised, "bass" = lean in; null = at ease).</summary>
        public static Pose PoseForFamily(string family)
        {
            switch (family) { case "kick": return Pose.Kick; case "snare": return Pose.Snare; case "bass": return Pose.Bass; default: return Pose.Idle; }
        }

        public void SetPose(int pad, Pose pose) { if (pad >= 1 && pad <= 4) _perf[pad].Pose = pose; }

        /// <summary>A judged hit: the stick comes down on the drum.</summary>
        public void Strike(int pad) { if (pad >= 1 && pad <= 4) { _perf[pad].Dip = 1f; Strikes++; } }

        public void SetTorch(int i, TorchState s)
        {
            if (i < 0 || i >= TorchCount) return;
            _torchState[i] = s;
            if (_flame[i] != null) _flame[i].gameObject.SetActive(s != TorchState.Unlit);
        }

        public TorchState TorchStateOf(int i) => i >= 0 && i < TorchCount ? _torchState[i] : TorchState.Unlit;
        public Vector3 TorchPoint(int i) => _torchPoint[Mathf.Clamp(i, 0, TorchCount - 1)];
        public void ClearTorches() { for (int i = 0; i < TorchCount; i++) SetTorch(i, TorchState.Unlit); }

        public void RingBell() { _bellSwingV = 7f; BellRings++; }

        // ------------------------------------------------------------------ animation

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;
            for (int p = 1; p <= 4; p++)
            {
                var pf = _perf[p];
                pf.Blend = pf.Pose == pf.Shown ? Mathf.MoveTowards(pf.Blend, 1f, dt * 6f) : Mathf.MoveTowards(pf.Blend, 0f, dt * 8f);
                if (pf.Blend <= 0f) pf.Shown = pf.Pose;
                pf.Dip = Mathf.MoveTowards(pf.Dip, 0f, dt * 5f);
            }
            ApplyPoses(Time.unscaledTime);
            // Bell: damped swing.
            BellSwing += _bellSwingV * dt;
            _bellSwingV += (-BellSwing * 60f - _bellSwingV * 2.2f) * dt;
            if (_bell != null) _bell.localRotation = Quaternion.Euler(0, 0, BellSwing * Mathf.Rad2Deg * 0.15f);
            for (int i = 0; i < TorchCount; i++)
            {
                if (_flame[i] == null || !_flame[i].gameObject.activeSelf) continue;
                float f = 1f + 0.12f * Mathf.Sin(Time.unscaledTime * 11f + i * 1.7f);
                switch (_torchState[i])
                {
                    case TorchState.Current: _flameMat[i].color = new Color(1f, 0.62f, 0.2f); _flame[i].localScale = new Vector3(0.3f, 0.5f * f, 0.3f); break;
                    case TorchState.Kept: _flameMat[i].color = new Color(1f, 0.88f, 0.4f); _flame[i].localScale = new Vector3(0.36f, 0.62f * f, 0.36f); break;
                    case TorchState.Missed: _flameMat[i].color = new Color(0.55f, 0.2f, 0.16f); _flame[i].localScale = new Vector3(0.22f, 0.3f * f, 0.22f); break;
                }
            }
        }

        private void ApplyPoses(float time)
        {
            for (int p = 1; p <= 4; p++)
            {
                var pf = _perf[p];
                // Blend: the shown pose fades out before the new one fades in (so a change reads as a change).
                Pose shown = pf.Shown;
                float k = pf.Blend;
                float bodyPitch = 0, rootDrop = 0, armR = 0, armL = 0, legR = 0, legL = 0, stance = 0;
                switch (shown)
                {
                    case Pose.Kick: bodyPitch = -6f; rootDrop = 0.10f; legR = 38f; legL = -8f; armR = 18f; armL = -18f; stance = 14f; break;
                    case Pose.Snare: bodyPitch = 0f; armR = 165f; armL = 25f; break;
                    case Pose.Bass: bodyPitch = 28f; armR = 70f; armL = 70f; legR = -6f; legL = 6f; break;
                    default: armR = 6f; armL = 6f; break;
                }
                float dip = pf.Dip * 55f;
                pf.Root.position = pf.Home + Vector3.down * rootDrop * k;
                pf.Body.localRotation = Quaternion.Euler(bodyPitch * k, 0, 0);
                pf.ArmR.localRotation = Quaternion.Euler((armR - (shown == Pose.Snare ? dip : 0f)) * k, 0, -stance * 0.3f * k);
                pf.ArmL.localRotation = Quaternion.Euler(armL * k, 0, stance * 0.3f * k);
                pf.LegR.localRotation = Quaternion.Euler(legR * k, 0, stance * 0.5f * k);
                pf.LegL.localRotation = Quaternion.Euler(legL * k, 0, -stance * 0.5f * k);
                if (shown != Pose.Snare && pf.Dip > 0f) pf.ArmR.localRotation = Quaternion.Euler(armR * k + dip, 0, 0);
            }
        }

        // ------------------------------------------------------------------ screen hit tests

        /// <summary>Radius in pixels of the smallest hit target (diameter 10 % of the screen height, more than the 7 % rule).</summary>
        public static float MinHitRadius => Mathf.Max(10f, Screen.height * 0.05f);

        /// <summary>
        /// The drum (1-4) whose projected shape (skin disc and body, enlarged) is under a screen point, else 0. Nearest
        /// shape wins. Registered with <see cref="TouchStrips.ShapeResolver"/> while the dance runs.
        /// </summary>
        public static int DrumAt(Camera cam, DanceCircle circle, Vector2 screen)
        {
            if (cam == null || circle == null) return 0;
            int best = 0; float bestD = float.MaxValue;
            for (int p = 1; p <= 4; p++)
            {
                var top = cam.WorldToScreenPoint(circle.DrumTop(p));
                if (top.z <= 0) continue;
                var foot = cam.WorldToScreenPoint(circle.Layout.DrumPosition(p));
                float rw = DanceCircle.SkinRadius * DanceCircle.DrumScale(p);
                var right = cam.WorldToScreenPoint(circle.DrumTop(p) + cam.transform.right * rw);
                var flat = cam.transform.forward; flat.y = 0; flat.Normalize();
                var far = cam.WorldToScreenPoint(circle.DrumTop(p) + flat * rw);
                float rx = Mathf.Max(Mathf.Abs(right.x - top.x) * 1.25f, MinHitRadius);
                float ry = Mathf.Max(Mathf.Abs(far.y - top.y) * 1.25f, MinHitRadius * 0.8f);
                float nx = (screen.x - top.x) / rx, ny = (screen.y - top.y) / ry;
                float e = nx * nx + ny * ny; // 1 on the skin's enlarged edge
                // The body under the skin, down to the ground, as wide as the skin.
                float body = DistanceToSegment(screen, top, foot) / (rx * 0.9f);
                float score = Mathf.Min(e, body * body);
                if (score <= 1f && score < bestD) { bestD = score; best = p; }
            }
            return best;
        }

        private static float DistanceToSegment(Vector2 p, Vector2 a, Vector2 b)
        {
            var ab = b - a; float l = ab.sqrMagnitude;
            float t = l < 1e-4f ? 0f : Mathf.Clamp01(Vector2.Dot(p - a, ab) / l);
            return Vector2.Distance(p, a + ab * t);
        }

        /// <summary>Screen position (origin bottom-left) of a world point, or null if behind the camera.</summary>
        public static Vector2? ScreenOf(Camera cam, Vector3 world)
        {
            if (cam == null) return null;
            var s = cam.WorldToScreenPoint(world);
            return s.z <= 0 ? (Vector2?)null : new Vector2(s.x, s.y);
        }

        /// <summary>Which world prop a screen point taps (never anything in the touch band): Sol, seat, bell, a torch (index in <paramref name="torch"/>).</summary>
        public Target TargetAt(Camera cam, Vector2 screen, out int torch)
        {
            torch = -1;
            if (cam == null) return Target.None;
            float best = float.MaxValue; var result = Target.None; int idxBest = -1;
            float limit = MinHitRadius * 1.3f;
            void Try(Vector3 world, Target t, int idx)
            {
                var sp = ScreenOf(cam, world); if (!sp.HasValue) return;
                float d = Vector2.Distance(sp.Value, screen);
                if (d <= limit && d < best) { best = d; result = t; idxBest = idx; }
            }
            Try(SolPoint, Target.Sol, -1);
            Try(SeatPoint, Target.Seat, -1);
            Try(BellPoint, Target.Bell, -1);
            for (int i = 0; i < TorchCount; i++) Try(_torchPoint[i], Target.Torch, i);
            torch = idxBest;
            return result;
        }
    }
}
