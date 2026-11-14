using System.Collections.Generic;

namespace World.Sim.Campaign
{
    /// <summary>
    /// Checks a definition before any campaign can start (PRD_CRYSTALS 6.5). Any problem with IsError blocks
    /// StartCampaign (invalid_definition with the first error's sentence). Owned by package I2
    /// (Sim/Campaign/Validation/**; J3 and G2 extend it); the A.5 signature is binding.
    ///
    /// Groups, in the order they run (Docs/CAMPAIGN_FORMAT.md, "What the validator checks"): form (version, ids,
    /// names, references, guards, tokens, lengths), roles and companions, crystals and towers, places on the island,
    /// interiors (size, connection, links, ways out), routes (every site discoverable, with and without companions;
    /// every theft recoverable; every restoration order the pack supports reaches the finale; both endings), and the
    /// narrative rules (no dead speaker, no crystal place as fixed text).
    ///
    /// A pack with Status Partial is a pack under construction: the route checks only warn (IsError false), so the
    /// developer start can play the routes that exist. Every other check is an error in every pack. The scripted-player
    /// simulation checks of 6.5 need the runtime and are not here (proof package I4, then G2).
    /// </summary>
    public static class CampaignValidator
    {
        /// <summary>Longest spoken line, in characters as written (tokens included). A bubble shows it in about three lines.</summary>
        public const int MaxLineChars = 140;

        /// <summary>Longest reply (a choice's answer line): it must fit a reply button.</summary>
        public const int MaxReplyChars = 32;

        public static IReadOnlyList<Problem> Validate(CampaignDefinition d) => Validate(d, null);

        /// <summary>As <see cref="Validate(CampaignDefinition)"/>, with options (for example the Ward profile ids that exist).</summary>
        public static IReadOnlyList<Problem> Validate(CampaignDefinition d, ValidationOptions options)
        {
            var list = new List<Problem>();
            if (d == null)
            {
                list.Add(new Problem(ProblemCodes.Missing, "there is no campaign definition."));
                return list;
            }
            if (d.FormatVersion < 1 || d.FormatVersion > CampaignDefinition.CurrentFormatVersion)
            {
                list.Add(new Problem(ProblemCodes.BadVersion, "its format version " + d.FormatVersion + " is not one this game reads.", d.Id ?? ""));
                return list;
            }
            if (d.Roles == null || d.Companions == null || d.Locations == null || d.Interiors == null || d.Connections == null
                || d.Crystals == null || d.Towers == null || d.Rival == null || d.Clues == null || d.Leads == null || d.Choices == null
                || d.Encounters == null || d.Events == null || d.Endings == null || d.Lines == null || d.Tuning == null)
            {
                list.Add(new Problem(ProblemCodes.Missing, "a part of the definition is missing.", d.Id ?? ""));
                return list;
            }
            new ValidationRun(d, options ?? new ValidationOptions(), list).Run();
            return list;
        }

        /// <summary>The errors only (what blocks a start).</summary>
        public static List<Problem> Errors(IReadOnlyList<Problem> problems)
        {
            var e = new List<Problem>();
            if (problems != null) foreach (var p in problems) if (p != null && p.IsError) e.Add(p);
            return e;
        }
    }

    /// <summary>Optional inputs of <see cref="CampaignValidator.Validate(CampaignDefinition, ValidationOptions)"/>.</summary>
    public sealed class ValidationOptions
    {
        /// <summary>The Ward opponent profile ids that exist (StreamingAssets/world/ward_opponents.json); null = not checked.</summary>
        public ICollection<string> KnownProfiles;
    }

    /// <summary>
    /// The validator's codes beyond the seventeen of 6.5 (those are <see cref="ProblemCodes"/>, whose list is pinned
    /// by I0's surface test). Each is used by one check of Docs/CAMPAIGN_FORMAT.md.
    /// </summary>
    public static class ValidationCodes
    {
        /// <summary>A role, companion, location, interior, marker, crystal, encounter or lead has no display name.</summary>
        public const string NoName = "no_name";
        /// <summary>A line is longer than <see cref="CampaignValidator.MaxLineChars"/>, or a reply than <see cref="CampaignValidator.MaxReplyChars"/>.</summary>
        public const string LineTooLong = "line_too_long";
        /// <summary>A guard is not in the grammar of <see cref="CampaignGuards"/>.</summary>
        public const string BadGuard = "bad_guard";
        /// <summary>A token is not in the grammar of <see cref="CampaignTokens"/>, or a brace is not closed.</summary>
        public const string BadToken = "bad_token";
        /// <summary>One of the five fixed roles (mira, prince, rival_leader, final_enemy, wizard) is not bound to an actor.</summary>
        public const string RoleUnbound = "role_unbound";
        /// <summary>Not exactly four crystals, one per element.</summary>
        public const string CrystalCount = "crystal_count";
        /// <summary>No crystal starts underground, or a crystal's Underground flag does not match where its site is.</summary>
        public const string Underground = "underground";
        /// <summary>An interior is too big, has too many floor cells, or uses its legend wrongly.</summary>
        public const string BadInterior = "bad_interior";
        /// <summary>A choice has no answers, more than three, or not exactly one correct answer.</summary>
        public const string BadChoice = "bad_choice";
        /// <summary>An encounter lacks a profile, a stage it needs, or a binding it needs; or there are two finales.</summary>
        public const string BadEncounter = "bad_encounter";
        /// <summary>Some restoration order cannot reach the finale at the fourth tower, or there is no finale.</summary>
        public const string FinaleUnreachable = "finale_unreachable";
        /// <summary>The restored ending (Outcome Won) or the terminal defeat (Outcome Lost) is missing or unreachable.</summary>
        public const string NoEnding = "no_ending";
        /// <summary>Warning: a reference into an interior uses a location other than the interior's declared Location.</summary>
        public const string InteriorLocation = "interior_location";

        public static readonly string[] All =
        {
            NoName, LineTooLong, BadGuard, BadToken, RoleUnbound, CrystalCount, Underground, BadInterior, BadChoice, BadEncounter,
            FinaleUnreachable, NoEnding, InteriorLocation,
        };
    }
}
