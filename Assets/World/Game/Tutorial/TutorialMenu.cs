using System;
using UnityEngine;

namespace World.Game
{
    /// <summary>The "Tutorials" page of the Esc menu (a system menu, not a game menu): one "Show me how again" for each game.</summary>
    public static class TutorialMenu
    {
        /// <summary>Draws the page in the menu's body rect. <paramref name="close"/> closes the menu after a replay is chosen.</summary>
        public static void Draw(Rect body, Action close)
        {
            var d = TutorialDirector.Ensure();
            var st = new GUIStyle(UiStyle.Label) { wordWrap = false, richText = true, alignment = TextAnchor.MiddleLeft };
            float rows = Mathf.Max(1, d.Games.Count);
            float rh = Mathf.Min(UiStyle.Px(64), body.height / (rows + 1f));
            for (int s = st.fontSize; s >= 10; s--) { st.fontSize = s; if (st.lineHeight <= rh * 0.8f) break; }
            float bw = Mathf.Min(UiStyle.Px(380), body.width * 0.42f);
            float y = body.y;
            GUI.Label(new Rect(body.x, y, body.width, rh), "Each game teaches its first turn once. Pick one to hear it again.", st);
            y += rh;
            foreach (var g in d.Games)
            {
                bool done = TutorialStore.IsDone(g.Id);
                string state = d.Active == g ? "playing now" : done ? "seen" : "not seen yet";
                GUI.Label(new Rect(body.x, y, body.width - bw - UiStyle.Px(16), rh), "<b>" + g.Title + "</b>  <color=#BBB0A0>" + state + "</color>", st);
                if (GUI.Button(new Rect(body.xMax - bw, y + rh * 0.08f, bw, rh * 0.84f), "Show me how again", UiStyle.Button))
                {
                    d.Replay(g.Id);
                    Hud.Notice(g.Title + ": the tutorial begins when you are there.", false);
                    close?.Invoke();
                }
                y += rh;
            }
        }
    }
}
