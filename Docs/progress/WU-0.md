# WU-0: shared world interaction and the zone outline

20 Nov 2026, branch `godboard/wu0-interaction` (from `fbe3b5b`). Scope: the WU-0 row of `Docs/WORLD_UI_PLAN.md` section 5.

## Done

1. **`WorldAction`** (`Assets/World/Game/UI/WorldAction.cs`): states idle, inspect, preview, confirm, refused, disabled. Tap once = preview, tap again = confirm (one command per confirm), tap elsewhere = cancel (the cancelling tap does nothing else). Tap guard: 0.6 s after each command, and while the camera moves (the controller watches the active camera: more than 0.35 m or 2.5 degrees in a frame; camera code can also call `WorldActions.NoteCameraMove`). A second tap sooner than 0.25 s after the preview (a double tap or a bounce) is ignored, so a double tap never confirms. A preview, refusal or inspect lapses after 12 s; a late tap then previews again. Refusal is re-checked at confirm time. Hit targets: `WorldHitTarget.MinSide(h)` = max(7 % of the screen height, 44 px at 390); registered actions are also hit when a tap lands inside their screen bounds grown to that size (`WorldActions.PickEnlarged`, used by `PointerInput.TapAt` when the ray hits nothing or only ground). Keyboard alternative: **E or Enter** previews the nearest action within its reach, the next press confirms; **Backspace** cancels. Menus, the map and encounters cancel an open preview.
2. **`LedgerCard`** (`UI/LedgerCard.cs`): one readable tablet that unfolds from the tapped object (0.18 s, with a tether), placed beside the object's screen bounds and never over them when there is room, inside the safe area. Text 15 to 30 px (16 px at 390 high). Tapping the card in a preview confirms; tapping elsewhere closes it. Content is caller text (`LedgerContent`: title, lines, question, refusal); `LedgerText.Violation` flags dice, pips, odds or percentages (AB-1; a warning in the editor). `LedgerCard.Content` / `LedgerCard.Changed` / `LedgerContent.AllText()` give the same text to a future Clear view layer.
3. **`WorldZone`** tag (`UI/WorldZone.cs`) and `WorldZones` (all section 2 ids as constants and the wireframe rectangles, normalised, ours only: no colours or labels from the diagrams). Council ids are `CNC-AF`, `CNC-GL`, `CNC-M`, `CNC-QRS`, `CNC-NOP`, `CNC-T`, `CNC-U`, `CNC-STATUS`, `CNC-VZ`, `CNC-EXIT` (the plan writes `CNC-A..F` etc.). **Debug outline** `UI/God/ZoneOutlineTool.cs`: the whole file is inside `#if UNITY_EDITOR`; F1 tool "Zones" (off by default) toggles it and picks the wireframe (auto from the tagged objects, or HUB, HEX, PLG, CNC, RHY, CRY, DUEL). Draws the safe area (red), each tagged object's screen bounds with its id (cyan) and the wireframe zones mapped onto the safe area, dashed, with their ids (yellow). Registered from `DevToolsInstaller` inside `#if UNITY_EDITOR`.
4. **Safe area generalised**: `ScreenSafeArea.Override` / `Current()` / `SimulatedNotch(w, h)` (the 844 x 390 phone's 47 px side insets and 21 px home bar, scaled by height). `DuelLayout.SafeAreaOverride` is now a property forwarding to it (callers unchanged).
5. **Capture driver** `AgentScripts/WorldUiCaptures.cs` (commands in `Temp/wu_cmd.txt`: `godboard`, `size W H`, `overlay on|off`, `wireframe ID`, `altar METRES`, `cancel`, `pause`, `shot NAME`, `check NAME W H`, `info`, `where`). `size` sets the game view rendering resolution and the simulated notch; `check` reads the saved PNG header. All 14 captures checked: every PNG is exactly its requested size.
6. **Adopted once: the player's altar** (`TempleAltar`, `Board/GodBoard/TempleView.cs`). First tap (or E) previews: a warm pulsing ring round the temple base and the altar, and the ledger "The altar / Sacrifice a lamb here to open the god board. / Sacrifice a lamb?". Second tap (or E, or a tap on the ledger) sends `Sacrifice` once. Too far: refused, the ledger says "Walk closer to the altar." Rules unchanged: the check is `GodBoardDirector.CheckSacrifice` (state reads plus `Sim.Check`), the command is the existing `TrySacrifice`. `IInteractable.Interact()` on the altar now goes through the same two-step path. The player's temple is tagged `HUB-TEMPLE`.

## API for the next packages

```csharp
// Make a world object a two-step action:
public sealed class Thing : MonoBehaviour, IWorldAction, IWorldActionAnchor {
    string Title; IEnumerable<string> Lines;           // the ledger text (words and counts only, AB-1)
    string PreviewLine => "Build a road here?";
    WorldActionCheck Check() => ok ? WorldActionCheck.Allowed : WorldActionCheck.Refuse("Why not.") /* or Disable(...) */;
    void ShowPreview(bool on);                          // ghost, highlight, a person turning; no command
    bool Commit();                                      // send the command once
    Transform Anchor; float KeyReach;                   // where the ledger unfolds; metres for the E key (0 = never)
    void OnEnable() => WorldActions.Register(this); void OnDisable() => WorldActions.Unregister(this);
}
WorldActions.Tap(a) / TapElsewhere() / Cancel() / State / Machine  // PointerInput routes taps here already
WorldActions.NoteCameraMove(seconds)                    // camera code: guard taps during a move
WorldHitTarget.MinSide(h), Enlarge(r, h), ScreenBounds(c, cam, out r)
LedgerCard.Show(content, anchor, mode, boundsComponent) / Hide() / Content / Changed   // for inspect-only things
WorldZone.Tag(go, WorldZones.HexStore)                  // tag every destination object
ScreenSafeArea.Override = ScreenSafeArea.SimulatedNotch(w, h)
```

`PointerInput.TapOutcome` gained `Preview`, `Confirm`, `Refused`, `Cancelled`, `Ignored`.

## Tests actually run (20 Nov, editor 6000.3.28f1)

- EditMode `World.Game.EditTests` (new assembly, `Assets/World/Tests/EditMode/Game/`, references World.Game): **19 / 19 passed**. State machine (preview, confirm once, cancel, move preview, refused, disabled, refused at confirm, double tap, burst after confirm, camera move, lapse, inspect), hit-target sizes, ledger layout at 844 x 390 and clear of the object, ledger AB-1 text, notch, zone table, overlay source editor-only, and a **build-define test**: `PlayerBuildInterface.CompilePlayerScripts` for StandaloneWindows64, then `World.Game.dll` of the player contains `WorldZone` and `LedgerCard` but not `ZoneOutlineTool` / `ZoneOutlineOverlay` (about 9 s).
- PlayMode `World.Game.Tests.WorldUiTests` (new): **6 / 6 passed**. Altar first tap previews and second confirms once (double tap ignored, burst after confirm sends nothing, table opens); tap elsewhere cancels; far is refused; E twice; tap during camera move ignored; zone outline off by default, F1 tool registered, nothing drawn, temple tagged `HUB-TEMPLE`; ledger at a real 844 x 390 game view with the notch: font, inside the safe area, target size, tap on the card confirms.
- PlayMode `World.Game.Tests.GodBoard*` (`GodBoardTests` + `GodBoardVillagerTests`): **17 / 17 passed**.
- Not run: `dotnet test DotNet` (no sim code changed), the full PlayMode suite, a real player build.
- Console: no errors from game code; the editor's own Search indexer logs `ArgumentOutOfRangeException` on this machine (UnityEditor.Search, not ours).

## Captures (local, not committed): `Evidence/worldui/`

`hub_{1920x1080,1024x768,844x390}_{off,on}.png` (overlay off and on at the three ratios, hub at night, notch safe area), `altar_preview_{ratio}.png` and `altar_preview_{ratio}_overlay.png` (the preview ring and the ledger), `altar_preview_844x390_far.png`, `ledger_refused_844x390.png`.

## Not done / open

- No temple keeper yet (WU-1): the preview highlights the altar and temple base only.
- The F1 "Plague" screen button sits outside the simulated notch at all three ratios (seen in the captures); WU-3 removes it.
- The tagged object's outline uses its renderer and collider bounds; when part of an object is behind the camera the box is cut to the visible corners.
- The simulated notch scales the phone's insets by screen height, so the 4:3 capture gets 93 px side insets (a stand-in, not a tablet's real safe area).
- Tap-to-walk and sideways drag (P-3) are WU-1. Only the altar uses `WorldAction` so far.
- Test pollution found: after a test that opens the board, `PointerInput.InputEnabled` stays false into the next test's scene; `WorldUiTests.SetUp` resets it. Other fixtures may want the same.
