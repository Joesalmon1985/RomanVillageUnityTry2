using System.IO;
using System.Linq;
using Newtonsoft.Json;
using NUnit.Framework;
using World.Sim.Eras;
using World.Sim.Encounters.Council;

namespace World.Sim.Tests
{
    public class CouncilContentTests
    {
        static readonly CouncilContent C = CouncilContent.BuiltIn;

        [Test]
        public void BuiltInContent_EqualsTheJsonFile()
        {
            var fromFile = CouncilContent.Parse(File.ReadAllText(CouncilTestKit.FindContentFile()));
            Assert.AreEqual(fromFile.ToJson(), C.ToJson());
        }

        [Test]
        public void ElevenDisputes_MatchTheBriefsLeadsAndProps()
        {
            var expected = new (int track, string role, string historicProp)[]
            {
                (2, "ferryman", "broken oar"), (3, "neighbour", "boundary rope"), (4, "forester", "sapling"), (5, "carpenter", "split beam"),
                (6, "healer", "empty jar"), (7, "storekeeper", "empty grain crate"), (8, "miner", "cracked pickaxe"), (9, "miller", "grain sack"),
                (10, "well keeper", "bucket"), (11, "carter", "cart wheel"), (12, "reeve", "deed scroll"),
            };
            Assert.AreEqual(11, C.Disputes.Count);
            foreach (var x in expected)
            {
                var d = C.Dispute(x.track);
                Assert.IsNotNull(d, "track " + x.track);
                Assert.AreEqual(x.role, d.Role);
                Assert.AreEqual(x.historicProp, d.Props["historic"]);
                Assert.IsFalse(string.IsNullOrWhiteSpace(d.Silhouette));
                Assert.IsFalse(string.IsNullOrWhiteSpace(d.FallbackTitle));
                Assert.IsFalse(string.IsNullOrWhiteSpace(d.Pattern));
            }
        }

        [Test]
        public void EveryDispute_HasAtLeastTwoVariantsOfEachLine_ShortAndFillable()
        {
            foreach (var d in C.Disputes)
                foreach (var kind in CouncilContent.LineKinds)
                {
                    var lines = d.Lines[kind];
                    Assert.GreaterOrEqual(lines.Length, 2, d.Id + "." + kind);
                    Assert.AreEqual(lines.Length, lines.Distinct().Count());
                    foreach (var l in lines)
                    {
                        Assert.LessOrEqual(l.Length, 100, l);
                        string filled = CouncilContent.Fill(l, "Aldis", "Bera", "cracked paddle", "chief", "Corin");
                        Assert.IsFalse(filled.Contains("{"), "unfilled token in: " + l);
                    }
                }
        }

        [Test]
        public void PrehistoricProps_ReplaceThoseOutOfPlace_AndKeepTheSilhouetteIdea()
        {
            foreach (var d in C.Disputes)
            {
                Assert.IsTrue(d.Props.ContainsKey("prehistoric") && d.Props.ContainsKey("historic"));
                Assert.AreEqual(C.Prop(d, Era.Prehistoric), d.Props["prehistoric"]);
                Assert.AreEqual(C.Prop(d, Era.Modern), d.Props["historic"], "later ages use the historic prop");
            }
            Assert.AreEqual("tally stick", C.Prop(C.Dispute(12), Era.Prehistoric));
            Assert.AreEqual("log roller", C.Prop(C.Dispute(11), Era.Prehistoric));
            Assert.AreEqual("cracked stone pick", C.Prop(C.Dispute(8), Era.Prehistoric));
        }

        [Test]
        public void Offices_AreAgeAppropriate()
        {
            Assert.AreEqual("chief", C.Office(Era.Prehistoric));
            Assert.AreEqual("reeve", C.Office(Era.Historic));
            Assert.AreEqual("mayor", C.Office(Era.Modern));
            Assert.AreEqual("mayor", C.Office(Era.Future));
        }

        [Test]
        public void ListeningLines_AreEvenlySpread_Short_AndVaried()
        {
            Assert.GreaterOrEqual(C.ListeningLines.Count, 60);
            var counts = new int[5];
            foreach (var l in C.ListeningLines)
            {
                counts[(int)l.Tag]++;
                Assert.LessOrEqual(l.Text.Length, 90, l.Text);
                Assert.IsFalse(string.IsNullOrWhiteSpace(l.Speaker));
            }
            Assert.LessOrEqual(counts.Max() - counts.Min(), 1, string.Join(",", counts));
            Assert.GreaterOrEqual(counts.Min(), 12);
            Assert.GreaterOrEqual(C.ListeningLines.Select(l => l.Speaker).Distinct().Count(), 10, "varied speakers");
            Assert.AreEqual(C.ListeningLines.Count, C.ListeningLines.Select(l => l.Id).Distinct().Count());
            Assert.AreEqual(C.ListeningLines.Count, C.ListeningLines.Select(l => l.Text).Distinct().Count());
        }

        [Test]
        public void Personalities_AreDistinct()
        {
            Assert.GreaterOrEqual(C.Personalities.Count, 4);
            Assert.AreEqual(C.Personalities.Count, C.Personalities.Select(p => p.BustTolerance).Distinct().Count());
            Assert.AreEqual(4, C.Emblems.Select(e => e.Shape).Distinct().Count(), "emblems differ by shape");
        }

        [Test]
        public void IncompleteContent_IsRejected()
        {
            var c = JsonConvert.DeserializeObject<CouncilContent>(C.ToJson());
            c.Disputes.RemoveAll(d => d.Track == 9);
            Assert.Throws<System.FormatException>(() => c.Validate());
        }
    }
}
