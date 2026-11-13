using System.Collections.Generic;
using UnityEngine;

namespace World.Game
{
    /// <summary>
    /// One fire, torch, brazier or glowing accent (package L, Docs/LIGHTING.md). It has no Update of its own:
    /// <see cref="WorldLights"/> flickers every registered one, fades it with the time of day and decides which few get
    /// a real per-pixel light. Other packages make one with <see cref="WorldLights.Add"/>.
    /// </summary>
    public sealed class FireLight : MonoBehaviour
    {
        public enum Style { Fire, Pulse, Steady }

        public Color Color = new Color(1f, 0.62f, 0.26f);
        /// <summary>Point light intensity and range at full night.</summary>
        public float Intensity = 1.6f, Range = 12f;
        /// <summary>Higher wins a real light when the budget is short (0 torch, 1 hearth, 2 the cast mark, 3 the dance fire).</summary>
        public int Priority;
        /// <summary>Owner switch (a held camp's fire is out, an unbuilt site has no torch).</summary>
        public bool On = true;
        /// <summary>Fraction of the night level kept by day (0: a torch is out by day; 0.35: a hearth still glows a little).</summary>
        public float DayLevel;
        public Style Look = Style.Fire;
        /// <summary>Radius of the soft glow on the ground (0: none) and of the far-seen halo round the flame (0: none).</summary>
        public float GroundGlow = 4f, Halo = 0.9f;
        public float GroundGlowStrength = 0.30f, HaloStrength = 0.42f;
        /// <summary>Sparks rising from it when the player is near.</summary>
        public bool Sparks = true;
        /// <summary>A flame mesh owned by this light (scaled by the flicker, hidden when out); null for fires drawn by their own view.</summary>
        public Transform Flame;
        public Vector3 FlameScale = Vector3.one;
        /// <summary>The light belongs to another package, which sets its intensity (the dance fire): only budgeted and dressed here.</summary>
        public bool External;
        /// <summary>Extra multiplier on an external light's own intensity and range at full night.</summary>
        public float ExternalNightBoost = 1f, ExternalNightRange;

        public Light Light { get; internal set; }
        /// <summary>0 out, 1 fully on (time of day and the owner switch, smoothed).</summary>
        public float Level { get; internal set; }
        /// <summary>The flicker multiplier this frame (shared by the light, the glow and the flame).</summary>
        public float Flicker { get; internal set; } = 1f;
        public WorldLights.Slot Slot { get; internal set; }

        internal Renderer GroundRenderer, HaloRenderer;
        internal Transform HaloTransform;
        internal float SlotFade, Seed, Score, ExternalRange;
        internal bool Dressed;
        internal Vector3 LightHome;

        private void OnEnable() { WorldLights.Register(this); }
        private void OnDisable() { WorldLights.Unregister(this); }
    }

    /// <summary>
    /// The registry and budget of every warm light in the world (package L). Built-in forward rendering draws each
    /// object once more per per-pixel light that reaches it, so only the <see cref="PixelBudget"/> nearest (by
    /// priority) get a real per-pixel point light, the next <see cref="VertexBudget"/> a per-vertex one, and every
    /// other fire is only its emissive flame, ground glow and halo.
    /// </summary>
    public sealed class WorldLights : MonoBehaviour
    {
        public enum Slot { None, Vertex, Pixel }

        public static WorldLights Instance { get; private set; }

        /// <summary>Per-pixel point lights this package may run at once (the player's lantern and the dance fire included).</summary>
        public const int PixelBudget = 6;
        /// <summary>
        /// Per-vertex point lights on top of that: none. The ground is made of very large triangles, and a per-vertex
        /// light near one corner floods the whole triangle; fires beyond the pixel budget are glow only.
        /// </summary>
        public const int VertexBudget = 0;
        /// <summary>QualitySettings.pixelLightCount: the budget, the sun or moon, and room for the dance's short drum flashes.</summary>
        public const int PixelLightCount = PixelBudget + 1 + 4;
        /// <summary>No real light beyond this distance from the eye (m); glows and halos carry on.</summary>
        public const float LightCullDistance = 85f;
        public const float SparkDistance = 38f, HaloDistance = 420f;
        /// <summary>Height of the ground glow discs: above the paths (0.05-0.08) and the cast marks (0.11-0.14), so nothing z-fights.</summary>
        public const float GlowHeight = 0.16f;
        public const int SparkFires = 6, SparksPerFire = 5;

        private static readonly List<FireLight> All = new List<FireLight>();
        public static IReadOnlyList<FireLight> Lights => All;
        public static int Registered => All.Count;
        public int PixelLightsNow { get; private set; }
        public int VertexLightsNow { get; private set; }
        public Light Lantern { get; private set; }
        /// <summary>The player's carried light at full night (a soft warm pool that shows the next steps).</summary>
        public static float LanternIntensity = 0.9f, LanternRange = 15f;

        private readonly List<FireLight> _ranked = new List<FireLight>();
        private readonly List<FireLight> _near = new List<FireLight>();
        private float _nextRank, _lanternDim = 1f;
        private Mesh _sparkMesh;
        private readonly List<Vector3> _sv = new List<Vector3>(); private readonly List<Color> _sc = new List<Color>(); private readonly List<int> _st = new List<int>();
        private MaterialPropertyBlock _mpb;
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { All.Clear(); LanternIntensity = 0.9f; LanternRange = 15f; }

        internal static void Register(FireLight f) { if (!All.Contains(f)) All.Add(f); }
        internal static void Unregister(FireLight f) { All.Remove(f); }

        public static WorldLights Create(Transform parent)
        {
            var go = new GameObject("WorldLights");
            go.transform.SetParent(parent, false);
            var w = go.AddComponent<WorldLights>();
            Instance = w;
            w._mpb = new MaterialPropertyBlock();
            w.BuildLantern();
            w.BuildSparks();
            return w;
        }

        private void OnDestroy() { if (Instance == this) Instance = null; if (_sparkMesh != null) Destroy(_sparkMesh); }

        // ---------------- making lights ----------------

        /// <summary>
        /// Adds a fire light under <paramref name="parent"/> at a local position: a point light (budgeted), a ground
        /// glow and a halo. Set the fields of the returned <see cref="FireLight"/> before the next frame to tune it.
        /// </summary>
        public static FireLight Add(Transform parent, Vector3 localPos, Color color, float intensity, float range, int priority = 0, string name = "FireLight")
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.SetActive(false);
            var f = go.AddComponent<FireLight>();
            f.Color = color; f.Intensity = intensity; f.Range = range; f.Priority = priority;
            f.Seed = (Mathf.Abs(go.GetInstanceID()) % 9973) * 0.137f;
            var lgo = new GameObject("Light");
            lgo.transform.SetParent(go.transform, false);
            f.Light = lgo.AddComponent<Light>();
            f.Light.type = LightType.Point;
            f.Light.shadows = LightShadows.None;
            f.Light.color = color; f.Light.range = range; f.Light.intensity = 0f;
            f.Light.renderMode = LightRenderMode.ForceVertex;
            f.Light.enabled = false;
            go.SetActive(true);
            return f;
        }

        /// <summary>Dresses and budgets a light another package owns and drives (the dance fire): no intensity is written, only a night boost.</summary>
        public static FireLight Adopt(Light light, Color color, int priority, string name = "FireLight (adopted)")
        {
            var go = new GameObject(name);
            go.transform.SetParent(light.transform, false);
            go.SetActive(false);
            var f = go.AddComponent<FireLight>();
            f.Color = color; f.Priority = priority; f.External = true; f.Light = light; f.Range = light.range; f.ExternalRange = light.range;
            f.Intensity = 1f; f.DayLevel = 1f;
            f.Seed = (Mathf.Abs(go.GetInstanceID()) % 9973) * 0.137f;
            go.SetActive(true);
            return f;
        }

        /// <summary>A standing torch (or a brazier) with its flame and light; the Ash Clan's burn a cold green.</summary>
        public static FireLight Torch(Transform parent, Vector3 worldPos, bool ash, bool brazier = false, string name = null)
        {
            var root = new GameObject(name ?? (brazier ? "Brazier" : "Torch")).transform;
            root.SetParent(parent, false);
            root.position = worldPos;
            root.rotation = Quaternion.Euler(0, (worldPos.x * 37f + worldPos.z * 53f) % 360f, 0);
            LandLook.Part(root, "Stand", brazier ? LightShapes.Brazier() : LightShapes.Torch());
            float y = brazier ? LightShapes.BrazierFlameY : LightShapes.TorchFlameY;
            var flame = LandLook.Part(root, "Flame", ash ? LightShapes.AshFlame() : LightShapes.Flame(), LandLook.Unlit, false);
            flame.transform.localPosition = new Vector3(0, y - 0.04f, 0);
            var f = Add(root, new Vector3(0, y + 0.3f, 0), ash ? AshFire : WarmFire, brazier ? 2.0f : 1.45f, brazier ? 13f : 10f);
            f.Flame = flame.transform;
            f.FlameScale = brazier ? new Vector3(0.27f, 0.72f, 0.27f) : new Vector3(0.105f, 0.42f, 0.105f);
            f.GroundGlow = brazier ? 4.2f : 3.2f;
            f.Halo = brazier ? 1.5f : 1.0f;
            return f;
        }

        /// <summary>Firelight of the player's people (warm) and of the Ash Clan (a cold, sickly green).</summary>
        public static readonly Color WarmFire = new Color(1f, 0.60f, 0.24f), AshFire = new Color(0.36f, 1f, 0.62f);
        public static Color FireOf(int faction) => faction == World.Sim.PlayerIds.Rival ? AshFire : WarmFire;

        private void BuildLantern()
        {
            var go = new GameObject("Player lantern");
            go.transform.SetParent(transform, false);
            Lantern = go.AddComponent<Light>();
            Lantern.type = LightType.Point;
            Lantern.shadows = LightShadows.None;
            Lantern.color = new Color(1f, 0.74f, 0.48f);
            Lantern.range = LanternRange; Lantern.intensity = 0f;
            Lantern.renderMode = LightRenderMode.ForcePixel;
        }

        private void BuildSparks()
        {
            _sparkMesh = new Mesh { name = "Fire sparks" };
            _sparkMesh.MarkDynamic();
            var go = new GameObject("Sparks");
            go.transform.SetParent(transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = _sparkMesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = LightShapes.Glow;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false;
        }

        private void Dress(FireLight f)
        {
            if (f.GroundGlow > 0f && f.GroundRenderer == null)
            {
                var g = LandLook.Part(f.transform, "GroundGlow", LightShapes.RadialDisc(false), LightShapes.Glow, false);
                g.transform.position = new Vector3(f.transform.position.x, BoardSpace.GroundY + GlowHeight, f.transform.position.z);
                g.transform.rotation = Quaternion.identity;
                g.transform.localScale = Vector3.one * f.GroundGlow;
                f.GroundRenderer = g.GetComponent<Renderer>();
                f.GroundRenderer.receiveShadows = false;
            }
            if (f.Halo > 0f && f.HaloRenderer == null)
            {
                var h = LandLook.Part(f.transform, "Halo", LightShapes.RadialDisc(true), LightShapes.Glow, false);
                h.transform.localScale = Vector3.one * f.Halo;
                f.HaloTransform = h.transform;
                f.HaloRenderer = h.GetComponent<Renderer>();
                f.HaloRenderer.receiveShadows = false;
            }
            if (f.Light != null && !f.External) f.LightHome = f.Light.transform.localPosition;
        }

        // ---------------- every frame ----------------

        private void LateUpdate()
        {
            var cam = ViewSwitcher.Instance != null ? ViewSwitcher.Instance.ActiveCamera : Camera.main;
            if (cam == null) return;
            var dn = DayNight.Instance;
            float night = dn != null ? dn.Night : 0f;
            float now = Time.time, dt = Time.unscaledDeltaTime;
            var eye = cam.transform.position;
            bool ortho = cam.orthographic;

            // The lantern rides with the first-person viewpoint (not with the map camera).
            var fp = WorldBootstrap.Instance != null && WorldBootstrap.Instance.Avatar != null ? WorldBootstrap.Instance.Avatar.Camera : cam;
            if (fp != null)
            {
                var ft = fp.transform;
                // Held high and a little ahead, so the pool falls on the next step rather than at the feet.
                var ahead = ft.forward; ahead.y = 0; if (ahead.sqrMagnitude > 1e-4f) ahead.Normalize();
                Lantern.transform.position = ft.position + Vector3.up * 1.7f + ahead * 1.5f;
                float lf = 0.94f + 0.06f * Mathf.PerlinNoise(now * 2.1f, 7.7f);
                // In an encounter (the dance, the duel) the scene's own fire leads: the lantern drops back.
                var enc = EncounterHost.Instance;
                _lanternDim = Mathf.MoveTowards(_lanternDim, enc != null && enc.IsRunning ? 0.3f : 1f, dt * 2f);
                Lantern.intensity = LanternIntensity * night * lf * _lanternDim;
                Lantern.range = LanternRange;
                Lantern.enabled = night > 0.02f && !ortho;
            }

            if (now >= _nextRank) { _nextRank = now + 0.2f; Rank(eye, ortho); }

            _near.Clear();
            for (int i = 0; i < All.Count; i++)
            {
                var f = All[i];
                if (f == null) continue;
                if (f.GroundRenderer == null && f.HaloRenderer == null && f.Level == 0f && f.SlotFade == 0f) Dress(f);
                float want = f.On ? Mathf.Lerp(f.DayLevel, 1f, night) : 0f;
                f.Level = Mathf.MoveTowards(f.Level, want, dt * 1.5f);
                float lvl = f.Level;
                bool lit = lvl > 0.004f;

                float fl;
                switch (f.Look)
                {
                    case FireLight.Style.Pulse: fl = 0.72f + 0.28f * Mathf.Sin(now * 1.9f + f.Seed); break;
                    case FireLight.Style.Steady: fl = 0.9f + 0.1f * Mathf.Sin(now * 3.2f + f.Seed); break;
                    default:
                        fl = 0.74f + 0.34f * Mathf.PerlinNoise(now * 3.3f + f.Seed, f.Seed) + 0.16f * Mathf.PerlinNoise(now * 11f, f.Seed * 1.7f);
                        break;
                }
                f.Flicker = fl;

                f.SlotFade = Mathf.MoveTowards(f.SlotFade, f.Slot != Slot.None ? 1f : 0f, dt * 3f);
                var l = f.Light;
                if (l != null)
                {
                    if (f.External)
                    {
                        // The owner wrote its intensity in Update; lift it for the night and budget it.
                        bool on = f.Slot != Slot.None;
                        if (l.enabled != on) l.enabled = on;
                        // Its glow, halo and sparks follow the owner's own pump (the dance fire on the beat).
                        fl = Mathf.Clamp(l.intensity / 1.7f, 0.6f, 2f); f.Flicker = fl;
                        l.intensity *= Mathf.Lerp(1f, f.ExternalNightBoost, night);
                        if (f.ExternalNightRange > 0f) l.range = Mathf.Lerp(f.ExternalRange, f.ExternalNightRange, night);
                    }
                    else
                    {
                        bool on = lit && f.SlotFade > 0.01f;
                        if (l.enabled != on) l.enabled = on;
                        if (on)
                        {
                            l.color = f.Color;
                            l.range = f.Range;
                            l.intensity = f.Intensity * fl * lvl * f.SlotFade;
                            if (f.Look == FireLight.Style.Fire)
                                l.transform.localPosition = f.LightHome + new Vector3(Mathf.PerlinNoise(now * 5f, f.Seed) - 0.5f, 0f, Mathf.PerlinNoise(f.Seed, now * 5f) - 0.5f) * 0.14f;
                        }
                    }
                }

                if (f.Flame != null)
                {
                    bool show = lit;
                    if (f.Flame.gameObject.activeSelf != show) f.Flame.gameObject.SetActive(show);
                    if (show)
                    {
                        float h = (0.78f + 0.5f * (fl - 0.74f) * 2f) * Mathf.Lerp(0.6f, 1f, lvl);
                        f.Flame.localScale = new Vector3(f.FlameScale.x, f.FlameScale.y * h, f.FlameScale.z);
                    }
                }

                float glowNight = lvl * night;
                if (f.GroundRenderer != null)
                {
                    bool show = glowNight > 0.01f;
                    if (f.GroundRenderer.enabled != show) f.GroundRenderer.enabled = show;
                    if (show)
                    {
                        _mpb.SetColor(ColorId, new Color(f.Color.r, f.Color.g, f.Color.b, f.GroundGlowStrength * glowNight * (0.8f + 0.2f * fl)));
                        f.GroundRenderer.SetPropertyBlock(_mpb);
                    }
                }
                float dist = (f.transform.position - eye).magnitude;
                if (f.HaloRenderer != null)
                {
                    bool show = glowNight > 0.01f && !ortho && dist < HaloDistance;
                    if (f.HaloRenderer.enabled != show) f.HaloRenderer.enabled = show;
                    if (show)
                    {
                        // Turned to the eye; a far fire keeps a small steady size on screen so the next camp reads as a point of light.
                        var to = f.HaloTransform.position - eye;
                        if (to.sqrMagnitude > 0.01f) f.HaloTransform.rotation = Quaternion.LookRotation(to, Vector3.up);
                        float grow = 1f + Mathf.Clamp01((dist - 25f) / 200f) * 3.2f;
                        f.HaloTransform.localScale = Vector3.one * (f.Halo * grow);
                        float fade = 1f - Mathf.Clamp01((dist - HaloDistance * 0.6f) / (HaloDistance * 0.4f));
                        _mpb.SetColor(ColorId, new Color(f.Color.r, f.Color.g, f.Color.b, f.HaloStrength * glowNight * fl * fade));
                        f.HaloRenderer.SetPropertyBlock(_mpb);
                    }
                }
                if (f.Sparks && lit && !ortho && dist < SparkDistance && f.Look == FireLight.Style.Fire)
                {
                    f.Score = dist;
                    _near.Add(f);
                }
            }
            DrawSparks(cam, now);
        }

        private void Rank(Vector3 eye, bool ortho)
        {
            _ranked.Clear();
            for (int i = 0; i < All.Count; i++)
            {
                var f = All[i];
                if (f == null || f.Light == null) continue;
                float d = (f.transform.position - eye).magnitude;
                bool candidate = !ortho && d < LightCullDistance && (f.External ? f.Light.gameObject.activeInHierarchy : f.Dressed ? f.Level > 0.004f : f.On);
                if (!candidate) { f.Slot = Slot.None; continue; }
                // Nearer first; each priority step is worth 18 m; whoever holds a slot keeps it against a near tie.
                f.Score = d - f.Priority * 18f - (f.Slot == Slot.Pixel ? 3f : f.Slot == Slot.Vertex ? 1.5f : 0f);
                _ranked.Add(f);
            }
            _ranked.Sort((a, b) => a.Score.CompareTo(b.Score));
            int pixel = PixelBudget - 1; // one is the lantern's
            int px = 0, vx = 0;
            for (int i = 0; i < _ranked.Count; i++)
            {
                var f = _ranked[i];
                if (px < pixel) { f.Slot = Slot.Pixel; px++; }
                else if (vx < VertexBudget) { f.Slot = Slot.Vertex; vx++; }
                else f.Slot = Slot.None;
                if (f.Slot != Slot.None)
                {
                    var mode = f.Slot == Slot.Pixel ? LightRenderMode.ForcePixel : LightRenderMode.ForceVertex;
                    if (f.Light.renderMode != mode) f.Light.renderMode = mode;
                }
            }
            PixelLightsNow = px + (Lantern != null && Lantern.enabled ? 1 : 0);
            VertexLightsNow = vx;
        }

        private void DrawSparks(Camera cam, float now)
        {
            _sv.Clear(); _sc.Clear(); _st.Clear();
            if (_near.Count > SparkFires) { _near.Sort((a, b) => a.Score.CompareTo(b.Score)); }
            int n = Mathf.Min(_near.Count, SparkFires);
            var right = cam.transform.right; var up = cam.transform.up;
            for (int k = 0; k < n; k++)
            {
                var f = _near[k];
                var basePos = f.Flame != null ? f.Flame.position + Vector3.up * (f.FlameScale.y * 0.6f) : f.transform.position;
                float spread = Mathf.Max(0.12f, f.FlameScale.x * 1.4f) + f.Halo * 0.12f;
                float rise = 1.1f + f.Halo * 1.2f;
                for (int i = 0; i < SparksPerFire; i++)
                {
                    float s = f.Seed + i * 1.93f;
                    float speed = 0.33f + 0.21f * Mathf.Repeat(s * 0.71f, 1f);
                    float t = Mathf.Repeat(now * speed + s, 1f);
                    float cyc = Mathf.Floor(now * speed + s);
                    float ax = Mathf.Repeat(cyc * 0.618f + s, 1f) * Mathf.PI * 2f;
                    var p = basePos + new Vector3(Mathf.Cos(ax), 0, Mathf.Sin(ax)) * (spread * (0.3f + t))
                            + Vector3.up * (t * rise) + new Vector3(Mathf.Sin(now * 2.3f + s), 0, Mathf.Cos(now * 1.9f + s)) * (0.07f * t);
                    float a = (1f - t) * (1f - t) * f.Level * Mathf.Clamp01(t * 8f);
                    float size = 0.016f + 0.012f * Mathf.Repeat(s, 1f);
                    var col = new Color(Mathf.Lerp(f.Color.r, 1f, 0.3f), Mathf.Lerp(f.Color.g, 0.9f, 0.3f), f.Color.b * 0.7f, a);
                    int v0 = _sv.Count;
                    _sv.Add(p - right * size); _sv.Add(p + up * size * 1.6f); _sv.Add(p + right * size); _sv.Add(p - up * size * 1.6f);
                    _sc.Add(col); _sc.Add(col); _sc.Add(col); _sc.Add(col);
                    _st.Add(v0); _st.Add(v0 + 1); _st.Add(v0 + 2); _st.Add(v0); _st.Add(v0 + 2); _st.Add(v0 + 3);
                }
            }
            _sparkMesh.Clear();
            if (_sv.Count == 0) return;
            _sparkMesh.SetVertices(_sv); _sparkMesh.SetColors(_sc); _sparkMesh.SetTriangles(_st, 0);
            _sparkMesh.RecalculateBounds();
        }
    }
}
