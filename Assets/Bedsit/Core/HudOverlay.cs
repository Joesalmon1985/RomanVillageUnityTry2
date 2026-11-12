using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Bedsit
{
    /// <summary>
    /// The only 2D layer: three faint chevrons that show where the three controls live (left, right, top-centre), a
    /// press flash, and the full-screen fade used for the wake-up. No text, no menus.
    /// </summary>
    public class HudOverlay : MonoBehaviour
    {
        public InputRouter Input;
        public bool ShowHints = true;

        Image _left, _right, _top, _fade;
        float _flashLeft, _flashRight, _flashTop;
        float _hintAlpha = 0f, _hintTarget = 0f;

        public float FadeAlpha => _fade != null ? _fade.color.a : 1f;

        void Awake()
        {
            var canvasGo = new GameObject("HudCanvas");
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720);
            scaler.matchWidthOrHeight = 0.5f;

            var chevron = BuildChevronSprite();
            _left = MakeImage(canvasGo.transform, "HintLeft", chevron, new Vector2(0.11f, 0.5f), 90f);
            _right = MakeImage(canvasGo.transform, "HintRight", chevron, new Vector2(0.89f, 0.5f), -90f);
            _top = MakeImage(canvasGo.transform, "HintTop", chevron, new Vector2(0.5f, 0.84f), 0f);

            _fade = new GameObject("Fade").AddComponent<Image>();
            _fade.transform.SetParent(canvasGo.transform, false);
            _fade.color = Color.black;                                  // starts fully dark
            _fade.raycastTarget = false;
            var rt = _fade.rectTransform;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
        }

        void Start()
        {
            if (Input != null) Input.ActionTriggered += OnAction;
        }

        void OnDestroy()
        {
            if (Input != null) Input.ActionTriggered -= OnAction;
        }

        void OnAction(PlayerAction a)
        {
            if (a == PlayerAction.TurnLeft) _flashLeft = 1f;
            else if (a == PlayerAction.TurnRight) _flashRight = 1f;
            else if (a == PlayerAction.Interact) _flashTop = 1f;
        }

        public void SetHintsVisible(bool v) => _hintTarget = v ? 1f : 0f;

        void Update()
        {
            _hintAlpha = Mathf.MoveTowards(_hintAlpha, ShowHints ? _hintTarget : 0f, Time.deltaTime * 1.5f);
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * 2.2f);
            _flashLeft = Mathf.MoveTowards(_flashLeft, 0f, Time.deltaTime * 3f);
            _flashRight = Mathf.MoveTowards(_flashRight, 0f, Time.deltaTime * 3f);
            _flashTop = Mathf.MoveTowards(_flashTop, 0f, Time.deltaTime * 3f);
            SetAlpha(_left, _hintAlpha * (0.16f + 0.10f * pulse) + _flashLeft * 0.5f);
            SetAlpha(_right, _hintAlpha * (0.16f + 0.10f * pulse) + _flashRight * 0.5f);
            SetAlpha(_top, _hintAlpha * (0.20f + 0.14f * pulse) + _flashTop * 0.5f);
        }

        static void SetAlpha(Image img, float a) { var c = img.color; c.a = Mathf.Clamp01(a); img.color = c; }

        public IEnumerator FadeTo(float alpha, float seconds)
        {
            float from = _fade.color.a, t = 0f;
            while (t < seconds)
            {
                t += Time.deltaTime;
                SetAlpha(_fade, Mathf.Lerp(from, alpha, Mathf.SmoothStep(0f, 1f, t / seconds)));
                yield return null;
            }
            SetAlpha(_fade, alpha);
        }

        public void SetFade(float alpha) => SetAlpha(_fade, alpha);

        static Image MakeImage(Transform parent, string name, Sprite sprite, Vector2 anchor, float zRot)
        {
            var img = new GameObject(name).AddComponent<Image>();
            img.transform.SetParent(parent, false);
            img.sprite = sprite;
            img.color = new Color(1f, 1f, 1f, 0f);
            img.raycastTarget = false;
            var rt = img.rectTransform;
            rt.anchorMin = rt.anchorMax = anchor;
            rt.sizeDelta = new Vector2(72, 72);
            rt.localRotation = Quaternion.Euler(0, 0, zRot);
            return img;
        }

        static Sprite BuildChevronSprite()
        {
            const int N = 64;
            var tex = new Texture2D(N, N, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[N * N];
            // an upward chevron (^) of thickness ~9px
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    float dx = Mathf.Abs(x - (N - 1) * 0.5f);
                    float centreY = N * 0.72f - dx * 0.9f;                 // apex at top, slopes down
                    float d = Mathf.Abs(y - centreY);
                    float a = Mathf.Clamp01(5f - d) * (dx < N * 0.42f ? 1f : 0f);
                    px[y * N + x] = new Color32(255, 255, 255, (byte)(a * 255));
                }
            tex.SetPixels32(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, N, N), new Vector2(0.5f, 0.5f), 100f);
        }
    }
}
