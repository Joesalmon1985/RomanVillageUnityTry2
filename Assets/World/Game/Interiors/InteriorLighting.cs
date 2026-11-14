using UnityEngine;

namespace World.Game
{
    /// <summary>
    /// Underground lighting (package I5, consistent with Docs/LIGHTING.md): while the player is in an interior the sky,
    /// the sun and the moon are switched off (the day-night cycle never lights an interior), the ambient light is a
    /// faint cold grey, black fog closes the far end of every passage, and the player's lantern is always lit, by day
    /// too. The interiors' own fires (braziers at the ways out, crystal light, the Ash altar's green fires) go through
    /// <see cref="WorldLights"/> and its per-pixel budget. Runs after the lighting package each frame and changes
    /// nothing on the surface.
    /// </summary>
    [DefaultExecutionOrder(1000)]
    public sealed class InteriorLighting : MonoBehaviour
    {
        public static readonly Color AmbientSky = new Color(0.040f, 0.040f, 0.048f);
        public static readonly Color AmbientEquator = new Color(0.032f, 0.030f, 0.032f);
        public static readonly Color AmbientGround = new Color(0.020f, 0.018f, 0.017f);
        public static readonly Color FogColour = new Color(0.010f, 0.009f, 0.009f);
        public const float FogStart = 5f, FogEnd = 36f;
        public const float LanternIntensity = 1.25f, LanternRange = 17f;

        public static InteriorLighting Instance { get; private set; }
        /// <summary>True while the underground look is applied (the player is in an interior).</summary>
        public bool Underground { get; private set; }
        /// <summary>
        /// At the end of the last frame's lighting: no sun, moon or sky drew (true on the surface only if that was so
        /// there, which it never is). What tests read, since the day-night cycle switches its lights back on in Update.
        /// </summary>
        public bool SkyDark { get; private set; }

        private Renderer _dome;
        private Renderer[] _sky;

        private void Awake() { Instance = this; }
        private void OnDestroy() { if (Instance == this) Instance = null; }

        private void LateUpdate()
        {
            bool under = Interiors.Current != null;
            if (under != Underground) { Underground = under; Toggle(under); }
            if (!under) { SkyDark = false; return; }
            var dn = DayNight.Instance;
            if (dn != null)
            {
                if (dn.Sun != null && dn.Sun.enabled) dn.Sun.enabled = false;
                if (dn.Moon != null && dn.Moon.enabled) dn.Moon.enabled = false;
            }
            var boot = WorldBootstrap.Instance;
            if (boot != null && boot.Sun != null && boot.Sun.enabled && (dn == null || (boot.Sun != dn.Sun && boot.Sun != dn.Moon))) boot.Sun.enabled = false;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = AmbientSky;
            RenderSettings.ambientEquatorColor = AmbientEquator;
            RenderSettings.ambientGroundColor = AmbientGround;
            RenderSettings.fogColor = FogColour;
            RenderSettings.fogStartDistance = FogStart;
            RenderSettings.fogEndDistance = FogEnd;
            if (boot != null && boot.MainCamera != null) boot.MainCamera.backgroundColor = FogColour;
            if (_sky == null) FindSky();
            if (_dome != null && _dome.enabled) _dome.enabled = false;
            if (_sky != null) foreach (var r in _sky) if (r != null && r.enabled) r.enabled = false;
            var wl = WorldLights.Instance;
            if (wl != null && wl.Lantern != null)
            {
                var enc = EncounterHost.Instance;
                float dim = enc != null && enc.IsRunning ? 0.45f : 1f;
                wl.Lantern.enabled = true;
                wl.Lantern.intensity = LanternIntensity * dim * (0.95f + 0.05f * Mathf.PerlinNoise(Time.time * 2.3f, 3.1f));
                wl.Lantern.range = LanternRange;
            }
            SkyDark = (dn == null || ((dn.Sun == null || !dn.Sun.enabled) && (dn.Moon == null || !dn.Moon.enabled)))
                      && (_dome == null || !_dome.enabled) && RenderSettings.ambientSkyColor.maxColorComponent < 0.06f;
        }

        private void FindSky()
        {
            var boot = WorldBootstrap.Instance;
            if (boot == null) return;
            var dome = boot.transform.Find("SkyDome");
            _dome = dome != null ? dome.GetComponent<Renderer>() : null;
            var dn = DayNight.Instance;
            var skyRoot = dn != null ? dn.transform.Find("Sky lights") : null;
            _sky = skyRoot != null ? skyRoot.GetComponentsInChildren<Renderer>(true) : new Renderer[0];
        }

        private void Toggle(bool under)
        {
            if (under) return;
            // Back on the surface: the dome is the only thing the day-night cycle does not switch back on by itself.
            if (_dome != null) _dome.enabled = true;
            DayNight.Instance?.Refresh();
        }
    }
}
