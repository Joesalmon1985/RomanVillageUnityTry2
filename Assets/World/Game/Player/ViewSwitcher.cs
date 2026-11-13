using System;
using UnityEngine;

namespace World.Game
{
    public enum ViewMode { FirstPerson, God }

    /// <summary>
    /// The F1 developer view (REFACTOR_SPEC 2.D): F1 switches between the player's viewpoint and the God View camera with
    /// the developer toolbar (<see cref="GodToolbar"/>); normal play never shows it. Tab is the map
    /// (<see cref="MapScreen"/>), not the God View. The player is never moved: in God View the walker holds a
    /// "GodView" lock and shows its marker. The cursor is always free (no mouse look).
    /// </summary>
    public sealed class ViewSwitcher : MonoBehaviour
    {
        public const string GodLock = "GodView";
        public const KeyCode DevKey = KeyCode.F1;
        public static ViewSwitcher Instance { get; private set; }

        public PlayerWalker Avatar { get; private set; }
        public PlayerWalker Walker => Avatar;
        public GodCamera God { get; private set; }
        public ViewMode CurrentView { get; private set; } = ViewMode.FirstPerson;
        public Camera ActiveCamera => CurrentView == ViewMode.God ? God.Camera : Avatar.Camera;
        private readonly System.Collections.Generic.List<GameObject> _godOnly = new System.Collections.Generic.List<GameObject>();
        private bool _godFocusSet;
        private bool _fogInFirstPerson = true;

        public event Action<ViewMode> ViewChanged;

        /// <summary>When false, F1 is ignored.</summary>
        public bool DevKeyEnabled = true;
        /// <summary>Old name of <see cref="DevKeyEnabled"/>.</summary>
        public bool TabEnabled { get => DevKeyEnabled; set => DevKeyEnabled = value; }

        public void Bind(PlayerWalker walker, GodCamera god)
        {
            Instance = this;
            Avatar = walker; God = god;
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
                if (MapScreen.IsOpen) MapScreen.Close();
                _fogInFirstPerson = RenderSettings.fog;
                Avatar.AddLock(GodLock);
                if (!_godFocusSet) { God.SetFocus(Avatar.Position, GodCamera.DefaultSize); _godFocusSet = true; }
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

        private void Update()
        {
            if (DevKeyEnabled && Input.GetKeyDown(DevKey) && !Hud.MenuOpen) Toggle();
        }

        private void LateUpdate()
        {
            if (Cursor.lockState != CursorLockMode.None) Cursor.lockState = CursorLockMode.None;
            if (!Cursor.visible) Cursor.visible = true;
        }
    }
}
