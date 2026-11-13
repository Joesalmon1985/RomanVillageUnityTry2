using World.Sim;
using World.Sim.Economy;

namespace World.Game
{
    /// <summary>What a carrier view needs to know about one carrier at a moment.</summary>
    public struct CarrierPose
    {
        public double X, Z, Yaw;
        public int Faction, Home;
        /// <summary>On a leg (walking); false while idle at the storehouse.</summary>
        public bool Moving;
        public bool HasLoad;
        public Ware Load;
        public int LoadCount;
        /// <summary>A shipment between storehouses along main roads (porter look).</summary>
        public bool Shipment;
    }

    /// <summary>
    /// Where the carrier views get carriers from. The default reads <c>WorldState.Economy.Carriers</c> and
    /// <see cref="EconomyApi.CarrierPosition"/>; PlayMode tests swap in a fake feed (positions and loads) so the views
    /// can be tested before the real logistics land (REFACTOR_SPEC 8.4; appendix B change log, V).
    /// </summary>
    public interface ICarrierFeed
    {
        int Count(WorldState s);
        /// <summary>Everything about a carrier (called once per tick).</summary>
        bool TryGet(WorldState s, int carrierId, double tickFraction, out CarrierPose pose);
        /// <summary>Position and facing only (called every frame).</summary>
        bool Position(WorldState s, int carrierId, double tickFraction, out double x, out double z, out double yaw);
    }

    /// <summary>The real feed: the sim's carriers and its interpolated positions.</summary>
    public sealed class SimCarrierFeed : ICarrierFeed
    {
        public static readonly SimCarrierFeed Instance = new SimCarrierFeed();

        public int Count(WorldState s) => s?.Economy != null ? s.Economy.Carriers.Count : 0;

        public bool Position(WorldState s, int id, double frac, out double x, out double z, out double yaw) =>
            EconomyApi.CarrierPosition(s, id, frac, out x, out z, out yaw);

        public bool TryGet(WorldState s, int id, double frac, out CarrierPose pose)
        {
            pose = default;
            if (!EconomyApi.CarrierPosition(s, id, frac, out pose.X, out pose.Z, out pose.Yaw)) return false;
            var c = s.Economy.Carriers[id];
            pose.Faction = c.Faction;
            pose.Home = c.Home;
            pose.Moving = c.State != CarrierState.Idle;
            pose.HasLoad = c.HasLoad;
            pose.Load = c.Load;
            pose.LoadCount = c.LoadCount;
            bool shipment = c.LegCorners != null && c.LegCorners.Length > 0;
            if (!shipment && c.JobId >= 0)
                foreach (var j in s.Economy.Jobs) if (j.Id == c.JobId) { shipment = j.IsShipment; break; }
            pose.Shipment = shipment;
            return true;
        }
    }
}
