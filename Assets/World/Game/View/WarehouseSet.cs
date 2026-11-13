using System.Collections.Generic;
using UnityEngine;

namespace World.Game
{
    /// <summary>The two closed areas off the warehouse floor.</summary>
    public enum Route { Bar, Balcony }

    /// <summary>
    /// The warehouse: an enterable hall with a lit doorway, dance floor, DJ decks, a bar annex and a
    /// balcony up a ramp. Bar and balcony are closed by barriers with their lights off until
    /// <see cref="SetRouteOpen"/>. Local frame: origin at the door on the ground, +Z out of the door.
    /// Size (map rework): hall 13 x 22 m, 7 m high, bar annex 4.75 m wide on the local -X side, so the whole
    /// building is about 17.75 x 22 m (the largest in the village, still under a 20 m plot spacing in depth + width).
    /// </summary>
    public sealed class WarehouseSet : MonoBehaviour, IInspectable
    {
        public const float HallHalfWidth = 6.5f, HallDepth = 22f, HallHeight = 7f;
        /// <summary>Outer width of the bar annex beyond the hall's -X wall.</summary>
        public const float BarAnnexWidth = 4.75f;
        /// <summary>Height of the balcony deck.</summary>
        public const float BalconyHeight = 3.4f;

        public Transform PartyLightsRoot { get; private set; }
        public AudioSource DoorMusic { get; private set; }

        /// <summary>Where the host stands behind the decks (world).</summary>
        public Vector3 DecksPosition => transform.TransformPoint(new Vector3(-1.5f, 0.3f, -19.8f));
        /// <summary>Yaw the host faces from the decks (towards the dance floor).</summary>
        public float DecksYaw => transform.eulerAngles.y;
        /// <summary>Where a player stands in front of the decks (world).</summary>
        public Vector3 DecksFrontPosition => transform.TransformPoint(new Vector3(-1.5f, 0f, -16.6f));
        public Vector3 DanceFloorPosition => transform.TransformPoint(new Vector3(-1.5f, 0f, -10.5f));
        /// <summary>Centre of the doorway on the ground (world).</summary>
        public Vector3 DoorPosition => transform.position;
        /// <summary>A point on the ground just outside the door (world).</summary>
        public Vector3 OutsideDoorPosition => transform.TransformPoint(new Vector3(0, 0, 3f));

        /// <summary>Doorway of a route (world, ground), the spot where the barrier stands.</summary>
        public Vector3 RouteDoorPosition(Route r) => transform.TransformPoint(r == Route.Bar ? new Vector3(-6.25f, 0, -13.5f) : new Vector3(4.8f, 0, -1.8f));
        /// <summary>A walkable point inside the route area (world).</summary>
        public Vector3 RouteInsidePosition(Route r) => transform.TransformPoint(r == Route.Bar ? new Vector3(-8.1f, 0, -13.5f) : new Vector3(4.8f, BalconyHeight, -14f));
        /// <summary>A hall floor point in front of a route doorway (world, ground), the place to walk from.</summary>
        public Vector3 RouteApproachPosition(Route r) => transform.TransformPoint(r == Route.Bar ? new Vector3(-3.8f, 0, -13.5f) : new Vector3(2.6f, 0, -1.2f));
        /// <summary>Balcony ramp: the foot just in front of the barrier and the head on the deck (world).</summary>
        public Vector3 RampFootPosition => transform.TransformPoint(new Vector3(4.8f, 0, -0.95f));
        public Vector3 RampHeadPosition => transform.TransformPoint(new Vector3(4.8f, BalconyHeight, -9.2f));

        /// <summary>Building footprint corners on the ground (world), including the bar annex, for layout checks.</summary>
        public Vector3[] FootprintCorners()
        {
            float x0 = -HallHalfWidth - BarAnnexWidth, x1 = HallHalfWidth, z0 = -HallDepth - 0.6f, z1 = 0.6f;
            return new[] { transform.TransformPoint(new Vector3(x0, 0, z0)), transform.TransformPoint(new Vector3(x1, 0, z0)), transform.TransformPoint(new Vector3(x1, 0, z1)), transform.TransformPoint(new Vector3(x0, 0, z1)) };
        }

        public GameObject Barrier(Route r) => _routes[(int)r].Barrier;
        public IReadOnlyList<Light> RouteLights(Route r) => _routes[(int)r].Lights;
        public bool IsRouteOpen(Route r) => _routes[(int)r].Open;
        public bool IsDoorMusicOn => DoorMusic != null && DoorMusic.isPlaying;

        private sealed class RouteParts
        {
            public GameObject Barrier;
            public readonly List<Light> Lights = new List<Light>();
            public readonly List<Renderer> Neon = new List<Renderer>();
            public Color OnColor;
            public bool Open;
        }

        private readonly RouteParts[] _routes = { new RouteParts(), new RouteParts() };
        private readonly List<Light> _hueLights = new List<Light>();
        private readonly List<Material> _hueMats = new List<Material>();
        private readonly List<float> _lightOffsets = new List<float>();
        private readonly List<float> _matOffsets = new List<float>();
        private readonly List<Material> _floorMats = new List<Material>();
        private readonly List<Transform> _beams = new List<Transform>();
        private TextMesh _signText;
        private AudioLowPassFilter _lowPass;
        private Material _offNeon;

        public string Title => "Warehouse";
        public IEnumerable<string> Lines
        {
            get
            {
                yield return "Party venue. The host plays at the decks inside.";
                yield return "Bar: " + (IsRouteOpen(Route.Bar) ? "open" : "closed") + ".  Balcony: " + (IsRouteOpen(Route.Balcony) ? "open" : "closed") + ".";
                yield return "Door music: " + (IsDoorMusicOn ? "playing" : "off") + ".";
            }
        }

        /// <summary>Opens or closes a route: removes/restores the barrier and switches its lights.</summary>
        public void SetRouteOpen(Route r, bool open)
        {
            var p = _routes[(int)r];
            p.Open = open;
            if (p.Barrier != null) p.Barrier.SetActive(!open);
            foreach (var l in p.Lights) l.enabled = open;
            foreach (var n in p.Neon) n.sharedMaterial = open ? MaterialFactory.SharedUnlit(p.OnColor) : _offNeon;
        }

        public void SetDoorMusic(bool on)
        {
            if (DoorMusic == null) return;
            if (on && !DoorMusic.isPlaying) DoorMusic.Play();
            if (!on && DoorMusic.isPlaying) DoorMusic.Stop();
        }

        /// <summary>True if a world point is inside the main hall.</summary>
        public bool IsInsideHall(Vector3 world)
        {
            var l = transform.InverseTransformPoint(world);
            return l.x > -HallHalfWidth && l.x < HallHalfWidth && l.z < 0 && l.z > -HallDepth && l.y < HallHeight;
        }

        private void Update()
        {
            float t = Time.unscaledTime;
            float beat = t * 174f / 60f;
            float pulse = 0.75f + 0.25f * Mathf.Pow(1f - (beat % 1f), 3f);
            for (int i = 0; i < _hueLights.Count; i++)
            {
                var c = Color.HSVToRGB(Mathf.Repeat(t * 0.07f + _lightOffsets[i], 1f), 0.8f, 1f);
                _hueLights[i].color = c;
                _hueLights[i].intensity = (i < 2 ? 2.6f : 2.2f) * pulse;
            }
            for (int i = 0; i < _hueMats.Count; i++)
            {
                var c = Color.HSVToRGB(Mathf.Repeat(t * 0.07f + _matOffsets[i], 1f), 0.8f, 1f);
                var a = _hueMats[i].color.a;
                c.a = a;
                _hueMats[i].color = c;
            }
            if (_signText != null) _signText.color = Color.HSVToRGB(Mathf.Repeat(t * 0.07f + 0.5f, 1f), 0.65f, 1f);
            int step = Mathf.FloorToInt(beat);
            for (int i = 0; i < _floorMats.Count; i++)
            {
                float h = Mathf.Repeat(i * 0.25f + step * 0.125f + t * 0.02f, 1f);
                _floorMats[i].color = Color.HSVToRGB(h, 0.75f, ((step + i) % 2 == 0 ? 0.45f : 0.18f));
            }
            for (int i = 0; i < _beams.Count; i++)
                _beams[i].localRotation = Quaternion.Euler(Mathf.Sin(t * 0.9f + i * 1.7f) * 18f, 0, Mathf.Cos(t * 0.7f + i) * 18f);

            if (_lowPass != null)
            {
                var listener = ViewSwitcher.Instance != null && ViewSwitcher.Instance.Avatar != null ? ViewSwitcher.Instance.Avatar.EyePosition : Vector3.zero;
                float target = IsInsideHall(listener) ? 7000f : 1100f;
                // Smooth in C# and write the filter only in coarse steps, never every frame, and never while the audio
                // output is stalled or changing device (per-frame filter writes there hit Unity's fatal ADTM error; see AudioGuard).
                _cutoffSmooth = Mathf.Lerp(_cutoffSmooth, target, Time.unscaledDeltaTime * 4f);
                if (Mathf.Abs(_cutoffSmooth - target) < 20f) _cutoffSmooth = target;
                if (AudioGuard.CanWriteControls && Mathf.Abs(_cutoffSmooth - _cutoffWritten) > (_cutoffSmooth == target ? 0.5f : 400f))
                {
                    _lowPass.cutoffFrequency = _cutoffSmooth;
                    _cutoffWritten = _cutoffSmooth;
                }
            }
        }

        private float _cutoffSmooth = 1100f, _cutoffWritten = 1100f;

        // ------------------------------------------------------------------ build

        public static WarehouseSet Build(Transform parent, VillageLayout layout)
        {
            var go = new GameObject("Warehouse");
            go.transform.SetParent(parent, false);
            go.transform.position = layout.WarehouseDoor;
            go.transform.rotation = Quaternion.Euler(0, layout.WarehouseYaw, 0);
            var w = go.AddComponent<WarehouseSet>();
            w.BuildAll();
            return w;
        }

        private void BuildAll()
        {
            _offNeon = MaterialFactory.SharedUnlit(new Color(0.16f, 0.15f, 0.15f));
            var stat = new GameObject("Static").transform;
            stat.SetParent(transform, false);

            var brick = new Color(0.40f, 0.22f, 0.18f);
            var trim = new Color(0.22f, 0.20f, 0.19f);
            var concrete = new Color(0.30f, 0.30f, 0.31f);
            float hw = HallHalfWidth, d = HallDepth, h = HallHeight, th = 0.5f;

            // Hall floor and walls.
            Signs.Box(stat, "Floor", new Vector3(0, 0.01f, -d / 2), new Vector3(2 * hw, 0.02f, d), concrete, false);
            Signs.Box(stat, "FrontL", new Vector3(-(hw + 2.25f) / 2, h / 2, -th / 2), new Vector3(hw - 2.25f, h, th), brick);
            Signs.Box(stat, "FrontR", new Vector3((hw + 2.25f) / 2, h / 2, -th / 2), new Vector3(hw - 2.25f, h, th), brick);
            Signs.Box(stat, "FrontLintel", new Vector3(0, (h + 4.2f) / 2, -th / 2), new Vector3(4.5f, h - 4.2f, th), brick);
            Signs.Box(stat, "Back", new Vector3(0, h / 2, -d + th / 2), new Vector3(2 * hw, h, th), brick);
            Signs.Box(stat, "Right", new Vector3(hw - th / 2, h / 2, -d / 2), new Vector3(th, h, d), brick);
            Signs.Box(stat, "LeftBack", new Vector3(-hw + th / 2, h / 2, -18.5f), new Vector3(th, h, 7f), brick);
            Signs.Box(stat, "LeftFront", new Vector3(-hw + th / 2, h / 2, -6f), new Vector3(th, h, 12f), brick);
            Signs.Box(stat, "LeftLintel", new Vector3(-hw + th / 2, (h + 3f) / 2, -13.5f), new Vector3(th, h - 3f, 3f), brick);
            // Plinth and cornice trims for some shape from outside.
            foreach (var (y, hh, o) in new[] { (0.4f, 0.8f, 0.15f), (h - 0.2f, 0.4f, 0.25f) })
            {
                if (y > 1f) Signs.Box(stat, "TrimFront", new Vector3(0, y, o / 2), new Vector3(2 * hw + 2 * o, hh, o), trim, false);
                else
                {
                    Signs.Box(stat, "TrimFrontL", new Vector3(-(hw + 2.25f) / 2, y, o / 2), new Vector3(hw - 2.25f + o, hh, o), trim, false);
                    Signs.Box(stat, "TrimFrontR", new Vector3((hw + 2.25f) / 2, y, o / 2), new Vector3(hw - 2.25f + o, hh, o), trim, false);
                }
                Signs.Box(stat, "TrimBack", new Vector3(0, y, -d - o / 2), new Vector3(2 * hw + 2 * o, hh, o), trim, false);
                Signs.Box(stat, "TrimRight", new Vector3(hw + o / 2, y, -d / 2), new Vector3(o, hh, d), trim, false);
                if (y > 1f) Signs.Box(stat, "TrimLeft", new Vector3(-hw - o / 2, y, -d / 2), new Vector3(o, hh, d), trim, false); // no plinth on the bar side
            }

            // Gabled roof along the hall depth.
            var roof = new MeshBuilder();
            var rc = new Color(0.30f, 0.32f, 0.36f);
            float rh = 2.5f, ov = 0.6f;
            Vector3 a0 = new Vector3(-hw - ov, h, ov), a1 = new Vector3(-hw - ov, h, -d - ov);
            Vector3 b0 = new Vector3(hw + ov, h, ov), b1 = new Vector3(hw + ov, h, -d - ov);
            Vector3 r0 = new Vector3(0, h + rh, ov), r1 = new Vector3(0, h + rh, -d - ov);
            roof.Quad(a0, r0, r1, a1, rc);
            roof.Quad(b1, r1, r0, b0, rc * 0.92f);
            roof.Tri(a0 + new Vector3(ov, 0, -ov), b0 + new Vector3(-ov, 0, -ov), r0 + new Vector3(0, 0, -ov), brick);
            roof.Tri(b1 + new Vector3(-ov, 0, ov), a1 + new Vector3(ov, 0, ov), r1 + new Vector3(0, 0, ov), brick);
            roof.Quad(a1, r1, r0, a0, rc * 0.5f); // underside
            roof.Quad(b0, r0, r1, b1, rc * 0.5f);
            roof.ToObject("Roof", stat, MaterialFactory.CreateVertexColored(Color.white, "WarehouseRoof"));
            Signs.Box(stat, "Ceiling", new Vector3(0, h + 0.05f, -d / 2), new Vector3(2 * hw, 0.1f, d), new Color(0.12f, 0.11f, 0.12f), false);

            BuildDoor(stat);
            BuildDanceFloorAndDecks(stat);
            BuildBar(stat);
            BuildBalcony(stat);

            var musicGo = new GameObject("DoorMusic");
            musicGo.transform.SetParent(transform, false);
            musicGo.transform.localPosition = new Vector3(0, 2.5f, -1.5f);
            DoorMusic = musicGo.AddComponent<AudioSource>();
            DoorMusic.clip = SynthClips.DrumLoop174();
            DoorMusic.loop = true;
            DoorMusic.spatialBlend = 1f;
            DoorMusic.rolloffMode = AudioRolloffMode.Logarithmic;
            DoorMusic.minDistance = 5f;
            DoorMusic.maxDistance = 90f;
            DoorMusic.volume = 0.8f;
            DoorMusic.dopplerLevel = 0f;
            _lowPass = musicGo.AddComponent<AudioLowPassFilter>();
            _lowPass.cutoffFrequency = 1100f;
            SetDoorMusic(true);

            SetRouteOpen(Route.Bar, false);
            SetRouteOpen(Route.Balcony, false);
            StaticBatchingUtility.Combine(stat.gameObject);
        }

        private Material HueMat(float offset, float alpha, bool glow)
        {
            var c = Color.HSVToRGB(offset, 0.8f, 1f); c.a = alpha;
            var m = glow ? MaterialFactory.CreateGlow(c) : MaterialFactory.CreateUnlit(c);
            _hueMats.Add(m);
            _matOffsets.Add(offset);
            return m;
        }

        private Light PointLight(string name, Vector3 local, float range, float intensity, Color c)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.transform.localPosition = local;
            var l = go.AddComponent<Light>();
            l.type = LightType.Point; l.range = range; l.intensity = intensity; l.color = c;
            l.shadows = LightShadows.None;
            l.renderMode = LightRenderMode.ForcePixel;
            return l;
        }

        private void BuildDoor(Transform stat)
        {
            // Neon frame around the door.
            var dyn = new GameObject("DoorNeon").transform; dyn.SetParent(transform, false);
            var m = HueMat(0.85f, 1f, false);
            Signs.Prim(PrimitiveType.Cube, dyn, "NeonL", new Vector3(-2.35f, 2.15f, 0.05f), new Vector3(0.18f, 4.3f, 0.12f), m);
            Signs.Prim(PrimitiveType.Cube, dyn, "NeonR", new Vector3(2.35f, 2.15f, 0.05f), new Vector3(0.18f, 4.3f, 0.12f), m);
            Signs.Prim(PrimitiveType.Cube, dyn, "NeonTop", new Vector3(0, 4.3f, 0.05f), new Vector3(4.88f, 0.18f, 0.12f), m);
            // Glow: haze filling the doorway and a spill on the ground outside.
            var haze = new MeshBuilder();
            haze.Quad(new Vector3(-2.25f, 0, -0.7f), new Vector3(-2.25f, 4.2f, -0.7f), new Vector3(2.25f, 4.2f, -0.7f), new Vector3(2.25f, 0, -0.7f), Color.white);
            haze.ToObject("DoorHaze", dyn, HueMat(0.85f, 0.3f, true));
            var spill = new MeshBuilder();
            spill.Quad(new Vector3(-2.2f, 0.06f, 0.1f), new Vector3(-4.5f, 0.06f, 7f), new Vector3(4.5f, 0.06f, 7f), new Vector3(2.2f, 0.06f, 0.1f), new Color(1, 1, 1, 0.35f));
            spill.Quad(new Vector3(-1.6f, 0.07f, 0.1f), new Vector3(-2.6f, 0.07f, 3.5f), new Vector3(2.6f, 0.07f, 3.5f), new Vector3(1.6f, 0.07f, 0.1f), new Color(1, 1, 1, 0.35f));
            spill.ToObject("DoorSpill", dyn, HueMat(0.85f, 0.6f, true));

            _hueLights.Add(PointLight("DoorLightInside", new Vector3(0, 3f, -3f), 12f, 2.6f, Color.magenta)); _lightOffsets.Add(0.85f);
            _hueLights.Add(PointLight("DoorLightOutside", new Vector3(0, 3.6f, 2.2f), 11f, 2.6f, Color.magenta)); _lightOffsets.Add(0.85f);

            // Sign on the facade.
            Signs.Box(stat, "SignBack", new Vector3(0, 5.5f, 0.12f), new Vector3(8.6f, 1.5f, 0.15f), new Color(0.1f, 0.09f, 0.1f), false);
            _signText = Signs.Text(transform, "WAREHOUSE", new Vector3(0, 5.5f, 0.25f), 0.95f, Color.cyan);
            // Bouncer rope posts by the door.
            foreach (float x in new[] { -3.4f, 3.4f })
            {
                Signs.Box(stat, "RopePost", new Vector3(x, 0.5f, 1.3f), new Vector3(0.18f, 1f, 0.18f), new Color(0.8f, 0.68f, 0.3f), false);
            }
        }

        private void BuildDanceFloorAndDecks(Transform stat)
        {
            var floorRoot = new GameObject("DanceFloor").transform; floorRoot.SetParent(transform, false);
            for (int g = 0; g < 4; g++) _floorMats.Add(MaterialFactory.CreateUnlit(Color.black, "DanceTile" + g));
            for (int ix = 0; ix < 4; ix++)
                for (int iz = 0; iz < 5; iz++)
                {
                    var p = new Vector3(-4.5f + ix * 2f, 0.04f, -6.5f - iz * 2f);
                    Signs.Prim(PrimitiveType.Cube, floorRoot, "Tile", p, new Vector3(1.9f, 0.06f, 1.9f), _floorMats[(ix + iz * 2) % 4]);
                }

            var dark = new Color(0.10f, 0.10f, 0.11f);
            Signs.Box(stat, "Stage", new Vector3(-1.5f, 0.15f, -19.625f), new Vector3(8.5f, 0.3f, 4.25f), new Color(0.16f, 0.14f, 0.13f));
            Signs.Box(stat, "Decks", new Vector3(-1.5f, 0.8f, -18.2f), new Vector3(3.6f, 1.0f, 0.9f), dark);
            var plat = MaterialFactory.Shared(new Color(0.05f, 0.05f, 0.05f));
            Signs.Prim(PrimitiveType.Cylinder, stat, "TurntableL", new Vector3(-2.6f, 1.32f, -18.2f), new Vector3(0.75f, 0.02f, 0.75f), plat);
            Signs.Prim(PrimitiveType.Cylinder, stat, "TurntableR", new Vector3(-0.4f, 1.32f, -18.2f), new Vector3(0.75f, 0.02f, 0.75f), plat);
            Signs.Box(stat, "Mixer", new Vector3(-1.5f, 1.36f, -18.2f), new Vector3(0.5f, 0.1f, 0.6f), new Color(0.2f, 0.2f, 0.22f), false);
            var deckGlow = HueMat(0.55f, 1f, false);
            Signs.Prim(PrimitiveType.Cube, transform, "DecksFrontStrip", new Vector3(-1.5f, 0.45f, -17.73f), new Vector3(3.6f, 0.08f, 0.04f), deckGlow);
            foreach (float x in new[] { -5.0f, 2.0f })
            {
                Signs.Box(stat, "Speaker", new Vector3(x, 1.6f, -19.9f), new Vector3(1.4f, 2.6f, 1.1f), dark);
                var cone = MaterialFactory.Shared(new Color(0.22f, 0.22f, 0.24f));
                Signs.Prim(PrimitiveType.Cylinder, stat, "Woofer", new Vector3(x, 1.2f, -19.32f), new Vector3(0.9f, 0.03f, 0.9f), cone, new Vector3(90, 0, 0));
                Signs.Prim(PrimitiveType.Cylinder, stat, "Tweeter", new Vector3(x, 2.3f, -19.32f), new Vector3(0.45f, 0.03f, 0.45f), cone, new Vector3(90, 0, 0));
            }
            // Backdrop wall glow behind the DJ.
            var back = new MeshBuilder();
            back.Quad(new Vector3(-6f, 0.3f, -21.4f), new Vector3(-6f, 5.5f, -21.4f), new Vector3(3f, 5.5f, -21.4f), new Vector3(3f, 0.3f, -21.4f), new Color(1, 1, 1, 0.25f));
            back.ToObject("Backdrop", transform, HueMat(0.6f, 0.35f, true));

            // Truss, fixtures, beams, lights.
            PartyLightsRoot = new GameObject("PartyLights").transform;
            PartyLightsRoot.SetParent(transform, false);
            var metal = new Color(0.25f, 0.25f, 0.27f);
            Signs.Box(stat, "TrussA", new Vector3(-1.5f, 6.4f, -8.5f), new Vector3(10f, 0.25f, 0.25f), metal, false);
            Signs.Box(stat, "TrussB", new Vector3(-1.5f, 6.4f, -14.5f), new Vector3(10f, 0.25f, 0.25f), metal, false);
            var spots = new[] { new Vector3(-5f, 6.1f, -8.5f), new Vector3(2f, 6.1f, -8.5f), new Vector3(-5f, 6.1f, -14.5f), new Vector3(2f, 6.1f, -14.5f) };
            for (int i = 0; i < spots.Length; i++)
            {
                float off = i * 0.25f;
                var fix = new GameObject("Fixture" + i).transform;
                fix.SetParent(PartyLightsRoot, false);
                fix.localPosition = spots[i];
                Signs.Prim(PrimitiveType.Sphere, fix, "Lamp", Vector3.zero, Vector3.one * 0.45f, HueMat(off, 1f, false));
                var beamPivot = new GameObject("Beam").transform; beamPivot.SetParent(fix, false);
                var beam = new MeshBuilder();
                beam.Cylinder(new Vector3(0, -6f, 0), 1.9f, 0.22f, 6f, 12, new Color(1, 1, 1, 0.12f), false);
                beam.ToObject("BeamMesh", beamPivot, HueMat(off, 0.13f, true));
                _beams.Add(beamPivot);
                var l = PointLight("PartyLight" + i, spots[i] - new Vector3(0, 1.2f, 0), 10f, 2.2f, Color.white);
                l.transform.SetParent(PartyLightsRoot, true);
                _hueLights.Add(l); _lightOffsets.Add(off);
            }
        }

        private void BuildBar(Transform stat)
        {
            // Annex on the hall's -X side: x from -6.5 (hall wall) to -11.25, z from -8.25 to -18.75.
            var brick = new Color(0.36f, 0.20f, 0.17f);
            float h = 4f, cx = -8.75f, cz = -13.5f;
            Signs.Box(stat, "BarFloor", new Vector3(cx, 0.01f, cz), new Vector3(4.5f, 0.02f, 10f), new Color(0.28f, 0.20f, 0.14f), false);
            Signs.Box(stat, "BarWallOuter", new Vector3(-11.0f, h / 2, cz), new Vector3(0.5f, h, 10.5f), brick);
            Signs.Box(stat, "BarWallBack", new Vector3(cx, h / 2, -18.5f), new Vector3(4.5f, h, 0.5f), brick);
            Signs.Box(stat, "BarWallFront", new Vector3(cx, h / 2, -8.5f), new Vector3(4.5f, h, 0.5f), brick);
            Signs.Box(stat, "BarRoof", new Vector3(cx - 0.05f, h + 0.15f, cz), new Vector3(5.1f, 0.3f, 11.1f), new Color(0.30f, 0.32f, 0.36f));
            var wood = new Color(0.42f, 0.27f, 0.16f);
            Signs.Box(stat, "BarCounter", new Vector3(-9.95f, 0.55f, cz), new Vector3(0.8f, 1.1f, 7f), wood);
            Signs.Box(stat, "BarShelf", new Vector3(-10.55f, 2.0f, cz), new Vector3(0.35f, 0.08f, 7f), wood, false);
            foreach (float z in new[] { -16.2f, -11f }) Signs.Box(stat, "Stool", new Vector3(-9.1f, 0.35f, z), new Vector3(0.45f, 0.7f, 0.45f), new Color(0.2f, 0.15f, 0.12f));

            var parts = _routes[(int)Route.Bar];
            parts.OnColor = new Color(1f, 0.45f, 0.65f);
            var neonRoot = new GameObject("BarNeon").transform; neonRoot.SetParent(transform, false);
            for (int i = 0; i < 10; i++)
            {
                var r = Signs.Prim(PrimitiveType.Cube, neonRoot, "Bottle", new Vector3(-10.55f, 2.3f, -16.7f + i * 0.7f), new Vector3(0.16f, 0.45f, 0.16f), _offNeon).GetComponent<Renderer>();
                parts.Neon.Add(r);
            }
            parts.Neon.Add(Signs.Prim(PrimitiveType.Cube, neonRoot, "BarNeonTube", new Vector3(-10.7f, 3.2f, cz), new Vector3(0.1f, 0.12f, 5f), _offNeon).GetComponent<Renderer>());
            var bl = PointLight("BarLight", new Vector3(cx, 3.2f, cz), 8f, 2.4f, new Color(1f, 0.62f, 0.35f));
            parts.Lights.Add(bl);
            var bl2 = PointLight("BarDoorLight", new Vector3(-7.3f, 2.4f, cz), 5f, 1.6f, parts.OnColor);
            parts.Lights.Add(bl2);

            parts.Barrier = BuildBarrier("BarBarrier", new Vector3(-6.25f, 0, cz), 90f, 3f, 3f);
            Signs.Board(parts.Barrier.transform, "BAR", new Vector3(0, 2.75f, 0.25f), new Vector2(1.4f, 0.45f), new Color(0.12f, 0.1f, 0.1f), new Color(1f, 0.6f, 0.75f));
        }

        private void BuildBalcony(Transform stat)
        {
            // Deck along the hall's +X wall (x 3.0..6.5, z -8.3..-19.5), reached by a ramp from beside the door.
            float top = BalconyHeight;
            var wood = new Color(0.33f, 0.24f, 0.17f);
            var metal = new Color(0.25f, 0.25f, 0.27f);
            Signs.Box(stat, "BalconyDeck", new Vector3(4.75f, top - 0.15f, -13.9f), new Vector3(3.5f, 0.3f, 11.2f), wood);
            foreach (float z in new[] { -19.2f, -13.9f, -8.6f }) Signs.Box(stat, "BalconyPost", new Vector3(3.15f, (top - 0.3f) / 2, z), new Vector3(0.3f, top - 0.3f, 0.3f), metal);
            Signs.Box(stat, "RailSide", new Vector3(3.05f, top + 0.55f, -13.9f), new Vector3(0.1f, 1.1f, 11.2f), metal);
            Signs.Box(stat, "RailBack", new Vector3(4.75f, top + 0.55f, -19.45f), new Vector3(3.5f, 1.1f, 0.1f), metal);
            Signs.Box(stat, "RailFront", new Vector3(3.3f, top + 0.55f, -8.35f), new Vector3(0.6f, 1.1f, 0.1f), metal);

            // Ramp from the floor beside the door up to the deck.
            Vector3 foot = new Vector3(4.8f, 0f, -2.0f), head = new Vector3(4.8f, top, -8.3f);
            var along = head - foot;
            var ramp = Signs.Box(stat, "Ramp", (foot + head) / 2, new Vector3(2.4f, 0.3f, along.magnitude + 0.6f), wood);
            ramp.transform.localRotation = Quaternion.LookRotation(along.normalized, Vector3.up);
            ramp.transform.localPosition -= ramp.transform.localRotation * Vector3.up * 0.15f;
            Signs.Box(stat, "RampWall", new Vector3(3.45f, 2.2f, -5.15f), new Vector3(0.3f, 4.4f, 6.3f), new Color(0.40f, 0.22f, 0.18f));
            // Doorway frame at the ramp foot.
            Signs.Box(stat, "RampFrameL", new Vector3(3.45f, 1.6f, -1.8f), new Vector3(0.3f, 3.2f, 0.3f), metal);
            Signs.Box(stat, "RampFrameTop", new Vector3(4.75f, 3.3f, -1.8f), new Vector3(2.9f, 0.25f, 0.3f), metal, false);

            var parts = _routes[(int)Route.Balcony];
            parts.OnColor = new Color(0.45f, 0.95f, 1f);
            var neonRoot = new GameObject("BalconyNeon").transform; neonRoot.SetParent(transform, false);
            for (int i = 0; i < 12; i++)
                parts.Neon.Add(Signs.Prim(PrimitiveType.Sphere, neonRoot, "Bulb", new Vector3(3.05f, top + 1.2f, -19.2f + i * 0.95f), Vector3.one * 0.2f, _offNeon).GetComponent<Renderer>());
            parts.Neon.Add(Signs.Prim(PrimitiveType.Cube, neonRoot, "RampTube", new Vector3(4.75f, 3.15f, -1.6f), new Vector3(2.6f, 0.1f, 0.1f), _offNeon).GetComponent<Renderer>());
            parts.Lights.Add(PointLight("BalconyLight", new Vector3(4.8f, 5.8f, -14f), 8f, 2.4f, parts.OnColor));
            parts.Lights.Add(PointLight("BalconyRampLight", new Vector3(4.8f, 2.5f, -3.5f), 5f, 1.6f, parts.OnColor));

            parts.Barrier = BuildBarrier("BalconyBarrier", new Vector3(4.8f, 0, -1.8f), 0f, 2.7f, 3f);
            Signs.Board(parts.Barrier.transform, "BALCONY", new Vector3(0, 2.75f, 0.25f), new Vector2(1.9f, 0.45f), new Color(0.12f, 0.1f, 0.1f), new Color(0.6f, 0.95f, 1f));
        }

        /// <summary>Hazard-striped gate blocking a doorway; its root collider blocks walking.</summary>
        private GameObject BuildBarrier(string name, Vector3 local, float yaw, float width, float height)
        {
            var root = new GameObject(name);
            root.transform.SetParent(transform, false);
            root.transform.localPosition = local;
            root.transform.localRotation = Quaternion.Euler(0, yaw, 0);
            var col = root.AddComponent<BoxCollider>();
            col.center = new Vector3(0, height / 2, 0);
            col.size = new Vector3(width, height, 0.4f);
            int n = 7;
            for (int i = 0; i < n; i++)
            {
                float x = -width / 2 + (i + 0.5f) * width / n;
                Signs.Box(root.transform, "Stripe", new Vector3(x, 1.2f, 0), new Vector3(width / n, 2.4f, 0.12f), i % 2 == 0 ? new Color(0.95f, 0.78f, 0.12f) : new Color(0.08f, 0.08f, 0.08f), false);
            }
            Signs.Box(root.transform, "Rail", new Vector3(0, 2.45f, 0), new Vector3(width, 0.12f, 0.16f), new Color(0.6f, 0.1f, 0.1f), false);
            return root;
        }
    }
}
