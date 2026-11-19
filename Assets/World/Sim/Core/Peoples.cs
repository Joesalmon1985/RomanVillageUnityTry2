using System.Collections.Generic;

namespace World.Sim
{
    /// <summary>
    /// One people of a game (PRD_GODBOARD PE-1): id (= player id and faction id), display name, colour (0-255 RGB) and
    /// banner id. Plain data, saved in <see cref="WorldState.Peoples"/>. The building palette is the shared set tinted
    /// to <see cref="ColorR"/>/<see cref="ColorG"/>/<see cref="ColorB"/> for now (PE-4).
    /// </summary>
    public sealed class People
    {
        public int Id;
        /// <summary>Name in running text, e.g. "Your people", "the Ash Clan".</summary>
        public string Name;
        public int ColorR;
        public int ColorG;
        public int ColorB;
        /// <summary>Banner id for views, e.g. "banner.ash".</summary>
        public string BannerId;
    }

    /// <summary>The fixed table of peoples (PE-1) and helpers to fill <see cref="WorldState.Peoples"/>.</summary>
    public static class PeopleDefs
    {
        private static readonly People[] Table =
        {
            new People { Id = PlayerIds.Human, Name = "Your people", ColorR = 66, ColorG = 120, ColorB = 196, BannerId = "banner.home" },
            new People { Id = PlayerIds.Rival, Name = "the Ash Clan", ColorR = 168, ColorG = 62, ColorB = 44, BannerId = "banner.ash" },
            new People { Id = PlayerIds.Elves, Name = "the Elves", ColorR = 72, ColorG = 158, ColorB = 92, BannerId = "banner.elves" },
            new People { Id = PlayerIds.Orcs, Name = "the Orcs", ColorR = 196, ColorG = 148, ColorB = 40, BannerId = "banner.orcs" },
            new People { Id = 4, Name = "the Reed Folk", ColorR = 128, ColorG = 88, ColorB = 168, BannerId = "banner.reed" },
            new People { Id = 5, Name = "the Salt Kin", ColorR = 210, ColorG = 210, ColorB = 200, BannerId = "banner.salt" },
        };

        /// <summary>A fresh copy of the definition of people <paramref name="id"/> (0 .. <see cref="PlayerIds.MaxCount"/> - 1).</summary>
        public static People Get(int id)
        {
            if (id < 0 || id >= Table.Length) throw new System.ArgumentOutOfRangeException(nameof(id), "No people " + id + ".");
            var d = Table[id];
            return new People { Id = d.Id, Name = d.Name, ColorR = d.ColorR, ColorG = d.ColorG, ColorB = d.ColorB, BannerId = d.BannerId };
        }

        /// <summary>The first <paramref name="count"/> peoples.</summary>
        public static List<People> FirstN(int count)
        {
            var list = new List<People>(count);
            for (int i = 0; i < count; i++) list.Add(Get(i));
            return list;
        }

        /// <summary>Display name of a people of this game ("a people" for an unknown id).</summary>
        public static string Name(WorldState s, int id)
        {
            if (s?.Peoples != null && id >= 0 && id < s.Peoples.Count && s.Peoples[id] != null) return s.Peoples[id].Name;
            return id >= 0 && id < Table.Length ? Table[id].Name : "a people";
        }

        /// <summary>"the Ash Clan's", "the Elves'".</summary>
        public static string Possessive(WorldState s, int id)
        {
            string n = Name(s, id);
            return n.EndsWith("s", System.StringComparison.Ordinal) ? n + "'" : n + "'s";
        }

        /// <summary>
        /// Keeps <see cref="WorldState.Peoples"/> as long as <see cref="WorldState.Players"/> (a save written before G0-1
        /// has no peoples; they come from the table, under the players' saved names). Idempotent.
        /// </summary>
        public static void Ensure(WorldState s)
        {
            if (s == null) return;
            if (s.Peoples == null) s.Peoples = new List<People>();
            for (int id = s.Peoples.Count; id < s.Players.Count && id < Table.Length; id++)
            {
                var p = Get(id);
                if (!string.IsNullOrEmpty(s.Players[id]?.Name)) p.Name = s.Players[id].Name;
                s.Peoples.Add(p);
            }
        }
    }
}
