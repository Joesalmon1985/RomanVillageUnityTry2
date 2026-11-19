using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using NUnit.Framework;
using World.Sim.Encounters.Council;

namespace World.Sim.Tests
{
    /// <summary>Helpers for the council tests.</summary>
    public static class CouncilTestKit
    {
        public static readonly string[] Personalities = { "careful", "steady", "bold", "closer" };

        public static ElectionState NewElection(ulong seed = 1, int playerSeat = 0) =>
            ElectionEngine.Create(seed, playerSeat, Personalities, CouncilContent.BuiltIn.Listening);

        /// <summary>Puts a push with the given internal values into the election as if it had just been made.</summary>
        public static List<Tray> Stage(ElectionState e, int v0, int v1, int v2, int v3)
        {
            var trays = ElectionEngine.BuildTrays(e, v0, v1, v2, v3);
            e.PushId++;
            e.Pending = new List<Tray>(trays);
            e.Phase = trays.Count > 0 ? ElectionPhase.Choosing : ElectionPhase.Ready;
            return trays;
        }

        public static ActionResult Do(ElectionState e, ElectionAction a, List<CouncilEvent> ev = null)
        {
            var r = ElectionEngine.Apply(e, a, ev ?? new List<CouncilEvent>());
            return r;
        }

        public static int[] SocketTracks(Tray t) => new[] { t.Sockets[0].Track, t.Sockets[1].Track };

        public static void CloseAllExcept(ElectionState e, params int[] open)
        {
            for (int t = 2; t <= 12; t++)
                if (Array.IndexOf(open, t) < 0) e.Claimed[t - 2] = 3;
        }

        public static JsonSerializerSettings SaveSettings() => new JsonSerializerSettings
        {
            Formatting = Formatting.None,
            TypeNameHandling = TypeNameHandling.None,
            MissingMemberHandling = MissingMemberHandling.Ignore,
            ObjectCreationHandling = ObjectCreationHandling.Replace,
        };

        public static T RoundTrip<T>(T value)
        {
            string json = JsonConvert.SerializeObject(value, SaveSettings());
            return JsonConvert.DeserializeObject<T>(json, SaveSettings());
        }

        public static string Json(object o) => JsonConvert.SerializeObject(o, SaveSettings());

        /// <summary>Plays one scripted action for the active seat with the given personality set. Returns the action taken.</summary>
        public static ElectionAction Step(ElectionState e, List<CouncilEvent> ev = null)
        {
            var p = CouncilContent.BuiltIn.Personality(e.Personality[e.Active]);
            var a = CouncilAi.Decide(e, p);
            var r = ElectionEngine.Apply(e, a, ev ?? new List<CouncilEvent>());
            Assert.IsTrue(r.Ok, r.ToString());
            return a;
        }

        public static void PlayOut(ElectionState e, List<CouncilEvent> ev = null, int maxActions = 20000)
        {
            for (int i = 0; i < maxActions && e.Phase != ElectionPhase.Complete; i++) Step(e, ev);
            Assert.AreEqual(ElectionPhase.Complete, e.Phase, "the election ends");
        }

        public static string FindContentFile()
        {
            foreach (var start in new[] { AppContext.BaseDirectory, Directory.GetCurrentDirectory() })
            {
                var d = new DirectoryInfo(start);
                while (d != null)
                {
                    var f = Path.Combine(d.FullName, "Assets", "StreamingAssets", "world", "council", "council_content.json");
                    if (File.Exists(f)) return f;
                    d = d.Parent;
                }
            }
            Assert.Fail("council_content.json not found");
            return null;
        }
    }
}
