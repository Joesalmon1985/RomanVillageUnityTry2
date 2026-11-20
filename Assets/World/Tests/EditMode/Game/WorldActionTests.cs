using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using World.Game;

namespace World.Game.EditTests
{
    /// <summary>WU-0: the preview / confirm / cancel / refused state machine, the tap guard, hit-target sizing, the
    /// ledger layout and text, the zone table and the editor-only overlay.</summary>
    public class WorldActionTests
    {
        private sealed class Fake : IWorldAction
        {
            public WorldActionCheck Next = WorldActionCheck.Allowed;
            public int Commits, PreviewOn, PreviewOff;
            public bool Showing;
            public string Title => "Fake";
            public IEnumerable<string> Lines => new[] { "A thing." };
            public string PreviewLine => "Do it?";
            public WorldActionCheck Check() => Next;
            public void ShowPreview(bool on) { Showing = on; if (on) PreviewOn++; else PreviewOff++; }
            public bool Commit() { Commits++; return true; }
        }

        private sealed class Sign : IInspectable
        {
            public string Title => "A sign";
            public IEnumerable<string> Lines => new[] { "Words." };
        }

        private WorldActionMachine M() => new WorldActionMachine();

        [Test]
        public void FirstTapPreviewsSecondConfirmsOnce()
        {
            var m = M(); var a = new Fake();
            Assert.AreEqual(WorldTapResult.Previewed, m.Tap(a, 0f));
            Assert.AreEqual(WorldActionState.Preview, m.State);
            Assert.IsTrue(a.Showing);
            Assert.AreEqual(0, a.Commits, "a preview sends nothing");
            Assert.AreEqual(WorldTapResult.Confirmed, m.Tap(a, 1f));
            Assert.AreEqual(1, a.Commits);
            Assert.AreEqual(1, m.CommandsSent);
            Assert.AreEqual(WorldActionState.Confirm, m.State);
            Assert.IsFalse(a.Showing, "the preview ends on confirm");
            m.Tick(1f + m.GuardSeconds + 0.01f);
            Assert.AreEqual(WorldActionState.Idle, m.State);
        }

        [Test]
        public void TapElsewhereCancelsAndSendsNothing()
        {
            var m = M(); var a = new Fake();
            m.Tap(a, 0f);
            Assert.AreEqual(WorldTapResult.Cancelled, m.TapElsewhere(1f));
            Assert.AreEqual(WorldActionState.Idle, m.State);
            Assert.IsFalse(a.Showing);
            Assert.AreEqual(0, a.Commits);
            // Next tap previews again, it does not confirm.
            Assert.AreEqual(WorldTapResult.Previewed, m.Tap(a, 2f));
            Assert.AreEqual(0, a.Commits);
        }

        [Test]
        public void TapOnAnotherActionMovesThePreview()
        {
            var m = M(); var a = new Fake(); var b = new Fake();
            m.Tap(a, 0f);
            Assert.AreEqual(WorldTapResult.Previewed, m.Tap(b, 1f));
            Assert.IsFalse(a.Showing); Assert.IsTrue(b.Showing);
            Assert.AreSame(b, m.Current);
            Assert.AreEqual(0, a.Commits + b.Commits);
            m.Tap(b, 2f);
            Assert.AreEqual(0, a.Commits); Assert.AreEqual(1, b.Commits);
        }

        [Test]
        public void RefusedNeverPreviewsOrSends()
        {
            var m = M(); var a = new Fake { Next = WorldActionCheck.Refuse("Walk closer.") };
            Assert.AreEqual(WorldTapResult.Refused, m.Tap(a, 0f));
            Assert.AreEqual(WorldActionState.Refused, m.State);
            Assert.AreEqual("Walk closer.", m.Reason);
            Assert.AreEqual(0, a.PreviewOn);
            Assert.AreEqual(WorldTapResult.Refused, m.Tap(a, 1f));
            Assert.AreEqual(0, a.Commits);
            // Once allowed, it takes two taps again.
            a.Next = WorldActionCheck.Allowed;
            Assert.AreEqual(WorldTapResult.Previewed, m.Tap(a, 2f));
            Assert.AreEqual(0, a.Commits);
        }

        [Test]
        public void DisabledNeverPreviews()
        {
            var m = M(); var a = new Fake { Next = WorldActionCheck.Disable("The age has turned.") };
            Assert.AreEqual(WorldTapResult.Disabled, m.Tap(a, 0f));
            Assert.AreEqual(WorldActionState.Disabled, m.State);
            Assert.AreEqual(0, a.PreviewOn + a.Commits);
        }

        [Test]
        public void RefusedAtConfirmTimeSendsNothing()
        {
            var m = M(); var a = new Fake();
            m.Tap(a, 0f);
            a.Next = WorldActionCheck.Refuse("Too late.");
            Assert.AreEqual(WorldTapResult.Refused, m.Tap(a, 1f));
            Assert.AreEqual(0, a.Commits);
            Assert.IsFalse(a.Showing);
        }

        [Test]
        public void FastDoubleTapPreviewsButNeverConfirms()
        {
            var m = M(); var a = new Fake();
            Assert.AreEqual(WorldTapResult.Previewed, m.Tap(a, 0f));
            Assert.AreEqual(WorldTapResult.Ignored, m.Tap(a, 0.08f), "the second half of a double tap is one tap");
            Assert.AreEqual(0, a.Commits);
            Assert.AreEqual(WorldActionState.Preview, m.State);
        }

        [Test]
        public void TapsAfterConfirmAreGuarded()
        {
            var m = M(); var a = new Fake();
            m.Tap(a, 0f); m.Tap(a, 1f);
            // A rapid burst after the command: nothing more is sent, nothing previews.
            for (int i = 1; i <= 5; i++) Assert.AreEqual(WorldTapResult.Ignored, m.Tap(a, 1f + i * 0.1f));
            Assert.AreEqual(1, a.Commits);
            Assert.AreEqual(1, m.CommandsSent);
            // After the guard the cycle starts again with a preview.
            Assert.AreEqual(WorldTapResult.Previewed, m.Tap(a, 1f + m.GuardSeconds + 0.1f));
            Assert.AreEqual(1, a.Commits);
        }

        [Test]
        public void TapDuringCameraMoveIsIgnored()
        {
            var m = M(); var a = new Fake();
            Assert.AreEqual(WorldTapResult.Ignored, m.Tap(a, 0f, cameraMoving: true));
            Assert.AreEqual(WorldActionState.Idle, m.State);
            m.Tap(a, 1f);
            Assert.AreEqual(WorldTapResult.Ignored, m.Tap(a, 2f, cameraMoving: true), "a confirm while the camera moves is dropped");
            Assert.AreEqual(0, a.Commits);
            Assert.AreEqual(WorldActionState.Preview, m.State, "the preview stays");
            Assert.AreEqual(WorldTapResult.Confirmed, m.Tap(a, 3f));
            Assert.AreEqual(1, a.Commits);
        }

        [Test]
        public void LatePreviewLapsesAndTheLateTapPreviewsAgain()
        {
            var m = M(); var a = new Fake();
            m.Tap(a, 0f);
            Assert.AreEqual(WorldTapResult.Previewed, m.Tap(a, m.PreviewTimeoutSeconds + 1f));
            Assert.AreEqual(0, a.Commits);
        }

        [Test]
        public void InspectOpensAndClosesWithoutCommands()
        {
            var m = M(); var a = new Fake(); var s = new Sign();
            m.Tap(a, 0f);
            Assert.AreEqual(WorldTapResult.Inspected, m.Inspect(s, 1f));
            Assert.AreEqual(WorldActionState.Inspect, m.State);
            Assert.IsFalse(a.Showing, "inspecting another thing cancels the preview");
            Assert.AreEqual(WorldTapResult.Cancelled, m.TapElsewhere(2f));
            Assert.AreEqual(0, a.Commits);
        }

        [Test]
        public void HitTargetsAreAtLeastSevenPercent()
        {
            foreach (var h in new[] { 390f, 768f, 1080f })
            {
                float min = WorldHitTarget.MinSide(h);
                Assert.GreaterOrEqual(min, h * 0.07f - 0.01f, "7 % of " + h);
                var r = WorldHitTarget.Enlarge(new Rect(100, 100, 4, 6), h);
                Assert.IsTrue(WorldHitTarget.Meets(r, h));
                Assert.AreEqual(102f, r.center.x, 0.01f); Assert.AreEqual(103f, r.center.y, 0.01f);
            }
            Assert.GreaterOrEqual(WorldHitTarget.MinSide(390f), 44f, "the 44 px phone rule");
        }

        [Test]
        public void LedgerReadableAt844x390()
        {
            var safe = ScreenSafeArea.SimulatedNotch(844, 390);
            var c = new LedgerContent { Title = "The altar", Question = "Sacrifice a lamb?" };
            c.Lines.Add("Sacrifice a lamb here to open the god board.");
            foreach (var anchor in new Vector2?[] { null, new Vector2(10, 10), new Vector2(830, 380), new Vector2(422, 195) })
            {
                var L = LedgerLayout.Compute(844, 390, safe, anchor, c);
                Assert.GreaterOrEqual(L.FontSize, LedgerLayout.MinFont);
                Assert.GreaterOrEqual(L.FontSize, 15);
                Assert.IsTrue(safe.Contains(L.Card.min) && safe.Contains(L.Card.max - Vector2.one * 0.01f), "inside the safe area: " + L.Card);
                Assert.GreaterOrEqual(L.Card.height, WorldHitTarget.MinSide(390), "the card is itself a big target");
                Assert.GreaterOrEqual(L.Card.width, L.FontSize * 14f - 0.5f, "room for a line of words");
            }
        }

        [Test]
        public void LedgerKeepsClearOfTheObject()
        {
            var safe = ScreenSafeArea.SimulatedNotch(844, 390);
            var c = new LedgerContent { Title = "The altar", Question = "Sacrifice a lamb?" };
            c.Lines.Add("Sacrifice a lamb here to open the god board.");
            foreach (var obj in new[] { new Rect(380, 100, 90, 120), new Rect(60, 150, 100, 80), new Rect(600, 40, 150, 200) })
            {
                var L = LedgerLayout.Compute(844, 390, safe, (Rect?)obj, c);
                Assert.IsFalse(L.Card.Overlaps(obj), "card " + L.Card + " covers the object " + obj);
                Assert.IsTrue(safe.Contains(L.Card.min) && safe.Contains(L.Card.max - Vector2.one * 0.01f));
            }
        }

        [Test]
        public void LedgerTextRefusesOddsAndDice()
        {
            Assert.IsNull(LedgerText.Violation("Sacrifice a lamb here to open the god board."));
            Assert.IsNull(LedgerText.Violation("Logs 3, bricks 1."), "counts are allowed");
            Assert.IsNotNull(LedgerText.Violation("A 14% chance of a seven."));
            Assert.IsNotNull(LedgerText.Violation("The dice show eight."));
            Assert.IsNotNull(LedgerText.Violation("Better odds here."));
            var c = LedgerContent.From(new Sign(), "Go?", "No.");
            CollectionAssert.AreEqual(new[] { "A sign", "Words.", "Go?", "No." }, c.AllText().ToArray(), "the Clear view reads the same text");
        }

        [Test]
        public void SimulatedNotchScalesWithTheScreen()
        {
            var p = ScreenSafeArea.SimulatedNotch(844, 390);
            Assert.AreEqual(new Rect(47, 0, 750, 369), p);
            var d = ScreenSafeArea.SimulatedNotch(1920, 1080);
            Assert.Greater(d.x, 0); Assert.Less(d.height, 1080); Assert.AreEqual(1920 - 2 * d.x, d.width, 0.01f);
        }

        [Test]
        public void ZoneTableCoversTheWireframes()
        {
            var ids = WorldZones.AllIds().ToList();
            Assert.Contains(WorldZones.HubTemple, ids);
            Assert.Contains(WorldZones.HexStore, ids);
            Assert.AreEqual(ids.Count, ids.Distinct().Count(), "ids are unique");
            foreach (var wf in new[] { WorldZones.Hub, WorldZones.Hex, WorldZones.Plague, WorldZones.Council, WorldZones.Rhythm, WorldZones.Crystal })
                Assert.IsTrue(WorldZones.Rects.Keys.Any(k => WorldZone.WireframeOf(k) == wf), wf + " has rectangles");
            foreach (var kv in WorldZones.Rects)
            {
                Assert.Contains(kv.Key, ids);
                Assert.IsTrue(kv.Value.xMin >= -0.01f && kv.Value.yMin >= -0.01f && kv.Value.xMax <= 1.01f && kv.Value.yMax <= 1.01f, kv.Key + " " + kv.Value);
            }
            Assert.AreEqual("HUB", WorldZone.WireframeOf("HUB-TEMPLE"));
        }

        /// <summary>The overlay source is wrapped whole in UNITY_EDITOR and nothing outside an editor guard names it.</summary>
        [Test]
        public void OverlaySourceIsEditorOnly()
        {
            string root = Path.Combine(Application.dataPath, "World", "Game");
            var src = File.ReadAllLines(Path.Combine(root, "UI", "God", "ZoneOutlineTool.cs"));
            Assert.AreEqual("#if UNITY_EDITOR", src.First(l => l.Trim().Length > 0).Trim());
            Assert.AreEqual("#endif", src.Last(l => l.Trim().Length > 0).Trim());
            foreach (var f in Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories))
            {
                if (f.EndsWith("ZoneOutlineTool.cs")) continue;
                int depth = 0; var lines = File.ReadAllLines(f);
                for (int i = 0; i < lines.Length; i++)
                {
                    var t = lines[i].Trim();
                    if (t.StartsWith("#if UNITY_EDITOR")) depth++;
                    else if (t.StartsWith("#endif") && depth > 0) depth--;
                    else if ((t.Contains("ZoneOutlineTool") || t.Contains("ZoneOutlineOverlay")) && depth == 0 && !t.StartsWith("//") && !t.StartsWith("///"))
                        Assert.Fail(f + ":" + (i + 1) + " names the overlay outside UNITY_EDITOR");
                }
            }
        }
    
        /// <summary>WU-8: no code outside an editor guard draws a zone id, a wireframe name or a zone rectangle on screen
        /// (a label or an outline would show in a build): GUI, Handles and Gizmos calls must not name them.</summary>
        [Test]
        public void NoWireframeLabelOrOutlineIsDrawnOutsideTheEditor()
        {
            string root = Path.Combine(Application.dataPath, "World", "Game");
            foreach (var f in Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories))
            {
                int depth = 0; var lines = File.ReadAllLines(f);
                for (int i = 0; i < lines.Length; i++)
                {
                    var t = lines[i].Trim();
                    if (t.StartsWith("#if UNITY_EDITOR")) depth++;
                    else if (t.StartsWith("#endif") && depth > 0) depth--;
                    if (depth > 0 || t.StartsWith("//")) continue;
                    bool draws = t.Contains("GUI.Label") || t.Contains("GUI.Box") || t.Contains("Handles.") || t.Contains("Gizmos.") || t.Contains("OnDrawGizmos") || t.Contains("DrawLabel");
                    bool zone = t.Contains("ZoneId") || t.Contains("WireframeOf") || t.Contains("WorldZones.Rects") || t.Contains("\"HUB-") || t.Contains("ZoneOutline");
                    if (draws && zone) Assert.Fail(f + ":" + (i + 1) + " draws a zone id or outline outside UNITY_EDITOR: " + t);
                }
            }
        }

        /// <summary>Build-define test: compile the player scripts (no build) and check World.Game.dll for the player has
        /// no zone outline type, while the editor assembly has it.</summary>
        [Test, Category("Slow")]
        public void PlayerScriptsHaveNoOverlay()
        {
            Assert.IsNotNull(typeof(WorldZone).Assembly.GetType("World.Game.ZoneOutlineTool"), "the editor has the tool");
            string outDir = Path.Combine(Path.GetTempPath(), "wu0_player_scripts");
            if (Directory.Exists(outDir)) Directory.Delete(outDir, true);
            Directory.CreateDirectory(outDir);
            var settings = new UnityEditor.Build.Player.ScriptCompilationSettings
            {
                target = UnityEditor.BuildTarget.StandaloneWindows64,
                group = UnityEditor.BuildTargetGroup.Standalone,
                options = UnityEditor.Build.Player.ScriptCompilationOptions.None,
            };
            var result = UnityEditor.Build.Player.PlayerBuildInterface.CompilePlayerScripts(settings, outDir);
            Assert.IsTrue(result.assemblies.Contains("World.Game.dll"), "World.Game compiled for the player");
            var bytes = File.ReadAllBytes(Path.Combine(outDir, "World.Game.dll"));
            string text = System.Text.Encoding.UTF8.GetString(bytes);
            StringAssert.Contains("WorldZone", text, "the zone tag stays in builds");
            StringAssert.Contains("LedgerCard", text);
            StringAssert.DoesNotContain("ZoneOutlineTool", text, "the overlay is not in the player");
            StringAssert.DoesNotContain("ZoneOutlineOverlay", text, "the overlay is not in the player");
        }
    }
}
