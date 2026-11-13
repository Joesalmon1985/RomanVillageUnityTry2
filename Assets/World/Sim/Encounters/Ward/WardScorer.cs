using System;

namespace World.Sim.Encounters.Ward
{
    /// <summary>Mastermind-style feedback; agrees with the source score_attack for equal lengths.</summary>
    public static class WardScorer
    {
        public static (int fractures, int echoes, int fades, bool broken) Score(int[] ward, int[] weave)
        {
            if (ward == null) throw new ArgumentNullException(nameof(ward));
            if (weave == null) throw new ArgumentNullException(nameof(weave));
            if (ward.Length != weave.Length) throw new ArgumentException("ward and weave must have equal length");
            int n = ward.Length;
            int fractures = 0;
            var fractured = new bool[n];
            int max = 0;
            for (int i = 0; i < n; i++)
            {
                if (weave[i] == ward[i]) { fractures++; fractured[i] = true; }
                if (ward[i] > max) max = ward[i];
                if (weave[i] > max) max = weave[i];
            }
            var remaining = new int[max + 1];
            for (int i = 0; i < n; i++) if (!fractured[i] && ward[i] >= 0) remaining[ward[i]]++;
            int echoes = 0;
            for (int i = 0; i < n; i++)
            {
                if (fractured[i]) continue;
                int g = weave[i];
                if (g >= 0 && remaining[g] > 0) { echoes++; remaining[g]--; }
            }
            return (fractures, echoes, n - fractures - echoes, n > 0 && fractures == n);
        }
    }
}
