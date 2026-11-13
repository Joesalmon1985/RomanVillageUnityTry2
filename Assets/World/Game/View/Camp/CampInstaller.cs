using UnityEngine;

namespace World.Game
{
    /// <summary>
    /// Package D's entry point (REFACTOR_SPEC 8.0, 8.3, appendix B): dance circle, camp dressing, Mira and Sol.
    /// Called second by <see cref="WorldBootstrap"/>, after <see cref="PlayerInstaller"/>. Owner: package D.
    ///
    /// F-0: not quite empty. It spawns Mira at <see cref="CampLayout.MiraSpot"/> so the Ward duel stays startable
    /// (the spec puts Mira's spawn in D's folder); D replaces this with its own spawn of Mira and Sol.
    /// </summary>
    public static class CampInstaller
    {
        public static void Install(WorldBootstrap boot)
        {
            var layout = new CampLayout(boot.Sim.State.Board);
            var spawn = WalkLattice.ToWorld(layout.Spawn);
            var mira = Npc.Spawn(NpcIds.Mira, "Mira", "Wizard. Practises Ward duels by the home camp.",
                "Fancy a practice duel? I promise to go easy. Mostly.", new Color(0.45f, 0.25f, 0.70f),
                layout.MiraSpot, BoardSpace.YawOf(spawn - layout.MiraSpot));
            mira.Lines.Add("A Ward is three loci of essence. Guess mine before I guess yours.");
            mira.Lines.Add("Each cast tells you fractures, echoes and fades. Nothing more.");
            mira.Options.Add(DialogueOption.Encounter("Practice duel", EncounterDirectory.WardMira));
            mira.Options.Add(DialogueOption.Leave("Maybe later"));
        }
    }
}
