using System;
using System.Collections.Generic;

namespace World.Game
{
    /// <summary>
    /// The tuning bell's tap-along (RHY-TUNE): eight evenly spaced clicks, the player taps along, the offset is the
    /// median of (tap - click). Positive = the player's taps arrive late, exactly the sign of <c>RhythmTuning.CalibrationMs</c>.
    /// Pure logic on any clock; the encounter feeds it the audio clock.
    /// </summary>
    public sealed class TapAlongCalibration
    {
        public const int ClickCount = 8;
        public const int MinMatched = 4;
        public const double MaxOffsetMs = 250;

        public readonly double[] ClickTimes = new double[ClickCount];
        public double Interval { get; }
        private readonly List<double> _offsets = new List<double>();
        private readonly bool[] _used = new bool[ClickCount];

        public TapAlongCalibration(double firstClick, double interval = 0.5)
        {
            Interval = interval;
            for (int i = 0; i < ClickCount; i++) ClickTimes[i] = firstClick + i * interval;
        }

        public int Matched => _offsets.Count;

        /// <summary>A tap at time t: matched to the nearest unmatched click within a quarter of the interval's half-width (0.5 interval each side). False if it matches none.</summary>
        public bool Tap(double t)
        {
            int best = -1; double bd = Interval * 0.5;
            for (int i = 0; i < ClickCount; i++)
            {
                if (_used[i]) continue;
                double d = Math.Abs(t - ClickTimes[i]);
                if (d < bd) { bd = d; best = i; }
            }
            if (best < 0) return false;
            _used[best] = true;
            _offsets.Add(t - ClickTimes[best]);
            return true;
        }

        /// <summary>True once the last click is over (a tap this late could not match any).</summary>
        public bool IsOver(double now) => now > ClickTimes[ClickCount - 1] + Interval * 0.5;

        /// <summary>The offset in whole milliseconds (median, clamped to +-250), or false with fewer than <see cref="MinMatched"/> matched taps.</summary>
        public bool TryResult(out double offsetMs)
        {
            offsetMs = 0;
            if (_offsets.Count < MinMatched) return false;
            var o = new List<double>(_offsets); o.Sort();
            int n = o.Count;
            double med = n % 2 == 1 ? o[n / 2] : 0.5 * (o[n / 2 - 1] + o[n / 2]);
            offsetMs = Math.Max(-MaxOffsetMs, Math.Min(MaxOffsetMs, Math.Round(med * 1000.0)));
            return true;
        }
    }
}
