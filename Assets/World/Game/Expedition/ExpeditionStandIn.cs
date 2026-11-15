using System.Collections.Generic;
using UnityEngine;
using World.Sim;
using World.Sim.Campaign;

namespace World.Game
{
    /// <summary>
    /// Stand-in interiors (package I6, only while package I5's interiors are the I0 shell): a plain floor, rock walls,
    /// a roof, doors and lit stairs for each interior of the definition, built from its rows far east of the island
    /// (<see cref="ExpeditionSpace.StandInOrigin"/>), so the expedition can be staged, captured and tested alone. The
    /// walker cannot step there (the walk grid's open regions are package I5's): the director leads the viewpoint
    /// behind the party instead (<see cref="ExpeditionDirector.Railed"/>). Nothing here is built once
    /// <see cref="ExpeditionSpace.RealInteriors"/> is true; at integration this file can be deleted.
    /// </summary>
    public sealed class ExpeditionStandIn : MonoBehaviour
    {
        public const float WallHeight = 4.6f;
        private WorldBootstrap _boot;
        private readonly List<(GameObject go, string fact)> _doors = new List<(GameObject, string)>();
        /// <summary>Interiors built (tests).</summary>
        public int Built { get; private set; }

        public static ExpeditionStandIn Create(ExpeditionSites sites, WorldBootstrap boot, Transform parent)
        {
            var go = new GameObject("Interior stand-ins (no package I5)");
            go.transform.SetParent(parent, false);
            var s = go.AddComponent<ExpeditionStandIn>();
            s._boot = boot;
            s.Build(boot.Sim.State.Campaign.Definition);
            return s;
        }

        private void Build(CampaignDefinition d)
        {
            var floorCol = new Color(0.30f, 0.28f, 0.26f);
            var rockCol = new Color(0.22f, 0.20f, 0.19f);
            foreach (var map in d.Interiors)
            {
                var root = new GameObject("Stand-in " + map.Id).transform;
                root.SetParent(transform, false);
                var floor = new MeshBuilder(); var rock = new MeshBuilder();
                var rnd = new PropRandom(map.Id.GetHashCode());
                int h = map.Rows.Count, w = 0;
                foreach (var row in map.Rows) w = Mathf.Max(w, row.Length);
                Vector3 min = new Vector3(float.MaxValue, 0, float.MaxValue), max = new Vector3(float.MinValue, 0, float.MinValue);
                for (int j = -1; j <= h; j++)
                    for (int i = -1; i <= w; i++)
                    {
                        var p = ExpeditionSpace.InteriorCell(d, map.Id, i, j);
                        if (InteriorMaps.IsFloor(map, i, j))
                        {
                            floor.Cylinder(p + Vector3.down * 0.3f, 3.0f, 3.0f, 0.3f, 6, floorCol * rnd.Range(0.9f, 1.1f));
                            min = Vector3.Min(min, p); max = Vector3.Max(max, p);
                            continue;
                        }
                        // Rock only where it borders floor (the rest is never seen).
                        bool border = false;
                        for (int k = 0; k < 6 && !border; k++) border = InteriorMaps.IsFloor(map, i + InteriorMaps.Steps[2 * k], j + InteriorMaps.Steps[2 * k + 1]);
                        if (!border) continue;
                        rock.Cylinder(p + Vector3.down * 0.3f, 3.05f, 2.75f, WallHeight + 0.3f, 6, rockCol * rnd.Range(0.85f, 1.15f));
                        for (int n = 0; n < 2; n++)
                            rock.Rock(p + new Vector3(rnd.Range(-2.4f, 2.4f), 0, rnd.Range(-2.4f, 2.4f)), rnd.Range(0.5f, 1.0f), rockCol * 1.2f, rnd.Next);
                    }
                // A roof over all of it: no sky, no moonlight underground.
                var centre = (min + max) * 0.5f; var size = max - min;
                rock.Box(new Vector3(centre.x, WallHeight + 0.4f, centre.z), new Vector3(size.x + 16f, 0.8f, size.z + 16f), rockCol * 0.6f);
                floor.ToObject("Floor", root, LandLook.Lit);
                rock.ToObject("Rock", root, LandLook.Lit);
                foreach (var m in InteriorMaps.Markers(map))
                {
                    var p = ExpeditionSpace.InteriorCell(d, map.Id, m.I, m.J);
                    switch (m.Kind)
                    {
                        case MarkerKind.Exit:
                        case MarkerKind.Stairs:
                        {
                            // Steps, and a torch that never goes out: the way back is always lit.
                            var mb = new MeshBuilder();
                            bool down = m.Kind == MarkerKind.Stairs && m.Id.Contains("down");
                            for (int k = 0; k < 5; k++)
                                mb.Box(new Vector3(0, down ? -0.05f - k * 0.02f : 0.12f + k * 0.24f, -1.0f + k * 0.5f), new Vector3(2.4f, down ? 0.06f : 0.24f, 0.5f), down ? new Color(0.04f, 0.04f, 0.05f) : LandLook.Stone * (0.75f - k * 0.05f));
                            mb.ToObject("Steps " + m.Id, root, LandLook.Lit).transform.position = p;
                            if (DayNight.Instance != null)
                            {
                                var t = WorldLights.Torch(root, p + new Vector3(1.6f, 0, 0.6f), false, false, "Way-out torch " + m.Id);
                                t.DayLevel = 1f; t.Priority = 1;
                            }
                            break;
                        }
                        case MarkerKind.Door:
                        {
                            var mb = new MeshBuilder();
                            for (int k = 0; k < 4; k++) mb.Box(new Vector3(0, 0.5f + k * 0.8f, 0), new Vector3(0.3f, 0.34f, 4.6f), LandLook.Wood * (0.8f + 0.06f * k));
                            mb.Box(new Vector3(0.2f, 1.7f, 0), new Vector3(0.14f, 3.2f, 0.4f), new Color(0.3f, 0.3f, 0.32f));
                            var door = mb.ToObject("Door " + m.Id, root, LandLook.Lit);
                            door.transform.position = p;
                            _doors.Add((door, m.OpenWhen));
                            break;
                        }
                    }
                }
                Built++;
            }
        }

        private void Update()
        {
            var c = _boot != null && _boot.Sim != null ? _boot.Sim.State.Campaign : null;
            if (c == null) return;
            foreach (var (go, fact) in _doors)
            {
                bool open = string.IsNullOrEmpty(fact) || c.Knows(fact);
                if (go.activeSelf == open) go.SetActive(!open);
            }
        }
    }
}
