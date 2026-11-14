using System;

namespace World.Sim.Campaign
{
    /// <summary>
    /// Entry point of the campaign module (A.5): one system at <see cref="SystemOrder.Campaign"/> (1250, after Strength)
    /// and a handler for every command of A.3. The behaviour lives in <c>CampaignRuntime</c> (Sim/Campaign/Runtime,
    /// package I1); this file only wires it. With <see cref="WorldState.Campaign"/> null the system does nothing and
    /// every command except StartCampaign is refused with no_campaign.
    /// </summary>
    public static class CampaignModule
    {
        /// <summary>Every campaign command type, in A.3 order.</summary>
        public static readonly Type[] CommandTypes =
        {
            typeof(StartCampaign), typeof(SetCompany), typeof(AcceptLead), typeof(DeclineLead), typeof(AdviseMira),
            typeof(SuggestScout), typeof(CommitEncounter), typeof(WithdrawFromEncounter), typeof(ResolveEncounter),
            typeof(RestartAttempt), typeof(ConfirmRestore), typeof(DevSetRivalProgress), typeof(DevRivalNextStep),
            typeof(DevSetCustody), typeof(DevSetCompanion),
        };

        /// <summary>
        /// Registers the system and the handlers. Idempotent (a second call on the same sim does nothing), so both
        /// <c>WorldSimFactory.CreateNew</c> and <c>SaveCodec.CreateSim</c> may call it.
        /// </summary>
        public static void Register(WorldSim sim)
        {
            if (sim == null || sim.HasHandler(typeof(StartCampaign))) return;
            sim.AddSystem(SystemOrder.Campaign, new CampaignSystem());
            sim.RegisterHandler(new CampaignHandler<StartCampaign>());
            sim.RegisterHandler(new CampaignHandler<SetCompany>());
            sim.RegisterHandler(new CampaignHandler<AcceptLead>());
            sim.RegisterHandler(new CampaignHandler<DeclineLead>());
            sim.RegisterHandler(new CampaignHandler<AdviseMira>());
            sim.RegisterHandler(new CampaignHandler<SuggestScout>());
            sim.RegisterHandler(new CampaignHandler<CommitEncounter>());
            sim.RegisterHandler(new CampaignHandler<WithdrawFromEncounter>());
            sim.RegisterHandler(new CampaignHandler<ResolveEncounter>());
            sim.RegisterHandler(new CampaignHandler<RestartAttempt>());
            sim.RegisterHandler(new CampaignHandler<ConfirmRestore>());
            sim.RegisterHandler(new CampaignHandler<DevSetRivalProgress>());
            sim.RegisterHandler(new CampaignHandler<DevRivalNextStep>());
            sim.RegisterHandler(new CampaignHandler<DevSetCustody>());
            sim.RegisterHandler(new CampaignHandler<DevSetCompanion>());
        }
    }

    /// <summary>The campaign slot (1250). Does nothing while WorldState.Campaign is null or not Running.</summary>
    internal sealed class CampaignSystem : ISimSystem
    {
        public void Tick(WorldState s, EventBuffer ev) => CampaignRuntime.Tick(s, ev);
    }

    /// <summary>Forwards one command type to CampaignRuntime (Validate never mutates; Apply re-checks first).</summary>
    internal sealed class CampaignHandler<T> : CommandHandler<T> where T : Command
    {
        protected override CommandResult Validate(WorldState s, T c) => CampaignRuntime.Check(s, c);

        protected override CommandResult Apply(WorldState s, T c, EventBuffer ev) => CampaignRuntime.Apply(s, c, ev);
    }
}
