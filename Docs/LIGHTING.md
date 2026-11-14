# Lighting and the day-night cycle (package L)

The game starts at night. Fire and warm light are the heart of the night look; moonlight keeps the open country dim but never black. Built-in render pipeline, forward rendering, no post-processing and no packages: the look comes from lights, fog, emissive (unlit and additive) meshes and colour. Namespace `World.Game`; code in `Assets/World/Game/View/Light/`. Original procedural shapes only.

## Files

| File | What |
|---|---|
| `LightingInstaller.cs` | Entry point, one line in `WorldBootstrap.Awake` after `LandViewsInstaller` (it dresses V's sky and views). Creates the three components below under a `Lighting` object. |
| `DayNight.cs` | The cycle: hour from the sim tick, sun and moon lights, sky dome colours, stars, moon and sun discs, ambient, fog, shader globals. `LightingState` is everything derived from the hour. |
| `WorldLights.cs` | `FireLight` (one fire, torch or glowing accent) and `WorldLights` (registry, flicker, ground glow, halo, sparks, the per-pixel budget, the player's lantern). |
| `LightDecorator.cs` | Finds the other packages' views at runtime and gives them lights and night-readable materials. `LitBy` marks a decorated view. |
| `LightShapes.cs` | Glow discs, torch and brazier meshes, flames, the slim cast-mark beam, and the night variants of materials. |
| `Tests/PlayMode/LightingTests.cs` | See Tests. |
| `AgentScripts/LightCaptures.cs` | Capture and inspection driver (commands in `Temp/l_cmd.txt`). |

## Time of day

- `DayNight.HourAtTick(tick)` = `StartHour + tick / (DayLengthSeconds * 10) * 24` (mod 24). It is a pure function of `WorldState.Tick`, so it pauses with `GameClock` (encounters, the menu), and a loaded game shows exactly the lighting of its saved game time: nothing of the lighting is saved. While the clock runs the hour is smoothed between ticks; while paused it is exactly the tick's.
- `DayNight.Evaluate(hour)` returns the whole `LightingState` (pure): sun and moon directions, colours and intensities, ambient trilight, sky zenith, horizon and horizon glow, fog colour and range, `Night` (0 day, 1 night: how far fires are on) and `Stars`. Colours are keyed on the sun's elevation (-16, -6, 0, 8, 26 degrees), so dawn and dusk use the same palette.
- Sunrise 06:00 in the east, noon 54 degrees up in the south, sunset 18:00. A new game starts at 22:30: night lasts until about 04:40 (3 real minutes), first light and dawn to about 06:25, when the last torches are out.
- The moon is up all night, 17 to 28 degrees high from the south-east to the south-west: low enough to be seen by a walker whose gaze is slightly downward, and it lights the sides of things.
- One shadow-casting directional light at a time: the sun (soft shadows, strength 0.75) while its intensity is above zero, otherwise the moon (0.55). `RenderSettings.sun` follows. Neither light is ever lower than `MinLightElevation` (14 degrees), which keeps shadows short enough not to shimmer; stable-fit projection, four cascades, very high resolution, shadow distance 90 m by day and 60 m at night.
- Developer scrub (editor and development builds, `DayNight.DevKeys`): F2 adds an hour, Shift+F2 takes one off, F3 toggles a time-lapse of one hour per second. From code (package P's F1 tools): `DayNight.Scrub(hours)`, `DayNight.Instance.SetHour(h)`, `DayNight.DevTimeLapse`. The scrub is an offset on top of the game time and is not saved.

### Tuning values (`DayNight`)

| Value | Default | Meaning |
|---|---|---|
| `DayLengthSeconds` | 720 | real seconds of running game clock per day |
| `StartHour` | 22.5 | hour at tick 0 |
| `MoonFullIntensity` | 0.62 | moonlight at full night (colour 0.56, 0.70, 1.0) |
| `SunFullIntensity` | 1.15 | sun above 26 degrees |
| `MinLightElevation` | 14 | lowest light direction, degrees |
| `ShadowBias`, `ShadowNormalBias` | 0.4, 0.9 | clear the paths and marks that lie 5 to 15 cm above the ground |
| `ShadowDistanceDay`, `ShadowDistanceNight` | 90, 60 | metres |
| key tables (`ZenithK`, `HorizonK`, `GlowK`, `AmbSkyK`, `AmbEqK`, `AmbGroundK`, `SunColK`, `SunIntK`) | | one colour per elevation key |
| night fog | 30 to 400 m | day: V's 140 to 700 m; colour = horizon colour |

Night ambient (sky, equator, ground): (0.13, 0.18, 0.33), (0.10, 0.13, 0.23), (0.055, 0.07, 0.12). In the God View (the interim map) the ambient is lifted to (0.20, 0.25, 0.40) and the moon by half, so the whole island stays legible at night while still reading as night.

## Sky

V's `SkyDome` (found by name under the bootstrap) keeps its mesh; its vertex colours are repainted when the hour moves: a horizon-to-zenith gradient, a warm glow low on the sun's side at dawn and dusk with a fainter wash all round, a faint glow round the moon. `Sky lights` (follows the camera, hidden for the orthographic camera): 1,500 additive stars that turn through the night and fade with the sun's elevation, a pale moon disc with three seas in a soft halo, an additive sun disc.

## Fires and lights

`FireLight` has no Update. `WorldLights.LateUpdate` runs every registered one: level (owner switch `On` and time of day: `Lerp(DayLevel, 1, Night)`, faded at 1.5 per second; a new light starts at its level), flicker (`Fire`: two octaves of noise; `Pulse`: slow sine, demons; `Steady`: marks and carried lights), the light's intensity and a small position jitter, its flame mesh, a soft additive glow disc on the ground (`GroundGlow`, 16 cm up so it never z-fights paths or marks), a halo billboard that grows with distance so the next camp's fires read as points of light up to 420 m, and five rising sparks for the six nearest fires within 38 m (one shared dynamic mesh).

What is lit (all by `LightDecorator`, twice a second, by component type and child name):

| Thing | Found as | Light |
|---|---|---|
| Camp hearth, both peoples | `SettlementView` with a `Fire` child | hearth light (3.2, 19 m, day level 0.35), off while V hides the fire (demon-held, overrun) |
| Camp entrances | same | a torch where each local path leaves the storehouse ring (a pair at your first camp) |
| Finished building | `BuildingView`, `ShownState == Active` | a torch by the door |
| Overrun camp | `Burning` child active | large fire light (2.6, 24 m, day level 0.7) |
| War camp | `WarCampView`, `WarFire` child | war fire light |
| Demons | `DemonView` | pulsing red glow and ground stain; V's additive spire is turned down to 30 % at night |
| Top "cast here" mark | `ProposalMarkerView`, `IsTop` | V's `Pillar` mesh is replaced by a slim soft beam (10 cm core in a 34 cm sleeve); a steady gold (blue for banish) light and ground glow |
| Dance fire | `DanceCircle.Current`, child `Fire light` | adopted: D still drives its intensity (flicker, beat pump, fire leap); L boosts it 1.45 times and to 21 m at night, keeps it per-pixel, and adds a ground glow and halo that follow the same pump |
| Home camp | `CampLayout` | braziers by Mira and beside the spawn, a torch at the prince's start, two torches at the dance circle's gate |
| The prince | `PrinceDirector.Instance.Prince` (when N lands) | a small carried light (`LightDecorator.CarryLight`) |
| The player | `WorldLights.Lantern` | a warm point light held high and ahead (0.9, 15 m); 30 % during an encounter |

The player's people burn warm (`WorldLights.WarmFire`); the Ash Clan's fires and torches are a cold green (`AshFire`, and a green flame mesh drawn in place of V's orange one).

### Light budget

Built-in forward rendering draws every object once more for each per-pixel light that reaches it.

- `WorldLights.PixelBudget` = 6 per-pixel point lights from this package at once, the lantern included. Every 0.2 s the fires within `LightCullDistance` (85 m) are ranked by distance, less 18 m per `Priority` step (0 torch, 1 hearth or brazier, 2 mark, overrun, war fire, 3 dance fire); the nearest five get a real light (`ForcePixel`), faded in and out over a third of a second.
- `VertexBudget` = 0. The ground is made of very large triangles; a per-vertex light near one corner floods the whole triangle, so fires beyond the budget are glow only (flame, ground glow, halo).
- One directional light. `QualitySettings.pixelLightCount` = 11: the six, the sun or moon, and room for the dance's four short drum flashes (D's lights, 3.5 m range, lit only while a cue is on a drum) and the duel's bolt.
- No point light casts shadows.

## Night readability

Three small additions to the World shaders (default 0, so nothing changes for materials that do not set them):

- `World/Lit` and `World/LitVertexColor`: `_SelfLit` (keeps this share of the albedo as emission) and `_Rim` (a rim colour), both multiplied by the global `_WorldNight`.
- `World/Unlit`: `_NightDim` (how far the colour is multiplied by the global `_WorldNightTint`, white by day).

`LightShapes.Apply(renderer, Read)` swaps a renderer's shared material for a cached night variant; `ApplyDim` does the same for unlit materials. Applied by the decorator:

| What | Variant |
|---|---|
| People (`LandFigure`: carriers, porters, gatherers, builders, warriors; every `PlaceholderActor`: Mira, Sol, dancers, villagers, the prince's cast) | `Person`: 22 % self-lit, cool rim |
| Loads on heads and backs, storehouse piles | `Load`: 40 % self-lit |
| Demons | `Demon`: 30 % self-lit, red rim; their eyes stay unlit |
| Stock items on a hex (trees, crops, sheep) | `Stock`: 15 % self-lit, so ripe (large) reads against young (small) |
| Number token disc, pips, stop cross | dimmed by the night tint to a soft cream: readable from far, not a white lamp |
| Ready flag, smoke, held ground | dimmed |
| Roll effects (`PaidBeam`, `PaidRing`, token `Halo`), demon spire | additive, made for daylight: turned down at night with a property block |

Also: a camp's `LocalPaths` mesh has overlapping coplanar strips, which a per-pixel fire light lights twice in stripes; each strip is lifted by its own fraction of a millimetre (up to 4 cm) once per mesh.

## For other packages

- A fire or torch: `WorldLights.Torch(parent, worldPos, ash, brazier)` (stand, flame, light), or `WorldLights.Add(parent, localPos, color, intensity, range, priority)` for a bare light with ground glow and halo. Set `On` from your own state; set `DayLevel`, `Look`, `GroundGlow`, `Halo`, `Sparks` as needed. Destroying the object unregisters it.
- A light you drive yourself: `WorldLights.Adopt(light, color, priority)`; L only budgets it, boosts it at night (`ExternalNightBoost`, `ExternalNightRange`) and dresses it.
- An emissive accent: use `MaterialFactory.CreateUnlit` or `CreateGlow` as before; if it glares at night, `LightShapes.ApplyDim(renderer, amount)`; to keep a lit object visible at night, `LightShapes.Apply(renderer, LightShapes.Read.Prop)` (or `Person`, `Load`, `Stock`).
- A character who must be followed in the dark: `LightDecorator.CarryLight(transform)`.
- Read the time: `DayNight.Instance.Hour`, `.Night`, `.State` (`IsNight`, `SunBelowHorizon`).
- Screen-space UI (speech bubbles, the HUD, the Tab map when P draws it as UI) is not lit and needs nothing. World-space text uses `World/Text`, which is unlit.

## Tests (`LightingTests`, PlayMode)

`NewGame_StartsAtNight_WithLightsOn` (sun 30 degrees or more below the horizon, moon on, one directional light, blue ambient, hearth, torches, Mira's brazier, dance fire and lantern lit, both camps lit, the Ash Clan's fire green; still full night three minutes of game clock later), `TimeOfDay_FollowsTheGameClock_AndHoldsWhilePaused`, `Lights_FadeAtDawn_AndComeBackAtDusk`, `LateGame_LightBudget_IsNotExceeded` (grown late game, every active camp visited at night), `Restore_FromSavedGameTime_GivesTheSameLighting` (pure function, then save and load during the dawn).

## Captures

`AgentScripts/LightCaptures.cs` reads `Temp/l_cmd.txt` (`open`, `hour H`, `grow TICKS MINE THEIRS`, `stockmix`, `at PLACE`, `moon`, `sun`, `god`, `fp`, `dance`, `shot NAME`, `info`) and renders the active camera at 1920x1080 into `Evidence/screenshots` (git-ignored). `shot` also reports mean luminance and the share of near-black and near-white pixels. IMGUI (the dance strip, panels) is not in these renders.
