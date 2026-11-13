using System;
using World.Sim;
using World.Sim.Economy;

namespace World.Game
{
    /// <summary>
    /// The single path from UI (God View tools, signs, the quartermaster, the bell, AutoPlay) to the sim:
    /// <c>WorldBootstrap.Instance.Sim.Submit</c>, with an accepted/refused toast carrying the sim's sentence.
    /// Strategy and military panels should submit through <see cref="Submit"/> too, so refusals look the same.
    /// </summary>
    public static class GodCommands
    {
        /// <summary>Result of the last submission (tests and scripts read it).</summary>
        public static CommandResult LastResult { get; private set; }
        public static Command LastCommand { get; private set; }
        /// <summary>Raised after every submission with the command and its result.</summary>
        public static event Action<Command, CommandResult> Submitted;

        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { Submitted = null; LastCommand = null; LastResult = default; }

        private static WorldSim Sim => WorldBootstrap.Instance != null ? WorldBootstrap.Instance.Sim : null;

        /// <summary>Validate only (UI previews). The command is stamped as the human player's. Does not change state.</summary>
        public static CommandResult Check(Command c)
        {
            var sim = Sim;
            if (sim == null) return CommandResult.Reject("no_sim", "The village is not running.");
            c.PlayerId = PlayerIds.Human;
            return sim.Check(c);
        }

        /// <summary>Submit with feedback: a toast with <paramref name="okText"/> or the refusal sentence.</summary>
        public static CommandResult Submit(Command c, string okText, bool toast = true)
        {
            var sim = Sim;
            if (sim == null) return CommandResult.Reject("no_sim", "The village is not running.");
            c.PlayerId = PlayerIds.Human;
            var r = sim.Submit(c);
            LastResult = r; LastCommand = c;
            if (toast)
            {
                if (r.Ok) EconomyHud.Toast(okText, false);
                else EconomyHud.Toast(string.IsNullOrEmpty(r.Message) ? r.Reason : r.Message, true);
            }
            Submitted?.Invoke(c, r);
            return r;
        }

        private static string Name(BuildingType t) => Sim != null ? EconomyApi.Def(Sim.State, t).Name : t.ToString();
        private static string NameOf(int id)
        {
            var s = Sim; if (s == null) return "Building";
            var b = EconomyApi.GetBuilding(s.State, id);
            return b == null ? "Building" : EconomyApi.Describe(s.State, id).Title;
        }

        public static CommandResult PlaceBuilding(BuildingType type, int vertex) =>
            Submit(new PlaceBuilding { Type = type, Vertex = vertex }, Name(type) + " site planned. Carriers will bring its materials.");

        public static CommandResult PlaceRoad(int edge) => Submit(new PlaceRoad { Edge = edge }, "Road segment built.");
        public static CommandResult RemoveRoad(int edge) => Submit(new RemoveRoad { Edge = edge }, "Road segment removed.");
        public static CommandResult CancelSite(int id) => Submit(new CancelSite { BuildingId = id }, NameOf(id) + " cancelled; its materials go back to the storehouse.");
        public static CommandResult SetPriority(int id, Priority p) => Submit(new SetPriority { BuildingId = id, Priority = p }, NameOf(id) + " priority set to " + p.ToString().ToLowerInvariant() + ".");
        public static CommandResult Toggle(int id, bool enabled) => Submit(new ToggleBuilding { BuildingId = id, Enabled = enabled }, NameOf(id) + (enabled ? " switched on." : " switched off."));

        /// <summary>
        /// End the round. Refusals (the round is still starting, a draft is pending, the game is over) come back as the
        /// sim's sentence in a toast; nothing else happens.
        /// </summary>
        public static CommandResult EndRound() => Submit(new EndRound(), "Round ended. The next round starts now.");

        /// <summary>Whether EndRound would be accepted now, with the refusal sentence if not (for buttons and the bell).</summary>
        public static CommandResult CheckEndRound() => Check(new EndRound());
    }
}
