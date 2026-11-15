using System.Collections;
using UnityEngine;
using World.Sim;
using World.Sim.Campaign;
using World.Sim.Eras;

namespace World.Game
{
    /// <summary>
    /// The F1 developer tool "Campaign" (PRD_CRYSTALS 8.1, package I8): start the crystal campaign on the authored pack
    /// from <c>dev.historic</c>, send the Ash expedition ahead, jump Mira to her next marker (the world clock stepped
    /// fast until she arrives), kill or revive a companion. Commands only (the sim's dev commands and the clock).
    /// </summary>
    public static class CampaignDevTool
    {
        public const string ToolId = "campaign";
        private static string s_last;

        private static WorldBootstrap B => WorldBootstrap.Instance;
        private static WorldState S => B != null && B.Sim != null ? B.Sim.State : null;

        public static void Register()
        {
            GodToolbar.Register(new GodTool
            {
                Id = ToolId, Label = "Campaign", Order = 96, Hotkey = KeyCode.K, PanelWidth = 540, DrawPanel = Panel,
                TipText = () => "Crystal campaign: start it on dev.historic, send the Ash ahead, move Mira on, kill or revive a companion.",
            });
        }

        private static void Panel(GodTool t, Rect a)
        {
            float x = a.x + UiStyle.Px(14), w = a.width - UiStyle.Px(28), y = a.y + UiStyle.Px(10), bh = UiStyle.Px(38), gap = UiStyle.Px(6);
            var s = S;
            var c = s?.Campaign;
            if (c == null)
            {
                GUI.Label(new Rect(x, y, w, UiStyle.Px(56)), "No campaign. Start the Earth route on the authored pack (the world is rebuilt on dev.historic first).", UiStyle.Small);
                y += UiStyle.Px(60);
                if (GUI.Button(new Rect(x, y, w, bh), "Start the campaign (authored_v1)", UiStyle.Button))
                    s_last = CampaignStart.Begin(CampaignStart.DefaultPack) ? "Starting..." : CampaignStart.LastError;
                y += bh + gap;
            }
            else
            {
                var p = c.Party;
                GUI.Label(new Rect(x, y, w, UiStyle.Px(56)), c.Definition.Id + ": Mira " + p.Mode + " at " + CampaignTokens.PlaceName(c.Definition, p.At)
                          + (p.PlayerPresent ? " (with you)" : " (you are away)") + ". " + CampaignApi.Crystal(s, Element.Earth).Custody + ".", UiStyle.Small);
                y += UiStyle.Px(60);
                if (GUI.Button(new Rect(x, y, w, bh), "Send the Ash expedition ahead (one step)", UiStyle.Button))
                {
                    var r = B.Sim.Submit(new DevRivalNextStep());
                    s_last = r.Ok ? "The Ash expedition finishes its step." : r.Message;
                }
                y += bh + gap;
                if (GUI.Button(new Rect(x, y, w, bh), "Mira to her next marker", UiStyle.Button))
                    B.StartCoroutine(MiraOn());
                y += bh + gap;
                float half = (w - gap) / 2f;
                foreach (var k in c.Companions)
                {
                    var def = c.Definition.Companion(k.Id);
                    string name = def != null ? def.Name : k.Id;
                    GUI.Label(new Rect(x, y, half, bh), name + (k.Alive ? "" : " (fallen)"), UiStyle.Small);
                    if (GUI.Button(new Rect(x + half + gap, y, half, bh * 0.85f), k.Alive ? "Kill" : "Revive", UiStyle.Button))
                    {
                        var r = B.Sim.Submit(new DevSetCompanion { Id = k.Id, Alive = !k.Alive });
                        s_last = r.Ok ? name + (k.Alive ? " is dead." : " lives again.") : r.Message;
                    }
                    y += bh * 0.9f;
                }
            }
            if (s_last != null) GUI.Label(new Rect(x, a.yMax - UiStyle.Px(60), w, UiStyle.Px(56)), s_last, UiStyle.Small);
        }

        /// <summary>
        /// Steps the world clock fast (paused, by hand) until Mira arrives at the marker she is heading for; when she is
        /// idle, her best lead is accepted first. A player in her company is put beside her (underground through I5).
        /// </summary>
        public static IEnumerator MiraOn(int maxTicks = 6000)
        {
            const string Hold = "CampaignDevTool";
            var s = S;
            if (s?.Campaign == null) yield break;
            var c = s.Campaign;
            if (c.Party.Mode != PartyMode.Travelling)
            {
                var top = CampaignApi.TopLead(s);
                if (top == null) { s_last = "Mira has no lead to follow."; yield break; }
                var r = B.Sim.Submit(new AcceptLead { LeadId = top.Id });
                if (!r.Ok) { s_last = r.Message; yield break; }
                B.Clock.StepOnce();
            }
            B.Clock.Pause(Hold);
            long end = s.Tick + maxTicks;
            try
            {
                while (s.Tick < end && c.Party.Mode == PartyMode.Travelling)
                {
                    for (int n = 0; n < 200 && s.Tick < end && c.Party.Mode == PartyMode.Travelling; n++) B.Clock.StepOnce();
                    yield return null;
                }
            }
            finally { B.Clock.Resume(Hold); }
            s_last = "Mira is " + c.Party.Mode + " at " + CampaignTokens.PlaceName(c.Definition, c.Party.At) + ".";
            if (c.Party.PlayerPresent) Interiors.Enter(c.Party.At);
            Debug.Log("[Campaign] dev: " + s_last);
        }
    }
}
