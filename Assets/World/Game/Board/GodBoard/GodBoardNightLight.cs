using UnityEngine;

namespace World.Game
{
    /// <summary>
    /// Board readability at night (WU-2b): while the god's view is up (rising, on the board, falling back), a cool moonlight
    /// key light and a lift of the ambient light make terrain, pieces and legal spots read at phone size. Only the board
    /// camera renders the island in that time (the walking camera is off), and both are undone on the next frame back at the
    /// altar: <see cref="DayNight"/> re-applies its own ambient every frame, so the walking-world night never changes.
    /// Runs before the plague's storm darkening so an epidemic still darkens the board.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public sealed class GodBoardNightLight : MonoBehaviour
    {
        public static readonly Color LiftSky = new Color(0.46f, 0.50f, 0.62f), LiftEquator = new Color(0.40f, 0.42f, 0.46f), LiftGround = new Color(0.26f, 0.25f, 0.23f);
        public const float KeyAtNight = 0.6f, KeyByDay = 0.12f;

        private GodBoardDirector _d;
        private Light _key;

        /// <summary>The moonlight key is on (the board view is up).</summary>
        public bool Active { get; private set; }
        public Light Key => _key;

        public static GodBoardNightLight Attach(GodBoardDirector d)
        {
            var n = d.GetComponent<GodBoardNightLight>();
            if (n == null) n = d.gameObject.AddComponent<GodBoardNightLight>();
            n._d = d;
            return n;
        }

        private void Awake()
        {
            var go = new GameObject("Board moonlight");
            go.transform.SetParent(transform, false);
            go.transform.rotation = Quaternion.Euler(62f, 28f, 0f);
            _key = go.AddComponent<Light>();
            _key.type = LightType.Directional;
            _key.color = new Color(0.78f, 0.85f, 1f);
            _key.shadows = LightShadows.None;
            _key.cullingMask = ~(1 << AltarLedgeRig.Layer);
            _key.enabled = false;
        }

        private void LateUpdate()
        {
            if (_d == null) _d = GetComponent<GodBoardDirector>();
            Active = _d != null && _d.View != BoardView.Altar && _d.BoardCam != null && _d.BoardCam.enabled;
            _key.enabled = Active;
            if (!Active) return;
            float night = DayNight.Instance != null ? DayNight.Instance.Night : 1f;
            _key.intensity = Mathf.Lerp(KeyByDay, KeyAtNight, night);
            RenderSettings.ambientSkyColor = Max(RenderSettings.ambientSkyColor, LiftSky * night);
            RenderSettings.ambientEquatorColor = Max(RenderSettings.ambientEquatorColor, LiftEquator * night);
            RenderSettings.ambientGroundColor = Max(RenderSettings.ambientGroundColor, LiftGround * night);
        }

        private static Color Max(Color a, Color b) => new Color(Mathf.Max(a.r, b.r), Mathf.Max(a.g, b.g), Mathf.Max(a.b, b.b), a.a);
    }
}
