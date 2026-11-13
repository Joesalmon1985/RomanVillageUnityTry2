using System;
using System.Globalization;

namespace World.Sim
{
    /// <summary>
    /// Closed list of stall/block reason codes (BUILD_PLAN section 3). Every stalled job, site,
    /// building and queue exposes exactly one of these. Do not add codes here without the owner.
    /// </summary>
    public static class Reasons
    {
        public const string NoRoad = "no_road";
        public const string NoCarrier = "no_carrier";
        public const string WaitingWare = "waiting_ware";
        public const string DemonOnRoute = "demon_on_route";
        public const string OutputFull = "output_full";
        public const string InputMissing = "input_missing";
        public const string NoPower = "no_power";
        public const string NoBatch = "no_batch";
        public const string InvalidSite = "invalid_site";
        public const string NoTurnAction = "no_turn_action";
        public const string Disabled = "disabled";

        public static readonly string[] All =
        {
            NoRoad, NoCarrier, WaitingWare, DemonOnRoute, OutputFull, InputMissing,
            NoPower, NoBatch, InvalidSite, NoTurnAction, Disabled,
        };

        public static bool IsKnown(string code) => Array.IndexOf(All, code) >= 0;

        /// <summary>
        /// Player-facing sentence for a code. args[0] is usually the subject (building/site name),
        /// args[1] a detail (ware, place). Missing args fall back to a generic sentence.
        /// Unknown codes return the code itself with args appended.
        /// </summary>
        public static string Sentence(string code, params object[] args)
        {
            int n = args == null ? 0 : args.Length;
            string a0 = n > 0 ? Str(args[0]) : null;
            string a1 = n > 1 ? Str(args[1]) : null;
            switch (code)
            {
                case NoRoad:
                    if (a1 != null) return Cap($"No road connects {a0} to {a1}.");
                    return a0 != null ? Cap($"{a0} is not connected by road.") : "There is no road connection.";
                case NoCarrier:
                    return a0 != null ? Cap($"{a0} is waiting for a free carrier.") : "No carrier is free.";
                case WaitingWare:
                    if (a1 != null) return Cap($"{a0} is waiting for {a1}.");
                    return a0 != null ? Cap($"{a0} is waiting for goods to arrive.") : "Waiting for goods to arrive.";
                case DemonOnRoute:
                    return a0 != null ? Cap($"A demon blocks the route to {a0}.") : "A demon blocks the route.";
                case OutputFull:
                    return a0 != null ? Cap($"{a0} has no room for its output.") : "There is no room for the output.";
                case InputMissing:
                    if (a1 != null) return Cap($"{a0} needs {a1} to work.");
                    return a0 != null ? Cap($"{a0} is missing an input.") : "An input is missing.";
                case NoPower:
                    return a0 != null ? Cap($"{a0} has no power.") : "There is not enough power.";
                case NoBatch:
                    // "No mature stock": a0 = subject, a1 = terrain (lower case).
                    if (a1 != null) return Cap($"{a0} is waiting for {a1} stock to grow.");
                    return a0 != null ? Cap($"{a0} is waiting for stock to grow.") : "Waiting for stock to grow.";
                case InvalidSite:
                    return a0 != null ? Cap($"{a0} cannot be built there.") : "That site is not valid.";
                case NoTurnAction:
                    return "There are no actions left this round.";
                case Disabled:
                    return a0 != null ? Cap($"{a0} is switched off.") : "That is switched off.";
                default:
                    return n == 0 ? code : code + ": " + string.Join(", ", Array.ConvertAll(args, Str));
            }
        }

        private static string Str(object o) => o == null ? "" : Convert.ToString(o, CultureInfo.InvariantCulture);

        private static string Cap(string s) =>
            string.IsNullOrEmpty(s) ? s : char.ToUpperInvariant(s[0]) + s.Substring(1);
    }
}
