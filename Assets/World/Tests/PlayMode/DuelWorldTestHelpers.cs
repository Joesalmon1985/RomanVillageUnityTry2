using System.Collections;
using NUnit.Framework;
using UnityEngine;
using World.Sim.Encounters.Ward;

namespace World.Game.Tests
{
    /// <summary>WU-7 test helpers: taps on the duel's world objects at their real screen points, with the waits the WU-0 tap guard needs.</summary>
    public static class DuelWorldTaps
    {
        /// <summary>One tap on a world object (the stone's index is its place in the pool). Fails if the object is not offered now.</summary>
        public static IEnumerator Tap(WardDuelEncounter e, DuelTarget t, int index = 0)
        {
            var p = e.Props.ScreenPointOf(t, index);
            Assert.IsTrue(p.HasValue, t + " " + index + " is offered in phase " + e.Phase);
            var got = e.TapAt(p.Value);
            Assert.AreEqual(t, got, "the tap at " + p.Value + " reached " + got + ", not " + t);
            yield return null;
        }

        /// <summary>Preview, then confirm (waits out the 0.25 s double-tap rule), then waits out the 0.6 s guard after the command.</summary>
        public static IEnumerator PreviewConfirm(WardDuelEncounter e, DuelTarget t)
        {
            yield return Tap(e, t);
            Assert.AreEqual(WorldActionState.Preview, e.Props.Machine.State, t + " previews on the first tap");
            yield return new WaitForSecondsRealtime(0.32f);
            yield return Tap(e, t);
            yield return new WaitForSecondsRealtime(0.65f);
        }

        /// <summary>Builds the hand from stones: one tap per essence (the essence must be in the pool).</summary>
        public static IEnumerator Build(WardDuelEncounter e, params Essence[] essences)
        {
            foreach (var x in essences)
            {
                int i = System.Array.IndexOf(e.Pool, x);
                Assert.GreaterOrEqual(i, 0, x + " is in the pool");
                yield return Tap(e, DuelTarget.Stone, i);
            }
        }

        public static void AssertHand(WardDuelEncounter e, params Essence[] want)
        {
            Assert.AreEqual(want.Length, e.Building.Count, "orbs in the hand");
            for (int i = 0; i < want.Length; i++) Assert.AreEqual((int)want[i], e.Building[i], "orb " + i);
        }
    }
}
