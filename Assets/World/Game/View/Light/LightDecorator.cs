using System.Collections.Generic;
using UnityEngine;
using World.Sim;
using World.Sim.Economy;

namespace World.Game
{
    /// <summary>Marks a view that package L has given its lights (and holds them, so its owner state can switch them).</summary>
    public sealed class LitBy : MonoBehaviour
    {
        public FireLight Main, Burn;
        public readonly List<FireLight> Torches = new List<FireLight>();
        /// <summary>The local-paths mesh already unstacked (see <see cref="LightDecorator"/>).</summary>
        public int PathMeshId;
    }

    /// <summary>
    /// Finds the other packages' views at runtime and gives them light (package L, Docs/LIGHTING.md): a hearth light
    /// and entrance torches on every camp of both peoples, a door torch on every finished building, the war fire, the
    /// overrun fire, an unsettling glow under demons, a slim beam and gold light on the top "cast here" mark, the
    /// home camp's braziers (Mira, the spawn, the way into the dance circle) and the dance fire. It also swaps
    /// materials for their night-readable variants (people, loads, stock, number tokens). Nothing in the other
    /// packages' code is called to do this: objects are found by component type and by child name, twice a second.
    /// </summary>
    public sealed class LightDecorator : MonoBehaviour
    {
        public static LightDecorator Instance { get; private set; }

        public const float ScanSeconds = 0.5f;
        public const float CampTorchDistance = 7.2f, CampTorchSide = 2.0f;
        public static readonly Color DemonGlow = new Color(0.95f, 0.07f, 0.24f), MarkGold = new Color(1f, 0.82f, 0.35f), MarkBlue = new Color(0.55f, 0.85f, 1f);

        public int CampsLit { get; private set; }
        public int BuildingsLit { get; private set; }
        public FireLight DanceFire { get; private set; }
        public FireLight MiraBrazier { get; private set; }
        public FireLight PrinceLight { get; private set; }

        private WorldBootstrap _boot;
        private Transform _root;
        private CampLayout _layout;
        private float _nextScan, _nextSlowScan;
        private readonly HashSet<int> _actorsDone = new HashSet<int>();

        public static LightDecorator Create(WorldBootstrap boot, Transform root)
        {
            var d = root.gameObject.AddComponent<LightDecorator>();
            d._boot = boot; d._root = root;
            d._layout = new CampLayout(boot.Sim.State.Board);
            Instance = d;
            d.BuildHomeFixtures();
            d.Scan(true);
            return d;
        }

        private void OnDestroy() { if (Instance == this) Instance = null; }

        private void Update()
        {
            if (Time.unscaledTime < _nextScan) return;
            _nextScan = Time.unscaledTime + ScanSeconds;
            bool slow = Time.unscaledTime >= _nextSlowScan;
            if (slow) _nextSlowScan = Time.unscaledTime + 2f;
            Scan(slow);
        }

        /// <summary>Runs the scan now (tests and captures call this after changing the world).</summary>
        public void ScanNow() => Scan(true);

        // ---------------- the home camp's own fixtures ----------------

        private void BuildHomeFixtures()
        {
            var board = _boot.Sim.State.Board;
            var home = new GameObject("Home camp lights").transform;
            home.SetParent(_root, false);

            // Mira: a brazier at her side, so she is found from the spawn and her face is lit for the duel.
            var face = BoardSpace.DirOf(_layout.MiraYaw);
            var side = new Vector3(face.z, 0, -face.x);
            MiraBrazier = WorldLights.Torch(home, FirstClear(board, _layout.MiraSpot + side * 2.3f - face * 0.5f, _layout.MiraSpot - side * 2.3f - face * 0.5f), false, true, "Brazier (Mira)");
            MiraBrazier.Priority = 1;

            // The spawn: a brazier beside the first steps towards the prince.
            var spawn = WalkLattice.ToWorld(_layout.Spawn); var prince = WalkLattice.ToWorld(_layout.PrinceStart);
            var along = prince - spawn; along.y = 0;
            if (along.sqrMagnitude > 0.01f)
            {
                along.Normalize();
                var across = new Vector3(along.z, 0, -along.x);
                var mid = spawn + along * 7.5f;
                WorldLights.Torch(home, FirstClear(board, mid + across * 3.1f, mid - across * 3.1f), false, true, "Brazier (spawn)").Priority = 1;
                var far = spawn + along * 17.5f;
                WorldLights.Torch(home, FirstClear(board, far - across * 3.1f, far + across * 3.1f), false, false, "Torch (prince's start)");
            }

            // The way into the dance circle: a torch inside each gate totem.
            var gate = _layout.DanceCircleCentre - _layout.Forward * ((float)PlotLayout.DanceCircleRadius + 1.2f);
            WorldLights.Torch(home, gate - _layout.Right * 2.5f, false, false, "Torch (dance gate)");
            WorldLights.Torch(home, gate + _layout.Right * 2.5f, false, false, "Torch (dance gate)");
        }

        private static Vector3 FirstClear(Board board, Vector3 a, Vector3 b)
        {
            a.y = b.y = BoardSpace.GroundY;
            if (CampLayout.Obstruction(board, a, 0.5f) == null) return a;
            if (CampLayout.Obstruction(board, b, 0.5f) == null) return b;
            return a;
        }

        // ---------------- scanning the other packages' views ----------------

        private void Scan(bool slow)
        {
            AdoptDanceFire();
            var views = LandViews.Instance;
            if (views != null)
            {
                int camps = 0, buildings = 0;
                foreach (var sv in views.Settlements) if (sv != null && Camp(sv)) camps++;
                foreach (var bv in views.Buildings) if (bv != null && Building(bv)) buildings++;
                CampsLit = camps; BuildingsLit = buildings;
                foreach (var dv in views.DemonViews) if (dv != null) Demons(dv);
                foreach (var mv in views.Markers) if (mv != null) Marker(mv);
                if (views.WarCamp != null) WarCamp(views.WarCamp);
                if (slow)
                {
                    int hexes = _boot.Sim.State.Board.Topology.HexCount;
                    for (int h = 0; h < hexes; h++) { var hv = views.Hex(h); if (hv != null) Hex(hv); }
                }
            }
            Figures();
            if (slow) Actors();
            Prince();
        }

        private void AdoptDanceFire()
        {
            if (DanceFire != null) return;
            var dc = DanceCircle.Current;
            if (dc == null) return;
            var lt = dc.transform.Find("Fire light");
            var light = lt != null ? lt.GetComponent<Light>() : null;
            if (light == null) return;
            // D drives this light (flicker, beat pump, the fire leap flare); L makes it reach the dancers and drums at
            // night, keeps it per-pixel and adds the ground glow, which follows the same pump.
            DanceFire = WorldLights.Adopt(light, DanceCircle.FireOrange, 3, "Dance fire glow");
            DanceFire.ExternalNightBoost = 1.45f;
            DanceFire.ExternalNightRange = 21f;
            DanceFire.GroundGlow = 8.5f; DanceFire.GroundGlowStrength = 0.26f;
            DanceFire.Halo = 2.4f; DanceFire.HaloStrength = 0.40f;
        }

        private bool Camp(SettlementView sv)
        {
            var fire = sv.transform.Find("Fire");
            if (fire == null) return false; // still a site
            var lit = sv.GetComponent<LitBy>();
            bool ash = sv.Faction == PlayerIds.Rival;
            if (lit == null)
            {
                lit = sv.gameObject.AddComponent<LitBy>();
                lit.Main = WorldLights.Add(sv.transform, new Vector3(0, 1.0f, 0), WorldLights.FireOf(sv.Faction), 3.2f, 19f, 1, "Hearth light");
                if (ash) AshFlameOver(fire, lit.Main, new Vector3(0.5f, 1.35f, 0.5f));
                lit.Main.DayLevel = 0.35f; lit.Main.GroundGlow = 7.5f; lit.Main.Halo = 2.6f;

                // Entrance torches where the local paths leave the storehouse ring: a pair at your first camp, one
                // per path elsewhere (the Ash Clan's burn green).
                var t = _boot.Sim.State.Board.Topology;
                bool home = sv.Corner == _layout.HomeCorner;
                var holder = new GameObject("Camp torches").transform;
                holder.SetParent(sv.transform, false);
                for (int k = 0; k < 3; k++)
                {
                    int h = PlotLayout.SectorHex(t, sv.Corner, k);
                    if (h < 0) continue;
                    var d = BoardSpace.Hex(t, h) - sv.transform.position; d.y = 0; d.Normalize();
                    var across = new Vector3(d.z, 0, -d.x);
                    var p = sv.transform.position + d * CampTorchDistance;
                    lit.Torches.Add(WorldLights.Torch(holder, p + across * CampTorchSide, ash));
                    if (home) lit.Torches.Add(WorldLights.Torch(holder, p - across * CampTorchSide, ash));
                }
            }
            // A demon-held or overrun camp is dark: its fire is out (V hides the flame), so are its torches.
            bool burning = fire.gameObject.activeSelf;
            lit.Main.On = burning;
            for (int i = 0; i < lit.Torches.Count; i++) lit.Torches[i].On = burning;

            var burn = sv.transform.Find("Burning");
            if (burn != null && burn.gameObject.activeSelf && lit.Burn == null)
            {
                lit.Burn = WorldLights.Add(sv.transform, new Vector3(0, 1.8f, 0), new Color(1f, 0.42f, 0.14f), 2.6f, 24f, 2, "Overrun fire light");
                lit.Burn.DayLevel = 0.7f; lit.Burn.GroundGlow = 10f; lit.Burn.GroundGlowStrength = 0.36f; lit.Burn.Halo = 3.2f;
            }
            if (lit.Burn != null) lit.Burn.On = burn != null && burn.gameObject.activeSelf;

            Unstack(sv.transform.Find("LocalPaths"), lit);
            Dim(sv.transform, "Smoke", 0.75f);
            Dim(sv.transform, "HeldGround", 0.4f);
            Lit(sv.transform, "StorePiles", LightShapes.Read.Load);
            Lit(sv.transform, "Totem", LightShapes.Read.Prop);
            return true;
        }

        /// <summary>
        /// The local paths of a camp are one mesh of flat strips that overlap along the spokes, all at exactly the same
        /// height. With one sun that is invisible; under a per-pixel fire light the overlapping strips are each lit
        /// again (the additive pass passes the depth test for every coplanar copy) and the path shows bright stripes.
        /// Each strip is lifted by its own fraction of a millimetre to 4 cm, so only the top one takes the light.
        /// Done once per mesh (V replaces the mesh when the camp's plots change).
        /// </summary>
        private static void Unstack(Transform paths, LitBy lit)
        {
            var mf = paths != null ? paths.GetComponent<MeshFilter>() : null;
            var mesh = mf != null ? mf.sharedMesh : null;
            if (mesh == null || mesh.GetInstanceID() == lit.PathMeshId) return;
            lit.PathMeshId = mesh.GetInstanceID();
            var v = mesh.vertices;
            for (int i = 0; i < v.Length; i++) v[i].y += ((i / 4) * 37 % 101) * 0.0004f;
            mesh.vertices = v;
            mesh.RecalculateBounds();
        }

        private bool Building(BuildingView bv)
        {
            bool active = bv.ShownState == BuildingState.Active;
            var lit = bv.GetComponent<LitBy>();
            if (lit == null)
            {
                if (!active) return false;
                lit = bv.gameObject.AddComponent<LitBy>();
                // By the door (the model's front is +Z), to the right, inside the plot.
                lit.Main = WorldLights.Torch(bv.transform, bv.transform.TransformPoint(new Vector3(2.9f, 0, 3.5f)), bv.Faction == PlayerIds.Rival, false, "Door torch");
            }
            lit.Main.On = active;
            return active;
        }

        private void Demons(DemonView dv)
        {
            // V's red spire is additive: right by day, a floodlight at night. It keeps its shape and is turned down.
            Fade(dv.transform, "Spire", Mathf.Lerp(1f, 0.30f, Night));
            if (dv.GetComponent<LitBy>() != null) return;
            var lit = dv.gameObject.AddComponent<LitBy>();
            lit.Main = WorldLights.Add(dv.transform, new Vector3(0, 1.3f, 0), DemonGlow, 1.6f, 11f, 1, "Demon glow");
            lit.Main.Look = FireLight.Style.Pulse;
            lit.Main.DayLevel = 0.35f; lit.Main.Sparks = false;
            lit.Main.GroundGlow = 7.5f; lit.Main.GroundGlowStrength = 0.34f;
            lit.Main.Halo = 2.6f; lit.Main.HaloStrength = 0.30f;
        }

        private void Marker(ProposalMarkerView mv)
        {
            var lit = mv.GetComponent<LitBy>();
            if (lit == null)
            {
                lit = mv.gameObject.AddComponent<LitBy>();
                bool banish = mv.name.Contains("Banish");
                var col = banish ? MarkBlue : MarkGold;
                // V's wide pillar becomes a slim soft beam (same object and material, so V's pulse still drives it).
                var pillar = mv.transform.Find("Pillar");
                var mf = pillar != null ? pillar.GetComponent<MeshFilter>() : null;
                if (mf != null) mf.sharedMesh = LightShapes.SlimBeam(col);
                lit.Main = WorldLights.Add(mv.transform, new Vector3(0, 1.5f, 0), col, 1.5f, 11f, 2, "Cast mark light");
                lit.Main.Look = FireLight.Style.Steady;
                lit.Main.DayLevel = 0.2f; lit.Main.Sparks = false; lit.Main.Halo = 0f;
                lit.Main.GroundGlow = mv.IsStrip ? 5f : Mathf.Max(3f, mv.PressRadius * 1.7f);
                lit.Main.GroundGlowStrength = 0.20f;
            }
            lit.Main.On = mv.IsTop;
        }

        private void WarCamp(WarCampView wv)
        {
            if (wv.GetComponent<LitBy>() != null) return;
            var fire = wv.transform.Find("WarFire");
            if (fire == null) return;
            var lit = wv.gameObject.AddComponent<LitBy>();
            bool ash = wv.Attacker == PlayerIds.Rival;
            lit.Main = WorldLights.Add(wv.transform, fire.localPosition + Vector3.up * 1.3f, ash ? WorldLights.AshFire : new Color(1f, 0.46f, 0.16f), 2.3f, 17f, 2, "War fire light");
            if (ash) AshFlameOver(fire, lit.Main, new Vector3(0.7f, 2.1f, 0.7f));
            lit.Main.DayLevel = 0.45f; lit.Main.GroundGlow = 7.5f; lit.Main.Halo = 2.2f;
        }

        /// <summary>
        /// The Ash Clan's fires burn green: V's orange flame mesh is not drawn (its renderer only; V still switches the
        /// object, which is what <see cref="FireLight.On"/> follows) and a green flame of the same size stands in its place.
        /// </summary>
        private static void AshFlameOver(Transform vFire, FireLight light, Vector3 scale)
        {
            var r = vFire.GetComponent<Renderer>();
            if (r != null) r.enabled = false;
            var flame = LandLook.Part(vFire.parent, "Ash flame", LightShapes.AshFlame(), LandLook.Unlit, false);
            flame.transform.localPosition = vFire.localPosition;
            light.Flame = flame.transform;
            light.FlameScale = scale;
        }

        private static void Hex(HexLandView hv)
        {
            var t = hv.transform;
            // Stock keeps a little of its own colour at night, so ripe (large, bright) reads against young (small, pale).
            Lit(t, "Items", LightShapes.Read.Stock);
            Dim(t, "ReadyMark", 0.55f);
            var token = hv.Token;
            if (token == null) return;
            // The roll effects are additive (made for daylight): turned down at night so a paying hex does not white out the view.
            float k = Mathf.Lerp(1f, 0.42f, DayNight.Instance != null ? DayNight.Instance.Night : 0f);
            Fade(t, "PaidBeam", k); Fade(t, "PaidRing", Mathf.Lerp(1f, 0.7f, 1f - k)); Fade(token, "Halo", Mathf.Lerp(k, 1f, 0.3f));
            // The number token is unlit (always readable); at night it is dimmed to a soft self-lit cream instead of a white lamp.
            Dim(token, "Disc", 1f); Dim(token, "Pips", 1f); Dim(token, "Stopped", 1f);
        }

        private static float Night => DayNight.Instance != null ? DayNight.Instance.Night : 0f;
        private static MaterialPropertyBlock _fade;
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        /// <summary>Scales the brightness of another package's additive effect (a property block on its renderer; its material is untouched).</summary>
        private static void Fade(Transform parent, string child, float k)
        {
            var c = parent != null ? parent.Find(child) : null;
            var r = c != null ? c.GetComponent<Renderer>() : null;
            if (r == null) return;
            if (_fade == null) _fade = new MaterialPropertyBlock();
            _fade.SetColor(ColorId, new Color(k, k, k, 1f));
            r.SetPropertyBlock(_fade);
        }

        private static void Lit(Transform parent, string child, LightShapes.Read read)
        {
            var c = parent.Find(child);
            if (c != null) LightShapes.Apply(c.GetComponent<Renderer>(), read);
        }

        private static void Dim(Transform parent, string child, float amount)
        {
            var c = parent.Find(child);
            if (c != null) LightShapes.ApplyDim(c.GetComponent<Renderer>(), amount);
        }

        private static void Figures()
        {
            // Every primitive person of the land views (carriers, porters, gatherers, builders, warriors, demons).
            var figs = FindObjectsByType<LandFigure>(FindObjectsSortMode.None);
            for (int i = 0; i < figs.Length; i++)
            {
                var body = figs[i].Body;
                if (body == null) continue;
                bool demon = figs[i].FigureKind == LandFigure.Kind.Demon;
                for (int c = 0; c < body.childCount; c++)
                {
                    var ch = body.GetChild(c);
                    var r = ch.GetComponent<Renderer>();
                    if (r == null) continue;
                    LightShapes.Apply(r, ch.name == "Load" ? LightShapes.Read.Load : demon ? LightShapes.Read.Demon : LightShapes.Read.Person);
                }
            }
        }

        private void Actors()
        {
            // Mira, Sol, the dancers, the camp's villagers and (when package N lands) the prince, sawyer and merchant.
            var actors = FindObjectsByType<PlaceholderActor>(FindObjectsSortMode.None);
            for (int i = 0; i < actors.Length; i++)
            {
                if (!_actorsDone.Add(actors[i].GetInstanceID())) continue;
                foreach (var r in actors[i].GetComponentsInChildren<Renderer>(true)) LightShapes.Apply(r, LightShapes.Read.Person);
            }
        }

        private void Prince()
        {
            if (PrinceLight != null) return;
            var pd = PrinceDirector.Instance;
            var prince = pd != null ? pd.Prince : null;
            if (prince == null) return;
            PrinceLight = CarryLight(prince.transform);
        }

        /// <summary>A small warm light carried by a character (the prince), so he can be followed across dark country.</summary>
        public static FireLight CarryLight(Transform character)
        {
            var f = WorldLights.Add(character, new Vector3(0.32f, 1.55f, 0.25f), new Color(1f, 0.80f, 0.52f), 1.1f, 7.5f, 2, "Carry light");
            f.Look = FireLight.Style.Steady; f.Sparks = false;
            f.GroundGlow = 2.6f; f.GroundGlowStrength = 0.22f; f.Halo = 0.42f; f.HaloStrength = 0.7f;
            return f;
        }
    }
}
