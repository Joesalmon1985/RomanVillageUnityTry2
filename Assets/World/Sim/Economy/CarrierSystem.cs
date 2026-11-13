using System.Collections.Generic;

namespace World.Sim.Economy
{
    /// <summary>
    /// Carriers slot: assigns waiting jobs to free carriers (priority, then id), then moves every
    /// carrier. A carrier re-plans its route at each flag over current roads and unblocked flags, so a
    /// removed road makes it stop at the next flag (keeping its load) until a route exists again.
    /// </summary>
    internal sealed class CarrierSystem : ISimSystem
    {
        private readonly EconomyHooks _hooks;
        public CarrierSystem(EconomyHooks hooks) { _hooks = hooks; }

        public void Tick(WorldState s, EventBuffer ev)
        {
            var blocked = _hooks.ComputeBlocked(s);
            Assign(s, blocked, ev);
            foreach (var c in s.Economy.Carriers) Move(s, blocked, c, ev);
        }

        private static void Assign(WorldState s, bool[] blocked, EventBuffer ev)
        {
            var e = s.Economy;
            var waiting = new List<DeliveryJob>();
            foreach (var j in e.Jobs) if (j.State == JobState.Waiting) waiting.Add(j);
            if (waiting.Count == 0) return;
            waiting.Sort((a, b) =>
            {
                int pa = (int)e.Buildings[a.DestId].Priority, pb = (int)e.Buildings[b.DestId].Priority;
                return pa != pb ? pb.CompareTo(pa) : a.Id.CompareTo(b.Id);
            });

            foreach (var j in waiting)
            {
                var src = e.Buildings[j.SourceId];
                var dst = e.Buildings[j.DestId];
                string reason = EconomyLogic.RouteReason(s, blocked, src.Vertex, dst.Vertex);
                if (reason != null)
                {
                    EconomyLogic.SetJobReason(j, reason, ev, s.Tick);
                    if (s.Tick - j.RouteStalledSince >= e.Tuning.ReleaseStalledJobTicks) EconomyLogic.ReleaseJob(s, j, ev);
                    continue;
                }
                Carrier best = null;
                int bestLen = int.MaxValue;
                bool anyFree = false;
                foreach (var c in e.Carriers)
                {
                    if (c.CurrentEdge >= 0 || c.JobId >= 0) continue;
                    if (c.State != CarrierState.Idle && c.State != CarrierState.Returning) continue;
                    anyFree = true;
                    var path = EconomyLogic.Route(s, blocked, c.AtVertex, src.Vertex);
                    if (path != null && path.Length < bestLen)
                    {
                        best = c;
                        bestLen = path.Length;
                    }
                }
                if (best == null)
                {
                    string r = Reasons.NoCarrier;
                    if (anyFree)
                    {
                        // Free carriers exist but none can reach the source.
                        r = Reasons.NoRoad;
                        foreach (var c in e.Carriers)
                        {
                            if (c.CurrentEdge >= 0 || c.JobId >= 0) continue;
                            if (EconomyLogic.RouteReason(s, blocked, c.AtVertex, src.Vertex) == Reasons.DemonOnRoute) r = Reasons.DemonOnRoute;
                        }
                    }
                    EconomyLogic.SetJobReason(j, r, ev, s.Tick);
                    if (r != Reasons.NoCarrier && s.Tick - j.RouteStalledSince >= e.Tuning.ReleaseStalledJobTicks) EconomyLogic.ReleaseJob(s, j, ev);
                    continue;
                }
                j.State = JobState.ToPickup;
                j.CarrierId = best.Id;
                best.JobId = j.Id;
                best.State = CarrierState.ToPickup;
                EconomyLogic.SetJobReason(j, null, ev);
            }
        }

        private static void Move(WorldState s, bool[] blocked, Carrier c, EventBuffer ev)
        {
            var e = s.Economy;
            if (c.CurrentEdge >= 0)
            {
                c.Progress++;
                if (c.Progress < e.Tuning.TicksPerEdge) return;
                c.AtVertex = c.ToVertex;
                c.CurrentEdge = -1;
                c.FromVertex = -1;
                c.ToVertex = -1;
                c.Progress = 0;
            }

            var job = c.JobId >= 0 ? EconomyLogic.FindJob(e, c.JobId) : null;
            if (job == null && (c.State == CarrierState.ToPickup || c.State == CarrierState.Carrying))
            {
                c.State = CarrierState.Idle;
                c.JobId = -1;
            }

            // Actions at the current flag.
            if (job != null && c.State == CarrierState.ToPickup && c.AtVertex == e.Buildings[job.SourceId].Vertex)
            {
                var src = e.Buildings[job.SourceId];
                int w = (int)job.Ware;
                src.Stock[w]--;
                src.Reserved[w]--;
                c.HasLoad = true;
                c.Load = job.Ware;
                c.State = CarrierState.Carrying;
                job.State = JobState.Carrying;
                ev.Add(new WarePickedUp { JobId = job.Id, CarrierId = c.Id, Ware = job.Ware, BuildingId = src.Id });
            }
            if (job != null && c.State == CarrierState.Carrying && c.AtVertex == e.Buildings[job.DestId].Vertex)
            {
                var dst = e.Buildings[job.DestId];
                int w = (int)job.Ware;
                dst.Stock[w]++;
                dst.Incoming[w]--;
                c.HasLoad = false;
                c.State = CarrierState.Idle;
                c.JobId = -1;
                e.Jobs.Remove(job);
                ev.Add(new WareDelivered { JobId = job.Id, CarrierId = c.Id, Ware = job.Ware, BuildingId = dst.Id });
                job = null;
            }

            int storeV = EconomyLogic.Storehouse(s).Vertex;
            if (c.State == CarrierState.Idle && c.AtVertex != storeV) c.State = CarrierState.Returning;
            if (c.State == CarrierState.Returning && c.AtVertex == storeV) c.State = CarrierState.Idle;

            int target;
            switch (c.State)
            {
                case CarrierState.ToPickup: target = e.Buildings[job.SourceId].Vertex; break;
                case CarrierState.Carrying: target = e.Buildings[job.DestId].Vertex; break;
                case CarrierState.Returning: target = storeV; break;
                default: return;
            }
            if (target == c.AtVertex) return;

            var path = EconomyLogic.Route(s, blocked, c.AtVertex, target);
            if (path == null)
            {
                if (job != null)
                {
                    EconomyLogic.SetJobReason(job, EconomyLogic.RouteReason(s, blocked, c.AtVertex, target), ev, s.Tick);
                    // A carrier that cannot reach the pickup gives the job back (not yet loaded, nothing to lose).
                    if (c.State == CarrierState.ToPickup && s.Tick - job.RouteStalledSince >= e.Tuning.ReleaseStalledJobTicks)
                        EconomyLogic.ReleaseJob(s, job, ev);
                }
                return;
            }
            if (job != null) EconomyLogic.SetJobReason(job, null, ev);
            c.FromVertex = c.AtVertex;
            c.ToVertex = path[1];
            c.CurrentEdge = s.Board.Topology.EdgeBetween(path[0], path[1]);
            c.Progress = 0;
        }
    }
}
