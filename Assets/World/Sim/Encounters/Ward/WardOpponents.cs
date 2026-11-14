using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace World.Sim.Encounters.Ward
{
    /// <summary>
    /// The opponent profile table (PRD_CRYSTALS 5.6, I3). <see cref="BuiltIn"/> is the shipped table and equals
    /// StreamingAssets/world/ward_opponents.json (a test pins that); the game may load the file with
    /// <see cref="Parse"/> to retune without code. Ids: practice_mira, keeper (generic), keeper_earth, keeper_fire,
    /// keeper_water, keeper_air, rival_hesk, friendly_orrin, finale_unweaver.
    /// </summary>
    public sealed class WardOpponents
    {
        public sealed class FileShape { public List<WardOpponentProfile> Profiles = new List<WardOpponentProfile>(); }

        readonly Dictionary<string, WardOpponentProfile> _byId = new Dictionary<string, WardOpponentProfile>(StringComparer.Ordinal);
        public readonly List<WardOpponentProfile> All = new List<WardOpponentProfile>();

        public const string PracticeMira = "practice_mira", Keeper = "keeper", RivalHesk = "rival_hesk",
            FriendlyOrrin = "friendly_orrin", FinaleUnweaver = "finale_unweaver";
        public static readonly string[] KeeperIds = { "keeper_earth", "keeper_fire", "keeper_water", "keeper_air" };

        public WardOpponents(IEnumerable<WardOpponentProfile> profiles)
        {
            foreach (var p in profiles)
            {
                if (p == null || string.IsNullOrEmpty(p.Id)) continue;
                if (_byId.ContainsKey(p.Id)) throw new ArgumentException("duplicate opponent profile id " + p.Id);
                _byId[p.Id] = p;
                All.Add(p);
            }
        }

        /// <summary>Parses the profile file ({ "Profiles": [ ... ] }). Throws on malformed JSON or duplicate ids.</summary>
        public static WardOpponents Parse(string json)
        {
            var f = JsonConvert.DeserializeObject<FileShape>(json) ?? new FileShape();
            return new WardOpponents(f.Profiles);
        }

        public bool TryGet(string id, out WardOpponentProfile p) => _byId.TryGetValue(id ?? "", out p);

        /// <summary>The profile for an id; an unknown id falls back to the generic keeper, and then to practice_mira.</summary>
        public WardOpponentProfile GetOrDefault(string id)
        {
            if (TryGet(id, out var p)) return p;
            if (TryGet(Keeper, out p)) return p;
            return TryGet(PracticeMira, out p) ? p : null;
        }

        /// <summary>A ready duel spec: the profile's tuning over baseTuning (Practice when null), its pool (or the practice format when it has none, using completedDuelsVsMira) and the seed.</summary>
        public WardDuelSpec Spec(string id, ulong seed, int completedDuelsVsMira = 0, WardTuning baseTuning = null)
        {
            var p = GetOrDefault(id);
            var t = WardTuning.For(p, baseTuning);
            var pool = p != null && p.Pool != null && p.Pool.Length > 0 ? (Essence[])p.Pool.Clone() : WardFormat.For(completedDuelsVsMira, t);
            return new WardDuelSpec { Tuning = t, Pool = pool, Seed = seed };
        }

        static readonly Essence[] Three = { Essence.Flame, Essence.Frost, Essence.Vine };
        static readonly Essence[] Four = { Essence.Flame, Essence.Frost, Essence.Vine, Essence.Light };

        static WardOpponentProfile P(string id, string name, int f0, int f1, int t0, int t1, int mistakes, Essence[] pool,
            string tempo, int anim, string note) => new WardOpponentProfile
        {
            Id = id, DisplayName = name, FirstCastMinMs = f0, FirstCastMaxMs = f1, ThinkMinMs = t0, ThinkMaxMs = t1,
            MistakeRatePercent = mistakes, Pool = pool == null ? null : (Essence[])pool.Clone(), CastTempo = tempo, CastAnimMs = anim, StyleNote = note,
        };

        // Tuned numbers (see Docs/WARD_API.md, "Opponent profiles").
        const int KeeperFirstMin = 4000, KeeperFirstMax = 7000, KeeperThinkMin = 7000, KeeperThinkMax = 11000, KeeperMistakes = 15;
        const int RivalFirstMin = 3500, RivalFirstMax = 6500, RivalThinkMin = 6500, RivalThinkMax = 10500, RivalMistakes = 5;
        const int FinaleFirstMin = 3000, FinaleFirstMax = 6000, FinaleThinkMin = 5500, FinaleThinkMax = 9500, FinaleMistakes = 0;

        /// <summary>The shipped table; the numbers are the measured ones (Docs/WARD_API.md).</summary>
        public static WardOpponents BuiltIn()
        {
            return new WardOpponents(new[]
            {
                P(PracticeMira, "Mira", 3000, 6000, 6000, 10000, 0, null, "measured", 1200, "as shipped"),
                P(Keeper, "Keeper", KeeperFirstMin, KeeperFirstMax, KeeperThinkMin, KeeperThinkMax, KeeperMistakes, Three, "slow", 1500, "unhurried, guarded"),
                P("keeper_earth", "Dolm", KeeperFirstMin, KeeperFirstMax, KeeperThinkMin, KeeperThinkMax, KeeperMistakes, Three, "slow", 1600, "stone-heavy, plants each cast"),
                P("keeper_fire", "Brask", KeeperFirstMin, KeeperFirstMax, KeeperThinkMin, KeeperThinkMax, KeeperMistakes, Three, "brisk", 1100, "sharp, impatient gestures"),
                P("keeper_water", "Nual", KeeperFirstMin, KeeperFirstMax, KeeperThinkMin, KeeperThinkMax, KeeperMistakes, Three, "measured", 1400, "flowing, sweeping arcs"),
                P("keeper_air", "Pike", KeeperFirstMin, KeeperFirstMax, KeeperThinkMin, KeeperThinkMax, KeeperMistakes, Three, "brisk", 1000, "light, flicking casts"),
                P(RivalHesk, "Hesk Varr", RivalFirstMin, RivalFirstMax, RivalThinkMin, RivalThinkMax, RivalMistakes, Three, "measured", 1200, "an ember-sworn duellist, deliberate"),
                P(FriendlyOrrin, "Orrin", 5000, 8000, 9000, 13000, 25, Three, "slow", 1800, "a teacher: shows each cast"),
                P(FinaleUnweaver, "the Unweaver, in Hesk Varr", FinaleFirstMin, FinaleFirstMax, FinaleThinkMin, FinaleThinkMax, FinaleMistakes, Four, "hasty", 900, "the loose knot: quick, cold, exact"),
            });
        }
    }
}
