using System;
using System.Collections.Generic;
using UnityEngine;
using World.Sim;
using World.Sim.Campaign;
using Cell = World.Game.WalkLattice.Cell;

namespace World.Game
{
    /// <summary>
    /// The objects that stand for an interior's markers (package I5), each driven by the sim: stairs and exits (lit,
    /// shut until their facts are known), doors and barred doors, pedestals and the altar with or without their crystal,
    /// carved marks you can tap to read, the tally spot by a robbed pedestal, the threshold, the duel spots, the safe
    /// point, a witness's seat, a lever, a plinth. Original procedural shapes.
    /// </summary>
    public static class InteriorProps
    {
        public static void Build(InteriorDirector d, InteriorModel m, MarkerDef md, List<IInteriorView> views)
        {
            var cell = m.ToCell(md.I, md.J);
            var root = new GameObject(md.Kind + " " + md.Id).transform;
            root.SetParent(m.Root, false);
            root.position = PlayerWalker.CellPosition(cell);
            var r = m.Ref(md.Id);
            var thing = root.gameObject.AddComponent<InteriorThing>();
            thing.Ref = r; thing.Marker = md; thing.Director = d;
            var link = Interiors.LinkAt(r);
            switch (md.Kind)
            {
                case MarkerKind.Exit:
                case MarkerKind.Stairs:
                    if (link != null) { views.Add(Stair(root, m, md, link, thing)); break; }
                    goto default;
                case MarkerKind.Door:
                    views.Add(Door(root, m, md, thing));
                    if (link != null) thing.Link = link;
                    break;
                case MarkerKind.Pedestal:
                case MarkerKind.Altar:
                    views.Add(CrystalStand(root, d, m, md, thing));
                    break;
                case MarkerKind.Inscription:
                    if (NextToStand(m, md)) views.Add(TallySpot(root, d, m, md, thing));
                    else Carving(root, m, md, thing);
                    break;
                case MarkerKind.Threshold: Threshold(root, m, md); break;
                case MarkerKind.SafePoint: Cairn(root, m, md); break;
                case MarkerKind.DuelCaster: Spot(root, new Color(0.80f, 0.74f, 0.58f), 1.2f, false); break;
                case MarkerKind.DuelOpponent: Spot(root, new Color(0.30f, 0.62f, 0.40f), 1.2f, true); break;
                case MarkerKind.Witness: Seat(root, m, md); break;
                case MarkerKind.Lever: Lever(root, m, md); break;
                default: Plinth(root); break;
            }
            LandLook.PickBox(root.gameObject, new Vector3(0, 1.0f, 0), new Vector3(2.2f, 2.0f, 2.2f));
            WorldZone.Tag(root.gameObject, WorldZones.CryInteract); // WU-6
            if (thing.Link == null && md.Kind != MarkerKind.Exit && md.Kind != MarkerKind.Stairs)
                root.gameObject.AddComponent<MiraActThing>().Thing = thing; // WU-6: Mira's act offered on a tap
            else if (thing.Link != null)
                root.gameObject.AddComponent<InteriorWay>().Thing = thing; // P2a: stairs and exits use the ledger preview, not a speech bubble
        }

        // ---------------- helpers ----------------

        private static Vector3 DirOf(int heading) { float y = WalkLattice.Yaw(heading) * Mathf.Deg2Rad; return new Vector3(Mathf.Sin(y), 0, Mathf.Cos(y)); }

        /// <summary>The heading of the rock wall a marker leans against (the one opposite the most open side), or -1 in the open.</summary>
        internal static int WallHeading(InteriorModel m, Cell c)
        {
            int best = -1, bestScore = int.MinValue;
            for (int h = 0; h < 6; h++)
            {
                if (m.Floor.Contains(c + WalkLattice.Dir(h))) continue;
                int score = (m.Floor.Contains(c + WalkLattice.Dir(h + 3)) ? 4 : 0) + (m.Floor.Contains(c + WalkLattice.Dir(h + 2)) ? 1 : 0) + (m.Floor.Contains(c + WalkLattice.Dir(h + 4)) ? 1 : 0);
                if (score > bestScore) { bestScore = score; best = h; }
            }
            return best;
        }

        /// <summary>The most open heading from a cell (towards the room).</summary>
        internal static int OpenHeading(InteriorModel m, Cell c)
        {
            int best = 0, bestScore = int.MinValue;
            for (int h = 0; h < 6; h++)
            {
                int score = (m.Floor.Contains(c + WalkLattice.Dir(h)) ? 4 : -10) + (m.Floor.Contains(c + WalkLattice.Dir(h - 1)) ? 1 : 0) + (m.Floor.Contains(c + WalkLattice.Dir(h + 1)) ? 1 : 0);
                if (score > bestScore) { bestScore = score; best = h; }
            }
            return best;
        }

        private static GameObject Mesh(Transform parent, string name, MeshBuilder mb, Material mat = null)
        {
            var go = mb.ToObject(name, parent, mat != null ? mat : InteriorBuilder.RockMaterial);
            go.transform.localPosition = Vector3.zero;
            go.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return go;
        }

        /// <summary>World mesh built in world coordinates under a root at a world position: builds relative points.</summary>
        private static Vector3 L(Transform root, Vector3 world) => root.InverseTransformPoint(world);

        private static FireLight Brazier(Transform parent, Vector3 world, bool ash, bool brazier, int priority)
        {
            var f = WorldLights.Torch(parent, world, ash, brazier, brazier ? "Brazier" : "Torch");
            f.DayLevel = 1f; f.GroundGlow = 0f; f.Halo = 0f; f.Priority = priority;
            f.Range = brazier ? 12f : 9f;
            return f;
        }

        public static Color ElementColour(CampaignDefinition def, string crystalId)
        {
            var cd = def?.Crystal(crystalId);
            if (cd != null)
            {
                var tower = def.Tower(cd.Tower);
                if (tower != null && ColorUtility.TryParseHtmlString(tower.BeamColour, out var col)) return col;
                switch (cd.Element)
                {
                    case Element.Fire: return new Color(1f, 0.45f, 0.15f);
                    case Element.Water: return new Color(0.3f, 0.6f, 1f);
                    case Element.Air: return new Color(0.85f, 0.92f, 1f);
                }
            }
            return new Color(0.66f, 0.72f, 0.24f);
        }

        /// <summary>A long faceted crystal (two six-sided points), in local coordinates round its middle.</summary>
        internal static void Crystal(MeshBuilder mb, Vector3 mid, float height, float radius, Color col)
        {
            var top = mid + Vector3.up * height * 0.5f; var bot = mid - Vector3.up * height * 0.5f;
            var ring = new Vector3[6];
            for (int i = 0; i < 6; i++) { float a = i * Mathf.PI / 3f + 0.2f; ring[i] = mid + new Vector3(Mathf.Cos(a), (i % 2 == 0 ? 0.08f : -0.05f) * height, Mathf.Sin(a)) * radius; }
            for (int i = 0; i < 6; i++)
            {
                var a = ring[i]; var b = ring[(i + 1) % 6];
                mb.Tri(top, b, a, col * (1.05f + 0.15f * (i % 3)));
                mb.Tri(bot, a, b, col * (0.75f + 0.1f * (i % 2)));
            }
        }

        // ---------------- stairs and exits ----------------

        private static IInteriorView Stair(Transform root, InteriorModel m, MarkerDef md, InteriorLink link, InteriorThing thing)
        {
            thing.Link = link;
            int back = link.OutHeading + 3;
            var bdir = DirOf(back);
            var side = new Vector3(bdir.z, 0, -bdir.x);
            var o = root.position;
            var mb = new MeshBuilder();
            var view = root.gameObject.AddComponent<StairView>();
            view.Link = link;
            if (link.Up)
            {
                // Steps rising away from the room into an arched opening, light falling down them from above.
                int steps = 7;
                for (int k = 0; k < steps; k++)
                {
                    float y = 0.22f * (k + 1);
                    var c = o + bdir * (0.2f + 0.36f * k) + Vector3.up * (y * 0.5f);
                    mb.Box(L(root, c), new Vector3(2.5f - 0.05f * k, y, 0.38f), InteriorBuilder.Shade(InteriorBuilder.CutStone * (0.95f - 0.06f * k), c, 0.06f), BoardSpace.YawOf(bdir));
                }
                // The arch: two dressed jambs and a lintel standing proud of the rock, framing the way up.
                var archAt = o + bdir * 2.4f;
                foreach (float sgn in new[] { -1f, 1f })
                {
                    var j = archAt + side * (1.45f * sgn) + Vector3.up * 1.5f;
                    mb.Box(L(root, j), new Vector3(0.55f, 3.0f, 0.6f), InteriorBuilder.Shade(InteriorBuilder.CutStone * 0.85f, j, 0.1f), BoardSpace.YawOf(bdir));
                }
                var lint = archAt + Vector3.up * 3.15f;
                mb.Box(L(root, lint), new Vector3(3.6f, 0.5f, 0.75f), InteriorBuilder.Shade(InteriorBuilder.CutStone * 0.8f, lint, 0.1f), BoardSpace.YawOf(bdir));
                Mesh(root, "Stair up", mb);
                // The light from above: a soft upright glow in the arch and a pale patch on the steps.
                bool surface = !link.To.IsInterior;
                var glowCol = surface ? new Color(0.70f, 0.80f, 1f, 0.85f) : new Color(1f, 0.72f, 0.40f, 0.7f);
                var g = LandLook.Part(root, "Way-up glow", LightShapes.RadialDisc(true), LightShapes.Glow, false);
                g.transform.position = archAt + Vector3.up * 1.7f - bdir * 0.05f;
                g.transform.rotation = Quaternion.LookRotation(-bdir, Vector3.up);
                g.transform.localScale = new Vector3(2.6f, 3.4f, 1f);
                SetGlow(g, glowCol);
                var pool = LandLook.Part(root, "Way-up pool", LightShapes.RadialDisc(false), LightShapes.Glow, false);
                pool.transform.position = o + bdir * 0.6f + Vector3.up * 0.05f;
                pool.transform.localScale = Vector3.one * 2.6f;
                SetGlow(pool, new Color(glowCol.r, glowCol.g, glowCol.b, 0.45f));
                view.Glows = new[] { g.GetComponent<Renderer>(), pool.GetComponent<Renderer>() };
                // Exits and stairs up are always lit: a brazier beside the foot of the stair.
                view.Light = Brazier(root, o + side * 1.5f - bdir * 1.1f, false, true, 2); // in front, seen down the passage
                view.Light.Range = 18f; view.Light.Intensity = 2.4f;
                if (surface) { view.Light.Color = new Color(1f, 0.70f, 0.42f); }
            }
            else
            {
                // A stair down: a stone kerb round a dark well, the steps fading into the black.
                for (int k = 0; k < 6; k++)
                {
                    var c = o + bdir * (-0.4f + 0.42f * k) + Vector3.up * (0.016f + 0.001f * k);
                    float shade = Mathf.Lerp(0.55f, 0.02f, k / 5f);
                    var a = c - side * 1.15f - bdir * 0.21f; var b = c - side * 1.15f + bdir * 0.21f;
                    var cc = c + side * 1.15f + bdir * 0.21f; var dd = c + side * 1.15f - bdir * 0.21f;
                    mb.Quad(L(root, a), L(root, b), L(root, cc), L(root, dd), InteriorBuilder.CutStone * shade);
                    // The lip of each step.
                    var lip = c - bdir * 0.21f + Vector3.up * 0.004f;
                    mb.Strip(L(root, lip - side * 1.15f), L(root, lip + side * 1.15f), 0.05f, InteriorBuilder.CutStone * (shade * 1.6f + 0.05f));
                }
                var black = o + bdir * 2.2f + Vector3.up * 0.022f;
                mb.Quad(L(root, black - side * 1.2f - bdir * 0.2f), L(root, black - side * 1.2f + bdir * 0.6f), L(root, black + side * 1.2f + bdir * 0.6f), L(root, black + side * 1.2f - bdir * 0.2f), new Color(0.01f, 0.01f, 0.01f));
                // Kerb on the two sides and the back.
                for (int k = 0; k < 4; k++)
                {
                    foreach (float sgn in new[] { -1f, 1f })
                    {
                        var c = o + bdir * (-0.3f + 0.75f * k) + side * (1.45f * sgn) + Vector3.up * 0.28f;
                        mb.Box(L(root, c), new Vector3(0.45f, 0.56f + 0.08f * (k % 2), 0.72f), InteriorBuilder.Shade(InteriorBuilder.CutStone * 0.8f, c, 0.12f), BoardSpace.YawOf(bdir) + 3f * sgn);
                    }
                }
                var bk = o + bdir * 2.75f + Vector3.up * 0.32f;
                mb.Box(L(root, bk), new Vector3(3.3f, 0.64f, 0.5f), InteriorBuilder.Shade(InteriorBuilder.CutStone * 0.75f, bk, 0.1f), BoardSpace.YawOf(bdir));
                Mesh(root, "Stair down", mb);
                view.Light = Brazier(root, o + side * 1.9f - bdir * 0.6f, false, false, 1);
            }
            // Shut: timbers lashed across (the way's fact is not known yet).
            var bars = new MeshBuilder();
            for (int k = 0; k < 3; k++)
            {
                var c = o + bdir * (link.Up ? 1.2f : 0.9f) + Vector3.up * (link.Up ? 0.8f + 0.75f * k : 0.35f + 0.1f * k);
                bars.Box(L(root, c), new Vector3(3.2f, 0.22f, 0.24f), LandLook.DarkWood * (0.9f + 0.1f * k), BoardSpace.YawOf(bdir) + (k - 1) * 6f);
            }
            view.Bars = Mesh(root, "Shut", bars);
            return view;
        }

        internal static void SetGlow(GameObject g, Color c)
        {
            var mpb = new MaterialPropertyBlock();
            mpb.SetColor("_Color", c);
            g.GetComponent<Renderer>().SetPropertyBlock(mpb);
        }

        // ---------------- doors ----------------

        private static IInteriorView Door(Transform root, InteriorModel m, MarkerDef md, InteriorThing thing)
        {
            var c = m.ToCell(md.I, md.J);
            // The passage runs between the door's two open sides; the door stands across it.
            int axis = 0;
            for (int h = 0; h < 3; h++)
                if (m.Floor.Contains(c + WalkLattice.Dir(h)) && m.Floor.Contains(c + WalkLattice.Dir(h + 3))) { axis = h; break; }
            var along = DirOf(axis);
            var across = new Vector3(along.z, 0, -along.x);
            var view = root.gameObject.AddComponent<DoorView>();
            view.OpenWhen = md.OpenWhen;
            var leaf = new GameObject("Leaf").transform;
            leaf.SetParent(root, false);
            leaf.localPosition = -across * 2.0f;         // hinged at one side
            leaf.rotation = Quaternion.LookRotation(along, Vector3.up);
            var mb = new MeshBuilder();
            // Upright logs lashed with three cross bars: a barred gate in the passage.
            for (int k = 0; k < 7; k++)
            {
                float x = 0.3f + 0.56f * k;
                mb.Cylinder(new Vector3(x, 0, 0), 0.22f, 0.2f, 3.1f + 0.15f * ((k * 7) % 3), 6, LandLook.Wood * (0.85f + 0.05f * (k % 3)));
            }
            for (int k = 0; k < 3; k++) mb.Box(new Vector3(2.0f, 0.6f + 1.0f * k, -0.28f), new Vector3(4.1f, 0.22f, 0.2f), LandLook.DarkWood);
            // The bar: a heavy beam in two stone brackets.
            mb.Box(new Vector3(2.0f, 1.55f, 0.3f), new Vector3(4.4f, 0.3f, 0.3f), LandLook.DarkWood * 0.9f);
            mb.ToObject("Gate", leaf, InteriorBuilder.RockMaterial).GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var brackets = new MeshBuilder();
            foreach (float sgn in new[] { -1f, 1f })
            {
                var b = root.position + across * (2.15f * sgn) + Vector3.up * 1.4f;
                brackets.Box(L(root, b), new Vector3(0.5f, 2.8f, 0.6f), InteriorBuilder.Shade(InteriorBuilder.CutStone * 0.75f, b, 0.1f), BoardSpace.YawOf(along));
            }
            Mesh(root, "Door posts", brackets);
            view.Leaf = leaf;
            view.ShutRotation = leaf.localRotation;
            view.OpenRotation = Quaternion.AngleAxis(-100f, Vector3.up) * leaf.localRotation;
            return view;
        }

        // ---------------- pedestal and altar ----------------

        private static IInteriorView CrystalStand(Transform root, InteriorDirector d, InteriorModel m, MarkerDef md, InteriorThing thing)
        {
            bool altar = md.Kind == MarkerKind.Altar;
            var mb = new MeshBuilder();
            var o = Vector3.zero;
            if (!altar)
            {
                // A cut-stone column on a stepped base; grooves of an inscription round its top.
                mb.Cylinder(o, 0.95f, 0.9f, 0.18f, 6, InteriorBuilder.CutStone * 0.8f);
                mb.Cylinder(o + Vector3.up * 0.18f, 0.62f, 0.5f, 0.95f, 6, InteriorBuilder.CutStone);
                mb.Cylinder(o + Vector3.up * 1.13f, 0.72f, 0.72f, 0.12f, 6, InteriorBuilder.CutStone * 0.95f);
                for (int k = 0; k < 6; k++)
                {
                    float a = (k * 60f + 30f) * Mathf.Deg2Rad;
                    var p = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * 0.55f + Vector3.up * 0.9f;
                    mb.Box(p, new Vector3(0.04f, 0.24f, 0.18f), new Color(0.12f, 0.11f, 0.10f), -k * 60f - 30f + 90f);
                }
            }
            else
            {
                // The Ash altar: a long dark block with horned uprights, ash heaped round it.
                mb.Box(o + Vector3.up * 0.5f, new Vector3(2.2f, 1.0f, 1.1f), InteriorBuilder.AshDark);
                mb.Box(o + Vector3.up * 1.05f, new Vector3(2.4f, 0.12f, 1.25f), InteriorBuilder.AshDark * 1.3f);
                foreach (float sgn in new[] { -1f, 1f })
                {
                    mb.Cylinder(o + new Vector3(sgn * 1.0f, 1.1f, 0), 0.12f, 0.05f, 0.9f, 5, new Color(0.55f, 0.52f, 0.45f));
                    mb.Cone(o + new Vector3(sgn * 1.0f, 2.0f, 0), 0.07f, 0.35f, 5, new Color(0.75f, 0.72f, 0.62f));
                }
                for (int k = 0; k < 8; k++)
                {
                    float a = k * 0.8f;
                    mb.Rock(o + new Vector3(Mathf.Cos(a) * 1.5f, 0, Mathf.Sin(a) * 1.0f), 0.25f, new Color(0.24f, 0.24f, 0.23f), new PropRandom(k + 3).Next);
                }
            }
            var stand = Mesh(root, altar ? "Altar" : "Pedestal", mb);
            int face = OpenHeading(m, m.ToCell(md.I, md.J));
            root.rotation = Quaternion.Euler(0, WalkLattice.Yaw(face), 0);
            var col = CrystalLook.Glow(d.Definition, md.Crystal);
            var cmb = new MeshBuilder();
            var cdef = d.Definition?.Crystal(md.Crystal);
            CrystalLook.Cluster(cmb, Vector3.zero, 0.7f, cdef != null ? cdef.Element : Element.Earth, 11 + (cdef != null ? (int)cdef.Element : 0));
            var crystal = new GameObject("Crystal").transform;
            crystal.SetParent(root, false);
            crystal.localPosition = Vector3.up * (altar ? 1.11f : 1.25f); // resting on the stand's top
            var cg = cmb.ToObject("Stone", crystal, MaterialFactory.CreateUnlit(Color.white, "Crystal"));
            cg.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var halo = LandLook.Part(crystal, "Halo", LightShapes.RadialDisc(true), LightShapes.Glow, false);
            halo.transform.localScale = Vector3.one * 1.3f;
            halo.transform.localPosition = Vector3.up * 0.35f;
            SetGlow(halo, new Color(col.r, col.g, col.b, 0.25f));
            var cl = WorldLights.Add(crystal, Vector3.up * 0.4f, col, 1.1f, 8f, 2, "Crystal light");
            cl.DayLevel = 1f; cl.GroundGlow = 0f; cl.Halo = 0f; cl.Sparks = false; cl.Look = FireLight.Style.Steady;
            var view = root.gameObject.AddComponent<CrystalStandView>();
            view.Crystal = crystal; view.CrystalLight = cl; view.Halo = halo.transform;
            view.CrystalId = md.Crystal; view.Ref = m.Ref(md.Id); view.Director = d; view.Altar = altar;
            if (altar)
            {
                // The Ash Clan's cold green fires either side of the altar (always burning).
                var right = root.right;
                Brazier(root.parent, root.position + right * 1.9f + root.forward * 0.6f, true, true, 2);
                Brazier(root.parent, root.position - right * 1.9f + root.forward * 0.6f, true, true, 1);
            }
            else if (d.Definition?.Crystal(md.Crystal)?.Element == Element.Earth || d.Definition == null)
            {
                // Roots of the Root Chamber hang from the roof over the pedestal.
                var roots = new MeshBuilder();
                var rnd = new PropRandom(md.I * 31 + md.J * 17 + 5);
                for (int k = 0; k < 14; k++)
                {
                    float a = rnd.Range(0, Mathf.PI * 2f), r = rnd.Range(0.8f, 2.4f);
                    var top = new Vector3(Mathf.Cos(a) * r, 4.6f, Mathf.Sin(a) * r);
                    float len = rnd.Range(0.6f, 2.0f);
                    roots.Cylinder(top - Vector3.up * len, 0.02f, 0.07f, len, 4, new Color(0.36f, 0.26f, 0.16f) * rnd.Range(0.8f, 1.1f), false);
                }
                Mesh(root, "Roots", roots);
            }
            return view;
        }

        private static bool NextToStand(InteriorModel m, MarkerDef md)
        {
            foreach (var o in m.Markers)
                if ((o.Kind == MarkerKind.Pedestal || o.Kind == MarkerKind.Altar) && WalkGrid.Distance(new Cell(o.I, o.J), new Cell(md.I, md.J)) == 1) return true;
            return false;
        }

        /// <summary>The floor by a pedestal: a rival's tally stick lies there once the crystal is gone to the Ash Clan.</summary>
        private static IInteriorView TallySpot(Transform root, InteriorDirector d, InteriorModel m, MarkerDef md, InteriorThing thing)
        {
            MarkerDef stand = null;
            foreach (var o in m.Markers)
                if ((o.Kind == MarkerKind.Pedestal || o.Kind == MarkerKind.Altar) && WalkGrid.Distance(new Cell(o.I, o.J), new Cell(md.I, md.J)) == 1) { stand = o; break; }
            var mb = new MeshBuilder();
            // Scuffed floor: a few dark boot scrapes.
            var rnd = new PropRandom(md.I * 13 + md.J * 7);
            for (int k = 0; k < 6; k++)
            {
                var p = new Vector3(rnd.Range(-1.4f, 1.4f), 0.03f, rnd.Range(-1.4f, 1.4f));
                mb.Box(p, new Vector3(0.18f, 0.01f, 0.36f), new Color(0.16f, 0.14f, 0.12f), rnd.Range(0, 360));
            }
            Mesh(root, "Scuffs", mb);
            var stick = new MeshBuilder();
            stick.Box(new Vector3(0, 0.08f, 0), new Vector3(0.13f, 0.13f, 1.5f), new Color(0.80f, 0.70f, 0.52f), 25f);
            for (int k = 0; k < 6; k++)
            {
                // Five notches and a cross.
                var q = Quaternion.Euler(0, 25f, 0);
                var p = q * new Vector3(0.07f, 0.14f, -0.55f + 0.18f * k);
                stick.Box(p, new Vector3(0.05f, 0.05f, k == 5 ? 0.14f : 0.05f), new Color(0.12f, 0.10f, 0.08f), 25f + (k == 5 ? 45f : 0f));
            }
            // A green Ash rag tied round it.
            stick.Box(Quaternion.Euler(0, 25f, 0) * new Vector3(0, 0.1f, 0.6f), new Vector3(0.2f, 0.16f, 0.16f), new Color(0.30f, 0.70f, 0.42f), 25f);
            var stickGo = Mesh(root, "Tally stick", stick);
            // Laid towards the pedestal, where the lantern finds it.
            if (stand != null) { var to = m.World(stand.I, stand.J) - root.position; to.y = 0; stickGo.transform.localPosition = to.normalized * 1.3f; }
            var view = root.gameObject.AddComponent<TallyView>();
            view.Stick = stickGo; view.Director = d; view.CrystalId = stand?.Crystal;
            return view;
        }

        /// <summary>Carved marks on a dressed face standing against the wall (survey marks, an inscription).</summary>
        private static void Carving(Transform root, InteriorModel m, MarkerDef md, InteriorThing thing)
        {
            var c = m.ToCell(md.I, md.J);
            int wall = WallHeading(m, c);
            if (wall < 0) { Plinth(root); return; }
            var wdir = DirOf(wall);
            var side = new Vector3(wdir.z, 0, -wdir.x);
            var o = root.position;
            var mb = new MeshBuilder();
            var face = o + wdir * 1.25f + Vector3.up * 1.4f;
            float yaw = BoardSpace.YawOf(wdir);
            mb.Box(L(root, face), new Vector3(2.0f, 2.6f, 0.5f), InteriorBuilder.Shade(InteriorBuilder.CutStone * 0.9f, face, 0.05f), yaw);
            // The marks: cuts in pairs or threes and a ring, from the marker's own seed; dark against the pale face.
            var rnd = new PropRandom(md.I * 101 + md.J * 37 + md.Id.Length);
            var front = face - wdir * 0.26f;
            var dark = new Color(0.10f, 0.09f, 0.08f);
            int groups = 2 + (int)(rnd.Next() * 2);
            for (int g = 0; g < groups; g++)
            {
                float gx = -0.6f + 1.2f * g / Mathf.Max(1, groups - 1);
                int cuts = 2 + (int)(rnd.Next() * 2);
                for (int k = 0; k < cuts; k++)
                {
                    var p = front + side * (gx + 0.12f * k) + Vector3.up * (0.45f + rnd.Range(-0.05f, 0.05f));
                    mb.Box(L(root, p), new Vector3(0.05f, 0.6f, 0.04f), dark, yaw + rnd.Range(-12f, 12f));
                }
                // An arrow-like chevron under each group.
                var ch = front + side * gx + Vector3.up * (-0.25f);
                mb.Box(L(root, ch + side * 0.1f), new Vector3(0.3f, 0.05f, 0.04f), dark, yaw + 35f);
                mb.Box(L(root, ch - side * 0.1f), new Vector3(0.3f, 0.05f, 0.04f), dark, yaw - 35f);
            }
            // The ring.
            var ringC = front + Vector3.up * 0.95f;
            for (int k = 0; k < 12; k++)
            {
                float a = k * Mathf.PI / 6f;
                var p = ringC + side * (Mathf.Cos(a) * 0.28f) + Vector3.up * (Mathf.Sin(a) * 0.28f);
                mb.Box(L(root, p), new Vector3(0.16f, 0.05f, 0.04f), dark, yaw);
            }
            Mesh(root, "Carving", mb);
            thing.TapPoint = face;
        }

        private static void Threshold(Transform root, InteriorModel m, MarkerDef md)
        {
            var c = m.ToCell(md.I, md.J);
            int open = OpenHeading(m, c);
            var along = DirOf(open);
            var across = new Vector3(along.z, 0, -along.x);
            var mb = new MeshBuilder();
            var rnd = new PropRandom(md.I * 3 + md.J * 11);
            // A line of ash-grey stones across the way, with two posts hung with green rags and bone.
            for (int k = 0; k < 9; k++)
            {
                var p = across * (-2.0f + 0.5f * k) + along * rnd.Range(-0.1f, 0.1f);
                mb.Rock(p, rnd.Range(0.16f, 0.26f), new Color(0.36f, 0.35f, 0.33f), rnd.Next);
            }
            foreach (float sgn in new[] { -1f, 1f })
            {
                var p = across * (2.0f * sgn);
                mb.Cylinder(p, 0.1f, 0.08f, 2.3f, 5, LandLook.DarkWood);
                mb.Box(p + Vector3.up * 2.0f + across * (-0.1f * sgn), new Vector3(0.12f, 0.5f, 0.25f), new Color(0.25f, 0.55f, 0.35f), BoardSpace.YawOf(along));
                mb.Cone(p + Vector3.up * 2.3f, 0.1f, 0.2f, 5, new Color(0.78f, 0.75f, 0.66f));
            }
            Mesh(root, "Threshold", mb);
        }

        private static void Cairn(Transform root, InteriorModel m, MarkerDef md)
        {
            var c = m.ToCell(md.I, md.J);
            int wall = WallHeading(m, c);
            var wdir = wall >= 0 ? DirOf(wall) : Vector3.forward;
            var mb = new MeshBuilder();
            var rnd = new PropRandom(md.I * 5 + md.J * 19 + 2);
            var at = wdir * 1.3f;
            for (int k = 0; k < 5; k++) mb.Rock(at + Vector3.up * (0.22f * k), 0.42f - 0.06f * k, InteriorBuilder.Shade(InteriorBuilder.CutStone * 0.8f, at + Vector3.up * k, 0.1f), rnd.Next);
            Mesh(root, "Cairn", mb);
        }

        private static void Spot(Transform root, Color col, float radius, bool scorched)
        {
            var mb = new MeshBuilder();
            for (int k = 0; k < 10; k++)
            {
                float a = k * Mathf.PI / 5f;
                var p = new Vector3(Mathf.Cos(a), 0.0f, Mathf.Sin(a)) * radius;
                mb.Box(p + Vector3.up * 0.05f, new Vector3(0.22f, 0.1f, 0.16f), col * (0.85f + 0.1f * (k % 2)), -a * Mathf.Rad2Deg);
            }
            if (scorched) mb.Cylinder(Vector3.up * 0.012f, radius * 0.8f, radius * 0.8f, 0.006f, 10, new Color(0.08f, 0.08f, 0.07f));
            Mesh(root, "Spot", mb);
        }

        private static void Seat(Transform root, InteriorModel m, MarkerDef md)
        {
            var mb = new MeshBuilder();
            mb.Box(new Vector3(0, 0.25f, 1.2f), new Vector3(1.6f, 0.5f, 0.6f), InteriorBuilder.CutStone * 0.8f);
            Mesh(root, "Seat", mb);
        }

        private static void Lever(Transform root, InteriorModel m, MarkerDef md)
        {
            var mb = new MeshBuilder();
            mb.Box(new Vector3(0, 0.5f, 1.2f), new Vector3(0.6f, 1.0f, 0.6f), InteriorBuilder.CutStone * 0.8f);
            mb.Box(new Vector3(0, 1.3f, 1.0f), new Vector3(0.12f, 1.2f, 0.12f), LandLook.DarkWood, 0f);
            Mesh(root, "Lever", mb);
        }

        private static void Plinth(Transform root)
        {
            var mb = new MeshBuilder();
            mb.Cylinder(Vector3.zero, 0.6f, 0.5f, 0.6f, 6, InteriorBuilder.CutStone * 0.85f);
            Mesh(root, "Plinth", mb);
        }

        // ---------------- reading markers ----------------

        /// <summary>What a tap on a marker shows: the lines of the story's reading events at that marker (guards applied), else a plain sentence.</summary>
        /// <summary>WU-6: whether the crystal stand at a marker shows a crystal now (state, as the view shows it).</summary>
        private static bool StandHolds(MarkerRef r)
        {
            foreach (var v in UnityEngine.Object.FindObjectsByType<CrystalStandView>(FindObjectsInactive.Include, FindObjectsSortMode.None)) if (v.Ref == r) return v.HasCrystal;
            return false;
        }

        internal static List<string> LinesFor(InteriorDirector d, MarkerRef r, MarkerDef md)
        {
            var list = new List<string>();
            var s = WorldBootstrap.Instance != null && WorldBootstrap.Instance.Sim != null ? WorldBootstrap.Instance.Sim.State : null;
            var def = d.Definition;
            if (s?.Campaign != null && def?.Events != null && (md.Kind == MarkerKind.Inscription))
                foreach (var ev in def.Events)
                {
                    if (ev == null || ev.At != r || (ev.Action != QuestAction.Examine && ev.Action != QuestAction.Search)) continue;
                    if (ev.Lines == null) continue;
                    foreach (var id in ev.Lines)
                    {
                        var line = CampaignApi.Line(s, id);
                        var ld = def.Lines?.Find(x => x != null && x.Id == id);
                        if (line != null && ld != null && (ld.Speaker == "mira" || ld.Speaker == "")) list.Add(line);
                        if (list.Count >= 2) break;
                    }
                    if (list.Count > 0) break;
                }
            if (list.Count > 0) return list;
            switch (md.Kind)
            {
                case MarkerKind.Inscription: list.Add("Marks cut into the rock by someone who meant to come back."); break;
                case MarkerKind.Threshold: list.Add("Ash-grey stones laid across the way. Beyond, someone keeps watch."); break;
                case MarkerKind.SafePoint: list.Add("A cairn of piled stones. A good place to fall back to."); break;
                case MarkerKind.DuelCaster: list.Add("A ring of pale stones on the floor."); break;
                case MarkerKind.DuelOpponent: list.Add("A scorched ring. Someone stands here often."); break;
                case MarkerKind.Witness: list.Add("A stone seat, worn smooth."); break;
                case MarkerKind.Lever: list.Add("A wooden lever set in a block of stone."); break;
                case MarkerKind.Door: list.Add(Interiors.Knows(md.OpenWhen) ? "It stands open." : "Barred fast."); break;
                case MarkerKind.Pedestal: list.Add(StandHolds(r) ? "A crystal rests on the stone." : "The stone is bare."); break;
                case MarkerKind.Altar: list.Add(StandHolds(r) ? "A crystal lies on the altar." : "The altar is bare."); break;
                default: list.Add("Cut stone."); break;
            }
            return list;
        }
    }

    // ---------------- the views ----------------

    /// <summary>A marker's object: a tap reads it (and offers the way through stairs and exits).</summary>
    public sealed class InteriorThing : MonoBehaviour, IInteractable, IInspectable
    {
        public MarkerRef Ref;
        public MarkerDef Marker;
        public InteriorLink Link;
        public InteriorDirector Director;
        public Vector3? TapPoint;

        public string Title => LandLook.Capitalise(string.IsNullOrEmpty(Marker?.Name) ? Marker?.Id : Marker.Name);
        public IEnumerable<string> Lines => Director != null ? InteriorProps.LinesFor(Director, Ref, Marker) : new List<string>();
        public string Prompt => Title;

        public void Interact()
        {
            var lines = new List<string>();
            var replies = new List<string>();
            var actions = new List<Action>();
            if (Link != null)
            {
                bool open = Link.IsOpen;
                lines.Add(open ? (Link.Up ? "The way up." : "The way down.") : "Shut fast. It will not open from here.");
                if (open)
                {
                    replies.Add(Link.Up ? "Go up" : "Go down");
                    var cell = Link.Cell;
                    actions.Add(() => { var w = PlayerWalker.Instance; if (w != null && w.Cell != cell) w.WalkTo(cell); });
                }
            }
            else lines.AddRange(Lines);
            if (Interiors.Current != null && (Link == null || !Link.Up))
            {
                replies.Add("Lead me out");
                actions.Add(() => Interiors.LeadOut());
            }
            SpeechBubbles.Inspect(transform, (TapPoint ?? transform.position + Vector3.up * 1.6f) - transform.position, Title, lines, 7f,
                replies.Count > 0 ? replies : null, i => { if (i >= 0 && i < actions.Count) actions[i](); });
        }
    }

    /// <summary>
    /// P2a: a stair or an exit as a world action: the first tap previews on the ledger ("Go up the stair?", a way that is shut says so),
    /// the second walks the player to the way (the walker's own steps; no sim command). Replaces the old speech-bubble choice.
    /// </summary>
    public sealed class InteriorWay : MonoBehaviour, IWorldAction, IWorldActionAnchor, IWorldPrimary
    {
        public InteriorThing Thing;
        private InteriorLink Link => Thing != null ? Thing.Link : null;

        public string Title => Thing != null ? Thing.Title : name;

        public IEnumerable<string> Lines
        {
            get
            {
                var l = Link;
                if (l == null) return new List<string>();
                return new[] { l.IsOpen ? (l.Up ? "The way up." : "The way down.") : "Shut fast. It will not open from here." };
            }
        }

        public string PreviewLine => Link == null ? null : Link.Up ? "Go up?" : "Go down?";
        public Transform Anchor => transform;
        public float KeyReach => 3f;

        public WorldActionCheck Check()
        {
            var l = Link;
            if (l == null) return WorldActionCheck.Disable(null);
            return l.IsOpen ? WorldActionCheck.Allowed : WorldActionCheck.Refuse("Shut fast. It will not open from here.");
        }

        public void ShowPreview(bool on) { }

        /// <summary>The walk to the way (a tap on the way itself; stepping onto the cell crosses it).</summary>
        public bool Commit()
        {
            var l = Link; var w = PlayerWalker.Instance;
            if (l == null || w == null || !l.IsOpen) return false;
            if (w.Cell != l.Cell) w.WalkTo(l.Cell);
            return true;
        }

        private void OnEnable() => WorldActions.Register(this);
        private void OnDisable() => WorldActions.Unregister(this);
    }

    public sealed class StairView : MonoBehaviour, IInteriorView
    {
        public InteriorLink Link;
        public FireLight Light;
        public GameObject Bars;
        public Renderer[] Glows;
        public bool Open { get; private set; }

        public void Sync(WorldState s)
        {
            Open = Link.IsOpen;
            if (Bars != null && Bars.activeSelf == Open) Bars.SetActive(!Open);
            if (Light != null) Light.On = true; // exits and stairs are always lit
            if (Glows != null) foreach (var g in Glows) if (g != null) g.enabled = Open || !Link.To.IsInterior;
        }
    }

    public sealed class DoorView : MonoBehaviour, IInteriorView
    {
        public string OpenWhen;
        public Transform Leaf;
        public Quaternion ShutRotation, OpenRotation;
        public bool Open { get; private set; }
        private bool _synced;

        public void Sync(WorldState s)
        {
            Open = Interiors.Knows(OpenWhen);
            if (!_synced) { _synced = true; Leaf.localRotation = Open ? OpenRotation : ShutRotation; }
        }

        private void Update()
        {
            if (!_synced || Leaf == null) return;
            Leaf.localRotation = Quaternion.RotateTowards(Leaf.localRotation, Open ? OpenRotation : ShutRotation, 60f * Time.deltaTime);
        }
    }

    public sealed class CrystalStandView : MonoBehaviour, IInteriorView
    {
        public Transform Crystal, Halo;
        public FireLight CrystalLight;
        public string CrystalId;
        public MarkerRef Ref;
        public InteriorDirector Director;
        public bool Altar;
        /// <summary>The crystal is shown on this stand now.</summary>
        public bool HasCrystal { get; private set; }
        /// <summary>True while another view draws the campaign's crystals (the expedition director): the stand then shows none of its own.</summary>
        public static bool DrawnElsewhere;

        public void Sync(WorldState s)
        {
            bool show;
            var cd = Director?.Definition?.Crystal(CrystalId);
            if (s?.Campaign == null || cd == null) show = !Altar;
            else
            {
                var v = CampaignApi.Crystal(s, cd.Element);
                show = v != null && v.At == Ref && (Altar ? v.Custody == Custody.AtRivalAltar : v.Custody == Custody.AtSite);
            }
            HasCrystal = show;
            bool drawn = show && !DrawnElsewhere; // the expedition (I6) draws the crystal itself: one body per crystal
            if (Crystal.gameObject.activeSelf != drawn) Crystal.gameObject.SetActive(drawn);
        }

        private void Update()
        {
            if (!HasCrystal) return;
            if (Halo != null && Camera.main != null) Halo.rotation = Quaternion.LookRotation(Halo.position - Camera.main.transform.position, Vector3.up);
        }
    }

    public sealed class TallyView : MonoBehaviour, IInteriorView
    {
        public GameObject Stick;
        public InteriorDirector Director;
        public string CrystalId;
        public bool Shown => Stick != null && Stick.activeSelf;

        public void Sync(WorldState s)
        {
            bool show = false;
            var cd = Director?.Definition?.Crystal(CrystalId);
            if (s?.Campaign != null && cd != null)
            {
                var v = CampaignApi.Crystal(s, cd.Element);
                show = v != null && (v.Custody == Custody.AtRivalAltar || v.Custody == Custody.RivalTransit);
            }
            if (Stick.activeSelf != show) Stick.SetActive(show);
        }
    }
}
