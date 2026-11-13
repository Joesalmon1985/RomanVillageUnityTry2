using UnityEngine;
using World.Sim;
using World.Sim.Encounters.Ward;

namespace World.Game
{
    /// <summary>
    /// Scripted Ward player for AutoPlay, captures and tests: picks a seeded Ward, then casts candidate-filter weaves
    /// (the same deduction Mira uses) at a fixed cadence through the encounter's public methods (the ones the keys call).
    /// </summary>
    public sealed class WardBot : MonoBehaviour
    {
        /// <summary>Seconds between the bot's casts (it never casts while recovering).</summary>
        public float CastInterval = 2.2f;
        /// <summary>Stop casting after this many casts per duel (-1 = no limit). Lets a capture hold the duel open.</summary>
        public int MaxCastsPerDuel = -1;
        /// <summary>Fixed Ward to lock (null = seeded random).</summary>
        public int[] FixedWard;
        /// <summary>Set by tests or scripts: also return to the village once the duel is over.</summary>
        public bool AutoReturn;
        public ulong Seed = 99;
        public int CastsThisDuel { get; private set; }

        private WardDuelSession _session;
        private WardRival _solver;
        private int _observed;
        private float _nextCastAt;

        public static WardBot Attach(GameObject host)
        {
            var b = host.GetComponent<WardBot>();
            if (b == null) b = host.AddComponent<WardBot>();
            b.enabled = true;
            return b;
        }

        private void Update()
        {
            var enc = WardDuelEncounter.Current;
            if (enc == null || enc.Session == null) return;
            if (enc.Session != _session)
            {
                _session = enc.Session;
                var rng = new Rng(Seed ^ enc.Seed);
                _solver = new WardRival(new WardTuning { Loci = enc.Tuning.Loci }, enc.Pool, rng);
                _observed = 0; CastsThisDuel = 0;
                _nextCastAt = Time.unscaledTime + 0.6f;
            }
            switch (enc.Phase)
            {
                case WardDuelEncounter.Screen.Setup:
                    if (Time.unscaledTime < _nextCastAt) return;
                    var ward = FixedWard ?? _solver.ChooseWard();
                    while (enc.RemoveLast()) { }
                    foreach (var e in ward) enc.AddEssence((Essence)e);
                    enc.LockWard();
                    _nextCastAt = Time.unscaledTime + CastInterval * 0.5f;
                    break;
                case WardDuelEncounter.Screen.Active:
                    var hist = enc.Session.PlayerHistory;
                    while (_observed < hist.Count) { var h = hist[_observed++]; _solver.Observe(h.Weave, h.Fractures, h.Echoes); }
                    if (Time.unscaledTime < _nextCastAt || enc.Session.MsUntilPlayerMayCast > 0 || enc.Session.PlayerCastsLeft <= 0) return;
                    if (MaxCastsPerDuel >= 0 && CastsThisDuel >= MaxCastsPerDuel) return;
                    var weave = _solver.NextCast();
                    while (enc.RemoveLast()) { }
                    foreach (var e in weave) enc.AddEssence((Essence)e);
                    var r = enc.Cast();
                    if (r != null && r.Ok) CastsThisDuel++;
                    _nextCastAt = Time.unscaledTime + CastInterval;
                    break;
                case WardDuelEncounter.Screen.Result:
                    if (AutoReturn) enc.Return();
                    break;
            }
        }
    }
}
