using System;
using System.Collections.Generic;
using UnityEngine;
using World.Sim;
using World.Sim.Campaign;
using Cell = World.Game.WalkLattice.Cell;

namespace World.Game
{
    /// <summary>Something of the expedition a tap names (a stone, a pedestal, a tower): title and one or two lines from state.</summary>
    public sealed class ExpeditionThing : MonoBehaviour, IInspectable
    {
        public Func<string> TitleOf;
        public Func<IEnumerable<string>> LinesOf;
        public string Title => TitleOf != null ? TitleOf() : name;
        public IEnumerable<string> Lines => LinesOf != null ? LinesOf() : new string[0];
    }

    /// <summary>A way down or up (the hidden stair, the Ash cut): a tap says what it is, and when it is open offers to take it.</summary>
    public sealed class ExpeditionStair : MonoBehaviour, IInteractable, IInspectable
    {
        public MarkerRef Marker, To;
        public string Label = "";
        public ExpeditionSites Sites;
        public string Prompt => "Go down";
        public string Title => Label;
        public IEnumerable<string> Lines { get { yield return Sites != null ? Sites.StairSentence(this) : ""; } }
        public void Interact() { if (Sites != null) Sites.UseStair(this); }
    }

    /// <summary>
    /// The expedition's places in the world (package I6): the Stonemark Glade (boundary stone, the slab over the
    /// hidden stair that slides back), the Ash cut, the four towers on the shore (dark until their crystal is set; a
    /// restored tower burns in its element's colour, its ground greens and a wave of light crosses the island), a
    /// marked place for each site that is not written yet, pedestals and Ash altars with the crystal on them when
    /// custody says so, the keepers at their altars, cleared ground round every site
    /// (<see cref="LandViews.AddClearing"/>). Everything follows state: custody, facts and the restored list, so a load
    /// shows the same world. Original primitive shapes only. Things inside interiors (pedestal, altar, marks, keeper,
    /// crystal) are placed through <see cref="ExpeditionSpace"/>; while package I5's interiors are missing,
    /// <see cref="ExpeditionStandIn"/> also builds a plain floor and walls for them.
    /// </summary>
    public sealed class ExpeditionSites : MonoBehaviour
    {
        public const string SolidOwner = "expedition_sites";
        public const float SiteClearing = 12f, TowerClearing = 13f;

        private ExpeditionDirector _dir;
        private WorldBootstrap _boot;
        private WorldState S => _boot.Sim.State;
        private CampaignState C => _boot.Sim.State.Campaign;
        private CampaignDefinition D => _boot.Sim.State.Campaign.Definition;

        // ---- what was built (tests read these) ----
        public sealed class TowerView
        {
            public Element Element; public string LocationId; public Transform Root; public Color Colour;
            public GameObject Dark, Lit, Beam, Bloom; public FireLight Light; public bool Restored; public float LitAt = -1f;
        }
        public sealed class StairView { public ExpeditionStair Stair; public Transform Slab; public GameObject Hole; public bool Open; public float Slide; public FireLight Torch; }
        public sealed class CrystalView { public Element Element; public GameObject Gem; public FireLight Light; public string Where = ""; }
        public sealed class KeeperView { public string EncounterId, ActorId, Space = ""; public Npc Npc; }

        public readonly List<TowerView> Towers = new List<TowerView>();
        public readonly List<StairView> Stairs = new List<StairView>();
        public readonly List<CrystalView> Crystals = new List<CrystalView>();
        public readonly List<KeeperView> Keepers = new List<KeeperView>();
        private readonly Dictionary<string, GameObject> _markerProps = new Dictionary<string, GameObject>();
        /// <summary>Marker props built, by marker text ("glade/stone", "glade/quarry_upper/pedestal").</summary>
        public IReadOnlyDictionary<string, GameObject> MarkerProps => _markerProps;
        public ExpeditionStandIn StandIn { get; private set; }
        /// <summary>Restorations whose effect was played in this scene (not counting towers found lit at a load).</summary>
        public int RestorationsPlayed { get; private set; }

        public TowerView Tower(Element e) => Towers.Find(t => t.Element == e);
        public StairView Stair(string markerText) => Stairs.Find(x => x.Stair.Marker.ToString() == markerText);
        public CrystalView Crystal(Element e) => Crystals.Find(x => x.Element == e);

        public static ExpeditionSites Create(ExpeditionDirector dir, WorldBootstrap boot, Transform parent)
        {
            var go = new GameObject("Expedition sites");
            go.transform.SetParent(parent, false);
            var s = go.AddComponent<ExpeditionSites>();
            s._dir = dir; s._boot = boot;
            s.Build();
            return s;
        }

        private void OnDestroy()
        {
            WalkGrid.RemoveStaticBlockers(SolidOwner);
            LandViews.ClearExtraClearings();
        }

        // =====================================================================================================
        // Building
        // =====================================================================================================

        public static Color ElementColour(CampaignDefinition d, Element e)
        {
            var cd = d?.Crystal(e);
            var tw = cd != null ? d.Tower(cd.Tower) : null;
            if (tw != null && ColorUtility.TryParseHtmlString(tw.BeamColour, out var c)) return c;
            switch (e)
            {
                case Element.Fire: return new Color(0.91f, 0.44f, 0.16f);
                case Element.Water: return new Color(0.24f, 0.55f, 0.85f);
                case Element.Air: return new Color(0.85f, 0.91f, 0.96f);
                default: return new Color(0.66f, 0.72f, 0.24f);
            }
        }

        /// <summary>A crystal: a faceted gem that glows in its element's colour (the same shape on a pedestal, in Mira's hands, on an altar, in a tower).</summary>
        public static GameObject MakeCrystal(Transform parent, string name, Color colour, float size, bool small)
        {
            var mb = new MeshBuilder();
            var bright = Color.Lerp(colour, Color.white, 0.35f);
            mb.Cylinder(new Vector3(0, 0, 0), 0.05f * size / 0.24f, size * 0.5f, size * 0.55f, 6, colour, false);
            mb.Cone(new Vector3(0, size * 0.55f, 0), size * 0.5f, size * 0.9f, 6, bright);
            var go = mb.ToObject(name, parent, LandLook.Unlit);
            go.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            // A soft halo so it reads at night from across a cave or a glade.
            var halo = LandLook.Part(go.transform, "Halo", LightShapes.RadialDisc(true), LightShapes.Glow, false);
            halo.transform.localPosition = new Vector3(0, size * 0.7f, 0);
            halo.transform.localScale = Vector3.one * size * (small ? 3.2f : 4.5f);
            var mpb = new MaterialPropertyBlock();
            mpb.SetColor("_Color", colour * 0.55f);
            halo.GetComponent<Renderer>().SetPropertyBlock(mpb);
            halo.AddComponent<FaceCamera>();
            return go;
        }

        private void Build()
        {
            var s = S; var d = D;
            WalkGrid.RemoveStaticBlockers(SolidOwner);
            LandViews.ClearExtraClearings();
            if (!ExpeditionSpace.RealInteriors(d) && d.Interiors.Count > 0) StandIn = ExpeditionStandIn.Create(this, _boot, transform);

            foreach (var loc in d.Locations)
            {
                if (loc == null || loc.Anchor == null || loc.Anchor.Kind == AnchorKind.HomeCamp) continue;
                if (!CampaignPlaces.TryAnchorPoint(d, loc, s.Board, out double ax, out double az)) continue;
                var anchor = new Vector3((float)ax, 0, (float)az);
                var root = new GameObject("Site " + loc.Id).transform;
                root.SetParent(transform, false);
                if (loc.Kind == LocationKind.Tower)
                {
                    BuildTower(loc, root, anchor);
                    // The tower's approach: the door, the steps and the path stay clear of trees.
                    LandViews.AddClearing(anchor.x, anchor.z, TowerClearing);
                    foreach (var m in loc.Markers)
                    {
                        var p = ExpeditionSpace.World(s, MarkerRef.Surface(loc.Id, m.Id));
                        LandViews.AddClearing(p.x, p.z, 5f);
                    }
                    continue;
                }
                LandViews.AddClearing(anchor.x, anchor.z, SiteClearing);
                BuildSiteSign(loc, root, anchor);
                foreach (var m in loc.Markers) BuildMarker(root, MarkerRef.Surface(loc.Id, m.Id), m, loc);
            }
            // The home camp's own campaign markers (the foresters' posts).
            var home = d.Locations.Find(l => l != null && l.Anchor != null && l.Anchor.Kind == AnchorKind.HomeCamp);
            if (home != null)
            {
                var root = new GameObject("Site " + home.Id).transform;
                root.SetParent(transform, false);
                foreach (var m in home.Markers)
                    if (m.Kind == MarkerKind.Inscription) BuildMarker(root, MarkerRef.Surface(home.Id, m.Id), m, home);
            }
            // Things inside interiors (the stand-in adds floor, walls, doors and lit stairs when package I5 is missing).
            foreach (var map in d.Interiors)
            {
                var root = new GameObject("Interior things " + map.Id).transform;
                root.SetParent(transform, false);
                foreach (var m in InteriorMaps.Markers(map)) BuildMarker(root, new MarkerRef(map.Location, map.Id, m.Id), m, null);
            }
            foreach (var cd in d.Crystals)
            {
                var gem = MakeCrystal(transform, "Crystal " + cd.Id, ElementColour(d, cd.Element), 0.55f, false);
                var cv = new CrystalView { Element = cd.Element, Gem = gem };
                if (DayNight.Instance != null)
                {
                    cv.Light = WorldLights.Add(gem.transform, new Vector3(0, 0.5f, 0), ElementColour(d, cd.Element), 1.3f, 9f, 1, "Crystal light");
                    cv.Light.Look = FireLight.Style.Pulse; cv.Light.Sparks = false; cv.Light.GroundGlow = 3.2f; cv.Light.Halo = 0.6f; cv.Light.DayLevel = 0.5f;
                }
                Crystals.Add(cv);
            }
            foreach (var e in d.Encounters)
            {
                if (e == null || e.Kind != EncounterKind.Lethal || string.IsNullOrEmpty(e.OpponentActorId) || e.Stage == null || e.Stage.Opponent.IsEmpty) continue;
                var at = ExpeditionSpace.World(s, e.Stage.Opponent);
                string space = ExpeditionSpace.SpaceOf(e.Stage.Opponent);
                if (space.Length == 0) at = ExpeditionSpace.Ground(at);
                var npc = Npc.Find(e.OpponentActorId) ?? Npc.Spawn(e.OpponentActorId, string.IsNullOrEmpty(e.OpponentName) ? e.Name : e.OpponentName, "Ward-bearer of the Ash Clan.", "", new Color(0.50f, 0.12f, 0.10f), at, 0f);
                npc.Directed = true;
                npc.OnTapped = OnKeeperTapped;
                DressKeeper(npc);
                Keepers.Add(new KeeperView { EncounterId = e.Id, ActorId = e.OpponentActorId, Npc = npc, Space = space });
            }
            _signature = null;
            Sync(true);
        }

        private static Material Lit => LandLook.Lit;

        private GameObject Finish(MeshBuilder mb, string name, Transform parent, Vector3 pos, Func<string> title, Func<IEnumerable<string>> lines, Vector3 pickCentre, Vector3 pickSize)
        {
            var go = mb.ToObject(name, parent, Lit);
            go.transform.position = pos;
            LightShapes.Apply(go.GetComponent<Renderer>(), LightShapes.Read.Prop);
            if (title != null)
            {
                var thing = go.AddComponent<ExpeditionThing>();
                thing.TitleOf = title; thing.LinesOf = lines;
                LandLook.PickBox(go, pickCentre, pickSize);
            }
            return go;
        }

        private static string Cap(string s) => LandLook.Capitalise(s);

        private Vector3 MarkerPoint(MarkerRef r)
        {
            var p = ExpeditionSpace.World(S, r);
            return r.IsInterior ? p : ExpeditionSpace.Ground(p);
        }

        /// <summary>A post with the place's name board where the site begins: every site, written or not, can be found and named.</summary>
        private void BuildSiteSign(LocationDef loc, Transform root, Vector3 anchor)
        {
            var safe = !string.IsNullOrEmpty(loc.SafePoint) ? MarkerPoint(MarkerRef.Surface(loc.Id, loc.SafePoint)) : anchor + Vector3.right * 5f;
            var dir = safe - anchor; dir.y = 0;
            var pos = ExpeditionSpace.Ground(safe + (dir.sqrMagnitude > 0.1f ? dir.normalized : Vector3.right) * 2.6f);
            var mb = new MeshBuilder();
            mb.Box(new Vector3(0, 0.9f, 0), new Vector3(0.14f, 1.8f, 0.14f), LandLook.DarkWood);
            mb.Box(new Vector3(0, 1.62f, 0.05f), new Vector3(1.1f, 0.42f, 0.06f), LandLook.Wood);
            for (int i = 0; i < 3; i++) mb.Rock(new Vector3(-0.3f + i * 0.3f, 0, 0.1f), 0.22f, LandLook.Stone, new PropRandom(loc.Id.GetHashCode() + i).Next);
            string name = loc.Name;
            Finish(mb, "Sign", root, pos, () => Cap(name), () => new[] { "A way-post of the old keepers." }, new Vector3(0, 1f, 0), new Vector3(1.2f, 2f, 0.6f))
                .transform.rotation = Quaternion.Euler(0, BoardSpace.YawOf(-dir), 0);
        }

        private void BuildMarker(Transform root, MarkerRef r, MarkerDef m, LocationDef loc)
        {
            var d = D;
            var pos = MarkerPoint(r);
            string key = r.ToString();
            string name = string.IsNullOrEmpty(m.Name) ? m.Id : m.Name;
            var rnd = new PropRandom(key.GetHashCode());
            bool inside = r.IsInterior;
            switch (m.Kind)
            {
                case MarkerKind.Inscription:
                {
                    var mb = new MeshBuilder();
                    bool wall = inside && (m.Id.Contains("mark") || m.Id.Contains("sign"));
                    if (m.Id.Contains("rubble") || m.Id.Contains("spoil"))
                    {
                        // A heap: fallen rock, or fresh spoil thrown out of a cut.
                        var col = m.Id.Contains("spoil") ? LandLook.Earth : LandLook.Stone * 0.8f;
                        for (int i = 0; i < 9; i++) mb.Rock(new Vector3(rnd.Range(-1.3f, 1.3f), 0, rnd.Range(-1.1f, 1.1f)), rnd.Range(0.5f, 1.1f), col * rnd.Range(0.8f, 1.1f), rnd.Next);
                    }
                    else if (m.Id.Contains("tally"))
                    {
                        // The Ash tally stick lying by the pedestal: notched, with a red binding.
                        mb.Box(new Vector3(0, 0.07f, 0), new Vector3(0.09f, 0.09f, 1.0f), LandLook.Wood, 25f);
                        for (int i = 0; i < 5; i++) mb.Box(new Vector3(0.02f * i, 0.125f, -0.36f + i * 0.16f), new Vector3(0.11f, 0.03f, 0.04f), LandLook.DarkWood, 25f);
                        mb.Box(new Vector3(0.17f, 0.08f, 0.38f), new Vector3(0.13f, 0.11f, 0.12f), new Color(0.72f, 0.14f, 0.10f), 25f);
                    }
                    else if (m.Id.Contains("post"))
                    {
                        // Foresters' posts: three leaning posts, each notched in threes.
                        for (int i = 0; i < 3; i++)
                        {
                            var b = new Vector3(-1.4f + i * 1.4f, 0, rnd.Range(-0.3f, 0.3f));
                            LandShapes.Beam(mb, b, b + new Vector3(0.35f, 1.9f, 0.1f), 0.11f, 6, LandLook.Wood);
                            for (int k = 0; k < 3; k++) mb.Box(b + new Vector3(0.08f + 0.06f * k, 0.9f + 0.28f * k, 0.1f), new Vector3(0.2f, 0.05f, 0.06f), new Color(0.9f, 0.85f, 0.7f));
                        }
                    }
                    else if (wall)
                    {
                        // Survey marks cut in a wall face: a standing slab with pale double cuts.
                        mb.Box(new Vector3(0, 1.3f, 0), new Vector3(1.9f, 2.6f, 0.35f), LandLook.Stone * 0.7f);
                        for (int i = 0; i < 4; i++)
                        {
                            mb.Box(new Vector3(-0.55f + i * 0.36f, 1.55f, 0.19f), new Vector3(0.05f, 0.5f, 0.03f), new Color(0.92f, 0.90f, 0.80f), 0f);
                            mb.Box(new Vector3(-0.47f + i * 0.36f, 1.55f, 0.19f), new Vector3(0.05f, 0.5f, 0.03f), new Color(0.92f, 0.90f, 0.80f), 0f);
                        }
                    }
                    else if (loc != null && loc.Kind == LocationKind.Tower) return; // the door's sign is part of the tower
                    else
                    {
                        // A standing stone: square-cut, taller than a man, with three cuts and a ring.
                        mb.Cylinder(Vector3.zero, 0.75f, 0.6f, 2.5f, 4, LandLook.Stone * 0.92f);
                        mb.Cylinder(new Vector3(0, 2.5f, 0), 0.6f, 0.18f, 0.45f, 4, LandLook.Stone * 0.8f);
                        for (int i = 0; i < 3; i++) mb.Box(new Vector3(-0.22f + i * 0.22f, 1.7f, 0.5f), new Vector3(0.07f, 0.42f, 0.05f), new Color(0.95f, 0.93f, 0.84f), 45f);
                        LandShapes.Ring(mb, new Vector3(0, 0.03f, 0), 1.25f, 0.16f, 20, LandLook.Stone * 0.7f);
                        for (int i = 0; i < 4; i++) mb.Rock(new Vector3(rnd.Range(-1.6f, 1.6f), 0, rnd.Range(-1.6f, 1.6f)), 0.3f, LandLook.Stone * 0.85f, rnd.Next);
                    }
                    var go = Finish(mb, "Inscription " + m.Id, root, pos, () => Cap(name), () => InscriptionLines(r), new Vector3(0, 1f, 0), new Vector3(2f, 2.2f, 2f));
                    if (wall) go.transform.rotation = Quaternion.Euler(0, YawToFloor(r), 0);
                    _markerProps[key] = go;
                    if (!inside && !m.Id.Contains("tally")) WalkGrid.AddSolid(pos, 0.9f, SolidOwner);
                    break;
                }
                case MarkerKind.Pedestal:
                case MarkerKind.Altar:
                {
                    bool altar = m.Kind == MarkerKind.Altar;
                    var mb = new MeshBuilder();
                    if (altar)
                    {
                        // An Ash altar: a black block on a stepped base, horned at the corners, ash-red cloth.
                        var black = new Color(0.14f, 0.13f, 0.13f);
                        mb.Box(new Vector3(0, 0.15f, 0), new Vector3(2.4f, 0.3f, 1.8f), black * 1.4f);
                        mb.Box(new Vector3(0, 0.65f, 0), new Vector3(1.7f, 0.7f, 1.1f), black);
                        mb.Box(new Vector3(0, 1.02f, 0), new Vector3(1.9f, 0.06f, 0.5f), new Color(0.62f, 0.14f, 0.10f));
                        for (int i = 0; i < 4; i++)
                            mb.Cone(new Vector3((i % 2 == 0 ? -1f : 1f) * 0.78f, 1.0f, (i / 2 == 0 ? -1f : 1f) * 0.48f), 0.11f, 0.5f, 5, new Color(0.9f, 0.86f, 0.76f));
                    }
                    else
                    {
                        // The keepers' pedestal: a waist-high drum of pale stone with a ringed top.
                        mb.Cylinder(Vector3.zero, 0.85f, 0.7f, 0.22f, 10, LandLook.Stone * 0.85f);
                        mb.Cylinder(new Vector3(0, 0.22f, 0), 0.45f, 0.4f, 0.75f, 8, LandLook.Stone);
                        mb.Cylinder(new Vector3(0, 0.97f, 0), 0.62f, 0.62f, 0.12f, 10, LandLook.Stone * 1.08f);
                        LandShapes.Ring(mb, new Vector3(0, 1.10f, 0), 0.5f, 0.07f, 16, new Color(0.95f, 0.93f, 0.84f));
                    }
                    string crystal = m.Crystal;
                    var go = Finish(mb, (altar ? "Altar " : "Pedestal ") + m.Id, root, pos, () => Cap(name), () => HolderLines(crystal, altar), new Vector3(0, 0.7f, 0), new Vector3(2f, 1.6f, 2f));
                    _markerProps[key] = go;
                    if (!inside) WalkGrid.AddSolid(pos, 0.9f, SolidOwner);
                    break;
                }
                case MarkerKind.Stairs:
                case MarkerKind.Exit:
                case MarkerKind.Door:
                {
                    if (string.IsNullOrEmpty(m.To) || !MarkerRef.TryParse(m.To, out var to)) break;
                    if (inside) break; // the far end of a stair is the interior's own (package I5, or the stand-in)
                    BuildStairMouth(root, r, m, to, pos, loc);
                    break;
                }
            }
        }

        /// <summary>The yaw that turns a wall slab's face to the nearest open floor.</summary>
        private float YawToFloor(MarkerRef r)
        {
            var d = D;
            var map = d.Interior(r.Interior);
            if (map == null || !InteriorMaps.TryFind(map, r.Marker, out int i, out int j)) return 0f;
            var at = ExpeditionSpace.InteriorCell(d, r.Interior, i, j);
            var sum = Vector3.zero;
            for (int k = 0; k < 6; k++)
            {
                int ni = i + InteriorMaps.Steps[2 * k], nj = j + InteriorMaps.Steps[2 * k + 1];
                if (InteriorMaps.IsFloor(map, ni, nj)) sum += ExpeditionSpace.InteriorCell(d, r.Interior, ni, nj) - at;
            }
            return sum.sqrMagnitude > 0.01f ? BoardSpace.YawOf(sum) : 0f;
        }

        private void BuildStairMouth(Transform root, MarkerRef r, MarkerDef m, MarkerRef to, Vector3 pos, LocationDef loc)
        {
            var go = new GameObject("Stair " + m.Id);
            go.transform.SetParent(root, false);
            go.transform.position = pos;
            bool cut = m.Id.Contains("cut");
            // The hole: a dark shaft with a few steps going down and a kerb.
            var hole = new MeshBuilder();
            var dark = new Color(0.03f, 0.03f, 0.04f);
            hole.Box(new Vector3(0, 0.03f, 0), new Vector3(2.2f, 0.04f, 2.6f), dark);
            for (int i = 0; i < 4; i++) hole.Box(new Vector3(0, 0.05f - i * 0.02f, -0.9f + i * 0.55f), new Vector3(1.8f - i * 0.1f, 0.05f, 0.45f), LandLook.Stone * (0.55f - i * 0.1f));
            hole.Box(new Vector3(-1.2f, 0.12f, 0), new Vector3(0.22f, 0.24f, 2.9f), LandLook.Stone * 0.8f);
            hole.Box(new Vector3(1.2f, 0.12f, 0), new Vector3(0.22f, 0.24f, 2.9f), LandLook.Stone * 0.8f);
            hole.Box(new Vector3(0, 0.12f, 1.4f), new Vector3(2.6f, 0.24f, 0.22f), LandLook.Stone * 0.8f);
            if (cut)
            {
                // The Ash cut: a timbered mouth dug into a hump of the hillside, raw earth round it.
                LandShapes.Beam(hole, new Vector3(-1.2f, 0, 1.5f), new Vector3(-1.2f, 2.3f, 1.5f), 0.14f, 6, LandLook.DarkWood);
                LandShapes.Beam(hole, new Vector3(1.2f, 0, 1.5f), new Vector3(1.2f, 2.3f, 1.5f), 0.14f, 6, LandLook.DarkWood);
                LandShapes.Beam(hole, new Vector3(-1.5f, 2.3f, 1.5f), new Vector3(1.5f, 2.3f, 1.5f), 0.16f, 6, LandLook.DarkWood);
                hole.Box(new Vector3(0, 1.1f, 1.7f), new Vector3(2.6f, 2.2f, 0.2f), dark);
                var raw = LandLook.Earth * 1.15f;
                hole.Cylinder(new Vector3(0, 0, 7.6f), 5.8f, 3.8f, 3.0f, 9, raw);
                hole.Cylinder(new Vector3(0, 3.0f, 7.6f), 3.8f, 1.4f, 1.0f, 9, new Color(0.26f, 0.40f, 0.20f));
                hole.Box(new Vector3(-1.2f, 1.15f, 1.45f), new Vector3(0.3f, 2.3f, 0.3f), LandLook.Wood);
                hole.Box(new Vector3(1.2f, 1.15f, 1.45f), new Vector3(0.3f, 2.3f, 0.3f), LandLook.Wood);
                hole.Box(new Vector3(0, 2.4f, 1.45f), new Vector3(3.1f, 0.32f, 0.36f), LandLook.Wood);
                var rr = new PropRandom(r.ToString().GetHashCode());
                for (int i = 0; i < 7; i++)
                    hole.Rock(new Vector3((i % 2 == 0 ? -1f : 1f) * rr.Range(1.7f, 3.4f), 0, rr.Range(0.6f, 2.6f)), rr.Range(0.5f, 1.0f), LandLook.Earth * rr.Range(0.8f, 1.1f), rr.Next);
            }
            var holeGo = hole.ToObject("Hole", go.transform, Lit);
            LightShapes.Apply(holeGo.GetComponent<Renderer>(), LightShapes.Read.Prop);
            // The lid: a ring-marked slab (the glade), or the bar across the cut.
            var lid = new MeshBuilder();
            if (cut)
            {
                for (int i = 0; i < 3; i++) lid.Box(new Vector3(0, 0.5f + i * 0.6f, 1.45f), new Vector3(2.7f, 0.22f, 0.16f), LandLook.Wood * (0.9f + 0.05f * i));
                lid.Box(new Vector3(0, 1.1f, 1.33f), new Vector3(0.24f, 1.9f, 0.1f), new Color(0.3f, 0.3f, 0.32f));
            }
            else
            {
                lid.Box(new Vector3(0, 0.11f, 0), new Vector3(2.6f, 0.2f, 3.0f), LandLook.Stone * 0.9f);
                LandShapes.Ring(lid, new Vector3(0, 0.215f, 0), 0.7f, 0.12f, 20, new Color(0.95f, 0.93f, 0.84f));
            }
            var slab = lid.ToObject("Slab", go.transform, Lit);
            LightShapes.Apply(slab.GetComponent<Renderer>(), LightShapes.Read.Prop);
            var stair = go.AddComponent<ExpeditionStair>();
            stair.Marker = r; stair.To = to; stair.Sites = this;
            stair.Label = Cap(string.IsNullOrEmpty(m.Name) ? "the stair" : m.Name);
            LandLook.PickBox(go, new Vector3(0, 0.6f, 0), new Vector3(2.8f, 1.2f, 3.2f));
            var sv = new StairView { Stair = stair, Slab = slab.transform, Hole = holeGo };
            // An open way down is always lit (PRD 5.10: the way back is never dark).
            if (DayNight.Instance != null)
            {
                sv.Torch = WorldLights.Torch(go.transform, pos + new Vector3(1.7f, 0, -1.2f), false, false, "Stair torch");
                sv.Torch.On = false;
                sv.Torch.DayLevel = 0.4f;
            }
            Stairs.Add(sv);
            _markerProps[r.ToString()] = go;
            // Face the hole towards where the party comes from (the site's safe point).
            if (loc != null && !string.IsNullOrEmpty(loc.SafePoint))
            {
                var safe = MarkerPoint(MarkerRef.Surface(loc.Id, loc.SafePoint));
                var dir = pos - safe; dir.y = 0;
                if (dir.sqrMagnitude > 0.5f) go.transform.rotation = Quaternion.Euler(0, BoardSpace.YawOf(dir), 0);
            }
            // The hump behind a cut is solid: nobody stands inside the hillside.
            if (cut) WalkGrid.AddSolid(go.transform.TransformPoint(new Vector3(0, 0, 7.6f)), 5.2f, SolidOwner);
        }

        private void BuildTower(LocationDef loc, Transform root, Vector3 anchor)
        {
            var d = D;
            var td = d.Towers.Find(t => t != null && t.Location == loc.Id);
            if (td == null) return;
            var colour = ElementColour(d, td.Element);
            var pos = ExpeditionSpace.Ground(anchor);
            root.position = pos;
            var door = d.Location(loc.Id).Markers.Find(m => m.Id == "door");
            var doorPos = door != null ? MarkerPoint(MarkerRef.Surface(loc.Id, door.Id)) : pos + Vector3.forward * 5f;
            var toDoor = doorPos - pos; toDoor.y = 0;
            root.rotation = Quaternion.Euler(0, toDoor.sqrMagnitude > 0.1f ? BoardSpace.YawOf(toDoor) : 0f, 0);
            var stone = new Color(0.56f, 0.55f, 0.52f);
            var mb = new MeshBuilder();
            // A round tower of old pale stone on a stepped foot, a band of its element's colour under the crown.
            mb.Cylinder(Vector3.zero, 4.3f, 4.0f, 0.5f, 14, stone * 0.85f);
            mb.Cylinder(new Vector3(0, 0.5f, 0), 3.3f, 2.6f, 12.5f, 14, stone);
            mb.Cylinder(new Vector3(0, 13f, 0), 2.9f, 2.9f, 0.9f, 14, colour * 0.55f);
            mb.Cylinder(new Vector3(0, 13.9f, 0), 3.2f, 3.2f, 0.5f, 14, stone * 0.92f);
            for (int i = 0; i < 6; i++)
            {
                float a = i * 60f * Mathf.Deg2Rad;
                mb.Box(new Vector3(Mathf.Sin(a) * 2.75f, 15.0f, Mathf.Cos(a) * 2.75f), new Vector3(0.9f, 1.2f, 0.7f), stone * 0.95f, i * 60f);
            }
            // The door (dark, towards the path) with its carved lintel, and the steps.
            mb.Box(new Vector3(0, 1.7f, 3.02f), new Vector3(1.5f, 2.6f, 0.5f), new Color(0.05f, 0.05f, 0.06f));
            mb.Box(new Vector3(0, 3.2f, 3.1f), new Vector3(2.2f, 0.45f, 0.5f), stone * 1.06f);
            mb.Box(new Vector3(0, 3.2f, 3.37f), new Vector3(1.5f, 0.2f, 0.04f), colour * 0.8f);
            for (int i = 0; i < 3; i++) mb.Box(new Vector3(0, 0.12f + i * 0.14f, 4.6f - i * 0.5f), new Vector3(2.6f - i * 0.2f, 0.16f, 0.5f), stone * 0.9f);
            if (td.Element == Element.Earth)
                for (int i = 0; i < 5; i++)
                {
                    // The Rooted Tower: stone roots gripping the ground.
                    float a = (36f + i * 72f) * Mathf.Deg2Rad;
                    LandShapes.Beam(mb, new Vector3(Mathf.Sin(a) * 2.9f, 3.2f, Mathf.Cos(a) * 2.9f), new Vector3(Mathf.Sin(a) * 6.0f, 0f, Mathf.Cos(a) * 6.0f), 0.42f, 6, stone * 0.8f);
                }
            string locName = loc.Name; var el = td.Element; string sign = td.Sign;
            var body = Finish(mb, "Tower", root, pos, () => Cap(locName), () => TowerLines(el, sign), new Vector3(0, 7f, 0), new Vector3(7f, 14f, 7f));
            body.transform.rotation = root.rotation;
            var tv = new TowerView { Element = td.Element, LocationId = loc.Id, Root = root, Colour = colour, Dark = body };

            // Lit: the crystal's fire in the crown, a beam into the sky, light on the ground.
            var lit = new GameObject("Lit");
            lit.transform.SetParent(root, false);
            var beam = new MeshBuilder();
            LandShapes.FadeBeam(beam, Vector3.zero, 1.3f, 0.5f, 55f, 12, colour * 0.22f);
            LandShapes.FadeBeam(beam, Vector3.zero, 0.45f, 0.12f, 85f, 8, colour * 0.6f);
            tv.Beam = beam.ToObject("Beam", lit.transform, LandLook.Additive);
            tv.Beam.transform.localPosition = new Vector3(0, 15.2f, 0);
            tv.Beam.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var glow = new MeshBuilder();
            glow.Cylinder(new Vector3(0, 13.02f, 0), 2.95f, 2.95f, 0.86f, 14, Color.Lerp(colour, Color.white, 0.25f), false);
            glow.Box(new Vector3(0, 3.2f, 3.38f), new Vector3(1.5f, 0.2f, 0.04f), Color.Lerp(colour, Color.white, 0.3f));
            glow.Box(new Vector3(0, 1.7f, 3.28f), new Vector3(1.3f, 2.4f, 0.04f), colour * 0.45f);
            glow.ToObject("Band glow", lit.transform, LandLook.Unlit).GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            // The ground round a lit tower: green ring of new growth and a ring of light.
            var bloom = new MeshBuilder();
            var rnd = new PropRandom(loc.Id.GetHashCode());
            LandShapes.Ring(bloom, new Vector3(0, 0.07f, 0), 8.5f, 0.5f, 40, colour * 0.6f);
            for (int i = 0; i < 46; i++)
            {
                float a = rnd.Range(0f, Mathf.PI * 2f), r = rnd.Range(5.2f, 12.5f);
                var c = new Vector3(Mathf.Sin(a) * r, 0, Mathf.Cos(a) * r);
                bloom.Cone(c, rnd.Range(0.16f, 0.3f), rnd.Range(0.35f, 0.7f), 5, new Color(0.30f, 0.62f, 0.24f) * rnd.Range(0.85f, 1.1f));
                if (i % 3 == 0) bloom.Cone(c + new Vector3(0.15f, 0.3f, 0.1f), 0.1f, 0.22f, 5, Color.Lerp(colour, Color.white, 0.4f));
            }
            tv.Bloom = bloom.ToObject("Bloom", lit.transform, Lit);
            LightShapes.Apply(tv.Bloom.GetComponent<Renderer>(), LightShapes.Read.Stock);
            if (DayNight.Instance != null)
            {
                tv.Light = WorldLights.Add(lit.transform, new Vector3(0, 6.5f, 4.5f), colour, 2.6f, 26f, 3, "Tower light");
                tv.Light.Look = FireLight.Style.Steady; tv.Light.Sparks = false; tv.Light.GroundGlow = 11f; tv.Light.Halo = 3.2f; tv.Light.DayLevel = 0.55f;
                tv.Light.HaloStrength = 0.9f;
            }
            lit.SetActive(false);
            tv.Lit = lit;
            Towers.Add(tv);
            WalkGrid.AddSolid(pos, 3.4f, SolidOwner);
        }

        private static void DressKeeper(Npc npc)
        {
            if (!(npc.Actor is PlaceholderActor a) || a.BodyPivot.Find("TallyStaff") != null) return;
            var bone = MaterialFactory.Shared(new Color(0.90f, 0.86f, 0.76f));
            var ash = MaterialFactory.Shared(new Color(0.20f, 0.19f, 0.19f));
            Signs.Prim(PrimitiveType.Cube, a.BodyPivot, "TallyStaff", new Vector3(0.42f, 1.2f, 0.1f), new Vector3(0.07f, 2.3f, 0.07f), MaterialFactory.Shared(new Color(0.36f, 0.25f, 0.15f)));
            for (int i = 0; i < 6; i++) Signs.Prim(PrimitiveType.Cube, a.BodyPivot, "Notch", new Vector3(0.42f, 1.0f + i * 0.2f, 0.14f), new Vector3(0.12f, 0.04f, 0.04f), bone);
            Signs.Prim(PrimitiveType.Cube, a.BodyPivot, "Robe", new Vector3(0, 0.75f, 0), new Vector3(0.62f, 1.1f, 0.46f), ash);
            Signs.Prim(PrimitiveType.Capsule, a.HeadPivot, "HornL", new Vector3(-0.16f, 0.32f, 0), new Vector3(0.06f, 0.16f, 0.06f), bone, new Vector3(0, 0, 35f));
            Signs.Prim(PrimitiveType.Capsule, a.HeadPivot, "HornR", new Vector3(0.16f, 0.32f, 0), new Vector3(0.06f, 0.16f, 0.06f), bone, new Vector3(0, 0, -35f));
            npc.transform.localScale = Vector3.one * 1.12f;
        }

        // =====================================================================================================
        // What a tap says
        // =====================================================================================================

        private IEnumerable<string> InscriptionLines(MarkerRef r)
        {
            // What the party has read here: the journal's own sentence for the place, else that nobody has read it yet.
            var c = C;
            foreach (var ev in c.Definition.Events)
            {
                if (ev == null || ev.At != r || !c.FiredEvents.Contains(ev.Id)) continue;
                foreach (var fx in ev.Effects)
                    if (fx.Kind == EffectKind.Journal) { var t = CampaignApi.Line(S, fx.Ref); if (t != null) { yield return t; yield break; } }
            }
            yield return "Old marks. Mira has not read them yet.";
        }

        private IEnumerable<string> HolderLines(string crystalId, bool altar)
        {
            var cd = D.Crystal(crystalId);
            if (cd == null) { yield return altar ? "An altar of the Ash Clan." : "A pedestal of the old keepers."; yield break; }
            var st = C.Crystal(cd.Element);
            bool here = altar ? st.Custody == Custody.AtRivalAltar : st.Custody == Custody.AtSite;
            yield return here ? Cap(cd.Name) + " rests here." : (altar ? "An altar of the Ash Clan. Nothing lies on it." : "Bare. " + Cap(cd.Name) + " is not here.");
        }

        private IEnumerable<string> TowerLines(Element e, string signLine)
        {
            var sign = CampaignApi.Line(S, signLine);
            if (sign != null) yield return sign;
            var st = C.Crystal(e);
            yield return st != null && st.Custody == Custody.Restored ? "Its fire burns again." : "Dark and cold. It waits for its crystal.";
        }

        public string StairSentence(ExpeditionStair st)
        {
            bool open = ExpeditionSpace.LinkOpen(C, st.Marker, st.To);
            string below = CampaignTokens.PlaceName(D, st.To);
            return open ? "A way down to " + below + ". The way back up stays lit." : "Shut. Nothing here moves by hand.";
        }

        /// <summary>A tap on an open stair: go down. With package I5 the walker is sent through the link; without it the director leads the viewpoint to the party below.</summary>
        public void UseStair(ExpeditionStair st)
        {
            if (!ExpeditionSpace.LinkOpen(C, st.Marker, st.To))
            {
                SpeechBubbles.Inspect(st.transform, Vector3.zero, st.Title, new[] { StairSentence(st) }, 5f);
                return;
            }
            string go = ExpeditionLines.Get(S, ExpeditionLines.ReplyGoDown, "Go down");
            SpeechBubbles.Inspect(st.transform, Vector3.zero, st.Title, new[] { StairSentence(st) }, 8f, new[] { go }, i =>
            {
                if (i != 0) return;
                if (!_dir.StandInInteriors) Interiors.Enter(st.To);
                else _dir.GoDownStandIn(st);
            });
        }

        private void OnKeeperTapped(Npc n)
        {
            var kv = Keepers.Find(k => k.Npc == n);
            if (kv == null) return;
            var e = D.Encounter(kv.EncounterId);
            // His own lines (the pack's topic named after the encounter), the newest that may be said now.
            string said = null;
            foreach (var l in D.Lines)
            {
                if (l.Topic != kv.EncounterId || l.Speaker != (D.Roles.Find(r => r.ActorId == kv.ActorId)?.Id ?? kv.ActorId)) continue;
                var t = CampaignApi.Line(S, l.Id);
                if (t != null) said = t;
            }
            SpeechBubbles.Say(n, said ?? (e != null ? e.Name : n.DisplayName), 5f);
        }

        /// <summary>A person the sites placed (a keeper), by actor id; null if none.</summary>
        public Npc Person(string actorId) => Keepers.Find(k => k.ActorId == actorId)?.Npc;
        public string SpaceOfPerson(Npc n) => Keepers.Find(k => k.Npc == n)?.Space ?? "";

        // =====================================================================================================
        // Following state
        // =====================================================================================================

        private string _signature;

        public void OnCustody(CrystalCustodyChanged cc) { _signature = null; }
        public void OnQuestEvent(QuestEventDef ev) { _signature = null; }

        public void OnRestored(CrystalRestored cr)
        {
            var tv = Tower(cr.Crystal);
            if (tv == null) return;
            tv.LitAt = Time.time;
            RestorationsPlayed++;
            _signature = null;
            StartWave(tv);
        }

        private void Update()
        {
            if (_boot == null || _boot.Sim == null || C == null) return;
            Sync(false);
            Animate();
        }

        private string Signature()
        {
            var c = C;
            var sb = new System.Text.StringBuilder();
            foreach (var st in c.Crystals) sb.Append((int)st.Custody).Append(st.At.ToString()).Append(';');
            sb.Append(c.Facts.Count).Append('|').Append(c.CompletedEncounters.Count).Append('|').Append((int)c.Party.Mode).Append('|').Append(c.Party.ActingAction).Append(c.Party.ActingCrystal);
            return sb.ToString();
        }

        private void Sync(bool force)
        {
            string sig = Signature();
            if (!force && sig == _signature) return;
            _signature = sig;
            var c = C; var d = D;
            foreach (var sv in Stairs) sv.Open = ExpeditionSpace.LinkOpen(c, sv.Stair.Marker, sv.Stair.To);
            foreach (var tv in Towers)
            {
                bool restored = c.Crystal(tv.Element).Custody == Custody.Restored;
                if (restored == tv.Restored && !force) continue;
                tv.Restored = restored;
                tv.Lit.SetActive(restored);
                if (tv.Light != null) tv.Light.On = restored;
            }
            foreach (var kv in Keepers)
            {
                var e = d.Encounter(kv.EncounterId);
                // The keeper stands at his altar while the crystal lies there, and stays (beaten) after a win.
                string k = KeeperCrystal(e);
                var cd = k.Length > 0 ? d.Crystal(k) : null;
                bool there = c.CompletedEncounters.Contains(kv.EncounterId) || (cd != null && c.Crystal(cd.Element).Custody == Custody.AtRivalAltar);
                if (kv.Npc.gameObject.activeSelf != there) kv.Npc.gameObject.SetActive(there);
                if (!there) continue;
                var at = ExpeditionSpace.World(S, e.Stage.Opponent);
                kv.Npc.transform.position = kv.Space.Length == 0 ? ExpeditionSpace.Ground(at) : at;
                var face = e.Threshold.IsEmpty ? at + Vector3.forward : ExpeditionSpace.World(S, e.Threshold);
                kv.Npc.FaceTowards(face);
            }
        }

        private static string KeeperCrystal(EncounterDef e)
        {
            if (e == null) return "";
            foreach (var fx in e.OnVictory) if (fx.Kind == EffectKind.TakeCrystal) return fx.Crystal ?? "";
            return "";
        }

        /// <summary>Where a crystal is drawn now: "pedestal", "altar", "tower", "rising" (being set), "lifting" (being taken), "" (carried by people).</summary>
        public string CrystalPlace(Element e) => Crystal(e)?.Where ?? "";

        private void Animate()
        {
            var c = C; var d = D; var s = S;
            float dt = Time.deltaTime;
            foreach (var sv in Stairs)
            {
                sv.Slide = Mathf.MoveTowards(sv.Slide, sv.Open ? 1f : 0f, dt / 2.5f);
                bool cut = sv.Stair.Marker.Marker.Contains("cut");
                // The slab grinds aside (the glade); the bars lift away (the cut).
                sv.Slab.localPosition = cut ? new Vector3(0, sv.Slide * 3.2f, 0) : new Vector3(sv.Slide * 2.9f, 0, 0);
                if (cut) sv.Slab.gameObject.SetActive(sv.Slide < 0.98f);
                if (sv.Torch != null) sv.Torch.On = sv.Open;
            }
            var p = c.Party;
            float acting = p.Mode == PartyMode.Acting && p.ActingTicks > 0 ? Mathf.Clamp01((p.ActingProgress + (float)(_boot.Clock.IsPaused ? 0 : 0.5f)) / p.ActingTicks) : 0f;
            foreach (var cv in Crystals)
            {
                var cd = d.Crystal(cv.Element);
                var st = c.Crystal(cv.Element);
                Vector3 pos = default; bool show = true; string where = "";
                switch (st.Custody)
                {
                    case Custody.AtSite:
                    {
                        pos = MarkerPoint(cd.Site) + Vector3.up * 1.14f; where = "pedestal";
                        // While Mira takes it, it lifts off the pedestal towards her hands.
                        if (p.Mode == PartyMode.Acting && p.ActingAction == QuestAction.Take && p.At == cd.Site && _dir != null && _dir.Mira != null)
                        {
                            var hands = _dir.Mira.transform.position + Vector3.up * 1.25f;
                            pos = Vector3.Lerp(pos, hands, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.35f, 1f, acting)));
                            where = "lifting";
                        }
                        break;
                    }
                    case Custody.AtRivalAltar:
                        pos = MarkerPoint(st.At) + Vector3.up * 1.08f; where = "altar";
                        break;
                    case Custody.Restored:
                    {
                        var tv = Tower(cv.Element);
                        if (tv == null) { show = false; break; }
                        pos = tv.Root.position + Vector3.up * 14.6f; where = "tower";
                        break;
                    }
                    case Custody.Carried:
                    {
                        var tv = Tower(cv.Element);
                        if (tv != null && p.Mode == PartyMode.Acting && p.ActingAction == QuestAction.Restore && p.ActingCrystal == cd.Id && _dir != null && _dir.Mira != null)
                        {
                            // "Set it": out of her hands, up the face of the tower and into the crown.
                            var hands = _dir.Mira.transform.position + Vector3.up * 1.6f;
                            var crown = tv.Root.position + Vector3.up * 14.6f;
                            float u = Mathf.SmoothStep(0f, 1f, acting);
                            pos = Vector3.Lerp(hands, crown, u) + Vector3.up * Mathf.Sin(u * Mathf.PI) * 2.5f;
                            where = "rising";
                        }
                        else show = false;
                        break;
                    }
                    default: show = false; break; // in the Ash runners' hands (ExpeditionAsh draws it)
                }
                cv.Where = show ? where : "";
                if (cv.Gem.activeSelf != show) cv.Gem.SetActive(show);
                if (!show) continue;
                float size = st.Custody == Custody.Restored ? 1.9f : 1f;
                cv.Gem.transform.position = pos + Vector3.up * (Mathf.Sin(Time.time * 1.7f + (int)cv.Element) * 0.05f);
                cv.Gem.transform.rotation = Quaternion.Euler(0, Time.time * 35f, 0);
                cv.Gem.transform.localScale = Vector3.one * size;
            }
            foreach (var tv in Towers)
            {
                if (!tv.Restored) continue;
                // Lighting up: the beam and the growth rise over a few seconds; afterwards the beam breathes slowly.
                float since = tv.LitAt < 0 ? 100f : Time.time - tv.LitAt;
                float k = Mathf.Clamp01(since / 4f);
                tv.Beam.transform.localScale = new Vector3(1f + 0.08f * Mathf.Sin(Time.time * 1.3f), Mathf.SmoothStep(0.02f, 1f, k), 1f + 0.08f * Mathf.Sin(Time.time * 1.3f));
                tv.Bloom.transform.localScale = new Vector3(1f, Mathf.SmoothStep(0.05f, 1f, Mathf.Clamp01(since / 7f)), 1f);
            }
            AnimateWave(dt);
        }

        // ---- the wave of light that crosses the island when a tower is lit ----

        private GameObject _wave;
        private float _waveT = -1f;
        private Color _waveColour;
        /// <summary>True while the wave of a restoration is crossing the island.</summary>
        public bool WavePlaying => _waveT >= 0f;

        private void StartWave(TowerView tv)
        {
            if (_wave == null)
            {
                _wave = LandLook.Part(transform, "Restoration wave", LightShapes.SoftRing(0.12f), LightShapes.Glow, false);
                _wave.transform.rotation = Quaternion.identity;
            }
            _wave.transform.position = tv.Root.position + Vector3.up * 0.35f;
            _waveColour = tv.Colour;
            _waveT = 0f;
            _wave.SetActive(true);
        }

        private void AnimateWave(float dt)
        {
            if (_waveT < 0f || _wave == null) return;
            _waveT += dt;
            float r = 6f + _waveT * 90f; // across the island in about twelve seconds
            _wave.transform.localScale = new Vector3(r, 1f, r);
            var mpb = new MaterialPropertyBlock();
            mpb.SetColor("_Color", _waveColour * Mathf.Clamp01(1.2f - _waveT / 13f) * 0.9f);
            _wave.GetComponent<Renderer>().SetPropertyBlock(mpb);
            if (_waveT > 13f) { _waveT = -1f; _wave.SetActive(false); }
        }
    }

    /// <summary>Turns a quad to the camera each frame (halos of crystals).</summary>
    public sealed class FaceCamera : MonoBehaviour
    {
        private void LateUpdate()
        {
            var cam = ViewSwitcher.Instance != null ? ViewSwitcher.Instance.ActiveCamera : Camera.main;
            if (cam != null) transform.rotation = Quaternion.LookRotation(transform.position - cam.transform.position);
        }
    }
}
