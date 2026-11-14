using System.IO;
using NUnit.Framework;
using World.Sim.Campaign;

namespace World.Sim.Tests
{
    /// <summary>Finds and loads the shipped campaign packs (StreamingAssets/world/campaigns) from the repository on disk.</summary>
    public static class PackFiles
    {
        public const string Authored = "authored_v1";
        public const string Skeleton = "skeleton_v1";

        private static string _root;

        /// <summary>The repository root (the folder holding Assets and Docs).</summary>
        public static string Root
        {
            get
            {
                if (_root != null) return _root;
                foreach (var start in new[] { TestContext.CurrentContext.TestDirectory, Directory.GetCurrentDirectory() })
                {
                    var d = new DirectoryInfo(start);
                    while (d != null)
                    {
                        if (Directory.Exists(Path.Combine(d.FullName, "Assets", "StreamingAssets")) && Directory.Exists(Path.Combine(d.FullName, "Docs")))
                            return _root = d.FullName;
                        d = d.Parent;
                    }
                }
                Assert.Fail("repository root not found");
                return null;
            }
        }

        public static string PackFolder(string pack) => Path.Combine(Root, "Assets", "StreamingAssets", "world", "campaigns", pack);

        public static CampaignDefinition Load(string pack) => CampaignLoader.LoadDirectory(PackFolder(pack));

        public static string FixtureFolder => Path.Combine(Root, "Assets", "World", "Tests", "EditMode", "Campaign", "Validation", "Fixtures");
    }
}
