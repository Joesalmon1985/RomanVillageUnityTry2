using System.Collections.Generic;

namespace World.Sim.Campaign
{
    /// <summary>
    /// Checks a definition before any campaign can start (6.5). Any problem with IsError blocks StartCampaign
    /// (invalid_definition with the first problem's sentence). OWNED BY PACKAGE I2 from tag cry-I0-sim
    /// (Sim/Campaign/Validation/**; J3 and G2 extend it); the signature is binding (A.5).
    /// I0 STUB: only the checks every later check needs: a definition exists, its format version is known, and its
    /// required parts are there. Everything else in 6.5 is I2's.
    /// </summary>
    public static class CampaignValidator
    {
        public static IReadOnlyList<Problem> Validate(CampaignDefinition d)
        {
            var list = new List<Problem>();
            if (d == null)
            {
                list.Add(new Problem(ProblemCodes.Missing, "there is no campaign definition."));
                return list;
            }
            if (d.FormatVersion < 1 || d.FormatVersion > CampaignDefinition.CurrentFormatVersion)
                list.Add(new Problem(ProblemCodes.BadVersion, "its format version " + d.FormatVersion + " is not one this game reads.", d.Id ?? ""));
            if (d.Roles == null || d.Companions == null || d.Locations == null || d.Interiors == null || d.Connections == null
                || d.Crystals == null || d.Towers == null || d.Rival == null || d.Clues == null || d.Leads == null || d.Choices == null
                || d.Encounters == null || d.Events == null || d.Endings == null || d.Lines == null || d.Tuning == null)
            {
                list.Add(new Problem(ProblemCodes.Missing, "a part of the definition is missing.", d.Id ?? ""));
                return list;
            }
            return list;
        }
    }
}
