namespace World.Sim
{
    public sealed partial class WorldState
    {
        /// <summary>The crystal campaign's run state (PRD_CRYSTALS A.2). Null = no campaign: then nothing of it has any effect.</summary>
        public Campaign.CampaignState Campaign;
    }
}

namespace World.Sim.Campaign
{
    /// <summary>
    /// SHELL created by PRD_AGES H1-0 so the world save (v5) owns the slot; handed to PRD_CRYSTALS I0 at tag ages-H1-0,
    /// which fills it (appendix A.2 of PRD_CRYSTALS). Changes inside it are versioned by FormatVersion, not by the world
    /// save version (crystals N-5).
    /// </summary>
    public sealed partial class CampaignState
    {
        /// <summary>Version of this state's own shape (1 = the first shape I0 lands).</summary>
        public int FormatVersion = 1;
    }
}
