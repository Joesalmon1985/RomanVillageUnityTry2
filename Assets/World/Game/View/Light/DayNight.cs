using System.Collections.Generic;
using UnityEngine;
using World.Sim;

namespace World.Game
{
    /// <summary>Everything the lighting derives from the time of day (a pure function of the hour: see <see cref="DayNight.Evaluate"/>).</summary>
    public struct LightingState
    {
        public float Hour;
        /// <summary>Unit vectors from the ground towards the sun and the moon.</summary>
        public Vector3 SunDir, MoonDir;
        /// <summary>Sun elevation in degrees (negative below the horizon).</summary>
        public float SunElevation;
        public Color SunColor, MoonColor;
        public float SunIntensity, MoonIntensity;
        public Color AmbientSky, AmbientEquator, AmbientGround;
        public Color Zenith, Horizon, HorizonGlow, Fog;
        public float FogStart, FogEnd;
        /// <summary>0 by day, 1 at night: how far the fires and torches are on.</summary>
        public float Night;
        public float Stars;

        public bool IsNight => Night > 0.5f;
        public bool SunBelowHorizon => SunElevation < 0f;
    }

    /// <summary>
    /// The day-night cycle (package L, Docs/LIGHTING.md). The hour is a pure function of the simulation tick
    /// (<see cref="HourAtTick"/>), so it pauses with the <see cref="GameClock"/> and a loaded game shows exactly the
    /// lighting it was saved in. Owns the sun and moon lights, the sky dome's colours, stars, moon and sun discs,
    /// ambient light, fog and the shader globals of the night-readability materials. A new game starts at night.
    /// </summary>
    public sealed class DayNight : MonoBehaviour
    {
        public static DayNight Instance { get; private set; }

        // ---------------- tuning ----------------

        /// <summary>Real seconds of running game clock per full day.</summary>
        public static float DayLengthSeconds = 720f;
        /// <summary>Hour of the day at tick 0 (a new game): deep night, about three and a half minutes before first light.</summary>
        public static float StartHour = 22.5f;
        public const float SunriseHour = 6f, SunsetHour = 18f;
        /// <summary>Lowest elevation (degrees) the shadow-casting light is allowed: lower gives long, shimmering shadows.</summary>
        public const float MinLightElevation = 14f;
        public const float MoonFullIntensity = 0.62f, SunFullIntensity = 1.15f;
        public const float ShadowDistanceDay = 90f, ShadowDistanceNight = 60f;

        // ---------------- developer scrub ----------------

        /// <summary>Hours added to the game time (developer scrub; not saved).</summary>
        public static double DevOffsetHours;
        /// <summary>Hours per real second added while the time-lapse is on (developer; F3).</summary>
        public static float DevTimeLapse;
        /// <summary>F2 / Shift+F2 step an hour, F3 toggles a time-lapse. Package P's F1 tools can call <see cref="Scrub"/> instead.</summary>
        public static bool DevKeys = true;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { DevOffsetHours = 0; DevTimeLapse = 0; DevKeys = true; DayLengthSeconds = 720f; StartHour = 22.5f; }

        public static void Scrub(double hours) => DevOffsetHours += hours;

        /// <summary>Developer and capture helper: jumps the scrub offset so that it is <paramref name="hour"/> now.</summary>
        public void SetHour(double hour)
        {
            DevOffsetHours += hour - Hour;
            Refresh();
        }

        // ---------------- state ----------------

        public double Hour { get; private set; }
        public LightingState State { get; private set; }
        public float Night => State.Night;
        public Light Sun { get; private set; }
        public Light Moon { get; private set; }

        private WorldBootstrap _boot;
        private long _lastTick = -1;
        private float _lastTickTime, _frac;
        private Mesh _dome; private Vector3[] _domeDirs; private Color[] _domeCols;
        private double _domeHour = -100; private float _domeMap = -1f;
        private Transform _skyRoot, _stars, _moonDisc, _sunDisc;
        private Renderer[] _skyRenderers;
        private Material _starMat, _moonMat, _moonHaloMat, _sunMat;
        private float _mapBoost;

        private static readonly int WorldNightId = Shader.PropertyToID("_WorldNight"), NightTintId = Shader.PropertyToID("_WorldNightTint");

        public static DayNight Create(WorldBootstrap boot)
        {
            var go = new GameObject("DayNight");
            go.transform.SetParent(boot.transform, false);
            var d = go.AddComponent<DayNight>();
            d._boot = boot;
            Instance = d;
            d.Build();
            d.Refresh();
            return d;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            Shader.SetGlobalFloat(WorldNightId, 0f);
            Shader.SetGlobalColor(NightTintId, Color.white);
        }

        // ---------------- time ----------------

        /// <summary>The hour of the day (0-24) at a simulation tick (fractions allowed), before the developer offset.</summary>
        public static double HourAtTick(double tick)
        {
            double days = tick / (DayLengthSeconds * (double)WorldSim.TicksPerSecond);
            double h = (StartHour + days * 24.0) % 24.0;
            return h < 0 ? h + 24.0 : h;
        }

        private double CurrentHour()
        {
            long tick = _boot.Sim.State.Tick;
            if (tick != _lastTick) { _lastTick = tick; _lastTickTime = Time.time; }
            // Smooth between ticks while the clock runs; hold exactly where it is while paused.
            if (_boot.Clock != null && !_boot.Clock.IsPaused) _frac = Mathf.Clamp01((Time.time - _lastTickTime) * WorldSim.TicksPerSecond);
            else _lastTickTime = Time.time - _frac / WorldSim.TicksPerSecond;
            double h = (HourAtTick(tick + _frac) + DevOffsetHours) % 24.0;
            return h < 0 ? h + 24.0 : h;
        }

        // ---------------- the pure function ----------------

        private static readonly float[] E = { -16f, -6f, 0f, 8f, 26f };
        private static readonly Color[] ZenithK = { C(0.012f, 0.022f, 0.065f), C(0.05f, 0.07f, 0.19f), C(0.19f, 0.27f, 0.50f), C(0.27f, 0.44f, 0.75f), C(0.33f, 0.55f, 0.85f) };
        private static readonly Color[] HorizonK = { C(0.045f, 0.065f, 0.13f), C(0.17f, 0.15f, 0.25f), C(0.52f, 0.42f, 0.44f), C(0.78f, 0.72f, 0.66f), C(0.78f, 0.84f, 0.88f) };
        private static readonly Color[] GlowK = { C(0, 0, 0), C(0.50f, 0.20f, 0.08f), C(1.0f, 0.50f, 0.16f), C(0.40f, 0.26f, 0.10f), C(0, 0, 0) };
        private static readonly Color[] AmbSkyK = { C(0.105f, 0.15f, 0.29f), C(0.16f, 0.17f, 0.31f), C(0.38f, 0.36f, 0.46f), C(0.55f, 0.58f, 0.68f), C(0.62f, 0.68f, 0.80f) };
        private static readonly Color[] AmbEqK = { C(0.07f, 0.095f, 0.175f), C(0.12f, 0.115f, 0.19f), C(0.33f, 0.29f, 0.30f), C(0.48f, 0.46f, 0.42f), C(0.52f, 0.52f, 0.48f) };
        private static readonly Color[] AmbGroundK = { C(0.035f, 0.045f, 0.085f), C(0.06f, 0.055f, 0.085f), C(0.18f, 0.16f, 0.15f), C(0.27f, 0.25f, 0.22f), C(0.30f, 0.28f, 0.24f) };
        private static readonly Color[] SunColK = { C(1f, 0.40f, 0.18f), C(1f, 0.40f, 0.18f), C(1f, 0.50f, 0.24f), C(1f, 0.80f, 0.58f), C(1f, 0.93f, 0.80f) };
        private static readonly float[] SunIntK = { 0f, 0f, 0.12f, 0.80f, SunFullIntensity };

        private static Color C(float r, float g, float b) => new Color(r, g, b, 1f);

        private static Color Key(Color[] k, float e)
        {
            if (e <= E[0]) return k[0];
            for (int i = 1; i < E.Length; i++)
                if (e <= E[i]) return Color.Lerp(k[i - 1], k[i], Mathf.SmoothStep(0f, 1f, (e - E[i - 1]) / (E[i] - E[i - 1])));
            return k[k.Length - 1];
        }

        private static float Key(float[] k, float e)
        {
            if (e <= E[0]) return k[0];
            for (int i = 1; i < E.Length; i++)
                if (e <= E[i]) return Mathf.Lerp(k[i - 1], k[i], (e - E[i - 1]) / (E[i] - E[i - 1]));
            return k[k.Length - 1];
        }

        /// <summary>The whole lighting state for an hour of the day (0-24). Pure: the same hour always gives the same state.</summary>
        public static LightingState Evaluate(double hour)
        {
            var s = new LightingState { Hour = (float)hour };
            // Sun: rises in the east (+X) at 6, 54 degrees up in the south at noon, sets in the west at 18.
            float a = (float)((hour - SunriseHour) / 24.0 * 2.0 * Mathf.PI);
            s.SunDir = (Mathf.Cos(a) * Vector3.right + Mathf.Sin(a) * new Vector3(0f, 0.81f, -0.586f)).normalized;
            s.SunElevation = Mathf.Asin(Mathf.Clamp(s.SunDir.y, -1f, 1f)) * Mathf.Rad2Deg;
            // Moon: up all night, from the south-east to the south-west, low enough to be seen by a walker who looks slightly down.
            float nightT = Mathf.Clamp01((float)(((hour - SunsetHour + 24.0) % 24.0) / 12.0));
            float mEl = (17f + 11f * Mathf.Sin(nightT * Mathf.PI)) * Mathf.Deg2Rad, mAz = Mathf.Lerp(105f, 255f, nightT) * Mathf.Deg2Rad;
            s.MoonDir = new Vector3(Mathf.Sin(mAz) * Mathf.Cos(mEl), Mathf.Sin(mEl), Mathf.Cos(mAz) * Mathf.Cos(mEl));

            float e = s.SunElevation;
            s.Zenith = Key(ZenithK, e);
            s.Horizon = Key(HorizonK, e);
            s.HorizonGlow = Key(GlowK, e);
            s.AmbientSky = Key(AmbSkyK, e);
            s.AmbientEquator = Key(AmbEqK, e);
            s.AmbientGround = Key(AmbGroundK, e);
            s.SunColor = Key(SunColK, e);
            s.SunIntensity = e <= -1f ? 0f : Key(SunIntK, e) * Mathf.Clamp01(e + 1f);
            s.MoonColor = new Color(0.56f, 0.70f, 1f);
            s.MoonIntensity = MoonFullIntensity * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(-2f, -10f, e));
            s.Night = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(5f, -4f, e));
            s.Stars = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(-3f, -12f, e));
            s.Fog = s.Horizon * Mathf.Lerp(1f, 1.2f, s.Night); s.Fog.a = 1f;
            // A little closer at night: depth without hiding the next camp's fires.
            s.FogStart = Mathf.Lerp(TerrainBuilder.FogStart, 30f, s.Night);
            s.FogEnd = Mathf.Lerp(TerrainBuilder.FogEnd, 400f, s.Night);
            return s;
        }

        // ---------------- build ----------------

        private void Build()
        {
            Sun = _boot.Sun;
            Sun.shadows = LightShadows.Soft;
            Sun.shadowBias = 0.06f; Sun.shadowNormalBias = 0.5f;
            Sun.renderMode = LightRenderMode.ForcePixel;

            var mgo = new GameObject("Moon");
            mgo.transform.SetParent(transform, false);
            Moon = mgo.AddComponent<Light>();
            Moon.type = LightType.Directional;
            Moon.shadows = LightShadows.Soft;
            Moon.shadowStrength = 0.55f;
            Moon.shadowBias = 0.06f; Moon.shadowNormalBias = 0.5f;
            Moon.renderMode = LightRenderMode.ForcePixel;

            // Stable shadows: fixed cascades fitted to stay still as the camera turns, high resolution, soft edges.
            QualitySettings.shadows = ShadowQuality.All;
            QualitySettings.shadowProjection = ShadowProjection.StableFit;
            QualitySettings.shadowResolution = ShadowResolution.VeryHigh;
            QualitySettings.shadowCascades = 4;
            QualitySettings.pixelLightCount = WorldLights.PixelLightCount;

            var dome = _boot.transform.Find("SkyDome");
            if (dome != null)
            {
                _dome = dome.GetComponent<MeshFilter>().mesh;
                _dome.MarkDynamic();
                var v = _dome.vertices;
                _domeDirs = new Vector3[v.Length];
                for (int i = 0; i < v.Length; i++) _domeDirs[i] = v[i].normalized;
                _domeCols = new Color[v.Length];
            }

            _skyRoot = new GameObject("Sky lights").transform;
            _skyRoot.SetParent(transform, false);
            BuildStars();
            BuildDiscs();
            _skyRenderers = _skyRoot.GetComponentsInChildren<Renderer>(true);
        }

        private const float SkyRadius = 1000f;

        private void BuildStars()
        {
            var rnd = new PropRandom(4711);
            var v = new List<Vector3>(); var c = new List<Color>(); var t = new List<int>();
            for (int i = 0; i < 1500; i++)
            {
                // Uniform over the sphere (the field turns through the night, so stars below the horizon rise later).
                float y = rnd.Range(-1f, 1f), az = rnd.Range(0f, Mathf.PI * 2f), r = Mathf.Sqrt(1f - y * y);
                var d = new Vector3(Mathf.Cos(az) * r, y, Mathf.Sin(az) * r);
                var right = Vector3.Cross(Vector3.up, d); if (right.sqrMagnitude < 1e-4f) right = Vector3.right; right.Normalize();
                var up = Vector3.Cross(d, right);
                float big = rnd.Next(); big *= big * big;
                float size = 0.75f + 2.3f * big, b = 0.30f + 0.70f * rnd.Next();
                var col = Color.Lerp(new Color(0.75f, 0.84f, 1f), new Color(1f, 0.90f, 0.74f), rnd.Next()); col.a = b;
                var p = d * SkyRadius; int k = v.Count;
                // A diamond, so a star never looks like a square pixel block.
                v.Add(p - right * size); v.Add(p + up * size); v.Add(p + right * size); v.Add(p - up * size);
                for (int q = 0; q < 4; q++) c.Add(col);
                t.Add(k); t.Add(k + 1); t.Add(k + 2); t.Add(k); t.Add(k + 2); t.Add(k + 3);
            }
            var m = new Mesh { name = "Stars" };
            m.SetVertices(v); m.SetColors(c); m.SetTriangles(t, 0);
            m.bounds = new Bounds(Vector3.zero, Vector3.one * SkyRadius * 2.2f);
            _starMat = MaterialFactory.CreateGlow(new Color(1, 1, 1, 0), "Stars");
            var go = new GameObject("Stars");
            go.transform.SetParent(_skyRoot, false);
            go.AddComponent<MeshFilter>().sharedMesh = m;
            var r2 = go.AddComponent<MeshRenderer>(); r2.sharedMaterial = _starMat;
            r2.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r2.receiveShadows = false;
            _stars = go.transform;
        }

        private void BuildDiscs()
        {
            // Moon: a pale self-lit disc with three grey seas, in a wide soft halo.
            var mb = new MeshBuilder();
            MoonFace(mb, 0f, 0f, 1f, new Color(0.90f, 0.93f, 1f), 0f);
            MoonFace(mb, -0.28f, 0.22f, 0.30f, new Color(0.70f, 0.75f, 0.86f), -0.01f);
            MoonFace(mb, 0.25f, -0.15f, 0.22f, new Color(0.72f, 0.77f, 0.88f), -0.01f);
            MoonFace(mb, 0.10f, 0.40f, 0.14f, new Color(0.74f, 0.79f, 0.89f), -0.01f);
            _moonMat = MaterialFactory.CreateUnlit(Color.white, "Moon");
            _moonMat.SetFloat("_NoFog", 1f);
            _moonDisc = new GameObject("MoonDisc").transform;
            _moonDisc.SetParent(_skyRoot, false);
            var face = mb.ToObject("Face", _moonDisc, _moonMat);
            face.transform.localScale = Vector3.one * 33f;
            NoShadows(face);
            _moonHaloMat = MaterialFactory.CreateGlow(new Color(0.45f, 0.60f, 1f, 0f), "Moon halo");
            var halo = Part(_moonDisc, "Halo", LightShapes.RadialDisc(true), _moonHaloMat);
            halo.transform.localScale = Vector3.one * 120f;
            halo.transform.localPosition = new Vector3(0, 0, 2f);

            _sunMat = MaterialFactory.CreateGlow(new Color(1f, 0.9f, 0.7f, 0f), "Sun disc");
            _sunDisc = new GameObject("SunDisc").transform;
            _sunDisc.SetParent(_skyRoot, false);
            var sun = Part(_sunDisc, "Glow", LightShapes.RadialDisc(true), _sunMat);
            sun.transform.localScale = Vector3.one * 150f;
            var core = Part(_sunDisc, "Core", LightShapes.RadialDisc(true), _sunMat);
            core.transform.localScale = Vector3.one * 46f;
        }

        private static void MoonFace(MeshBuilder mb, float x, float y, float r, Color col, float z)
        {
            const int seg = 24;
            var c = new Vector3(x, y, z);
            for (int i = 0; i < seg; i++)
            {
                float a0 = i * Mathf.PI * 2f / seg, a1 = (i + 1) * Mathf.PI * 2f / seg;
                var p0 = c + new Vector3(Mathf.Cos(a0), Mathf.Sin(a0), 0) * r; var p1 = c + new Vector3(Mathf.Cos(a1), Mathf.Sin(a1), 0) * r;
                mb.Tri(c, p0, p1, col); mb.Tri(c, p1, p0, col);
            }
        }

        private static GameObject Part(Transform parent, string name, Mesh mesh, Material mat)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = mat;
            NoShadows(go);
            return go;
        }

        private static void NoShadows(GameObject go)
        {
            var r = go.GetComponent<Renderer>();
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false;
        }

        // ---------------- apply ----------------

        private void Update()
        {
            if (DevKeys && (Application.isEditor || Debug.isDebugBuild))
            {
                bool shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
                if (Input.GetKeyDown(KeyCode.F2)) Scrub(shift ? -1 : 1);
                if (Input.GetKeyDown(KeyCode.F3)) DevTimeLapse = DevTimeLapse > 0f ? 0f : 1f;
            }
            if (DevTimeLapse > 0f) DevOffsetHours += DevTimeLapse * Time.unscaledDeltaTime;
            Refresh();
        }

        /// <summary>Recomputes the hour and applies the lighting now (also called by <see cref="SetHour"/>).</summary>
        public void Refresh()
        {
            if (_boot == null || _boot.Sim == null) return;
            Hour = CurrentHour();
            var s = Evaluate(Hour);
            State = s;

            bool map = ViewSwitcher.Instance != null && ViewSwitcher.Instance.CurrentView == ViewMode.God;
            _mapBoost = Mathf.MoveTowards(_mapBoost, map ? 1f : 0f, Time.unscaledDeltaTime * 4f);
            float mb = _mapBoost * s.Night;

            // Sun and moon: one shadow-casting directional light at a time; neither is ever lower than MinLightElevation.
            ApplyLight(Sun, s.SunDir, s.SunColor, s.SunIntensity, 0.75f);
            ApplyLight(Moon, s.MoonDir, s.MoonColor, s.MoonIntensity * (1f + 0.5f * mb), 0.55f);
            RenderSettings.sun = s.SunIntensity > 0f || s.MoonIntensity <= 0f ? Sun : Moon;
            QualitySettings.shadowDistance = Mathf.Lerp(ShadowDistanceDay, ShadowDistanceNight, s.Night);

            // Ambient: never black. The map (God View) gets a brighter moonlit fill so the whole island stays readable.
            var mapSky = new Color(0.20f, 0.25f, 0.40f); var mapEq = new Color(0.18f, 0.22f, 0.34f); var mapGround = new Color(0.12f, 0.14f, 0.22f);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = Color.Lerp(s.AmbientSky, mapSky, mb);
            RenderSettings.ambientEquatorColor = Color.Lerp(s.AmbientEquator, mapEq, mb);
            RenderSettings.ambientGroundColor = Color.Lerp(s.AmbientGround, mapGround, mb);
            RenderSettings.fogColor = s.Fog;
            RenderSettings.fogStartDistance = s.FogStart;
            RenderSettings.fogEndDistance = s.FogEnd;
            if (_boot.MainCamera != null) _boot.MainCamera.backgroundColor = s.Fog;
            if (_boot.GodCam != null && _boot.GodCam.Camera != null)
                _boot.GodCam.Camera.backgroundColor = Color.Lerp(new Color(0.22f, 0.42f, 0.55f), new Color(0.05f, 0.09f, 0.16f), s.Night);

            Shader.SetGlobalFloat(WorldNightId, s.Night);
            Shader.SetGlobalColor(NightTintId, Color.Lerp(Color.white, new Color(0.56f, 0.57f, 0.62f), s.Night));

            if (_dome != null && (System.Math.Abs(Hour - _domeHour) > 0.002 || Mathf.Abs(_mapBoost - _domeMap) > 0.01f)) PaintDome(s);

            // Stars turn slowly through the night; the discs ride on the sun and moon directions.
            _starMat.color = new Color(1f, 1f, 1f, s.Stars * (0.86f + 0.14f * Mathf.Sin(Time.unscaledTime * 1.7f)));
            _stars.localRotation = Quaternion.AngleAxis((float)Hour * 15f, new Vector3(0f, 0.58f, 0.81f));
            float moonUp = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(2f, -6f, s.SunElevation));
            _moonDisc.localPosition = s.MoonDir * (SkyRadius - 40f);
            _moonDisc.localRotation = Quaternion.LookRotation(s.MoonDir, Vector3.up);
            _moonDisc.gameObject.SetActive(moonUp > 0.01f);
            _moonMat.color = Color.Lerp(s.Horizon, Color.white, moonUp);
            _moonHaloMat.color = new Color(0.45f, 0.60f, 1f, 0.34f * moonUp);
            float sunUp = Mathf.Clamp01((s.SunElevation + 5f) / 6f);
            _sunDisc.localPosition = s.SunDir * (SkyRadius - 40f);
            _sunDisc.localRotation = Quaternion.LookRotation(s.SunDir, Vector3.up);
            _sunDisc.gameObject.SetActive(sunUp > 0.01f);
            var sc = Color.Lerp(s.SunColor, Color.white, 0.25f);
            _sunMat.color = new Color(sc.r, sc.g, sc.b, 0.85f * sunUp);
        }

        private static void ApplyLight(Light l, Vector3 dir, Color color, float intensity, float shadowStrength)
        {
            bool on = intensity > 0.001f;
            if (l.enabled != on) l.enabled = on;
            if (!on) return;
            float minY = Mathf.Sin(MinLightElevation * Mathf.Deg2Rad);
            if (dir.y < minY)
            {
                var flat = new Vector3(dir.x, 0, dir.z);
                flat = flat.sqrMagnitude > 1e-6f ? flat.normalized : Vector3.right;
                dir = flat * Mathf.Cos(MinLightElevation * Mathf.Deg2Rad) + Vector3.up * minY;
            }
            l.transform.rotation = Quaternion.LookRotation(-dir, Vector3.up);
            l.color = color;
            l.intensity = intensity;
            l.shadowStrength = shadowStrength;
        }

        private void PaintDome(in LightingState s)
        {
            _domeHour = Hour; _domeMap = _mapBoost;
            var sunFlat = new Vector3(s.SunDir.x, 0, s.SunDir.z);
            sunFlat = sunFlat.sqrMagnitude > 1e-6f ? sunFlat.normalized : Vector3.right;
            var moonGlow = new Color(0.05f, 0.075f, 0.14f) * (s.MoonIntensity / MoonFullIntensity);
            for (int i = 0; i < _domeDirs.Length; i++)
            {
                var d = _domeDirs[i];
                float up = Mathf.Clamp01(d.y);
                var c = Color.Lerp(s.Horizon, s.Zenith, Mathf.Pow(up, 0.6f));
                // Dawn and dusk: a warm glow low on the sun's side, with a fainter wash all round the horizon.
                float toward = Mathf.Max(0f, d.x * sunFlat.x + d.z * sunFlat.z);
                float low = 1f - up; low *= low; low *= low;
                c += s.HorizonGlow * (low * (0.18f + 0.82f * toward * toward * toward));
                float md = Mathf.Max(0f, Vector3.Dot(d, s.MoonDir)); md *= md; md *= md; md *= md;
                c += moonGlow * md;
                c.a = 1f;
                _domeCols[i] = c;
            }
            _dome.colors = _domeCols;
        }

        private void LateUpdate()
        {
            var cam = ViewSwitcher.Instance != null ? ViewSwitcher.Instance.ActiveCamera : Camera.main;
            if (cam == null) return;
            bool show = !cam.orthographic;
            for (int i = 0; i < _skyRenderers.Length; i++) if (_skyRenderers[i].enabled != show) _skyRenderers[i].enabled = show;
            _skyRoot.position = cam.transform.position;
        }
    }
}
