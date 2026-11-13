using System.Collections.Generic;

namespace World.Sim.Encounters.Ward
{
    /// <summary>Mira: candidate-filter deduction (keeps every Ward consistent with feedback on her own casts).</summary>
    public sealed class WardRival
    {
        readonly WardTuning _tuning;
        readonly int[] _pool;
        readonly Rng _rng;
        readonly List<int[]> _all = new List<int[]>();
        List<int[]> _candidates;
        int _castCount;

        public WardRival(WardTuning tuning, Essence[] pool, Rng rng)
        {
            _tuning = tuning;
            _rng = rng;
            _pool = new int[pool.Length];
            for (int i = 0; i < pool.Length; i++) _pool[i] = (int)pool[i];
            Build(new int[tuning.Loci], 0);
            _candidates = new List<int[]>(_all);
        }

        void Build(int[] cur, int idx)
        {
            if (idx == cur.Length) { _all.Add((int[])cur.Clone()); return; }
            for (int i = 0; i < _pool.Length; i++) { cur[idx] = _pool[i]; Build(cur, idx + 1); }
        }

        public int CandidateCount => _candidates.Count;

        /// <summary>Mira's own secret Ward, uniformly random.</summary>
        public int[] ChooseWard() => (int[])_all[_rng.NextInt(_all.Count)].Clone();

        public int[] RandomLegalWeave()
        {
            var w = new int[_tuning.Loci];
            for (int i = 0; i < w.Length; i++) w[i] = _pool[_rng.NextInt(_pool.Length)];
            return w;
        }

        public int[] NextCast()
        {
            int[] weave;
            if (_tuning.MiraMistakeRatePercent > 0 && _rng.Chance(_tuning.MiraMistakeRatePercent, 100))
            {
                weave = RandomLegalWeave();
            }
            else if (_castCount == 0 && _pool.Length >= 2 && _tuning.Loci >= 2)
            {
                weave = Opening();
            }
            else
            {
                if (_candidates.Count == 0) _candidates = new List<int[]>(_all);
                weave = (int[])_candidates[_rng.NextInt(_candidates.Count)].Clone();
            }
            _castCount++;
            return weave;
        }

        // Two distinct essences split across the loci (1..Loci-1 slots of the first, rest the second), shuffled.
        int[] Opening()
        {
            int a = _pool[_rng.NextInt(_pool.Length)];
            int b;
            do { b = _pool[_rng.NextInt(_pool.Length)]; } while (b == a);
            int nA = _rng.NextRange(1, _tuning.Loci);
            var w = new int[_tuning.Loci];
            for (int i = 0; i < w.Length; i++) w[i] = i < nA ? a : b;
            _rng.Shuffle(w);
            return w;
        }

        /// <summary>Feed back the totals for a weave she cast. An empty candidate set resets on the next cast.</summary>
        public void Observe(int[] weave, int fractures, int echoes)
        {
            var kept = new List<int[]>();
            for (int i = 0; i < _candidates.Count; i++)
            {
                var s = WardScorer.Score(_candidates[i], weave);
                if (s.fractures == fractures && s.echoes == echoes) kept.Add(_candidates[i]);
            }
            _candidates = kept;
        }
    }
}
