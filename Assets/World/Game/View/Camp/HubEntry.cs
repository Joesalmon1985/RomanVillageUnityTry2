using System;
using System.Collections.Generic;
using UnityEngine;

namespace World.Game
{
    /// <summary>
    /// WU-1b: a hub entry as a two-tap world action (WORLD_UI_PLAN 2.1, P-3). A small pick prop beside the person who offers the
    /// entry: the first tap previews with the shared <see cref="LedgerCard"/> line and a warm ring on the ground, the second tap
    /// commits the same call the person's conversation makes (offering, hear the dispute, practice duel, lead to the spires).
    /// The conversation stays as the long-form option: tapping the person still talks. Marked <see cref="IWorldPrimary"/> so the prop
    /// wins the tap over a person standing on it. Never writes state itself; the callbacks do what the conversations did.
    /// </summary>
    public sealed class HubEntryAction : MonoBehaviour, IWorldPrimary, IInteractable, IWorldAction, IWorldActionAnchor
    {
        public string TitleText = "Entry", QuestionText = "Go in?", PromptText = "Go in";
        public Func<IEnumerable<string>> LinesFn;
        public Func<WorldActionCheck> CheckFn;
        public Func<bool> CommitFn;
        public float Reach = 8f;
        /// <summary>How many times it committed (tests).</summary>
        public int Commits { get; private set; }
        public bool Highlighted => _glow != null && _glow.activeSelf;

        private GameObject _glow; private Material _glowMat; private float _radius = 1.1f;

        public string Prompt => PromptText;
        public string Title => TitleText;
        public IEnumerable<string> Lines => LinesFn != null ? LinesFn() : new string[0];
        public string PreviewLine => QuestionText;
        public Transform Anchor => transform;
        public float KeyReach => Reach;

        public void Interact() => WorldActions.Tap(this);
        public WorldActionCheck Check() => CheckFn != null ? CheckFn() : WorldActionCheck.Allowed;
        public bool Commit() { bool ok = CommitFn != null && CommitFn(); if (ok) Commits++; return ok; }

        public void ShowPreview(bool on)
        {
            if (on && _glow == null) BuildGlow();
            if (_glow != null) _glow.SetActive(on);
        }

        private void OnEnable() => WorldActions.Register(this);
        private void OnDisable() { WorldActions.Unregister(this); if (_glow != null) _glow.SetActive(false); }

        private void Update()
        {
            if (_glow == null || !_glow.activeSelf) return;
            _glowMat.color = new Color(1f, 0.78f, 0.35f, 0.55f + 0.3f * Mathf.Sin(Time.unscaledTime * 5f));
        }

        private void BuildGlow()
        {
            var mb = new MeshBuilder();
            const int N = 28;
            for (int i = 0; i < N; i++)
            {
                float a0 = i * Mathf.PI * 2 / N, a1 = (i + 1) * Mathf.PI * 2 / N;
                var o0 = new Vector3(Mathf.Sin(a0), 0, Mathf.Cos(a0)); var o1 = new Vector3(Mathf.Sin(a1), 0, Mathf.Cos(a1));
                mb.Quad(o0 * _radius, o0 * (_radius + 0.4f), o1 * (_radius + 0.4f), o1 * _radius, Color.white);
                mb.Quad(o0 * _radius, o1 * _radius, o1 * (_radius + 0.4f), o0 * (_radius + 0.4f), Color.white);
            }
            _glowMat = MaterialFactory.CreateGlow(new Color(1f, 0.78f, 0.35f, 0.8f), "EntryPreview");
            _glow = mb.ToObject("EntryPreviewGlow", transform, _glowMat);
            _glow.transform.localPosition = new Vector3(0, 0.12f, 0);
            _glow.layer = 2;
        }

        /// <summary>
        /// A small low stone seat or marker at a world point with its pick box, carrying a <see cref="HubEntryAction"/>. The box is
        /// small (about <paramref name="size"/> metres) so it never swallows a ground tap round it.
        /// </summary>
        public static HubEntryAction Make(Transform parent, string name, Vector3 at, float yaw, Vector2 size, string title, string question, string prompt,
            Func<IEnumerable<string>> lines, Func<WorldActionCheck> check, Func<bool> commit, bool stone = true)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(at, Quaternion.Euler(0, yaw, 0));
            if (stone)
            {
                var mb = new MeshBuilder();
                mb.Box(new Vector3(0, 0.16f, 0), new Vector3(size.x * 0.8f, 0.32f, size.y * 0.8f), HubArt.Slab);
                mb.Box(new Vector3(0, 0.38f, 0), new Vector3(size.x * 0.5f, 0.12f, size.y * 0.5f), HubArt.Slab * 0.85f);
                var body = mb.ToObject(name + " stone", go.transform, MaterialFactory.CreateVertexColored(Color.white, name + " stone"));
                body.layer = 2;
            }
            LandLook.PickBox(go, new Vector3(0, 0.45f, 0), new Vector3(size.x, 0.9f, size.y));
            var a = go.AddComponent<HubEntryAction>();
            a.TitleText = title; a.QuestionText = question; a.PromptText = prompt;
            a.LinesFn = lines; a.CheckFn = check; a.CommitFn = commit;
            a._radius = Mathf.Max(size.x, size.y) * 0.7f;
            return a;
        }
    }
}
