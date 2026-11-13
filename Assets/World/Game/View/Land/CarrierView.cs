using System.Collections.Generic;
using UnityEngine;
using World.Sim;
using World.Sim.Economy;

namespace World.Game
{
    /// <summary>
    /// A carrier (REFACTOR_SPEC 8.4): a figure in its people's colour walking exactly where the feed (by default
    /// <see cref="EconomyApi.CarrierPosition"/>) puts it, a bundle in the ware's colour on its head on a local trip,
    /// or a porter's basket with one bundle per unit (up to 4) on a shipment along the main roads. Idle carriers wait
    /// in a ring beside their storehouse. Followable and inspectable.
    /// </summary>
    public sealed class CarrierView : MonoBehaviour, IInspectable, IFollowable
    {
        public int CarrierId { get; private set; }
        public CarrierPose Pose { get; private set; }
        public LandFigure Figure { get; private set; }
        public Vector3 IdleSpot { get; set; }
        public Vector3 IdleFace { get; set; }
        public bool Shipment => Pose.Shipment;

        public string Title => Pose.Shipment ? "Porter" : "Carrier";
        public IEnumerable<string> Lines
        {
            get
            {
                var s = LandText.State;
                if (s == null) yield break;
                string d = EconomyApi.DescribeCarrier(s, CarrierId);
                if (!string.IsNullOrEmpty(d)) yield return d;
                else yield return "A carrier of " + LandLook.PeopleName(Pose.Faction) + ".";
            }
        }

        public string FollowName => Pose.Shipment ? "the porter" : "the carrier";
        public Transform FollowTransform => transform;

        public static CarrierView Create(Transform parent, int id, in CarrierPose pose)
        {
            var fig = LandFigure.Create(parent, "Carrier " + id, pose.Faction);
            var v = fig.gameObject.AddComponent<CarrierView>();
            v.CarrierId = id;
            v.Figure = fig;
            v.Pose = pose;
            LandLook.PickBox(fig.gameObject, Vector3.up * 1.1f, new Vector3(1.0f, 2.3f, 1.0f));
            return v;
        }

        /// <summary>Once per tick: load and look.</summary>
        public void Sync(in CarrierPose pose)
        {
            Pose = pose;
            Figure.SetLoad(pose.HasLoad, pose.Load, pose.LoadCount, pose.Shipment);
            Figure.Pose = pose.Moving ? FigurePose.Walk : FigurePose.Stand;
        }

        /// <summary>Every frame: exactly on the feed's position while moving, at the idle spot otherwise.</summary>
        public void Animate(ICarrierFeed feed, WorldState s, double frac, float now)
        {
            if (Pose.Moving && feed.Position(s, CarrierId, frac, out double x, out double z, out double yaw))
                Figure.Place(LandLook.Ground(x, z), (float)yaw);
            else
                Figure.Place(IdleSpot, BoardSpace.YawOf(IdleFace - IdleSpot));
            Figure.Animate(now);
        }
    }
}
