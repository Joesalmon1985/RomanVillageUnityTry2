using System.Collections.Generic;
using NUnit.Framework;
using World.Sim.Campaign;

namespace World.Sim.Tests
{
    /// <summary>The pack loader: strict reading with errors that name the file, the JSON path and the line.</summary>
    public class LoaderTests
    {
        private static CampaignDefinition Load(params (string name, string text)[] files)
        {
            var map = new Dictionary<string, string>();
            var names = new List<string>();
            foreach (var (n, t) in files) { map[n] = t; names.Add(n); }
            return CampaignLoader.LoadFolder(f => map[f], names);
        }

        private static CampaignLoadError Fails(params (string name, string text)[] files)
        {
            var e = Assert.Throws<System.FormatException>(() => Load(files));
            var info = CampaignLoadError.Of(e);
            Assert.IsNotNull(info, "a loader error carries its details");
            Assert.AreEqual(info.ToString(), e.Message);
            return info;
        }

        [Test]
        public void MarkerRefs_ReadFromText_OrObject_AndLegendCellsAreFilled()
        {
            var d = Load(("campaign.json", "{ \"Id\": \"t\", \"Crystals\": [ { \"Id\": \"earth\", \"Site\": \"glade/q/pedestal\" }, { \"Id\": \"fire\", \"Site\": { \"Location\": \"kiln\", \"Marker\": \"pedestal\" } } ] }"),
                         ("earth.json", "{ \"Interiors\": [ { \"Id\": \"q\", \"Rows\": [ \"####\", \"#.P#\", \"####\" ], \"Legend\": { \"P\": { \"Id\": \"pedestal\", \"Kind\": \"Pedestal\" } } } ] }"));
            Assert.AreEqual(new MarkerRef("glade", "q", "pedestal"), d.Crystal("earth").Site);
            Assert.AreEqual(MarkerRef.Surface("kiln", "pedestal"), d.Crystal("fire").Site);
            Assert.AreEqual(2, d.Interior("q").Legend['P'].I);
            Assert.AreEqual(1, d.Interior("q").Legend['P'].J);
        }

        [Test]
        public void UnknownPart_NamesTheFile()
        {
            var e = Fails(("campaign.json", "{ \"Id\": \"t\" }"), ("earth.json", "{\n  \"Locationz\": [] }"));
            Assert.AreEqual("earth.json", e.File);
            StringAssert.Contains("Locationz", e.Reason);
            Assert.AreEqual(2, e.Line);
        }

        [Test]
        public void WrongCase_IsRefusedWithTheRightSpelling()
        {
            var e = Fails(("earth.json", "{ \"locations\": [] }"));
            StringAssert.Contains("write 'Locations'", e.Reason);
        }

        [Test]
        public void UnknownMember_NamesThePath()
        {
            var e = Fails(("earth.json", "{ \"Locations\": [ { \"Id\": \"glade\" },\n { \"Id\": \"x\", \"Anchr\": {} } ] }"));
            Assert.AreEqual("earth.json", e.File);
            StringAssert.Contains("Locations[1]", e.JsonPath);
            StringAssert.Contains("Anchr", e.Reason);
            Assert.AreEqual(2, e.Line);
        }

        [Test]
        public void BadEnum_NamesThePathAndLine()
        {
            var e = Fails(("earth.json", "{\n \"Locations\": [\n { \"Id\": \"glade\", \"Kind\": \"Glade\" } ] }"));
            Assert.AreEqual("Locations[0].Kind", e.JsonPath);
            Assert.AreEqual(3, e.Line);
            StringAssert.StartsWith("earth.json at Locations[0].Kind (line 3", e.ToString());
        }

        [Test]
        public void EnumAsNumber_IsRefused()
        {
            Fails(("earth.json", "{ \"Locations\": [ { \"Id\": \"glade\", \"Kind\": 1 } ] }"));
        }

        [Test]
        public void BadMarkerRef_IsRefused()
        {
            var e = Fails(("campaign.json", "{ \"Crystals\": [ { \"Id\": \"earth\", \"Site\": \"just-a-word\" } ] }"));
            Assert.AreEqual("Crystals[0].Site", e.JsonPath);
            StringAssert.Contains("not a marker reference", e.Reason);
        }

        [Test]
        public void SingleValuePart_SetInTwoFiles_IsRefused()
        {
            var e = Fails(("campaign.json", "{ \"Id\": \"a\" }"), ("earth.json", "{ \"Id\": \"b\" }"));
            Assert.AreEqual("earth.json", e.File);
            StringAssert.Contains("already set by campaign.json", e.Reason);
        }

        [Test]
        public void ListPart_GivenAsObject_IsRefused()
        {
            var e = Fails(("earth.json", "{ \"Lines\": { \"Id\": \"x\" } }"));
            StringAssert.Contains("must be a list", e.Reason);
        }

        [Test]
        public void BrokenJson_AndDuplicateKeys_AndNonObjects_AreRefused()
        {
            var e = Fails(("earth.json", "{\n \"Lines\": [ \n"));
            Assert.AreEqual("earth.json", e.File);
            Assert.Greater(e.Line, 0);
            StringAssert.Contains("not valid JSON", e.Reason);
            StringAssert.Contains("Id", Fails(("a.json", "{ \"Id\": \"x\", \"Id\": \"y\" }")).Reason);
            StringAssert.Contains("one JSON object", Fails(("a.json", "[ 1, 2 ]")).Reason);
            Assert.AreEqual("a.json", Fails(("a.json", "nope")).File);
        }

        [Test]
        public void EmptyFiles_AreSkipped_AndReadErrorsNameTheFile()
        {
            var d = Load(("campaign.json", "{ \"Id\": \"t\" }"), ("notes.json", "  "));
            Assert.AreEqual("t", d.Id);
            var e = Assert.Throws<System.FormatException>(() => CampaignLoader.LoadFolder(f => throw new System.IO.FileNotFoundException("gone"), new[] { "earth.json" }));
            Assert.AreEqual("earth.json", CampaignLoadError.Of(e).File);
            Assert.Throws<System.FormatException>(() => CampaignLoader.LoadDirectory("Z:/no/such/pack"));
        }
    }
}
