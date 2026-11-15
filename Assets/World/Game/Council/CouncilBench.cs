using World.Sim;
using UnityEngine;

namespace World.Game
{
    /// <summary>
    /// The listening bench (the left view): the aide, one speaker waist-up with a short bubble, the next speaker
    /// farther back, the candidate's lantern, firelight beyond, and the two stone slabs (two matching leaf seals =
    /// Same; a leaf and a gate = Different). Built beside the stage and placed from the brief's left-view bands with the
    /// same safe-area projection as the forward view (camera level, no pitch).
    /// </summary>
    public sealed class CouncilBench : MonoBehaviour
    {
        public const float CamY = 1.19f;
        public static float TanV => CouncilLayout.TanV;
        public static float TanH => CouncilLayout.TanH;

        public CouncilScene Scene { get; private set; }
        public Vector3 Origin { get; private set; }
        public Transform Root { get; private set; }
        public Vector3 CamPosLocal => Origin + new Vector3(0, CamY, 0);

        PlaceholderActor _speaker, _next, _aide;
        CouncilSpeaker _speakerSpeech, _aideSpeech;
        GameObject _slabSame, _slabDiff, _lantern;
        GameObject[] _seals = new GameObject[4];
        float _slabPulseSame, _slabPulseDiff; Vector3 _sealScale;
        float _stepT = -1f;
        int _count;
        public string SpeakerName { get; private set; }
        public int SpeakersShown => _count;
        public PlaceholderActor Speaker => _speaker;
        public PlaceholderActor AideActor => _aide;
        public CouncilSpeaker SpeakerTalk => _speakerSpeech;
        public CouncilSpeaker AideTalk => _aideSpeech;
        public GameObject SlabSame => _slabSame;
        public GameObject SlabDifferent => _slabDiff;
        public GameObject LanternObject => _lantern;

        /// <summary>Point at safe-area (u, v) on the plane at the given distance in front of the bench camera (local to the stage).</summary>
        public Vector3 At(float u, float v, float depth) =>
            Origin + new Vector3((u - 0.5f) * 2f * TanH * depth, CamY + (0.5f - v) * 2f * TanV * depth, depth);

        public static CouncilBench Build(CouncilScene scene)
        {
            var go = new GameObject("CouncilBench");
            go.transform.SetParent(scene.Root, false);
            var b = go.AddComponent<CouncilBench>();
            b.Scene = scene; b.Root = go.transform;
            b.Origin = PickOrigin(scene);
            b.BuildSet();
            return b;
        }

        /// <summary>The bench set stands on its own open ground beside the stage: off stock patches, hills, plots, paths and camp props.</summary>
        static Vector3 PickOrigin(CouncilScene scene)
        {
            var st = scene.State; var board = st.Board; var topo = board.Topology;
            var layout = new CampLayout(board);
            Vector3 best = new Vector3(-30f, 0, 4f); float bestScore = float.MinValue;
            for (float x = -22f; x >= -48f; x -= 2f)
                for (float z = -14f; z <= 34f; z += 2f)
                {
                    var local = new Vector3(x, 0, z);
                    var w = scene.Root.TransformPoint(local);
                    float clear = 99f;
                    for (int h = 0; h < topo.HexCount; h++)
                    {
                        var hc = BoardSpace.Hex(topo, h); hc.y = 0; w.y = 0;
                        clear = Mathf.Min(clear, (w - hc).magnitude - (float)PlotLayout.StockPatchRadius);
                    }
                    if (clear < 4f) continue;
                    if (CouncilSite.Blocked(board, layout, w, 5f) != null) continue;
                    int hx = BoardSpace.HexAt(w); if (hx < 0) continue;
                    var tr = board.HexTerrain[hx];
                    if (tr == World.Sim.Terrain.Hills || tr == World.Sim.Terrain.Mountain) continue;
                    float score = clear * 2f + x * 0.3f; // clear of stock first, then close to the stage
                    if (score > bestScore) { bestScore = score; best = local; }
                }
            return best;
        }

        void BuildSet()
        {
            var floor = CouncilScene.Cube(Root, "Bench floor", Origin + new Vector3(0, 0.0f, 12f), new Vector3(60f, 0.06f, 40f), new Color(0.34f, 0.26f, 0.17f));
            // the slabs (foreground, 60-92% height): left Same, right Different
            float d = 1.8f; float W = 2f * TanH * d, H = 2f * TanV * d;
            _slabSame = Slab("Slab same", 0.255f, d, W, H);
            _slabDiff = Slab("Slab different", 0.745f, d, W, H);
            float sealH = 0.19f * H;
            _seals[0] = CouncilShapes.Make("Leaf A", Root, CouncilShapes.LeafSeal(true), At(0.145f, 0.805f, d - 0.31f), sealH);
            _seals[1] = CouncilShapes.Make("Leaf B", Root, CouncilShapes.LeafSeal(true), At(0.365f, 0.805f, d - 0.31f), sealH);
            _seals[2] = CouncilShapes.Make("Leaf C", Root, CouncilShapes.LeafSeal(true), At(0.635f, 0.805f, d - 0.31f), sealH);
            _seals[3] = CouncilShapes.Make("Gate", Root, CouncilShapes.GateSeal(true), At(0.855f, 0.805f, d - 0.31f), sealH);
            foreach (var s in _seals) s.transform.position = s.transform.position; // keep as placed
            // fire and the gathering beyond (0-14%): braziers and dark shapes
            for (int k = 0; k < 3; k++)
            {
                var p = At(0.15f + 0.35f * k, 0.12f, 13f);
                p.y = 0;
                var t = WorldLights.Torch(Root, Scene.Root.TransformPoint(p), false, true, "Bench brazier " + k);
                t.transform.parent.localScale = Vector3.one * 1.3f;
            }
            var body = new[] { new Color(0.30f, 0.26f, 0.20f), new Color(0.26f, 0.28f, 0.30f), new Color(0.34f, 0.28f, 0.22f) };
            for (int i = 0; i < 9; i++)
            {
                float x = -9f + i * 2.3f + Mathf.Sin(i * 2.1f) * 0.6f;
                var g = CouncilShapes.Make("Gathering " + i, Root, CouncilShapes.Stander(body[i % 3], new Color(0.8f, 0.62f, 0.48f)), Origin + new Vector3(x, 0, 14f + Mathf.Sin(i * 1.3f) * 1.5f), 1.7f);
            }
            // the candidate's lantern held towards the council (right side)
            _lantern = CouncilShapes.Make("Candidate lantern", Root, CouncilShapes.LanternMesh(), At(0.94f, 0.50f, 3.6f), 0.55f);
            _lantern.GetComponent<Renderer>().sharedMaterial = MaterialFactory.CreateUnlit(new Color(1f, 0.9f, 0.6f), "Bench lantern glow");
            var lf = WorldLights.Add(Root, At(0.92f, 0.34f, 3.4f), new Color(1f, 0.74f, 0.42f), 1.3f, 8f, 1, "Candidate lantern light");
            lf.GroundGlow = 1.5f; lf.Halo = 0.7f;
            var key = WorldLights.Add(Root, Origin + new Vector3(0, 2.4f, 1.5f), new Color(1f, 0.78f, 0.5f), 1.1f, 9f, 2, "Bench key light");
            key.GroundGlow = 0f; key.Halo = 0f; key.Sparks = false;
            // the aide (left) and the farther speaker (right) are built per line
            SpawnAide();
        }

        GameObject Slab(string name, float uc, float d, float W, float H)
        {
            var c = At(uc, 0.76f, d);
            var go = CouncilScene.Cube(Root, name, c, new Vector3(0.48f * W, 0.31f * H, 0.5f), new Color(0.50f, 0.50f, 0.52f));
            CouncilScene.Cube(go.transform, "Edge", new Vector3(0, 0.5f, 0), new Vector3(1.02f, 0.06f, 1.02f), new Color(0.60f, 0.60f, 0.62f));
            return go;
        }

        PlaceholderActor Person(string id, Color tunic, float u, float depth, float aScale, float yaw)
        {
            var a = PlaceholderActor.Build(id, tunic, Root);
            var col = a.GetComponent<Collider>(); if (col != null) Destroy(col);
            var p = At(u, 0.5f, depth); p.y = 0f;
            a.transform.localPosition = p;
            a.transform.localScale = Vector3.one * aScale;
            a.SetYaw(Scene.Root.eulerAngles.y + yaw);
            return a;
        }

        void SpawnAide()
        {
            _aide = Person("aide", new Color(0.35f, 0.58f, 0.48f), 0.125f, 2.6f, 0.94f, 180f - 28f);
            _aide.name = "Bench aide";
            _aideSpeech = _aide.gameObject.AddComponent<CouncilSpeaker>(); _aideSpeech.Id = "aide"; _aideSpeech.Name = "Aide"; _aideSpeech.Head = _aide.HeadPivot;
        }

        public void ShowSpeaker(string speaker, int ordinal)
        {
            SpeakerName = speaker;
            if (_speaker != null) Destroy(_speaker.gameObject);
            var tun = Color.HSVToRGB((Mathf.Abs(speaker.GetHashCode()) % 100) / 100f, 0.45f, 0.65f);
            if (_next != null)
            {
                _speaker = _next; _next = null; // the farther speaker steps up
                _speaker.name = "Speaker " + speaker;
                _stepT = 0f;
            }
            else _speaker = Person("sp" + speaker + ordinal, tun, 0.5f, 3f, 1.08f, 180f);
            if (_speakerSpeech != null) Destroy(_speakerSpeech);
            _speakerSpeech = _speaker.gameObject.GetComponent<CouncilSpeaker>() ?? _speaker.gameObject.AddComponent<CouncilSpeaker>();
            _speakerSpeech.Id = "speaker"; _speakerSpeech.Name = speaker; _speakerSpeech.Head = _speaker.HeadPivot;
            _count++;
        }

        public void ShowNext(string speaker, int ordinal)
        {
            if (_next != null) Destroy(_next.gameObject);
            var tun = Color.HSVToRGB((Mathf.Abs((speaker ?? "x").GetHashCode()) % 100) / 100f, 0.45f, 0.65f);
            _next = Person("nx" + speaker + ordinal, tun, 0.875f, 6.5f, 1.0f, 180f + 10f);
            _next.name = "Next speaker";
        }

        public void HideSpeakers()
        {
            if (_speaker != null) { Destroy(_speaker.gameObject); _speaker = null; }
            if (_next != null) { Destroy(_next.gameObject); _next = null; }
        }

        public void PulseSlab(bool same) { if (same) _slabPulseSame = 1f; else _slabPulseDiff = 1f; }

        public void AideReact(bool good) { _aide.PlayOneShot(good ? ActorKeys.Cheer : ActorKeys.Surprise); }

        public void SpeakerGesture(int tag)
        {
            // each intention has its own gesture so the line can be read without the words: complain (surprise shrug),
            // encourage (cheer), warn (beckon), reassure (greet), question (stretch)
            if (_speaker == null) return;
            string[] keys = { ActorKeys.Surprise, ActorKeys.Cheer, ActorKeys.Beckon, ActorKeys.Greet, ActorKeys.Stretch };
            _speaker.PlayOneShot(keys[Mathf.Clamp(tag, 0, 4)]);
        }

        void Update()
        {
            _slabPulseSame = Mathf.MoveTowards(_slabPulseSame, 0f, Time.unscaledDeltaTime * 2.5f);
            _slabPulseDiff = Mathf.MoveTowards(_slabPulseDiff, 0f, Time.unscaledDeltaTime * 2.5f);
            if (_sealScale == Vector3.zero) _sealScale = _seals[0].transform.localScale;
            for (int k = 0; k < 4; k++) _seals[k].transform.localScale = _sealScale * (1f + 0.35f * (k < 2 ? _slabPulseSame : _slabPulseDiff));
            bool ls = true, ld = true; // seals stay bright; a tap makes the pair swell
            _seals[0].GetComponent<MeshFilter>().sharedMesh = CouncilShapes.LeafSeal(ls); _seals[1].GetComponent<MeshFilter>().sharedMesh = CouncilShapes.LeafSeal(ls);
            _seals[2].GetComponent<MeshFilter>().sharedMesh = CouncilShapes.LeafSeal(ld); _seals[3].GetComponent<MeshFilter>().sharedMesh = CouncilShapes.GateSeal(ld);
            if (_stepT >= 0f && _speaker != null)
            {
                _stepT += Time.unscaledDeltaTime / 0.7f;
                var to = At(0.5f, 0.5f, 3f); to.y = 0;
                _speaker.transform.localPosition = Vector3.Lerp(_speaker.transform.localPosition, to, Mathf.SmoothStep(0, 1, Mathf.Clamp01(_stepT)));
                _speaker.transform.localScale = Vector3.Lerp(_speaker.transform.localScale, Vector3.one * 1.08f, Time.unscaledDeltaTime * 6f);
                if (_stepT >= 1f) _stepT = -1f;
            }
        }

    }
}
