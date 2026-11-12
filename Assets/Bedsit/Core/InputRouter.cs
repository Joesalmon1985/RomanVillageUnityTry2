using System;
using UnityEngine;

namespace Bedsit
{
    /// <summary>
    /// Routes Windows mouse clicks, touch taps and (for convenience) keyboard keys to exactly three actions.
    /// Touches arrive through Unity's touch-to-mouse simulation, so one code path serves both platforms.
    /// Gameplay listens to the event and never reads the Input API itself.
    /// </summary>
    public class InputRouter : MonoBehaviour
    {
        public event Action<PlayerAction> ActionTriggered;
        /// <summary>A rhythm pad (0..3) was tapped. Only raised while <see cref="PadsEnabled"/> (RHYTHM mode).</summary>
        public event Action<int> PadTriggered;
        public bool PadsEnabled;
        /// <summary>Optional: offered every click/tap (after the rhythm pads) BEFORE the three screen-region controls; return true to consume it
        /// (Act One uses it to tidy a piece of clutter under the pointer).</summary>
        public Func<Vector2, bool> ClickHandler;

        /// <summary>When true all input is ignored (e.g. during the wake-up sequence).</summary>
        public bool Locked { get; set; }

        /// <summary>Last action raised; the HUD uses it to flash a cue.</summary>
        public PlayerAction LastAction { get; private set; }

        public bool KeyboardEnabled = true;

        void Update()
        {
            if (Locked) return;
            if (PadsEnabled)
            {
                if (Input.GetMouseButtonDown(0))
                {
                    int pad = InputRegions.ClassifyPadScreen(Input.mousePosition, Screen.width, Screen.height);
                    if (pad >= 0) { PadTriggered?.Invoke(pad); return; }
                }
                // keyboard pads D F J K; turning is then Left/Right arrows (A still turns left)
                if (Input.GetKeyDown(KeyCode.D)) PadTriggered?.Invoke(0);
                if (Input.GetKeyDown(KeyCode.F)) PadTriggered?.Invoke(1);
                if (Input.GetKeyDown(KeyCode.J)) PadTriggered?.Invoke(2);
                if (Input.GetKeyDown(KeyCode.K)) PadTriggered?.Invoke(3);
            }
            if (Input.GetMouseButtonDown(0))
            {
                if (ClickHandler != null && ClickHandler(Input.mousePosition)) return;
                var action = InputRegions.ClassifyScreen(Input.mousePosition, Screen.width, Screen.height);
                if (action != PlayerAction.None) Raise(action);
            }
            if (!KeyboardEnabled) return;
            if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A)) Raise(PlayerAction.TurnLeft);
            else if (Input.GetKeyDown(KeyCode.RightArrow) || (!PadsEnabled && Input.GetKeyDown(KeyCode.D))) Raise(PlayerAction.TurnRight);
            else if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.Space)) Raise(PlayerAction.Interact);
        }

        /// <summary>Raise a pad tap programmatically (tests and bots).</summary>
        public void RaisePad(int pad) { if (!Locked && PadsEnabled) PadTriggered?.Invoke(pad); }

        /// <summary>Raise an action programmatically (tests and automation).</summary>
        public void Raise(PlayerAction action)
        {
            if (Locked || action == PlayerAction.None) return;
            LastAction = action;
            ActionTriggered?.Invoke(action);
        }
    }
}
