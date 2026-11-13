using System;
using UnityEngine;

namespace World.Game
{
    public enum ViewMode { FirstPerson, God }

    /// <summary>
    /// Tab toggles first person and God View. The avatar is never moved: in God View it holds a
    /// "GodView" lock (no move, no look) and shows a marker; returning releases the lock. Also owns
    /// the cursor lock: locked only in first person while the avatar may look around.
    /// </summary>
    public sealed class ViewSwitcher : MonoBehaviour
    {
        public const string GodLock = "GodView";
        public static ViewSwitcher Instance { get; private set; }

        public FirstPersonAvatar Avatar { get; private set; }
        public GodCamera God { get; private set; }
        public ViewMode CurrentView { get; private set; } = ViewMode.FirstPerson;
        public Camera ActiveCamera => CurrentView == ViewMode.God ? God.Camera : Avatar.Camera;
        /// <summary>Objects shown only in God View (hex labels...). Add your own with <see cref="AddGodViewOnly"/>.</summary>
        private readonly System.Collections.Generic.List<GameObject> _godOnly = new System.Collections.Generic.List<GameObject>();
        private bool _godFocusSet;
        private bool _fogInFirstPerson = true;

        public event Action<ViewMode> ViewChanged;

        /// <summary>When false, Tab is ignored (e.g. while a text field is focused).</summary>
        public bool TabEnabled = true;

        public void Bind(FirstPersonAvatar avatar, GodCamera god)
        {
            Instance = this;
            Avatar = avatar; God = god;
        }

        private void OnDestroy() { if (Instance == this) Instance = null; }

        public void AddGodViewOnly(GameObject go)
        {
            if (go == null) return;
            _godOnly.Add(go);
            go.SetActive(CurrentView == ViewMode.God);
        }

        public void Toggle() => SetView(CurrentView == ViewMode.God ? ViewMode.FirstPerson : ViewMode.God);

        public void SetView(ViewMode mode)
        {
            if (mode == CurrentView) return;
            CurrentView = mode;
            bool god = mode == ViewMode.God;
            if (god)
            {
                _fogInFirstPerson = RenderSettings.fog;
                Avatar.AddLock(GodLock);
                if (!_godFocusSet) { God.HomeFocus = DefaultFocus(); God.FrameHome(); _godFocusSet = true; }
                RenderSettings.fog = false;
            }
            else
            {
                Avatar.RemoveLock(GodLock);
                RenderSettings.fog = _fogInFirstPerson;
            }
            Avatar.Camera.enabled = !god;
            God.Camera.enabled = god;
            Avatar.Marker.SetActive(god);
            foreach (var g in _godOnly) if (g != null) g.SetActive(god);
            ViewChanged?.Invoke(mode);
        }

        private Vector3 DefaultFocus()
        {
            var b = WorldBootstrap.Instance;
            if (b == null || b.GodCam == null) return Avatar.Position;
            return b.GodCam.HomeFocus;
        }

        private void Update()
        {
            if (TabEnabled && Input.GetKeyDown(KeyCode.Tab) && !Hud.MenuOpen) Toggle();
        }

        private void LateUpdate()
        {
            bool wantLock = CurrentView == ViewMode.FirstPerson && Avatar.CanLook && Application.isFocused;
            var want = wantLock ? CursorLockMode.Locked : CursorLockMode.None;
            if (Cursor.lockState != want) Cursor.lockState = want;
            Cursor.visible = !wantLock;
        }
    }
}
