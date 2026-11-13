using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using NUnit.Framework;

namespace World.Sim.Tests
{
    public class RngTests
    {
        [Test]
        public void SameSeed_SameSequence()
        {
            var a = new Rng(42);
            var b = new Rng(42);
            for (int i = 0; i < 1000; i++) Assert.AreEqual(a.NextULong(), b.NextULong());
        }

        [Test]
        public void KnownVector_SplitMix64()
        {
            // Reference SplitMix64 output for seed 0.
            var r = new Rng(0);
            Assert.AreEqual(0xE220A8397B1DCDAFUL, r.NextULong());
            Assert.AreEqual(0x6E789E6AA1B965F4UL, r.NextULong());
        }

        [Test]
        public void DifferentSeeds_Differ()
        {
            Assert.AreNotEqual(new Rng(1).NextULong(), new Rng(2).NextULong());
        }

        [Test]
        public void NextInt_InRange_AndCoversAll()
        {
            var r = new Rng(7);
            var seen = new HashSet<int>();
            for (int i = 0; i < 2000; i++)
            {
                int x = r.NextInt(6);
                Assert.That(x >= 0 && x < 6);
                seen.Add(x);
                int y = r.NextRange(-3, 4);
                Assert.That(y >= -3 && y < 4);
            }
            Assert.AreEqual(6, seen.Count);
        }

        [Test]
        public void Shuffle_IsPermutation_AndDeterministic()
        {
            var a = Enumerable.Range(0, 20).ToList();
            var b = Enumerable.Range(0, 20).ToList();
            new Rng(5).Shuffle(a);
            new Rng(5).Shuffle(b);
            CollectionAssert.AreEqual(a, b);
            CollectionAssert.AreEquivalent(Enumerable.Range(0, 20), a);
            CollectionAssert.AreNotEqual(Enumerable.Range(0, 20), a);
        }

        [Test]
        public void Fork_IsStable_DoesNotAdvanceParent_AndDependsOnLabel()
        {
            var r = new Rng(99);
            ulong before = r.State;
            var f1 = r.Fork("draft");
            var f2 = r.Fork("draft");
            var g = r.Fork("demons");
            Assert.AreEqual(before, r.State);
            Assert.AreEqual(f1.NextULong(), f2.NextULong());
            Assert.AreNotEqual(new Rng(99).Fork("draft").NextULong(), g.NextULong());
        }

        [Test]
        public void State_RoundTripsThroughJson()
        {
            var r = new Rng(123);
            for (int i = 0; i < 10; i++) r.NextULong();
            var copy = JsonConvert.DeserializeObject<Rng>(JsonConvert.SerializeObject(r));
            for (int i = 0; i < 100; i++) Assert.AreEqual(r.NextInt(1000), copy.NextInt(1000));
        }
    }
}
