using System.Collections.Generic;
using UnityEngine;
using World.Sim;

namespace World.Game
{
    /// <summary>
    /// F1 "Land" tool (god board G1-4): rebuilds the land as a <see cref="HexDressingView"/> from
    /// <c>BoardGenerator.CreateDealtBoard(seed)</c> so the five abundance levels, the ports and the production pulse can be
    /// checked for any seed without a god board game. It only hides the live terrain; "Back" shows it again. View only:
    /// the simulation and its board are untouched.
    /// </summary>
    public static class DressingDevTool
    {
        public const string ToolId = "land";
        public static ulong Seed = 1;
        private static GameObject _hidden;

        public static void Register()
        {
            GodToolbar.Register(new GodTool
            {
                Id = ToolId, Label = "Land", Order = 98, Hotkey = KeyCode.L, PanelWidth = 520, DrawPanel = Panel,
                TipText = () => "Dress the land from a dealt board seed (abundance levels, ports, pulse).",
            });
        }

        /// <summary>Builds the dressed land for a seed, hiding the live terrain. Returns the view.</summary>
        public static HexDressingView Show(ulong seed)
        {
            Seed = seed;
            var boot = WorldBootstrap.Instance;
            if (_hidden == null && boot != null)
            {
                var t = boot.transform.Find("Terrain");
                if (t != null) { _hidden = t.gameObject; _hidden.SetActive(false); }
            }
            return HexDressingView.Create(BoardGenerator.CreateDealtBoard(seed), boot != null ? boot.transform : null);
        }

        /// <summary>Removes the preview and shows the live terrain again.</summary>
        public static void Back()
        {
            if (HexDressingView.Instance != null) Object.Destroy(HexDressingView.Instance.gameObject);
            if (_hidden != null) _hidden.SetActive(true);
            _hidden = null;
        }

        /// <summary>Demo of the production pulse: every hex of the number that was "rolled" (picks a random number among those dealt).</summary>
        public static void PulseDemo()
        {
            var v = HexDressingView.Instance;
            if (v == null) return;
            var b = v.Board;
            int n = 2 + (int)(Time.frameCount % 11); if (n == 7) n = 8;
            var hexes = new List<int>();
            for (int h = 0; h < b.HexNumber.Length; h++) if (b.HexNumber[h] == n) hexes.Add(h);
            HexDressingView.Pulse(hexes);
        }

        private static void Panel(GodTool t, Rect a)
        {
            float x = a.x + UiStyle.Px(14), w = a.width - UiStyle.Px(28), y = a.y + UiStyle.Px(10), bh = UiStyle.Px(40);
            GUI.Label(new Rect(x, y, w, UiStyle.Px(50)), "Seed " + Seed + ". Rebuilds only the view from a dealt board; the game is unchanged.", UiStyle.Small);
            y += UiStyle.Px(54);
            float half = (w - UiStyle.Px(8)) / 2f;
            if (GUI.Button(new Rect(x, y, half, bh), "Seed -1", UiStyle.Button)) Show(Seed > 1 ? Seed - 1 : 1);
            if (GUI.Button(new Rect(x + half + UiStyle.Px(8), y, half, bh), "Seed +1", UiStyle.Button)) Show(Seed + 1);
            y += bh + UiStyle.Px(6);
            if (GUI.Button(new Rect(x, y, w, bh), "Dress land from seed", UiStyle.Button)) Show(Seed);
            y += bh + UiStyle.Px(6);
            if (GUI.Button(new Rect(x, y, w, bh), "Pulse producing hexes (demo)", UiStyle.Button)) PulseDemo();
            y += bh + UiStyle.Px(6);
            if (GUI.Button(new Rect(x, y, w, bh), "Back to the live land", UiStyle.Button)) Back();
        }
    }
}
