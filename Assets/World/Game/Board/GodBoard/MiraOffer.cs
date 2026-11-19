using World.Sim;
using World.Sim.Campaign;

namespace World.Game
{
    /// <summary>
    /// Mira's offer in a god board game (G1-7; PRD_GODBOARD CC-1): "Set out for the crystals" from the first minute, while no
    /// campaign runs. Choosing it is <c>StartCampaign</c> (through <see cref="CampaignStart"/>). The practice duel stays.
    /// </summary>
    public static class MiraOffer
    {
        public const string Label = "Set out for the crystals";

        /// <summary>True when the offer is open: a god board game with no campaign yet.</summary>
        public static bool Available(WorldState s) =>
            s != null && s.Ruleset == Ruleset.GodBoard && s.Campaign == null && CampaignRuleset.GodBoard(s);

        public static void Wire(Npc mira)
        {
            mira.DialogueBuilder = n =>
            {
                var s = WorldBootstrap.Instance != null && WorldBootstrap.Instance.Sim != null ? WorldBootstrap.Instance.Sim.State : null;
                n.Options.Clear();
                n.Lines.Clear();
                if (Available(s))
                {
                    n.Lines.Add("The crystals are waiting. Shall we set out?");
                    n.Options.Add(DialogueOption.Do(Label, () => CampaignStart.StartNow(CampaignStart.DefaultPack)));
                }
                else n.Lines.Add("A Ward is three loci of essence. Guess mine before I guess yours.");
                n.Options.Add(DialogueOption.Encounter("Practice duel", EncounterDirectory.WardMira));
                n.Options.Add(DialogueOption.Leave("Maybe later"));
            };
        }
    }
}
