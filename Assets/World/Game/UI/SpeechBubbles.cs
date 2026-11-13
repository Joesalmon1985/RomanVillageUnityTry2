using System;
using System.Collections.Generic;
using UnityEngine;

namespace World.Game
{
    /// <summary>
    /// Speech bubbles above heads (REFACTOR_SPEC 2.D "Screen", appendix B): the only always-on UI. Owner: package P.
    /// <list type="bullet">
    /// <item><see cref="Say"/> shows a line above the speaker's <see cref="ISpeaker.Head"/> for a while;
    /// <see cref="Ask"/> shows a question with up to three replies as small tappable bubbles under it (keys 1-3 answer
    /// the nearest open Ask). Each speaker has a queue: its next bubble shows when the current one closes; a Say's
    /// seconds and an Ask's timeout run from when it shows.</item>
    /// <item>Bubbles keep their size on screen (readable at distance; slightly smaller far away). A shown Ask whose
    /// speaker is off screen draws a pointer at the screen edge with the speaker's name (someone calling you).</item>
    /// <item>Replies under a far speaker's question fold away until the bubble is tapped (or the player is within
    /// <see cref="ReplyRange"/> m); keys 1-3 work either way.</item>
    /// <item>Point bubbles (<see cref="SayAt"/>) and inspect labels (<see cref="Inspect"/>) hang at a world point or
    /// object (refusals such as "Walk closer to cast.", "what is this").</item>
    /// </list>
    /// Drawn with IMGUI by <see cref="SpeechBubblesView"/>; taps reach it through <see cref="PointerInput"/>.
    /// </summary>
    public static class SpeechBubbles
    {
        public const int MaxReplies = 3;
        /// <summary>Replies show unfolded when the player is this close to the speaker.</summary>
        public const float ReplyRange = 20f;
        /// <summary>Say bubbles beyond this distance are not drawn (asks always are, with an edge pointer).</summary>
        public const float SayRange = 90f;

        internal enum Kind { Say, Ask, Inspect }

        internal sealed class Bubble
        {
            public int Handle;
            public Kind Kind;
            public object Key;                 // queue key: the speaker, or the bubble itself for point bubbles
            public ISpeaker Who;
            public Transform Anchor;           // object to hang over (inspect)
            public Vector3 Point;              // world point (point bubbles) or offset
            public bool HasPoint;
            public string Name;                // speaker name or inspect title
            public string Text;
            public List<string> Replies;
            public Action<int> OnReply;
            public float Seconds;              // Say / Inspect lifetime; Ask timeout (<= 0 none)
            public float ShownAt = -1f;
            public bool Expanded;
            // Drawn this frame (GUI coordinates).
            public Rect Body;
            public Rect[] ReplyRects = new Rect[0];
            public int DrawnFrame = -10;
            public bool OffScreen;
        }

        private static int _next = 1;
        internal static readonly List<Bubble> All = new List<Bubble>();

        /// <summary>Raised when a reply is chosen (handle, index); -1 = timed out.</summary>
        public static event Action<int, int> Replied;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { _next = 1; All.Clear(); Replied = null; }

        // ---------------- API (appendix B) ----------------

        /// <summary>Shows a line above the speaker for a while (queued behind its open bubbles); returns a handle.</summary>
        public static int Say(ISpeaker who, string text, float seconds)
        {
            var b = new Bubble { Kind = Kind.Say, Who = who, Key = KeyOf(who), Name = NameOf(who), Text = text ?? "", Seconds = Mathf.Max(0.5f, seconds) };
            if (who == null) { b.HasPoint = true; b.Point = InFrontOfPlayer(); }
            return Add(b);
        }

        /// <summary>
        /// Shows a question with up to three replies; <paramref name="onReply"/> gets the reply index (0-based), or -1 on
        /// timeout (<paramref name="timeoutSeconds"/> &lt;= 0: no timeout) or when the speaker goes away.
        /// </summary>
        public static int Ask(ISpeaker who, string text, IList<string> replies, Action<int> onReply, float timeoutSeconds)
        {
            var b = new Bubble { Kind = Kind.Ask, Who = who, Key = KeyOf(who), Name = NameOf(who), Text = text ?? "", Seconds = timeoutSeconds, OnReply = onReply };
            if (who == null) { b.HasPoint = true; b.Point = InFrontOfPlayer(); b.Expanded = true; }
            b.Replies = new List<string>();
            if (replies != null) for (int i = 0; i < replies.Count && i < MaxReplies; i++) b.Replies.Add(replies[i]);
            return Add(b);
        }

        /// <summary>Closes a bubble (shown or queued) without calling its reply callback.</summary>
        public static void Close(int handle)
        {
            for (int i = 0; i < All.Count; i++) if (All[i].Handle == handle) { All.RemoveAt(i); return; }
        }

        /// <summary>True while the bubble is shown or queued.</summary>
        public static bool IsOpen(int handle) => Find(handle) != null;

        // ---------------- additions (P) ----------------

        /// <summary>A line hanging at a world point (refusals, notices on the ground). Not queued.</summary>
        public static int SayAt(Vector3 worldPoint, string text, float seconds = 3.5f)
        {
            var b = new Bubble { Kind = Kind.Say, HasPoint = true, Point = worldPoint, Text = text ?? "", Seconds = Mathf.Max(0.5f, seconds) };
            b.Key = b;
            return Add(b);
        }

        /// <summary>
        /// A small "what is this" label over an object (or at a point when <paramref name="anchor"/> is null), with
        /// optional replies (e.g. "Follow"). Only one inspect label is open at a time; a new one replaces it.
        /// </summary>
        public static int Inspect(Transform anchor, Vector3 point, string title, IEnumerable<string> lines, float seconds = 6f, IList<string> replies = null, Action<int> onReply = null)
        {
            for (int i = All.Count - 1; i >= 0; i--) if (All[i].Kind == Kind.Inspect) All.RemoveAt(i);
            var text = new System.Text.StringBuilder();
            if (lines != null)
            {
                int n = 0;
                foreach (var l in lines)
                {
                    if (string.IsNullOrEmpty(l)) continue;
                    if (n++ >= 4) break;
                    if (text.Length > 0) text.Append('\n');
                    text.Append(l);
                }
            }
            var b = new Bubble { Kind = Kind.Inspect, Anchor = anchor, HasPoint = anchor == null, Point = point, Name = title, Text = text.ToString(), Seconds = seconds, OnReply = onReply, Expanded = true };
            b.Replies = new List<string>();
            if (replies != null) for (int i = 0; i < replies.Count && i < MaxReplies; i++) b.Replies.Add(replies[i]);
            b.Key = b;
            return Add(b);
        }

        /// <summary>The open inspect label's handle, or 0.</summary>
        public static int InspectHandle { get { foreach (var b in All) if (b.Kind == Kind.Inspect) return b.Handle; return 0; } }

        /// <summary>Answers a shown bubble's reply (0-based). False if it is not shown or has no such reply.</summary>
        public static bool Choose(int handle, int index)
        {
            var b = Find(handle);
            if (b == null || !IsShown(b) || b.Replies == null || index < 0 || index >= b.Replies.Count) return false;
            All.Remove(b);
            Replied?.Invoke(handle, index);
            b.OnReply?.Invoke(index);
            return true;
        }

        /// <summary>Answers the nearest shown Ask that has that reply (keys 1-3). False if there is none.</summary>
        public static bool AnswerNearest(int index)
        {
            int h = NearestAsk(index + 1);
            return h != 0 && Choose(h, index);
        }

        /// <summary>The nearest shown bubble with at least <paramref name="minReplies"/> replies (Ask or inspect with replies), or 0.</summary>
        public static int NearestAsk(int minReplies = 1)
        {
            Bubble best = null; float bestD = float.MaxValue;
            var me = PlayerPos;
            foreach (var b in All)
            {
                if (!IsShown(b) || b.Replies == null || b.Replies.Count < Mathf.Max(1, minReplies)) continue;
                float d = BoardSpace.Flat(AnchorPos(b) - me);
                if (d < bestD) { bestD = d; best = b; }
            }
            return best != null ? best.Handle : 0;
        }

        /// <summary>Unfolds an Ask's replies (as a tap on the bubble does).</summary>
        public static void Expand(int handle) { var b = Find(handle); if (b != null) b.Expanded = true; }

        public static string TextOf(int handle) => Find(handle)?.Text;
        /// <summary>Speaker name or inspect title of a bubble (null if none).</summary>
        public static string TitleOf(int handle) => Find(handle)?.Name;
        public static IReadOnlyList<string> RepliesOf(int handle) => Find(handle)?.Replies;
        /// <summary>True when the bubble is the one showing for its speaker (not waiting in the queue).</summary>
        public static bool IsShowing(int handle) { var b = Find(handle); return b != null && IsShown(b); }
        /// <summary>Handles of every open bubble (shown or queued), oldest first.</summary>
        public static List<int> OpenHandles() { var l = new List<int>(); foreach (var b in All) l.Add(b.Handle); return l; }
        /// <summary>Bubbles shown now (tests, captures).</summary>
        public static int ShownCount { get { int n = 0; foreach (var b in All) if (IsShown(b)) n++; return n; } }
        /// <summary>Screen rect (GUI coordinates) of a bubble drawn last frame, or an empty rect.</summary>
        public static Rect ScreenRectOf(int handle) { var b = Find(handle); return b != null && b.DrawnFrame >= Time.frameCount - 2 ? b.Body : Rect.zero; }
        /// <summary>Screen rect (GUI coordinates) of a reply drawn last frame, or an empty rect.</summary>
        public static Rect ReplyRectOf(int handle, int index)
        {
            var b = Find(handle);
            if (b == null || b.DrawnFrame < Time.frameCount - 2 || index < 0 || index >= b.ReplyRects.Length) return Rect.zero;
            return b.ReplyRects[index];
        }

        /// <summary>
        /// A tap at a GUI point (y down): answers a reply, unfolds an Ask, or closes an inspect label. True if a bubble
        /// took the tap (it then does not reach the world).
        /// </summary>
        public static bool HandleTap(Vector2 guiPoint)
        {
            int f = Time.frameCount;
            for (int i = All.Count - 1; i >= 0; i--)
            {
                var b = All[i];
                if (b.DrawnFrame < f - 2 || b.OffScreen) continue;
                for (int r = 0; r < b.ReplyRects.Length; r++)
                    if (b.ReplyRects[r].Contains(guiPoint)) { Choose(b.Handle, r); return true; }
            }
            for (int i = All.Count - 1; i >= 0; i--)
            {
                var b = All[i];
                if (b.DrawnFrame < f - 2 || !b.Body.Contains(guiPoint)) continue;
                if (b.OffScreen) return true;
                if (b.Kind == Kind.Ask) b.Expanded = true;
                else if (b.Kind == Kind.Inspect && (b.Replies == null || b.Replies.Count == 0)) All.Remove(b);
                return true;
            }
            return false;
        }

        /// <summary>True if a GUI point is over a drawn bubble or reply.</summary>
        public static bool Contains(Vector2 guiPoint)
        {
            int f = Time.frameCount;
            foreach (var b in All)
            {
                if (b.DrawnFrame < f - 2) continue;
                if (b.Body.Contains(guiPoint)) return true;
                foreach (var r in b.ReplyRects) if (r.Contains(guiPoint)) return true;
            }
            return false;
        }

        // ---------------- internals ----------------

        private static int Add(Bubble b)
        {
            b.Handle = _next++;
            All.Add(b);
            return b.Handle;
        }

        internal static Bubble Find(int handle)
        {
            foreach (var b in All) if (b.Handle == handle) return b;
            return null;
        }

        private static object KeyOf(ISpeaker who) => who != null ? (object)who : new object();
        private static string NameOf(ISpeaker who)
        {
            if (who is Npc n) return n.DisplayName;
            if (who is IFollowable f) return f.FollowName;
            return null;
        }

        /// <summary>The first open bubble per key is the shown one.</summary>
        internal static bool IsShown(Bubble b)
        {
            foreach (var o in All) if (ReferenceEquals(o.Key, b.Key)) return ReferenceEquals(o, b);
            return false;
        }

        private static Vector3 InFrontOfPlayer()
        {
            var w = PlayerWalker.Instance;
            if (w == null) return Vector3.up * 2f;
            var f = w.transform.forward; f.y = 0;
            return w.Position + f.normalized * 6f + Vector3.up * 2.2f;
        }

        internal static Vector3 PlayerPos
        {
            get
            {
                var w = PlayerWalker.Instance;
                return w != null ? w.Position : Vector3.zero;
            }
        }

        internal static bool SpeakerGone(Bubble b)
        {
            if (b.Who != null)
            {
                if (b.Who is UnityEngine.Object o && o == null) return true;
                if (b.Who.Head == null) return true;
            }
            return b.Kind == Kind.Inspect && !b.HasPoint && b.Anchor == null;
        }

        /// <summary>World point the bubble hangs from.</summary>
        internal static Vector3 AnchorPos(Bubble b)
        {
            if (b.HasPoint) return b.Point;
            if (b.Who != null && b.Who.Head != null) return b.Who.Head.position + Vector3.up * 0.35f;
            if (b.Anchor != null) return TopOf(b.Anchor);
            return b.Point;
        }

        /// <summary>Top centre of an object's renderers (for inspect labels).</summary>
        internal static Vector3 TopOf(Transform t)
        {
            var rs = t.GetComponentsInChildren<Renderer>();
            if (rs.Length == 0) return t.position + Vector3.up * 2f;
            var bnd = rs[0].bounds;
            for (int i = 1; i < rs.Length; i++) bnd.Encapsulate(rs[i].bounds);
            if (bnd.size.y > 40f) return t.position + Vector3.up * 2f; // a whole hex tile: use the pivot
            return new Vector3(bnd.center.x, bnd.max.y + 0.3f, bnd.center.z);
        }

        /// <summary>Starts timers, runs timeouts and drops bubbles whose speaker is gone. Called each frame by the view.</summary>
        internal static void Tick()
        {
            float now = Time.unscaledTime;
            for (int i = 0; i < All.Count; i++)
            {
                var b = All[i];
                if (SpeakerGone(b))
                {
                    All.RemoveAt(i); i--;
                    if (b.Kind == Kind.Ask) { Replied?.Invoke(b.Handle, -1); b.OnReply?.Invoke(-1); }
                    continue;
                }
                if (!IsShown(b)) continue;
                if (b.ShownAt < 0) b.ShownAt = now;
                bool expire = b.Kind == Kind.Ask ? b.Seconds > 0 && now - b.ShownAt >= b.Seconds : now - b.ShownAt >= b.Seconds && (b.Kind != Kind.Inspect || b.Replies.Count == 0 || now - b.ShownAt >= b.Seconds * 2f);
                if (!expire) continue;
                All.RemoveAt(i); i--;
                if (b.Kind == Kind.Ask) { Replied?.Invoke(b.Handle, -1); b.OnReply?.Invoke(-1); }
            }
        }
    }

    /// <summary>Draws <see cref="SpeechBubbles"/> (IMGUI) and reads keys 1-3. Added by <see cref="PlayerInstaller"/>.</summary>
    public sealed class SpeechBubblesView : MonoBehaviour
    {
        private static readonly Color Paper = new Color(0.98f, 0.95f, 0.86f, 0.97f);
        private static readonly Color PaperEdge = new Color(0.30f, 0.22f, 0.14f, 0.95f);
        private static readonly Color Ink = new Color(0.14f, 0.10f, 0.07f, 1f);
        private static readonly Color ReplyPaper = new Color(0.99f, 0.86f, 0.55f, 0.97f);
        private static readonly Color ReplyHover = new Color(1f, 0.95f, 0.70f, 1f);
        private static readonly Color InspectPaper = new Color(0.12f, 0.11f, 0.10f, 0.88f);
        private static Texture2D _tail, _tailDark;
        private GUIStyle _text, _name, _reply, _inspectTitle, _inspectText;
        private int _stylesFor = -1;

        private static bool Hidden =>
            Hud.MenuOpen || MapScreen.IsOpen || (EncounterHost.Instance != null && EncounterHost.Instance.IsRunning);

        private void Update()
        {
            SpeechBubbles.Tick();
            if (Hidden || (ViewSwitcher.Instance != null && ViewSwitcher.Instance.CurrentView == ViewMode.God)) return;
            for (int i = 0; i < SpeechBubbles.MaxReplies; i++)
                if (Input.GetKeyDown(KeyCode.Alpha1 + i) || Input.GetKeyDown(KeyCode.Keypad1 + i)) { SpeechBubbles.AnswerNearest(i); break; }
        }

        private void EnsureStyles()
        {
            if (_stylesFor == Screen.height && _text != null) return;
            _stylesFor = Screen.height;
            _text = new GUIStyle(GUI.skin.label) { fontSize = UiStyle.Px(23), wordWrap = true, richText = true, alignment = TextAnchor.UpperLeft };
            _text.normal.textColor = Ink;
            _name = new GUIStyle(_text) { fontSize = UiStyle.Px(18), fontStyle = FontStyle.Bold, wordWrap = false };
            _name.normal.textColor = new Color(0.55f, 0.30f, 0.08f);
            _reply = new GUIStyle(_text) { fontSize = UiStyle.Px(21), wordWrap = false, alignment = TextAnchor.MiddleLeft };
            _inspectTitle = new GUIStyle(_text) { fontSize = UiStyle.Px(20), fontStyle = FontStyle.Bold, wordWrap = false };
            _inspectTitle.normal.textColor = UiStyle.AccentColor;
            _inspectText = new GUIStyle(_text) { fontSize = UiStyle.Px(17) };
            _inspectText.normal.textColor = UiStyle.TextColor;
            if (_tail == null) _tail = MakeTail(Paper);
            if (_tailDark == null) _tailDark = MakeTail(InspectPaper);
        }

        private static Texture2D MakeTail(Color c)
        {
            const int W = 32, H = 16;
            var t = new Texture2D(W, H, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave, wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    // Texture row 0 is the bottom: the point of the tail is at the bottom centre.
                    float half = (y + 0.5f) / H * (W * 0.5f);
                    float a = Mathf.Clamp01(half - Mathf.Abs(x + 0.5f - W * 0.5f) + 0.5f);
                    t.SetPixel(x, y, new Color(c.r, c.g, c.b, c.a * a));
                }
            t.Apply();
            return t;
        }

        private void OnGUI()
        {
            if (SpeechBubbles.All.Count == 0 || Hidden) return;
            var views = ViewSwitcher.Instance;
            if (views != null && views.CurrentView == ViewMode.God) return;
            var cam = views != null ? views.ActiveCamera : Camera.main;
            if (cam == null) return;
            EnsureStyles();
            GUI.depth = -8;
            var me = SpeechBubbles.PlayerPos;

            // Far first, so near bubbles draw on top.
            var shown = new List<SpeechBubbles.Bubble>();
            foreach (var b in SpeechBubbles.All) if (SpeechBubbles.IsShown(b)) shown.Add(b);
            shown.Sort((a, b) => BoardSpace.Flat(SpeechBubbles.AnchorPos(b) - me).CompareTo(BoardSpace.Flat(SpeechBubbles.AnchorPos(a) - me)));
            var placed = new List<Rect>();
            for (int i = shown.Count - 1; i >= 0; i--) Layout(shown[i], cam, me, placed); // near first: they keep their place
            foreach (var b in shown) Draw(b);
        }

        private void Layout(SpeechBubbles.Bubble b, Camera cam, Vector3 me, List<Rect> placed)
        {
            b.ReplyRects = new Rect[0];
            var world = SpeechBubbles.AnchorPos(b);
            float dist = BoardSpace.Flat(world - me);
            var sp = cam.WorldToScreenPoint(world);
            bool onScreen = sp.z > 0.2f && sp.x >= 0 && sp.x <= Screen.width && sp.y >= 0 && sp.y <= Screen.height;
            b.OffScreen = !onScreen;
            if (b.Kind == SpeechBubbles.Kind.Say && !b.HasPoint && dist > SpeechBubbles.SayRange) { b.DrawnFrame = -10; return; }
            if (!onScreen)
            {
                if (b.Kind != SpeechBubbles.Kind.Ask || b.Who == null) { b.DrawnFrame = -10; return; }
                b.Body = EdgePointerRect(cam, world, b);
                b.DrawnFrame = Time.frameCount;
                return;
            }
            float scale = Mathf.Clamp(1.1f - dist / 160f, 0.78f, 1f);
            bool inspect = b.Kind == SpeechBubbles.Kind.Inspect;
            var textStyle = inspect ? _inspectText : _text;
            float pad = UiStyle.Px(14) * scale;
            float maxW = UiStyle.Px(inspect ? 380 : 460) * scale;
            var content = new GUIContent(b.Text);
            float tw = Mathf.Min(maxW, Mathf.Max(UiStyle.Px(80), textStyle.CalcSize(content).x + 2));
            string name = b.Name;
            if (!string.IsNullOrEmpty(name)) tw = Mathf.Max(tw, Mathf.Min(maxW, (inspect ? _inspectTitle : _name).CalcSize(new GUIContent(name)).x + 2));
            float th = string.IsNullOrEmpty(b.Text) ? 0 : textStyle.CalcHeight(content, tw);
            float nh = string.IsNullOrEmpty(name) ? 0 : UiStyle.Px(inspect ? 26 : 22);
            float w = tw + 2 * pad, h = th + nh + 2 * pad - (th == 0 ? pad * 0.5f : 0);
            float x = sp.x - w / 2, y = Screen.height - sp.y - h - UiStyle.Px(14);
            x = Mathf.Clamp(x, 4, Screen.width - w - 4);

            // Replies under the body.
            var replyRects = new List<Rect>();
            bool showReplies = b.Replies != null && b.Replies.Count > 0 && (b.Expanded || dist <= SpeechBubbles.ReplyRange || b.Kind == SpeechBubbles.Kind.Inspect);
            float ry = 0;
            if (showReplies)
            {
                float rh = UiStyle.Px(40) * scale;
                for (int i = 0; i < b.Replies.Count; i++)
                {
                    float rw = _reply.CalcSize(new GUIContent((i + 1) + "   " + b.Replies[i])).x + UiStyle.Px(30);
                    replyRects.Add(new Rect(0, ry, rw, rh));
                    ry += rh + UiStyle.Px(5);
                }
            }
            // Push up while overlapping a nearer bubble already placed.
            var body = new Rect(x, y, w, h);
            for (int guard = 0; guard < 6; guard++)
            {
                bool moved = false;
                foreach (var r in placed)
                    if (r.Overlaps(new Rect(body.x, body.y, body.width, body.height + ry)))
                    { body.y = r.y - (body.height + ry) - UiStyle.Px(6); moved = true; }
                if (!moved) break;
            }
            body.y = Mathf.Max(4, body.y);
            b.Body = body;
            var rects = new Rect[replyRects.Count];
            for (int i = 0; i < rects.Length; i++)
            {
                var r = replyRects[i];
                rects[i] = new Rect(Mathf.Clamp(body.x + UiStyle.Px(18), 4, Screen.width - r.width - 4), body.yMax + UiStyle.Px(16) + r.y, r.width, r.height);
            }
            b.ReplyRects = rects;
            placed.Add(new Rect(body.x, body.y, body.width, body.height + ry + UiStyle.Px(16)));
            b.DrawnFrame = Time.frameCount;
            _anchorScreen[b.Handle] = new Vector2(Mathf.Clamp(sp.x, body.x + UiStyle.Px(16), body.xMax - UiStyle.Px(16)), Screen.height - sp.y);
            _scale[b.Handle] = scale;
        }

        private readonly Dictionary<int, Vector2> _anchorScreen = new Dictionary<int, Vector2>();
        private readonly Dictionary<int, float> _scale = new Dictionary<int, float>();

        private Rect EdgePointerRect(Camera cam, Vector3 world, SpeechBubbles.Bubble b)
        {
            // Direction of the speaker relative to the view, projected onto the screen plane.
            var local = cam.transform.InverseTransformPoint(world);
            var dir = new Vector2(local.x, local.y);
            if (local.z < 0) dir = -dir;
            if (dir.sqrMagnitude < 1e-4f) dir = new Vector2(local.x >= 0 ? 1 : -1, 0);
            dir.Normalize();
            float w = UiStyle.Px(240), h = UiStyle.Px(52), m = UiStyle.Px(12);
            var c = new Vector2(Screen.width / 2f, Screen.height / 2f);
            float kx = (Screen.width / 2f - w / 2 - m) / Mathf.Max(1e-4f, Mathf.Abs(dir.x));
            float ky = (Screen.height / 2f - h / 2 - m) / Mathf.Max(1e-4f, Mathf.Abs(dir.y));
            float k = Mathf.Min(kx, ky);
            var p = c + new Vector2(dir.x, -dir.y) * k;
            _edgeDir[b.Handle] = new Vector2(dir.x, -dir.y);
            return new Rect(p.x - w / 2, p.y - h / 2, w, h);
        }

        private readonly Dictionary<int, Vector2> _edgeDir = new Dictionary<int, Vector2>();

        private void Draw(SpeechBubbles.Bubble b)
        {
            if (b.DrawnFrame != Time.frameCount) return;
            bool inspect = b.Kind == SpeechBubbles.Kind.Inspect;
            if (b.OffScreen)
            {
                DrawRounded(b.Body, ReplyPaper, PaperEdge);
                var d = _edgeDir.TryGetValue(b.Handle, out var v) ? v : Vector2.right;
                string arrow = Mathf.Abs(d.x) > Mathf.Abs(d.y) ? (d.x > 0 ? "▶" : "◀") : (d.y > 0 ? "▼" : "▲");
                var st = new GUIStyle(_reply) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
                GUI.Label(b.Body, (d.x < 0 || d.y < -0.7f ? arrow + "  " : "") + (b.Name ?? "Someone") + " calls you" + (d.x >= 0 && d.y >= -0.7f ? "  " + arrow : ""), st);
                return;
            }
            float scale = _scale.TryGetValue(b.Handle, out var s) ? s : 1f;
            var body = b.Body;
            DrawRounded(body, inspect ? InspectPaper : Paper, inspect ? new Color(0.9f, 0.7f, 0.3f, 0.9f) : PaperEdge);
            // Tail towards the anchor.
            var a = _anchorScreen.TryGetValue(b.Handle, out var av) ? av : new Vector2(body.center.x, body.yMax + 12);
            float tw = UiStyle.Px(26) * scale, tth = UiStyle.Px(14) * scale;
            GUI.DrawTexture(new Rect(a.x - tw / 2, body.yMax - 1, tw, tth), inspect ? _tailDark : _tail);
            float pad = UiStyle.Px(14) * scale;
            float y = body.y + pad * (string.IsNullOrEmpty(b.Text) ? 0.75f : 1f);
            if (!string.IsNullOrEmpty(b.Name))
            {
                var ns = inspect ? _inspectTitle : _name;
                GUI.Label(new Rect(body.x + pad, y - UiStyle.Px(4), body.width - 2 * pad, UiStyle.Px(28)), b.Name, ns);
                y += UiStyle.Px(inspect ? 26 : 22);
            }
            if (!string.IsNullOrEmpty(b.Text))
                GUI.Label(new Rect(body.x + pad, y, body.width - 2 * pad, body.yMax - y), b.Text, inspect ? _inspectText : _text);
            if (b.ReplyRects.Length == 0 && b.Replies != null && b.Replies.Count > 0)
            {
                var hint = new GUIStyle(_name) { fontStyle = FontStyle.Italic, alignment = TextAnchor.MiddleCenter };
                hint.normal.textColor = new Color(0.4f, 0.3f, 0.2f);
                GUI.Label(new Rect(body.x, body.yMax + UiStyle.Px(12), body.width, UiStyle.Px(26)), "tap to answer", hint);
            }
            var m = Event.current.mousePosition;
            for (int i = 0; i < b.ReplyRects.Length; i++)
            {
                var r = b.ReplyRects[i];
                DrawRounded(r, r.Contains(m) ? ReplyHover : ReplyPaper, PaperEdge);
                GUI.Label(new Rect(r.x + UiStyle.Px(14), r.y, r.width - UiStyle.Px(14), r.height), "<b>" + (i + 1) + "</b>   " + b.Replies[i], _reply);
            }
        }

        private static void DrawRounded(Rect r, Color fill, Color edge)
        {
            float e = Mathf.Max(1, UiStyle.Px(2));
            // Edge, then fill inset; corners trimmed by two overlapping rects (a cheap rounded look).
            GUI.DrawTexture(new Rect(r.x + e * 2, r.y, r.width - e * 4, r.height), UiStyle.SolidTexture(edge));
            GUI.DrawTexture(new Rect(r.x, r.y + e * 2, r.width, r.height - e * 4), UiStyle.SolidTexture(edge));
            GUI.DrawTexture(new Rect(r.x + e * 2, r.y + e, r.width - e * 4, r.height - e * 2), UiStyle.SolidTexture(fill));
            GUI.DrawTexture(new Rect(r.x + e, r.y + e * 2, r.width - e * 2, r.height - e * 4), UiStyle.SolidTexture(fill));
        }
    }
}
