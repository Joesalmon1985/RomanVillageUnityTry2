using System.IO;
using UnityEngine;
using World.Sim;
using World.Sim.Campaign;

namespace World.Game
{
    /// <summary>
    /// Developer helpers of the expedition (package I6): load a shipped pack, start the campaign on the running world
    /// (the Slice 1 test start is the developer start <c>dev.historic</c> plus <c>StartCampaign</c>), send the Ash
    /// expedition ahead, settle a pending duel. Commands only; nothing here writes state. Package I8's
    /// <c>CampaignStart</c> and F1 campaign tool replace the starting part at integration.
    /// </summary>
    public static class ExpeditionDev
    {
        public const string AuthoredPack = "authored_v1", SkeletonPack = "skeleton_v1";

        private static WorldBootstrap B => WorldBootstrap.Instance;

        public static string PackFolder(string pack) => Path.Combine(Application.streamingAssetsPath, "world", "campaigns", pack);

        /// <summary>Reads a shipped pack from StreamingAssets (a folder on disk: Windows and the editor).</summary>
        public static CampaignDefinition LoadPack(string pack) => CampaignLoader.LoadDirectory(PackFolder(pack));

        /// <summary>Sends <c>StartCampaign</c> with a shipped pack. Applied at the next tick.</summary>
        public static CommandResult StartCampaign(string pack = AuthoredPack) => StartCampaign(LoadPack(pack));

        public static CommandResult StartCampaign(CampaignDefinition definition)
        {
            if (B == null || B.Sim == null) return CommandResult.Reject("no_world", "The world is not running.");
            var r = B.Sim.Submit(new StartCampaign { Definition = definition });
            if (!r.Ok) Debug.LogWarning("[Expedition] StartCampaign refused: " + r.Message);
            return r;
        }

        /// <summary>The Ash expedition finishes its current step now (march: it seizes; carry: the crystal is on the altar).</summary>
        public static CommandResult AshNextStep() => B.Sim.Submit(new DevRivalNextStep());

        /// <summary>Settles the pending duel with a result and applies it at once (the world clock steps one tick).</summary>
        public static CommandResult Resolve(DuelResult result)
        {
            var a = CampaignApi.Attempt(B.Sim.State);
            if (a == null) return CampaignRefusals.Reject(CampaignRefusals.UnknownAttempt);
            var r = B.Sim.Submit(new ResolveEncounter { AttemptId = a.Id, Result = result });
            if (r.Ok) B.Clock.StepOnce();
            return r;
        }
    }
}
