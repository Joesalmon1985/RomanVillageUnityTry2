using System;
using System.Collections.Generic;
using UnityEngine;
using World.Sim;
using World.Sim.Campaign;
using Cell = World.Game.WalkLattice.Cell;

namespace World.Game
{
    // The party's bodies (package I6): Mira and the six placed where the sim says, legs walked in the leg's ticks,
    // formation behind her, gestures while she acts, the crystals in her hands, the retreat with one staying behind,
    // and the viewpoint led underground while interiors are a stand-in.
    public sealed partial class ExpeditionDirector
    {
        // ---------------- companions ----------------

        /// <summary>A companion's body and what the views know of them.</summary>
        public sealed class Mate
        {
            public string Id;
            public CompanionDef Def;
            public Npc Npc;
            public Color Colour;
            /// <summary>Index in the definition's roster.</summary>
            public int Order;
            public bool Alive = true;
            /// <summary>Out scouting (not walking with the party).</summary>
            public bool Out;
            public string Space = "";
            /// <summary>Where they fell (world) and in which space; set when not alive.</summary>
            public Vector3 FellPos;
            public string FellSpace = "";
            /// <summary>The cairn beside a fallen companion (null while alive).</summary>
            public GameObject FallenMark;
        }

        private readonly List<Mate> _mates = new List<Mate>();
        /// <summary>The six companions with their state for views and tests (roster order).</summary>
        public IReadOnlyList<Mate> Mates => _mates;
        public Mate MateOf(string id) => _mates.Find(m => m.Id == id);

        private static readonly Color[] MateColours =
        {
            new Color(0.42f, 0.50f, 0.62f), // Garrick: steel blue
            new Color(0.88f, 0.76f, 0.30f), // Ivo: lamp yellow
            new Color(0.82f, 0.30f, 0.16f), // Petra: ember red
            new Color(0.18f, 0.56f, 0.70f), // Nell: water teal
            new Color(0.26f, 0.52f, 0.22f), // Odo: forest green
            new Color(0.86f, 0.52f, 0.72f), // Tansy: heather pink
        };

        /// <summary>The colour a companion wears (by roster order), so the six tell apart at a glance and on the map.</summary>
        public static Color MateColour(int order) => MateColours[Mathf.Abs(order) % MateColours.Length];

        private FireLight _miraLight, _lampLight;
        private GameObject _carried;
        private readonly List<GameObject> _carriedGems = new List<GameObject>();

        private void DressMira()
        {
            if (!(Mira.Actor is PlaceholderActor a)) return;
            if (a.BodyPivot.Find("ExpeditionStaff") != null) return;
            // A leader must read from the back of the file: a tall staff with a pale stone, and a violet travelling cloak.
            var wood = MaterialFactory.Shared(new Color(0.36f, 0.25f, 0.15f));
            Signs.Prim(PrimitiveType.Cylinder, a.BodyPivot, "ExpeditionStaff", new Vector3(0.42f, 1.15f, 0.12f), new Vector3(0.05f, 1.15f, 0.05f), wood);
            var stone = Signs.Prim(PrimitiveType.Sphere, a.BodyPivot, "StaffStone", new Vector3(0.42f, 2.36f, 0.12f), Vector3.one * 0.17f, MaterialFactory.CreateUnlit(new Color(0.80f, 0.70f, 1f), "Mira staff stone"));
            LightShapes.ApplyDim(stone.GetComponent<Renderer>(), 0.25f);
            Signs.Prim(PrimitiveType.Cube, a.BodyPivot, "Cloak", new Vector3(0, 1.0f, -0.2f), new Vector3(0.56f, 1.0f, 0.05f), MaterialFactory.Shared(new Color(0.34f, 0.18f, 0.56f)), new Vector3(8f, 0, 0));
        }

        private void SpawnCompanions()
        {
            var d = D;
            var home = ExpeditionSpace.World(S, C.Party.At);
            for (int i = 0; i < d.Companions.Count; i++)
            {
                var def = d.Companions[i];
                var colour = MateColour(i);
                string actorId = string.IsNullOrEmpty(def.ActorId) ? "companion_" + def.Id : def.ActorId;
                var npc = Npc.Find(actorId);
                if (npc == null)
                {
                    var pos = ExpeditionSpace.Ground(home + new Vector3(Mathf.Cos(i * 1.05f), 0, Mathf.Sin(i * 1.05f)) * 3f);
                    npc = Npc.Spawn(actorId, def.Name, def.Role, "", colour, pos, 0f);
                }
                npc.Directed = true;
                npc.OnTapped = OnCompanionTapped;
                var m = new Mate { Id = def.Id, Def = def, Npc = npc, Colour = colour, Order = i };
                Dress(m);
                _mates.Add(m);
                _companionNpcs.Add(npc);
            }
        }

        /// <summary>
        /// A readable mark per companion (primitives only; a rigged cast replaces the bodies through ActorFactory and
        /// keeps the ids): Garrick's round shield, Ivo's lamp on a pole, Petra's tongs and leather apron, Nell's coiled
        /// rope and paddle, Odo's long axe with a sprig, Tansy's feathered cap and short bow.
        /// </summary>
        private void Dress(Mate m)
        {
            if (!(m.Npc.Actor is PlaceholderActor a)) return;
            var body = a.BodyPivot; var head = a.HeadPivot;
            var wood = MaterialFactory.Shared(new Color(0.40f, 0.28f, 0.16f));
            var iron = MaterialFactory.Shared(new Color(0.56f, 0.58f, 0.62f));
            var col = MaterialFactory.Shared(m.Colour);
            // A sash in the companion's colour across the chest: the same colour as their dot on the map.
            Signs.Prim(PrimitiveType.Cube, body, "Sash", new Vector3(0, 1.16f, 0.17f), new Vector3(0.50f, 0.13f, 0.04f), col, new Vector3(0, 0, 32f));
            Signs.Prim(PrimitiveType.Cube, body, "SashBack", new Vector3(0, 1.16f, -0.17f), new Vector3(0.50f, 0.13f, 0.04f), col, new Vector3(0, 0, -32f));
            switch (m.Def.Service)
            {
                case CompanionServices.Shield:
                    Signs.Prim(PrimitiveType.Cylinder, body, "Shield", new Vector3(-0.38f, 1.08f, 0.10f), new Vector3(0.78f, 0.035f, 0.78f), col, new Vector3(0, 0, 90f));
                    Signs.Prim(PrimitiveType.Sphere, body, "ShieldBoss", new Vector3(-0.43f, 1.08f, 0.10f), new Vector3(0.10f, 0.20f, 0.20f), iron);
                    Signs.Prim(PrimitiveType.Sphere, head, "Helm", new Vector3(0, 0.22f, 0), new Vector3(0.33f, 0.22f, 0.33f), iron);
                    break;
                case CompanionServices.Lamp:
                {
                    Signs.Prim(PrimitiveType.Cylinder, body, "LampPole", new Vector3(0.40f, 1.25f, 0.14f), new Vector3(0.04f, 1.25f, 0.04f), wood);
                    var lamp = Signs.Prim(PrimitiveType.Cube, body, "Lamp", new Vector3(0.40f, 2.52f, 0.14f), new Vector3(0.20f, 0.26f, 0.20f), MaterialFactory.CreateUnlit(new Color(1f, 0.86f, 0.45f), "Ivo lamp"));
                    LightShapes.ApplyDim(lamp.GetComponent<Renderer>(), 0.15f);
                    Signs.Prim(PrimitiveType.Cylinder, body, "MapCase", new Vector3(-0.05f, 1.05f, -0.24f), new Vector3(0.10f, 0.28f, 0.10f), MaterialFactory.Shared(new Color(0.80f, 0.72f, 0.52f)), new Vector3(0, 0, 70f));
                    if (DayNight.Instance != null)
                    {
                        _lampLight = WorldLights.Add(m.Npc.transform, new Vector3(0.40f, 2.5f, 0.14f), new Color(1f, 0.84f, 0.50f), 1.25f, 9f, 2, "Ivo's lamp");
                        _lampLight.Look = FireLight.Style.Steady; _lampLight.Sparks = false; _lampLight.GroundGlow = 3.0f; _lampLight.Halo = 0.5f;
                    }
                    break;
                }
                case CompanionServices.Fire:
                    Signs.Prim(PrimitiveType.Cube, body, "Apron", new Vector3(0, 0.92f, 0.18f), new Vector3(0.40f, 0.62f, 0.04f), MaterialFactory.Shared(new Color(0.24f, 0.16f, 0.10f)));
                    Signs.Prim(PrimitiveType.Cube, body, "Tongs", new Vector3(0.36f, 0.98f, 0.06f), new Vector3(0.05f, 0.62f, 0.05f), iron, new Vector3(0, 0, 8f));
                    Signs.Prim(PrimitiveType.Sphere, head, "Kerchief", new Vector3(0, 0.22f, -0.02f), new Vector3(0.34f, 0.20f, 0.34f), col);
                    break;
                case CompanionServices.Water:
                    Signs.Prim(PrimitiveType.Cylinder, body, "Rope", new Vector3(-0.22f, 1.12f, -0.02f), new Vector3(0.36f, 0.05f, 0.36f), MaterialFactory.Shared(new Color(0.78f, 0.70f, 0.50f)), new Vector3(0, 0, 60f));
                    Signs.Prim(PrimitiveType.Cube, body, "Paddle", new Vector3(0.20f, 1.30f, -0.26f), new Vector3(0.06f, 1.5f, 0.04f), wood, new Vector3(0, 0, -14f));
                    Signs.Prim(PrimitiveType.Cube, body, "PaddleBlade", new Vector3(0.36f, 1.95f, -0.26f), new Vector3(0.20f, 0.42f, 0.03f), wood, new Vector3(0, 0, -14f));
                    break;
                case CompanionServices.Earth:
                    Signs.Prim(PrimitiveType.Cube, body, "AxeHaft", new Vector3(0.40f, 1.10f, 0.10f), new Vector3(0.05f, 1.7f, 0.05f), wood);
                    Signs.Prim(PrimitiveType.Cube, body, "AxeHead", new Vector3(0.52f, 1.86f, 0.10f), new Vector3(0.26f, 0.16f, 0.04f), iron);
                    Signs.Prim(PrimitiveType.Sphere, head, "Sprig", new Vector3(0.10f, 0.34f, 0), new Vector3(0.20f, 0.12f, 0.20f), MaterialFactory.Shared(new Color(0.30f, 0.62f, 0.24f)));
                    Signs.Prim(PrimitiveType.Cylinder, head, "Hood", new Vector3(0, 0.24f, 0), new Vector3(0.36f, 0.05f, 0.36f), col);
                    break;
                default: // the scout
                    Signs.Prim(PrimitiveType.Cylinder, head, "Cap", new Vector3(0, 0.26f, 0), new Vector3(0.30f, 0.05f, 0.30f), col);
                    Signs.Prim(PrimitiveType.Cube, head, "Feather", new Vector3(0.10f, 0.46f, -0.04f), new Vector3(0.05f, 0.40f, 0.02f), MaterialFactory.Shared(new Color(0.96f, 0.94f, 0.88f)), new Vector3(-14f, 0, -16f));
                    Signs.Prim(PrimitiveType.Cube, body, "Bow", new Vector3(-0.04f, 1.20f, -0.24f), new Vector3(0.04f, 1.05f, 0.04f), wood, new Vector3(0, 0, 24f));
                    m.Npc.transform.localScale = new Vector3(0.92f, 0.92f, 0.92f);
                    break;
            }
        }

        // ---------------- the leg being walked ----------------

        /// <summary>What a leg looked like in the world (tests: the pace comes from the event).</summary>
        public struct LegInfo
        {
            public MarkerRef From, To;
            public int Ticks;
            public bool Running, Link;
            /// <summary>Length of the walked path in metres (0 for a stair, exit or door).</summary>
            public float Metres;
            /// <summary>Metres per game second (Metres / (Ticks / 10)).</summary>
            public float Speed;
            public long Tick;
        }

        private sealed class LegView
        {
            public MarkerRef From, To;
            public int Ticks;
            public bool Running, Link;
            public string SpaceFrom = "", SpaceTo = "";
            public readonly List<Vector3> Pts = new List<Vector3>();
            public float[] Cum;
            public float Length;
        }

        private LegView _leg;
        /// <summary>The last leg begun (from <c>PartySetOut</c>, or rebuilt from state after a load).</summary>
        public LegInfo LastLeg { get; private set; }
        /// <summary>Legs begun since this director started (tests).</summary>
        public int LegCount { get; private set; }

        /// <summary>The space Mira's body is in: an interior id, or "" on the surface.</summary>
        public string MiraSpace { get; private set; } = "";
        private Vector3 _stand;
        private MarkerRef _standRef;
        private bool _hasStand;
        private Vector3? _approach;      // the opening: she comes to the player
        private float _stoodSince;
        private Vector3 _lastMiraPos;
        private float _miraSpeed;

        private struct Crumb { public Vector3 Pos; public string Space; public float Dist; }
        private readonly List<Crumb> _trail = new List<Crumb>();

        private void OnSetOut(PartySetOut so)
        {
            BuildLeg(so.From, so.To, so.LegTicks, so.Running);
            _lastSetOutLead = so.LeadId;
            OnSetOutTalk(so);
        }

        private void OnArrived(PartyArrived ar) { }

        private LegView BuildLeg(MarkerRef from, MarkerRef to, int ticks, bool running)
        {
            var s = S; var d = D;
            var lv = new LegView { From = from, To = to, Ticks = Mathf.Max(1, ticks), Running = running, SpaceFrom = ExpeditionSpace.SpaceOf(from), SpaceTo = ExpeditionSpace.SpaceOf(to) };
            var miraPos = Mira.transform.position;
            if (lv.SpaceFrom != lv.SpaceTo)
            {
                lv.Link = true;
                lv.Pts.Add(ExpeditionSpace.World(s, from));
                lv.Pts.Add(ExpeditionSpace.World(s, to));
            }
            else if (lv.SpaceFrom.Length > 0)
            {
                int[] cells = CampaignApi.LegCells(s);
                var map = d.Interior(lv.SpaceFrom);
                bool matches = map != null && cells.Length >= 4 && ExpeditionSpace.TryInteriorCell(d, to, out int ti, out int tj)
                               && cells[cells.Length - 2] == ti && cells[cells.Length - 1] == tj;
                if (!matches && map != null && ExpeditionSpace.TryInteriorCell(d, from, out int fi, out int fj) && ExpeditionSpace.TryInteriorCell(d, to, out ti, out tj))
                    cells = InteriorMaps.Path(map, fi, fj, ti, tj, f => C.Knows(f)) ?? new[] { fi, fj, ti, tj };
                lv.Pts.AddRange(ExpeditionSpace.CellPath(d, lv.SpaceFrom, cells));
                if (lv.Pts.Count == 0) { lv.Pts.Add(ExpeditionSpace.World(s, from)); lv.Pts.Add(ExpeditionSpace.World(s, to)); }
                if (MiraSpace == lv.SpaceFrom && BoardSpace.Flat(miraPos - lv.Pts[0]) < 7f) lv.Pts[0] = new Vector3(miraPos.x, lv.Pts[0].y, miraPos.z);
                EndBeforeProp(lv, d, to);
            }
            else
            {
                var a = ExpeditionSpace.World(s, from);
                var b = ExpeditionSpace.World(s, to);
                var start = MiraSpace.Length == 0 && BoardSpace.Flat(miraPos - a) < 45f ? miraPos : a;
                lv.Pts.Add(start);
                var fc = WalkLattice.ToCell(start); var tc = WalkLattice.ToCell(b);
                if (fc != tc)
                {
                    var path = WalkGrid.FindPath(fc, tc);
                    if (path == null)
                    {
                        var free = WalkGrid.NearestFree(tc, 3);
                        if (free.HasValue && free.Value != fc) path = WalkGrid.FindPath(fc, free.Value);
                    }
                    if (path != null)
                        for (int i = 0; i < path.Count; i++)
                        {
                            if (i == path.Count - 1 && path[i] == tc) break; // the marker's own point ends the leg
                            lv.Pts.Add(WalkLattice.ToWorld(path[i]));
                        }
                }
                lv.Pts.Add(b);
                EndBeforeProp(lv, d, to);
            }
            lv.Cum = new float[lv.Pts.Count];
            for (int i = 1; i < lv.Pts.Count; i++) lv.Cum[i] = lv.Cum[i - 1] + BoardSpace.Flat(lv.Pts[i] - lv.Pts[i - 1]);
            lv.Length = lv.Link ? 0f : lv.Cum[lv.Cum.Length - 1];
            _leg = lv;
            LegCount++;
            LastLeg = new LegInfo
            {
                From = from, To = to, Ticks = lv.Ticks, Running = running, Link = lv.Link, Metres = lv.Length,
                Speed = lv.Length / (lv.Ticks / (float)WorldSim.TicksPerSecond), Tick = S.Tick,
            };
            return lv;
        }

        /// <summary>The leader stops a pace short of a thing (stone, pedestal, altar, witness) on the side she came from.</summary>
        private static void EndBeforeProp(LegView lv, CampaignDefinition d, MarkerRef to)
        {
            if (!ExpeditionSpace.IsProp(ExpeditionSpace.MarkerOf(d, to)) || lv.Pts.Count < 2) return;
            var end = lv.Pts[lv.Pts.Count - 1];
            // Back along the path until far enough from the thing.
            for (int i = lv.Pts.Count - 2; i >= 0; i--)
            {
                var prev = lv.Pts[i];
                float dist = BoardSpace.Flat(prev - end);
                if (dist < 0.05f) { if (lv.Pts.Count > 2) lv.Pts.RemoveAt(lv.Pts.Count - 1); continue; }
                if (dist <= ExpeditionSpace.PropStandOff) { lv.Pts.RemoveAt(lv.Pts.Count - 1); return; }
                var dir = (prev - end); dir.y = 0;
                var stop = end + dir.normalized * ExpeditionSpace.PropStandOff;
                stop.y = end.y;
                lv.Pts[lv.Pts.Count - 1] = stop;
                return;
            }
        }

        private static Vector3 Sample(LegView lv, float t, out Vector3 ahead)
        {
            float dist = Mathf.Clamp01(t) * lv.Length;
            for (int i = 1; i < lv.Pts.Count; i++)
            {
                if (dist > lv.Cum[i] && i < lv.Pts.Count - 1) continue;
                float seg = lv.Cum[i] - lv.Cum[i - 1];
                float u = seg > 1e-4f ? Mathf.Clamp01((dist - lv.Cum[i - 1]) / seg) : 1f;
                ahead = lv.Pts[i];
                return Vector3.Lerp(lv.Pts[i - 1], lv.Pts[i], u);
            }
            ahead = lv.Pts[lv.Pts.Count - 1];
            return ahead;
        }

        // ---------------- placing the party each frame ----------------

        private void UpdateParty(bool frozen)
        {
            var s = S; var c = C; var p = c.Party;
            float dt = Time.deltaTime;
            if (_retreat != null) { UpdateRetreat(); UpdateCarried(); return; }
            if (!frozen)
            {
                bool travelling = CampaignApi.PartyPosition(s, TickFraction, out var from, out var to, out double t);
                if (travelling)
                {
                    if (_leg == null || _leg.From != from || _leg.To != to) BuildLeg(from, to, p.Leg.Ticks, p.Running);
                    _approach = null;
                    if (Mira.IsWalking) Mira.StopWalking();
                    PlaceOnLeg(_leg, (float)t);
                    _hasStand = false;
                    _stoodSince = -1f;
                }
                else PlaceStanding(dt);
            }
            var pos = Mira.transform.position;
            _miraSpeed = dt > 1e-5f ? BoardSpace.Flat(pos - _lastMiraPos) / dt : 0f;
            _lastMiraPos = pos;
            var crumb = pos;
            if (_leg != null && _leg.Link && C.Party.Mode == PartyMode.Travelling)
            {
                // On a stair she sinks out of sight; the way she came stays at floor level.
                var end = MiraSpace == _leg.SpaceFrom ? _leg.Pts[0] : _leg.Pts[1];
                crumb.y = MiraSpace.Length == 0 ? PlayerWalker.GroundHeight(end) : end.y;
            }
            Drop(crumb, MiraSpace);
            UpdateMates(dt, frozen);
            UpdateGestures();
            UpdateCarried();
        }

        private void PlaceOnLeg(LegView lv, float t)
        {
            if (lv.Link)
            {
                // A stair, an exit or a door: down out of sight at one end, up into sight at the other.
                bool first = t < 0.5f;
                float u = first ? t / 0.5f : (1f - t) / 0.5f;
                var at = first ? lv.Pts[0] : lv.Pts[1];
                var pos = lv.SpaceFrom.Length == 0 && first || lv.SpaceTo.Length == 0 && !first ? ExpeditionSpace.Ground(at) : at;
                pos.y -= 1.9f * Mathf.SmoothStep(0f, 1f, u);
                SetMira(pos, first ? lv.SpaceFrom : lv.SpaceTo, null);
                return;
            }
            var point = Sample(lv, t, out var ahead);
            if (lv.SpaceTo.Length == 0) point = ExpeditionSpace.Ground(point);
            SetMira(point, lv.SpaceTo, ahead);
        }

        private void SetMira(Vector3 pos, string space, Vector3? face)
        {
            MiraSpace = space ?? "";
            Mira.transform.position = pos;
            if (face.HasValue && BoardSpace.Flat(face.Value - pos) > 0.05f) Mira.FaceTowards(face.Value);
        }

        /// <summary>Not travelling: she stands at the party's place (in front of the thing there), or comes to the player for the opening.</summary>
        private void PlaceStanding(float dt)
        {
            var s = S; var p = C.Party;
            if (!_hasStand || _standRef != p.At)
            {
                // The leg just walked ends where she stands; any other way of getting here (a duel's outcome, a load) uses the place's own stand point.
                bool fromLeg = _leg != null && _leg.To == p.At && _leg.Pts.Count > 0;
                _stand = fromLeg ? _leg.Pts[_leg.Pts.Count - 1] : ExpeditionSpace.DefaultStand(s, p.At);
                _standRef = p.At; _hasStand = true;
                string space = ExpeditionSpace.SpaceOf(p.At);
                bool far = space != MiraSpace || BoardSpace.Flat(Mira.transform.position - _stand) > 30f;
                if (far || space.Length > 0 && fromLeg)
                {
                    if (Mira.IsWalking) Mira.StopWalking();
                    SetMira(space.Length == 0 ? ExpeditionSpace.Ground(_stand) : _stand, space, null);
                    if (far) _trail.Clear();
                }
                MiraSpace = space;
                _stoodSince = Time.time;
            }
            if (_stoodSince < 0f) _stoodSince = Time.time;
            var target = _approach ?? _stand;
            var pos = Mira.transform.position;
            float dist = BoardSpace.Flat(pos - target);
            if (MiraSpace.Length > 0)
            {
                if (Mira.IsWalking) Mira.StopWalking();
                if (dist > 0.05f)
                {
                    var next = Vector3.MoveTowards(new Vector3(pos.x, target.y, pos.z), target, 3.2f * dt);
                    SetMira(next, MiraSpace, target);
                }
                return;
            }
            if (dist > 0.6f)
            {
                if (!Mira.IsWalking || !Mira.Goal.HasValue || BoardSpace.Flat(Mira.Goal.Value - target) > 0.5f)
                {
                    Mira.WalkSpeed = _approach.HasValue ? ApproachSpeed : 3.5f;
                    Mira.WalkTo(ExpeditionSpace.Ground(target));
                }
            }
        }

        /// <summary>True while Mira stands where the party is (not walking a leg, not still on her way to the spot).</summary>
        public bool MiraSettled => _hasStand && !Mira.IsWalking && C.Party.Mode != PartyMode.Travelling && BoardSpace.Flat(Mira.transform.position - (_approach ?? _stand)) < 1.2f;

        /// <summary>Everyone to where the state says, at once (start, load).</summary>
        private void PlaceFromState(bool snap)
        {
            var s = S; var p = C.Party;
            _trail.Clear();
            _leg = null; _hasStand = false; _approach = null;
            if (CampaignApi.PartyPosition(s, 0, out var from, out var to, out double t))
            {
                BuildLeg(from, to, p.Leg.Ticks, p.Running);
                // After a load she is on the leg's own line, not at the camp spot she left from.
                PlaceOnLeg(_leg, (float)t);
            }
            else
            {
                _stand = ExpeditionSpace.DefaultStand(s, p.At);
                _standRef = p.At; _hasStand = true;
                string space = ExpeditionSpace.SpaceOf(p.At);
                SetMira(space.Length == 0 ? ExpeditionSpace.Ground(_stand) : _stand, space, null);
                var thing = ExpeditionSpace.World(s, p.At);
                if (BoardSpace.Flat(thing - _stand) > 0.5f && Mira.Actor is PlaceholderActor pa) pa.SetYaw(BoardSpace.YawOf(thing - _stand));
            }
            _lastMiraPos = Mira.transform.position;
            Drop(_lastMiraPos, MiraSpace);
            if (!snap) return;
            int k = 0;
            foreach (var m in _mates)
            {
                if (!m.Alive) continue;
                var slot = GatherSlot(k++, LivingCount());
                m.Space = MiraSpace;
                m.Npc.transform.position = MiraSpace.Length == 0 ? ExpeditionSpace.Ground(slot) : slot;
                m.Npc.FaceTowards(Mira.transform.position);
            }
        }

        private int LivingCount() { int n = 0; foreach (var m in _mates) if (m.Alive && !m.Out) n++; return n; }

        // ---------------- the trail and the file behind her ----------------

        private void Drop(Vector3 pos, string space)
        {
            if (_trail.Count == 0) { _trail.Add(new Crumb { Pos = pos, Space = space, Dist = 0f }); return; }
            var last = _trail[_trail.Count - 1];
            if (last.Space != space) { _trail.Add(new Crumb { Pos = pos, Space = space, Dist = last.Dist }); return; }
            float d = BoardSpace.Flat(pos - last.Pos);
            if (d < 0.35f) return;
            if (d > 25f) { _trail.Clear(); _trail.Add(new Crumb { Pos = pos, Space = space, Dist = 0f }); return; } // placed elsewhere
            _trail.Add(new Crumb { Pos = pos, Space = space, Dist = last.Dist + d });
            while (_trail.Count > 2 && _trail[_trail.Count - 1].Dist - _trail[1].Dist > 48f) _trail.RemoveAt(0);
        }

        /// <summary>The point <paramref name="back"/> metres behind Mira along the way she came, with the direction of travel there.</summary>
        private bool TrailAt(float back, out Vector3 pos, out Vector3 dir, out string space)
        {
            pos = Mira.transform.position; dir = Mira.transform.forward; space = MiraSpace;
            if (_trail.Count == 0) return false;
            float want = _trail[_trail.Count - 1].Dist - back;
            for (int i = _trail.Count - 1; i >= 1; i--)
            {
                var a = _trail[i - 1]; var b = _trail[i];
                if (a.Dist > want) continue;
                if (a.Space != b.Space) { pos = b.Pos; space = b.Space; dir = i + 1 < _trail.Count ? _trail[i + 1].Pos - b.Pos : dir; dir.y = 0; return true; }
                float seg = b.Dist - a.Dist;
                float u = seg > 1e-4f ? Mathf.Clamp01((want - a.Dist) / seg) : 1f;
                pos = Vector3.Lerp(a.Pos, b.Pos, u); space = b.Space;
                dir = b.Pos - a.Pos; dir.y = 0;
                return true;
            }
            pos = _trail[0].Pos; space = _trail[0].Space;
            if (_trail.Count > 1) { dir = _trail[1].Pos - _trail[0].Pos; dir.y = 0; }
            return false;
        }

        /// <summary>Where the k-th living companion stands when the party is at a place: an arc round the party's spot, open towards the thing.</summary>
        private Vector3 GatherSlot(int k, int n)
        {
            var centre = _hasStand ? _stand : Mira.transform.position;
            var thing = ExpeditionSpace.World(S, C.Party.At);
            var foe = EnemyHere();
            if (foe.HasValue) thing = foe.Value; // at a threshold they stand to either side of her, facing the keeper
            if (_approach.HasValue && Walker != null)
            {
                // The opening: the six come up behind her, facing the player.
                centre = _approach.Value; thing = Walker.Position;
            }
            var fwd = thing - centre; fwd.y = 0;
            if (fwd.sqrMagnitude < 0.2f) fwd = Mira.transform.forward;
            fwd.Normalize();
            // Three to each side of her and the thing, never straight behind: whoever follows sees Mira and what she does.
            float back = Mathf.Atan2(-fwd.x, -fwd.z) * Mathf.Rad2Deg;
            float ang = (back + (k % 2 == 0 ? -1f : 1f) * (58f + 26f * (k / 2))) * Mathf.Deg2Rad;
            float r = 2.7f + (k / 2) * 0.35f;
            for (int tries = 0; tries < 4; tries++)
            {
                var slot = centre + new Vector3(Mathf.Sin(ang), 0, Mathf.Cos(ang)) * r;
                if (MiraSpace.Length == 0 || ExpeditionSpace.OnFloor(D, MiraSpace, slot)) return slot;
                r *= 0.6f;
            }
            return centre;
        }

        private void UpdateMates(float dt, bool frozen)
        {
            bool travelling = C.Party.Mode == PartyMode.Travelling;
            int n = LivingCount(), k = 0;
            float legSpeed = Mathf.Max(3.2f, _miraSpeed * 1.35f + 1.2f);
            foreach (var m in _mates)
            {
                m.Npc.Hold = frozen || !m.Alive;
                // The roster is the state's: a companion killed or brought back by the developer tools follows it too.
                var st = C.Companion(m.Id);
                if (st != null && st.Alive != m.Alive)
                {
                    if (!st.Alive) LayFallen(m, m.Npc.transform.position, m.Space);
                    else Revive(m);
                }
                if (!m.Alive) continue;
                if (m.Out) { UpdateScoutBody(m, dt); continue; }
                int slotIndex = k++;
                if (frozen) continue;
                Vector3 want; string space; Vector3 face;
                if (travelling)
                {
                    // Two files behind her, along the way she walked.
                    float back = 1.9f + (slotIndex / 2) * 1.55f;
                    TrailAt(back, out want, out var dir, out space);
                    if (dir.sqrMagnitude < 1e-4f) dir = Mira.transform.forward;
                    dir.Normalize();
                    var side = new Vector3(dir.z, 0, -dir.x) * ((slotIndex % 2 == 0 ? -1f : 1f) * (space.Length > 0 ? 0.55f : 0.8f));
                    if (space.Length == 0 || ExpeditionSpace.OnFloor(D, space, want + side)) want += side;
                    face = want + dir * 3f;
                }
                else
                {
                    want = GatherSlot(slotIndex, n); space = MiraSpace;
                    face = _approach.HasValue && Walker != null ? Walker.Position : EnemyHere() ?? (_hasStand ? ExpeditionSpace.World(S, C.Party.At) : Mira.transform.position);
                    if (BoardSpace.Flat(face - want) < 1f) face = Mira.transform.position;
                }
                var pos = m.Npc.transform.position;
                if (m.Space != space)
                {
                    // Through a stair after her, one by one as the file reaches it.
                    m.Space = space;
                    pos = want;
                }
                else
                {
                    float dist = BoardSpace.Flat(want - pos);
                    if (dist > 45f) pos = want;
                    else if (dist > 0.04f)
                    {
                        float speed = Mathf.Min(legSpeed, Mathf.Max(1.2f, dist * 2.2f));
                        var flat = Vector3.MoveTowards(new Vector3(pos.x, 0, pos.z), new Vector3(want.x, 0, want.z), speed * dt);
                        pos = new Vector3(flat.x, want.y, flat.z);
                        if (dist > 0.3f) m.Npc.FaceTowards(new Vector3(want.x, pos.y, want.z));
                    }
                    else if (!travelling || _miraSpeed < 0.2f) m.Npc.FaceTowards(face);
                }
                if (space.Length == 0) pos.y = PlayerWalker.GroundHeight(pos);
                else if (travelling && C.Party.Leg != null && _leg != null && _leg.Link) pos.y = Mira.transform.position.y > want.y - 0.5f ? want.y : pos.y;
                m.Npc.transform.position = pos;
            }
        }

        // ---------------- gestures while she acts ----------------

        private QuestAction _gestureFor = (QuestAction)(-1);
        private float _nextGesture;

        private void UpdateGestures()
        {
            var p = C.Party;
            if (p.Mode != PartyMode.Acting)
            {
                if ((int)_gestureFor >= 0) { Mira.PointAt(null); _gestureFor = (QuestAction)(-1); }
                // Standing with the player in talk: she faces whoever she speaks to.
                if (p.Mode != PartyMode.Travelling && !Mira.IsWalking && Walker != null && (_ask != 0 || _scriptHandle != 0) && PlayerNear(AskDistance))
                    Mira.FaceTowards(Walker.Position);
                return;
            }
            var thing = ExpeditionSpace.World(S, p.At);
            if (_gestureFor != p.ActingAction) { _gestureFor = p.ActingAction; _nextGesture = 0f; }
            if (BoardSpace.Flat(thing - Mira.transform.position) > 0.4f) Mira.FaceTowards(thing);
            var action = p.ActingAction;
            if (action == QuestAction.Take)
            {
                // Taking from a bare pedestal is looking at a bare pedestal.
                var here = D.Crystals.Find(x => x.Site == p.At);
                if (here == null || C.Crystal(here.Element).Custody != Custody.AtSite) action = QuestAction.Examine;
            }
            switch (action)
            {
                case QuestAction.Take:
                case QuestAction.Restore:
                case QuestAction.Light:
                case QuestAction.Open:
                    Mira.PointAt(null);
                    if (Time.time >= _nextGesture) { Mira.Actor?.PlayOneShot(ActorKeys.Cast); _nextGesture = Time.time + 1.6f; }
                    break;
                case QuestAction.Talk:
                    Mira.PointAt(null);
                    if (Time.time >= _nextGesture) { Mira.Actor?.PlayOneShot(ActorKeys.Greet); _nextGesture = Time.time + 6f; }
                    break;
                default: // Examine, Search: she bends to it and points; whoever reads such marks leans in too
                    Mira.PointAt(thing + Vector3.up * 0.9f);
                    if (Time.time >= _nextGesture)
                    {
                        Mira.Actor?.PlayOneShot(ActorKeys.Greet);
                        var reader = _mates.Find(m => m.Alive && !m.Out && m.Def.Service == CompanionServices.Earth) ?? _mates.Find(m => m.Alive && !m.Out);
                        reader?.Npc.Actor?.PlayOneShot(ActorKeys.Greet);
                        _nextGesture = Time.time + 3.4f;
                    }
                    break;
            }
        }

        // ---------------- the crystals she carries ----------------

        /// <summary>Crystals shown in Mira's hands now (tests).</summary>
        public int CarriedShown { get; private set; }

        private void UpdateCarried()
        {
            var c = C;
            int n = 0;
            for (int i = 0; i < c.Crystals.Length; i++)
            {
                if (c.Crystals[i].Custody != Custody.Carried) continue;
                // While she sets it, the crystal is on its way into the tower (the site view shows it there).
                var cd = c.Definition.Crystal((Element)i);
                if (c.Party.Mode == PartyMode.Acting && c.Party.ActingAction == QuestAction.Restore && cd != null && c.Party.ActingCrystal == cd.Id) continue;
                if (n >= _carriedGems.Count) _carriedGems.Add(null);
                var want = "Carried " + (Element)i;
                if (_carriedGems[n] == null || _carriedGems[n].name != want)
                {
                    if (_carriedGems[n] != null) Destroy(_carriedGems[n]);
                    var parent = Mira.Actor is PlaceholderActor pa ? pa.BodyPivot : Mira.transform;
                    _carriedGems[n] = ExpeditionSites.MakeCrystal(parent, want, (Element)i, 0.24f, true);
                }
                _carriedGems[n].transform.localPosition = new Vector3(-0.16f + 0.2f * n, 1.24f, 0.36f);
                _carriedGems[n].transform.localRotation = Quaternion.Euler(0, 20f + 40f * n, 0);
                n++;
            }
            for (int i = _carriedGems.Count - 1; i >= n; i--) { if (_carriedGems[i] != null) Destroy(_carriedGems[i]); _carriedGems.RemoveAt(i); }
            CarriedShown = n;
        }

        private void OnCustody(CrystalCustodyChanged cc) { if (Sites != null) Sites.OnCustody(cc); }

        // ---------------- the scout ----------------

        private void OnScoutSent(ScoutSent ss)
        {
            var m = MateOf(ss.CompanionId);
            if (m != null) { m.Out = true; m.Npc.Actor?.PlayOneShot(ActorKeys.Wave); }
        }

        private void OnScoutReturned()
        {
            foreach (var m in _mates)
                if (m.Out) { m.Out = false; m.Space = "?"; }
        }

        /// <summary>Where the scout is on the island (straight between the party's place and her target, by the sim's leg). False when nobody is out.</summary>
        public bool ScoutPosition(out Vector3 pos, out string companionId)
        {
            pos = default; companionId = null;
            var sc = C?.Scout;
            if (sc == null || !sc.Out || sc.Leg == null) return false;
            companionId = sc.CompanionId;
            var d = D;
            Vector3 a = AnchorPoint(sc.Leg.From.Location), b = AnchorPoint(sc.Leg.To.Location);
            float t = sc.Leg.Ticks > 0 ? Mathf.Clamp01(sc.Leg.Progress / (float)sc.Leg.Ticks) : 1f;
            pos = sc.Phase >= 3 ? Vector3.Lerp(b, a, t) : sc.Phase == 2 ? b : Vector3.Lerp(a, b, t);
            return true;
        }

        private Vector3 AnchorPoint(string locationId)
        {
            var loc = D.Location(locationId);
            if (loc != null && CampaignPlaces.TryAnchorPoint(D, loc, S.Board, out double x, out double z)) return new Vector3((float)x, 0, (float)z);
            return Mira.transform.position;
        }

        private void UpdateScoutBody(Mate m, float dt)
        {
            if (!ScoutPosition(out var pos, out var who) || who != m.Id) { m.Out = false; return; }
            m.Space = "";
            var from = m.Npc.transform.position;
            var want = ExpeditionSpace.Ground(pos);
            if (BoardSpace.Flat(want - from) > 0.2f) m.Npc.FaceTowards(want);
            m.Npc.transform.position = BoardSpace.Flat(want - from) > 60f ? want : Vector3.MoveTowards(from, want, 9f * dt);
        }

        // ---------------- a companion covers the retreat ----------------

        private sealed class RetreatScene
        {
            public float Start, Seconds = 4.6f;
            public string Volunteer;          // companion id, "" when nobody fell (a draw)
            public Vector3 Gap;               // where the volunteer stands
            public Vector3 Enemy;             // what they face
            public string Space = "";
            public bool Began, Ended;
            public float RunFrom;             // trail distance of Mira at the start
        }

        private RetreatScene _retreat;
        private CompanionFell _pendingFell;
        private Vector3 _preResolvePos;
        private string _preResolveSpace = "";
        /// <summary>True while the retreat is being played out (the party runs, one stays).</summary>
        public bool RetreatPlaying => _retreat != null;
        /// <summary>Retreats played since this director started (tests).</summary>
        public int RetreatsPlayed { get; private set; }
        /// <summary>The companion who stayed in the last retreat ("" for a draw).</summary>
        public string LastVolunteer { get; private set; } = "";

        private void OnCompanionFell(CompanionFell cf) { _pendingFell = cf; }

        private void OnRetreated(PartyRetreated pr)
        {
            if (pr.Why != "defeat" && pr.Why != "draw") { OnWithdrawTalk(pr); return; }
            // The sim has already put the party at the safe point; the scene shows how they got out.
            var e = _lastResolvedEncounter != null ? D.Encounter(_lastResolvedEncounter) : null;
            var scene = new RetreatScene { Start = -1f, Space = _preResolveSpace, Volunteer = _pendingFell != null ? _pendingFell.CompanionId : "" };
            var enemy = e != null && !e.Stage.Opponent.IsEmpty ? ExpeditionSpace.World(S, e.Stage.Opponent) : _preResolvePos + Mira.transform.forward * 5f;
            scene.Enemy = enemy;
            // The gap: a pace towards the enemy from where the party stood (the mouth of the vault, the foot of the steps).
            var toEnemy = enemy - _preResolvePos; toEnemy.y = 0;
            scene.Gap = _preResolvePos + (toEnemy.sqrMagnitude > 0.01f ? toEnemy.normalized : Vector3.forward) * 2.4f;
            _pendingFell = null;
            _retreat = scene;
            if (!string.IsNullOrEmpty(scene.Volunteer))
            {
                var m = MateOf(scene.Volunteer);
                if (m != null) { m.Alive = false; m.FellPos = scene.Gap; m.FellSpace = scene.Space; }
            }
            LastVolunteer = scene.Volunteer ?? "";
        }

        private void UpdateRetreat()
        {
            var sc = _retreat;
            bool encounter = EncounterHost.Instance != null && EncounterHost.Instance.IsRunning;
            if (encounter) return; // the duel's own screen first
            if (sc.Start < 0f)
            {
                sc.Start = Time.unscaledTime; sc.Began = true;
                RetreatsPlayed++;
                // Everyone back where they stood when the duel ended.
                if (Mira.IsWalking) Mira.StopWalking();
                SetMira(_preResolvePos, _preResolveSpace, null);
                sc.RunFrom = _trail.Count > 0 ? _trail[_trail.Count - 1].Dist : 0f;
                OnRetreatBegins(sc);
            }
            float t = Time.unscaledTime - sc.Start;
            float run = Mathf.Max(0f, t - 0.5f) * 6.2f;
            // Mira and the living run back along the way they came; the volunteer steps into the gap and holds.
            TrailBack(run, out var mp, out var mdir, out var mspace);
            if (mspace == sc.Space) { Mira.transform.position = mspace.Length == 0 ? ExpeditionSpace.Ground(mp) : mp; if (t > 0.5f) Mira.FaceTowards(mp - mdir * 4f); }
            int k = 0;
            foreach (var m in _mates)
            {
                if (m.Id == sc.Volunteer)
                {
                    var pos = Vector3.MoveTowards(m.Npc.transform.position, sc.Gap, 4.5f * Time.unscaledDeltaTime);
                    if (sc.Space.Length == 0) pos.y = PlayerWalker.GroundHeight(pos);
                    m.Npc.transform.position = pos;
                    m.Npc.FaceTowards(sc.Enemy);
                    m.Npc.PointAt(sc.Enemy + Vector3.up * 1.2f);
                    continue;
                }
                if (!m.Alive || m.Out) continue;
                float back = run + 1.6f + (k / 2) * 1.4f + 0.6f;
                k++;
                TrailBack(back, out var p, out var dir, out var space);
                if (space != sc.Space) continue;
                var side = new Vector3(dir.z, 0, -dir.x).normalized * ((k % 2 == 0 ? -1f : 1f) * 0.55f);
                var want = p + (sc.Space.Length == 0 || ExpeditionSpace.OnFloor(D, sc.Space, p + side) ? side : Vector3.zero);
                var cur = m.Npc.transform.position;
                var next = Vector3.MoveTowards(cur, want, 7.5f * Time.unscaledDeltaTime);
                if (sc.Space.Length == 0) next.y = PlayerWalker.GroundHeight(next);
                m.Npc.transform.position = next;
                if (BoardSpace.Flat(want - cur) > 0.2f) m.Npc.FaceTowards(want);
            }
            if (t < sc.Seconds) return;
            // Over: the fallen lies where they stood; the rest are at the safe point, as the state says.
            var fallen = MateOf(sc.Volunteer);
            if (fallen != null) { fallen.Npc.PointAt(null); LayFallen(fallen, sc.Gap, sc.Space); }
            _retreat = null;
            PlaceFromState(true);
            OnRetreatEnded(sc);
        }

        /// <summary>The point reached by running <paramref name="run"/> metres back along the trail from where Mira stood.</summary>
        private void TrailBack(float run, out Vector3 pos, out Vector3 dir, out string space)
        {
            TrailAt(run, out pos, out dir, out space);
        }

        private Vector3 FellPoint(Mate m, CompanionState st)
        {
            var e = D.Encounter(st.FellAt);
            if (e == null) return ExpeditionSpace.DefaultStand(S, C.Party.At);
            MarkerRef at = !e.Stage.Volunteer.IsEmpty ? e.Stage.Volunteer : e.Threshold;
            if (at.IsEmpty) return ExpeditionSpace.DefaultStand(S, C.Party.At);
            // Fallen companions lie side by side at the gap.
            var p = ExpeditionSpace.World(S, at);
            return p + new Vector3(0.9f * (m.Order % 3 - 1), 0, 0.5f * (m.Order / 3));
        }

        private string FellSpace(CompanionState st)
        {
            var e = D.Encounter(st.FellAt);
            if (e == null) return "";
            MarkerRef at = !e.Stage.Volunteer.IsEmpty ? e.Stage.Volunteer : e.Threshold;
            return ExpeditionSpace.SpaceOf(at);
        }

        /// <summary>A fallen companion: the body lies where they held the way, with their mark beside them; a tap names them.</summary>
        private void LayFallen(Mate m, Vector3 at, string space)
        {
            m.Alive = false; m.Out = false;
            m.FellPos = at; m.FellSpace = space; m.Space = space;
            var t = m.Npc.transform;
            var pos = space.Length == 0 ? ExpeditionSpace.Ground(at) : at;
            m.Npc.StopWalking();
            m.Npc.Hold = true;
            m.Npc.PointAt(null);
            if (m.Npc.Actor is PlaceholderActor pa) pa.enabled = false; // no idle bob or turning: they lie still
            t.SetPositionAndRotation(pos + Vector3.up * 0.28f, Quaternion.Euler(-88f, 40f + m.Order * 47f, 0f));
            m.Npc.Role = (m.Def.Role ?? "") + ". " + (ExpeditionLines.Get(S, ExpeditionLines.CompanionFallen) ?? "fell here.");
            if (m.FallenMark == null)
            {
                // A small cairn in their colour at their head.
                var mb = new MeshBuilder();
                var rnd = new PropRandom(m.Order * 977 + 5);
                for (int i = 0; i < 5; i++) mb.Rock(new Vector3(rnd.Range(-0.35f, 0.35f), 0, rnd.Range(-0.3f, 0.3f)), rnd.Range(0.22f, 0.36f), LandLook.Stone * rnd.Range(0.8f, 1.05f), rnd.Next);
                mb.Box(new Vector3(0, 0.62f, 0), new Vector3(0.10f, 0.5f, 0.10f), LandLook.DarkWood);
                mb.Box(new Vector3(0, 0.82f, 0.02f), new Vector3(0.36f, 0.16f, 0.03f), m.Colour);
                m.FallenMark = mb.ToObject("Cairn of " + m.Def.Name, _root, LandLook.Lit);
            }
            m.FallenMark.transform.position = pos + new Vector3(0.9f, 0, 0.3f);
        }

        private void Revive(Mate m)
        {
            m.Alive = true; m.Space = "?";
            if (m.Npc.Actor is PlaceholderActor pa) { pa.enabled = true; pa.SetYaw(0f); }
            m.Npc.transform.rotation = Quaternion.identity;
            m.Npc.Role = m.Def.Role;
            if (m.FallenMark != null) { Destroy(m.FallenMark); m.FallenMark = null; }
        }

        // ---------------- the player's space (package I5's interiors) ----------------

        /// <summary>The space the player is in: an interior id, or "" on the surface.</summary>
        public string PlayerSpace => Interiors.Current ?? "";

        /// <summary>The keeper the party stands before (the opponent of the encounter whose threshold is the party's place), or null.</summary>
        private Vector3? EnemyHere()
        {
            var p = C.Party;
            foreach (var e in D.Encounters)
            {
                if (e == null || e.Threshold.IsEmpty || e.Threshold != p.At || e.Stage == null || e.Stage.Opponent.IsEmpty) continue;
                return ExpeditionSpace.World(S, e.Stage.Opponent);
            }
            return null;
        }
    }
}
