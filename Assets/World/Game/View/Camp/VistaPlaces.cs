using System;
using System.Collections.Generic;
using UnityEngine;

namespace World.Game
{
    /// <summary>
    /// One destination as the opening vista sees it: where it stands, how big it is on screen, its warm night light, the glow when it is
    /// picked, where the player lands and what starts there. A <see cref="IWorldAction"/> on the WU-0 machine (first tap previews with the
    /// ledger card, second tap commits = travel), but never registered with <see cref="WorldActions"/>: only the vista's regions tap it,
    /// so the walking view's generous hit targets and the E key never see it. Never writes state; <see cref="Enter"/> is the place's own entry.
    /// </summary>
    public sealed class VistaPlace : MonoBehaviour, IWorldAction, IWorldActionAnchor
    {
        public string Zone;
        public string TitleText, Question;
        public Func<string> QuestionFn;
        public string[] Info;
        public Vector3 Centre;
        public float Radius = 6f, Height = 4f;
        public Func<Vector3> Stand, Face;
        /// <summary>Runs on arrival: the place's existing entry (an offering, the steward's bench, Sol's seat, the healer, Mira, the ring).</summary>
        public Action Enter;
        /// <summary>A place that cannot be entered now still previews (the entry itself gives the honest line on arrival).</summary>
        public Func<WorldActionCheck> CheckFn;

        private Transform _anchor;
        private Light _light; private float _baseIntensity;
        private GameObject _ring; private Material _ringMat;
        private bool _preview; private float _pulseUntil;

        public string Title => TitleText;
        public IEnumerable<string> Lines => Info ?? new string[0];
        public string PreviewLine => QuestionFn != null ? QuestionFn() : Question;
        public Transform Anchor => _anchor;
        public float KeyReach => 0f;
        public bool Lit => _preview || Time.unscaledTime < _pulseUntil;

        public WorldActionCheck Check() => CheckFn != null ? CheckFn() : WorldActionCheck.Allowed;
        public bool Commit() => OpeningVista.Instance != null && OpeningVista.Instance.TravelTo(this);
        public void ShowPreview(bool on) => _preview = on;
        /// <summary>Lights the place for a moment (the welcome); 0 switches it off.</summary>
        public void Pulse(float seconds) => _pulseUntil = seconds > 0f ? Time.unscaledTime + seconds : 0f;

        public Vector3 StandPoint() => Stand != null ? Stand() : Centre;
        public Vector3 FacePoint() => Face != null ? Face() : Centre;

        /// <summary>World points whose screen box is this place's region (a ring at the ground and the top).</summary>
        public IEnumerable<Vector3> Samples()
        {
            for (int i = 0; i < 8; i++)
            {
                float a = i * Mathf.PI / 4f;
                var o = new Vector3(Mathf.Sin(a), 0, Mathf.Cos(a)) * Radius;
                yield return Centre + o;
                if (i % 2 == 0) yield return Centre + o * 0.4f + Vector3.up * Height;
            }
        }

        /// <summary>Moves and grows the place (the cave place once the hill stands); the light, ring and anchor follow.</summary>
        internal void Reshape(Vector3 centre, float radius, float height)
        {
            centre.y = 0f;
            Centre = centre; Radius = radius; Height = height;
            float li = _baseIntensity;
            foreach (Transform c in transform) Destroy(c.gameObject);
            Build(li);
        }

        internal void Build(float lightIntensity)
        {
            _anchor = new GameObject("Anchor").transform;
            _anchor.SetParent(transform, false);
            _anchor.position = Centre + Vector3.up * Height;
            var lg = new GameObject("Vista light");
            lg.transform.SetParent(transform, false);
            lg.transform.position = Centre + Vector3.up * Mathf.Max(4f, Height * 0.8f);
            _light = lg.AddComponent<Light>();
            _light.type = LightType.Point; _light.color = new Color(1f, 0.76f, 0.45f);
            _light.range = Radius * 2.6f + 8f; _light.intensity = _baseIntensity = lightIntensity;
            _light.shadows = LightShadows.None;

            var mb = new MeshBuilder();
            const int N = 48;
            float r0 = Radius + 0.6f, r1 = Radius + 1.6f;
            for (int i = 0; i < N; i++)
            {
                float a0 = i * Mathf.PI * 2 / N, a1 = (i + 1) * Mathf.PI * 2 / N;
                var o0 = new Vector3(Mathf.Sin(a0), 0, Mathf.Cos(a0)); var o1 = new Vector3(Mathf.Sin(a1), 0, Mathf.Cos(a1));
                mb.Quad(o0 * r0, o0 * r1, o1 * r1, o1 * r0, Color.white);
                mb.Quad(o0 * r0, o1 * r0, o1 * r1, o0 * r1, Color.white);
            }
            _ringMat = MaterialFactory.CreateGlow(new Color(1f, 0.8f, 0.4f, 0.8f), "VistaPick");
            _ring = mb.ToObject("Vista pick glow", transform, _ringMat);
            _ring.transform.position = new Vector3(Centre.x, Centre.y + 0.15f, Centre.z);
            _ring.layer = 2;
            _ring.SetActive(false);
        }

        private void Update()
        {
            bool lit = Lit;
            if (_ring != null && _ring.activeSelf != lit) _ring.SetActive(lit);
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 4f);
            if (lit && _ringMat != null) _ringMat.color = new Color(1f, 0.8f, 0.4f, 0.55f + 0.35f * pulse);
            if (_light != null) _light.intensity = lit ? _baseIntensity * (2.6f + 0.8f * pulse) : _baseIntensity;
        }
    }

    /// <summary>Builds the six <see cref="VistaPlace"/>s from the hub that <see cref="HubInstaller"/> made, and the vista's uneven ground.</summary>
    public static class VistaPlaces
    {
        public static void Build(OpeningVista v, Transform root, CampLayout layout, List<VistaPlace> into)
        {
            var hubCentre = Vector3.zero; int n = 0;
            foreach (var p in layout.Places) { hubCentre += p.Position; n++; }
            hubCentre += layout.DanceCircleCentre + layout.MiraSpot; n += 2;
            if (CouncilDirector.Instance != null) { hubCentre += CouncilDirector.Instance.Site.Position; n++; }
            hubCentre /= n; hubCentre.y = 0;


            Vector3 Toward(Vector3 from, float metres) { var d = hubCentre - from; d.y = 0; return d.sqrMagnitude < 1e-3f ? from : from + d.normalized * metres; }

            if (layout.TryPlace(WorldZones.HubTemple, out var temple))
            {
                var tf = TempleForecourt.Instance;
                into.Add(Make(root, WorldZones.HubTemple, temple.Position, Mathf.Max(7f, temple.Radius * 0.8f), 9f, 2.2f,
                    "The temple", "Play the gods' board game at the temple?", new[] { "Offer a lamb at the altar and the whole island becomes the gods' board." },
                    () => tf != null ? tf.AltarApproach : Toward(temple.Position, temple.Radius + 2f),
                    () => GodBoardDirector.Instance != null && GodBoardDirector.Instance.OwnTemple != null ? GodBoardDirector.Instance.OwnTemple.AltarSpot.position : temple.Position,
                    () =>
                    {
                        var offer = tf != null ? tf.Offer : null;
                        var d = GodBoardDirector.Instance;
                        if (d == null || (offer != null && !offer.Check().Ok)) { if (offer != null) WorldActions.Tap(offer); return; }
                        d.TrySacrifice();
                    }));
            }
            var dir = CouncilDirector.Instance;
            if (dir != null && CouncilSteward.Instance != null)
            {
                var steward = CouncilSteward.Instance;
                into.Add(Make(root, WorldZones.HubCouncil, dir.Site.Position, 6f, CouncilScene.TowerH * Mathf.Max(1f, dir.Site.Scale) + 1.5f, 2.4f,
                    "The lookout tower", "Join the council at the tower?", new[] { "The village settles its disputes at the tower. Help it choose." },
                    () => Toward(dir.StewardSpot, 2.5f), () => dir.StewardSpot,
                    () => Use(steward.Entry)));
            }
            {
                var sol = SolGuide.Instance;
                var centre = layout.DanceCircleCentre;
                var p = Make(root, WorldZones.HubRhythm, centre, 7.5f, 3.5f, 2.2f,
                    "The dance circle", "Drum and dance at the fire?", new[] { "Sol keeps the beat. Four drums round a fire, and dancers who follow you." },
                    () => sol != null && sol.Entry != null ? Toward(sol.Entry.transform.position, 2f) : Toward(centre, 9f), () => centre,
                    () => Use(sol != null ? sol.Entry : null));
                p.QuestionFn = () => SolGuide.CircleIsCold ? "Follow Sol to the spires and dance there?" : "Drum and dance at the fire?";
                into.Add(p);
            }
            if (layout.TryPlace(WorldZones.HubHealer, out var lodge))
            {
                into.Add(Make(root, WorldZones.HubHealer, lodge.Position, Mathf.Max(6f, lodge.Radius * 0.8f), 5.5f, 3.2f,
                    "The healer's lodge", "Visit the healer's lodge?", new[] { "The healer keeps watch over the sickness spreading across the island." },
                    () => Toward(lodge.Position, lodge.Radius + 1.5f), () => lodge.Position,
                    () => { if (HealersLodge.Instance != null) HealersLodge.Instance.Tap(); }));
            }
            {
                var mira = layout.MiraSpot;
                into.Add(Make(root, WorldZones.HubCave, mira, 6f, 3f, 2.2f,
                    "Mira's expedition", "Join Mira's expedition to the crystal caves?", new[] { "Mira's party camps by the cave mouth, ready to go down for the crystals." },
                    () => Toward(mira, 3f), () => mira,
                    () => { var m = Npc.Find(NpcIds.Mira); if (m != null) m.Interact(); }));
            }
            if (layout.TryPlace(WorldZones.HubDuel, out var ring))
            {
                var r = DuelRing.Instance;
                var tap = r != null ? r.GetComponentInChildren<RingTap>() : null;
                into.Add(Make(root, WorldZones.HubDuel, ring.Position, Mathf.Max(6f, ring.Radius * 0.8f), 3f, 3.2f,
                    "The practice ring", "Fight a practice duel with Mira?", new[] { "A Ward duel: weave against the Ward Mira keeps hidden." },
                    () => r != null ? r.Frame.W(0f, DuelRing.FenceRadius + 2.5f) : Toward(ring.Position, ring.Radius + 2f), () => ring.Position,
                    () => Use(tap)));
            }
        }

        /// <summary>
        /// After the lookout's bearing is chosen: the hill and cave mouth beyond Mira's camp, and the cave place grown to hold both;
        /// the council's lookout tower and its waiting crowd (the council builds its own tower only when it sits).
        /// </summary>
        public static void BuildScenery(Transform root, CampLayout layout, IReadOnlyList<VistaPlace> places, float yaw)
        {
            VistaPlace cavePlace = null;
            foreach (var p in places) if (p.Zone == WorldZones.HubCave) cavePlace = p;
            if (cavePlace != null)
            {
                var hill = VistaTerrain.BuildCaveHill(root, layout.MiraSpot, BoardSpace.DirOf(yaw), places);
                cavePlace.Reshape(Vector3.Lerp(layout.MiraSpot, hill.Mouth, 0.5f), 9f, 6.5f);
            }
            var dir = CouncilDirector.Instance;
            if (dir != null) VistaTerrain.BuildHubTower(root, dir);
        }

        /// <summary>Runs a place's two-tap entry at once if it can be done now, else opens its honest refusal on the ledger card.</summary>
        private static void Use(IWorldAction a)
        {
            if (a == null) return;
            var c = a.Check();
            if (c.Ok) a.Commit();
            else WorldActions.Tap(a);
        }

        private static VistaPlace Make(Transform root, string zone, Vector3 centre, float radius, float height, float light, string title, string question, string[] info,
            Func<Vector3> stand, Func<Vector3> face, Action enter)
        {
            var go = new GameObject("Vista place " + zone);
            go.transform.SetParent(root, false);
            var p = go.AddComponent<VistaPlace>();
            p.Zone = zone; p.TitleText = title; p.Question = question; p.Info = info;
            centre.y = 0f;
            p.Centre = centre; p.Radius = radius; p.Height = height;
            p.Stand = stand; p.Face = face; p.Enter = enter;
            p.Build(light);
            return p;
        }
    }
}
