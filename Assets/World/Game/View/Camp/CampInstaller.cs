using UnityEngine;

namespace World.Game
{
    /// <summary>
    /// Package D's entry point (REFACTOR_SPEC 8.0, 8.3, appendix B), called second by <see cref="WorldBootstrap"/>:
    /// the home camp's dressing (<see cref="CampBuilder"/>), the dance circle (<see cref="DanceCircle"/>, its fire and
    /// drums registered as static blockers with <see cref="WalkGrid"/>), Mira with her Ward duel and Sol the drummer
    /// who offers the dance. Everything is placed by <see cref="CampLayout"/>. Owner: package D.
    /// </summary>
    public static class CampInstaller
    {
        public const string BlockerOwner = "dance_circle";

        public static Transform Root { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Root = null;

        public static void Install(WorldBootstrap boot)
        {
            var layout = new CampLayout(boot.Sim.State.Board);
            Root = new GameObject("HomeCamp").transform;
            Root.SetParent(boot.transform, false);
            CampBuilder.Build(Root, layout);
            DanceCircle.Build(Root, layout);

            WalkGrid.RemoveStaticBlockers(BlockerOwner);
            WalkGrid.AddStaticBlocker(layout.DanceCircleCentre, CampLayout.FireBlockRadius, BlockerOwner);
            for (int p = 1; p <= 4; p++) WalkGrid.AddStaticBlocker(layout.DrumPosition(p), CampLayout.DrumBlockRadius, BlockerOwner);

            SpawnMira(layout);
            SpawnSol(layout);
        }

        private static void SpawnMira(CampLayout layout)
        {
            var mira = Npc.Spawn(NpcIds.Mira, "Mira", "Wizard. Practises Ward duels by the home camp.",
                "Fancy a practice duel? I promise to go easy. Mostly.", new Color(0.45f, 0.25f, 0.70f),
                layout.MiraSpot, layout.MiraYaw);
            mira.Lines.Add("A Ward is three loci of essence. Guess mine before I guess yours.");
            mira.Lines.Add("Each cast tells you fractures, echoes and fades. Nothing more.");
            mira.Options.Add(DialogueOption.Encounter("Practice duel", EncounterDirectory.WardMira));
            mira.Options.Add(DialogueOption.Leave("Maybe later"));
        }

        private static void SpawnSol(CampLayout layout)
        {
            var sol = Npc.Spawn(NpcIds.Host, "Sol", "Drummer. Keeps the beat at the dance circle.",
                "The fire's lit. Dance with us?", new Color(0.75f, 0.30f, 0.12f),
                layout.DrummerSpot, layout.DrummerYaw);
            sol.Lines.Add("Four drums round the fire. Embers fly from the fire to the drum you strike: D F J K, left to right.");
            sol.Lines.Add("Hit as the ember lands. Keep the beat and the bass joins in; at the end, leap the fire or call the spirits.");
            sol.Options.Add(DialogueOption.Encounter("Dance at the fire", EncounterDirectory.RhythmParty));
            sol.Options.Add(DialogueOption.Leave("Not now"));
            // The dance is played from the dancer's spot in front of the drums, facing the fire.
            sol.Stage = new EncounterStage(layout.DancerSpot, layout.DancerYaw);
        }
    }
}
