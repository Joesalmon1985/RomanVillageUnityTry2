using System;
using System.Collections.Generic;

namespace World.Sim.Eras
{
    /// <summary>
    /// A stable named place on the island (PRD_AGES 4.1, A.2), kept through every era. One of Corner, Hex, HexEdge or
    /// Plot is set (the others are -1). State is free text owned by whoever placed it (e.g. "ruined", "restored").
    /// </summary>
    public sealed class Landmark
    {
        public string Id;
        public int Corner = -1, Hex = -1, HexEdge = -1, Plot = -1;
        public string State;
    }

    /// <summary>Landmark ids (4.1). The four tower ids are reserved here and placed by the crystal campaign package.</summary>
    public static class LandmarkIds
    {
        public const string HomeHearth = "lm.home_hearth";
        public const string AshHearth = "lm.ash_hearth";
        public const string DanceCircle = "lm.dance_circle";
        public const string Harbour = "lm.harbour";
        public const string TowerEarth = "lm.tower.earth";
        public const string TowerWater = "lm.tower.water";
        public const string TowerAir = "lm.tower.air";
        public const string TowerFire = "lm.tower.fire";
        /// <summary>The Ash altars (PRD_CRYSTALS Appendix C), registered by the campaign module with the towers.</summary>
        public const string AltarEarth = "lm.altar.earth";
        public const string AltarWater = "lm.altar.water";
        public const string AltarAir = "lm.altar.air";
        public const string AltarFire = "lm.altar.fire";
        /// <summary>Placed by M1 (rule in 9.1).</summary>
        public const string Relay = "lm.relay";
        /// <summary>Placed by F1 (rule in 9.2).</summary>
        public const string District = "lm.district";

        /// <summary>Reserved for the crystal campaign (PRD_CRYSTALS places them; altar ids are theirs, e.g. "lm.altar.earth").</summary>
        public static readonly string[] Towers = { TowerEarth, TowerWater, TowerAir, TowerFire };
    }

    /// <summary>Read helpers for Board.Landmarks (static, null-safe, never mutate).</summary>
    public static class LandmarkApi
    {
        /// <summary>The landmark with that id, or null.</summary>
        public static Landmark Get(WorldState s, string id)
        {
            var list = s?.Board?.Landmarks;
            if (list == null || id == null) return null;
            foreach (var l in list) if (string.Equals(l.Id, id, StringComparison.Ordinal)) return l;
            return null;
        }

        /// <summary>Every landmark, sorted by id (ordinal). Empty when none.</summary>
        public static IReadOnlyList<Landmark> All(WorldState s) =>
            (IReadOnlyList<Landmark>)s?.Board?.Landmarks ?? Array.Empty<Landmark>();

        /// <summary>
        /// Adds or replaces a landmark (keeps Board.Landmarks sorted by id). For the crystal campaign module only (its
        /// StartCampaign registers the towers and altars); sim code, inside a command's Apply.
        /// </summary>
        public static void Register(WorldState s, Landmark lm) => LandmarkLogic.Set(s, lm);

        /// <summary>Bearings (degrees, 0 = north, clockwise) of the four towers (PRD_CRYSTALS Appendix C): earth SW, fire E, water S, air N.</summary>
        public const double BearingEarth = 225, BearingFire = 90, BearingWater = 180, BearingAir = 0;

        /// <summary>
        /// The shore corner rule of PRD_CRYSTALS Appendix C: the rim corner whose bearing from the board centre is nearest
        /// the given bearing (ties by lower id); -1 if none. Shared so the towers and the harbour agree.
        /// </summary>
        public static int ShoreCorner(BoardTopology t, double bearing)
        {
            if (t == null) return -1;
            int best = -1;
            double bestD = double.MaxValue;
            for (int c = 0; c < t.CornerCount; c++)
            {
                if (t.VertexHexes(c).Count >= 3) continue;
                double d = Math.Abs(PlotLayout.Yaw(t.VertexX(c), t.VertexZ(c)) - bearing) % 360;
                if (d > 180) d = 360 - d;
                d = Math.Round(d, 6);
                if (d < bestD) { bestD = d; best = c; }
            }
            return best;
        }

        /// <summary>The four tower corners in the order earth, fire, water, air (Appendix C bearings).</summary>
        public static int[] TowerCorners(BoardTopology t) => new[]
        {
            ShoreCorner(t, BearingEarth), ShoreCorner(t, BearingFire), ShoreCorner(t, BearingWater), ShoreCorner(t, BearingAir),
        };

        /// <summary>
        /// The harbour corner rule (4.1): the rim corner (fewer than three board hexes) nearest the given corner by
        /// hex-edge distance, ties by lower id, skipping the four tower corners; -1 if none.
        /// </summary>
        public static int HarbourCorner(BoardTopology t, int nearCorner)
        {
            if (t == null || !t.IsCorner(nearCorner)) return -1;
            var towers = TowerCorners(t);
            int best = -1, bestD = int.MaxValue;
            for (int c = 0; c < t.CornerCount; c++)
            {
                if (t.VertexHexes(c).Count >= 3 || Array.IndexOf(towers, c) >= 0) continue;
                int d = t.CornerDistance(nearCorner, c);
                if (d < 0) continue;
                if (d < bestD) { bestD = d; best = c; }
            }
            return best;
        }
    }

    /// <summary>Writes Board.Landmarks (sim code only: generation, the crystal campaign's tower placement, transfers).</summary>
    internal static class LandmarkLogic
    {
        /// <summary>Adds or replaces a landmark by id and keeps the list sorted by id.</summary>
        public static void Set(WorldState s, Landmark l)
        {
            if (s?.Board == null || l?.Id == null) return;
            var list = s.Board.Landmarks ?? (s.Board.Landmarks = new List<Landmark>());
            list.RemoveAll(x => string.Equals(x.Id, l.Id, StringComparison.Ordinal));
            list.Add(l);
            list.Sort((a, b) => string.CompareOrdinal(a.Id, b.Id));
        }

        /// <summary>
        /// The landmarks every board has from the start (4.1): the two hearths, the dance circle and the harbour. Towers,
        /// the relay and the district are placed later by their owners. Keeps landmarks already present.
        /// </summary>
        public static void Seed(WorldState s)
        {
            var b = s?.Board;
            if (b == null) return;
            if (b.Landmarks == null) b.Landmarks = new List<Landmark>();
            var t = b.Topology;
            var starts = b.StartCorners ?? new int[0];
            if (starts.Length > 0 && LandmarkApi.Get(s, LandmarkIds.HomeHearth) == null)
                Set(s, new Landmark { Id = LandmarkIds.HomeHearth, Corner = starts[0] });
            if (starts.Length > 1 && LandmarkApi.Get(s, LandmarkIds.AshHearth) == null)
                Set(s, new Landmark { Id = LandmarkIds.AshHearth, Corner = starts[1] });
            if (b.ReservedPlots != null && b.ReservedPlots.Length > 0 && LandmarkApi.Get(s, LandmarkIds.DanceCircle) == null)
                Set(s, new Landmark { Id = LandmarkIds.DanceCircle, Plot = b.ReservedPlots[0] });
            if (starts.Length > 0 && LandmarkApi.Get(s, LandmarkIds.Harbour) == null)
            {
                int c = LandmarkApi.HarbourCorner(t, starts[0]);
                if (c >= 0) Set(s, new Landmark { Id = LandmarkIds.Harbour, Corner = c });
            }
        }
    }
}
