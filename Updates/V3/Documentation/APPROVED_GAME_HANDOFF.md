# Cascade: integrated musical world

Open **Updates/V3** as the Unity project, using **Unity 6000.6.8f1**. The entry scene is `Assets/Scenes/CascadeGame.unity`. The earlier `CascadePrototype.unity` and standalone `CascadeDream/MiDreamworld.unity` are preserved.

The approved Figma home, settings, language selector, hero selector and world map are native uGUI controls. The 6×6 board uses the approved separate artwork and a live Dreamworld view above it. The selected Mi/Fa runs in place; scenery approaches the camera and exactly one companion sphere stays ahead of the runner. Pause and return to menu stop the world. No boost buttons were added.

## Rhythm

`RhythmGrid` derives quarter anchors near each track's nominal tempo, follows their local timing and subdivides into four ticks per quarter. The existing detected attack maps can already contain eighth-note attacks, so they are not blindly divided again. Original audio hashes and listening-review status are preserved.

All grid ticks are eligible simultaneously. The player can move freely between quarters, eighths and sixteenths, with no mode or streak prerequisite. Skipping ticks is allowed. Only one reward is granted for a tick. Original input-event timestamps, calibration, rests, pause/resume and non-overlapping hit windows remain in effect. The temporary +2 bonus lasts four quarter-note durations, not four sixteenth notes.

The moving visual marks use the same corrected DSP clock as hit judgement. Large marks are quarters, medium marks eighths and small marks sixteenths. A fixed frame shows the placement target; the player taps a board cell when a mark reaches it. Music-off mutes the source without stopping the clock. The guide does not require a streak and does not flash the whole screen.

## Settings and progress

Music, sounds, vibration, Russian/English and Mi/Fa selection persist in the existing checksum-protected save. Legacy volume and calibration are retained. A light confirmation sound is separate from music; iOS uses a short light haptic.

Record completion lights the home city's windows and marks an instrument in the local collection. Daily tasks count rhythm hits, cascades and completed tracks, resetting on the next local calendar day. These tasks do not invent currency or server rewards. The events entry displays an honest empty state.

The existing catalog's testing unlock flag is respected; test access is not converted into earned progress. Current eight tracks remain their existing 90-second testing edits.

## Mac and Git

The Git repository is the parent Cascade folder. Commit `Updates/V3/Assets` (including `.meta` files), `Packages`, `ProjectSettings`, and the relevant documentation/scripts. Unity's generated `Library`, `Temp`, `Logs`, `UserSettings`, and `Builds` are already ignored. No source assets are loaded from a Windows-only path at runtime.

On the Mac, pull the repository, open **Updates/V3** with the same Unity version and iOS Build Support, then build the integrated scene. `CascadeIOSBuild` now targets `CascadeGame.unity`. Unity exports the Xcode project; Xcode performs device signing/archive. This change does not upload a build or push Git automatically.

## Remaining release checks

- Support, terms and privacy URLs must be filled in `Assets/Cascade/Resources/Approved/service-links.json`. Empty URLs show a clear unavailable message; no fabricated legal text is shipped.
- Listen-review the automatically analysed musical timing on device, particularly tempo changes and Bluetooth latency. Calibration is available from pause.
- Profile the live world and both characters on the target phone. The approved Fa Rodin source still contains approximately one million triangles; Mi contains approximately 100,000. Desktop functional checks are not mobile performance certification.
- iOS native haptics and Xcode signing require a real Mac/device validation.

Validation outputs and actual gameplay screenshots are in `Documentation/IntegrationReview`. Tests use isolated temporary saves and preserve the user's progression.

## Verified on 11 November 2026

Core rhythm, bonus, audio/map integrity and save checks passed. Native Play Mode checks passed, including muted playback, the visible timing guide before the first hit, screen-coordinate input, pause/resume, both runners and three phone aspect ratios.

The Windows player built successfully with zero errors and two warnings about existing `DEVELOPMENT_BUILD` directives. The standalone player was launched and visually checked: home, level start, running Mi, moving world, a single companion sphere, timing marks and an actual scored board tap. The local executable is `Builds/DesktopReview/Cascade.exe`; generated builds remain excluded from Git.

No Mac/iOS device build has been verified in this session. No Git commit or push was performed.

## Sunset gameplay refinement

The gameplay composition follows the approved Sunset Figma frame (`40007724:529`): corner vinyl, pause icon, native 6×6 cells, warm neon rim and footer ribbons. The subsequent requested refinement removes the large gameplay wordmark, leaving a compact level/track header. Exact vector exports are retained in `Documentation/DesignSources/Gameplay`; their Unity textures and the original four-piece atlas are in `Resources/Approved/Gameplay`.

The live bridge blends into the matching violet UI background. The rhythm guide is a light strip without a separate panel. The existing world and runners remain animated; no boost controls were added.

The companion keeps its single moving world anchor 3.8 metres ahead of the runner. Its artwork is projected into the viewport and uses the identical sprite reference and charge as the next-piece preview and board pieces. This prevents world lighting or tone mapping from changing its colour. The old coloured mesh and its light are hidden in the integrated game only. All four charge appearances, board input, pause and both runners passed Play Mode checks; compact-phone gameplay was also captured. The update's Windows build is `Builds/SunsetReview/Cascade.exe`.

## Soft evening world

The latest integrated scene uses a blue/lavender dusk sky, reduced shadow strength and contrast, softer material highlights and atmospheric distance fog. Warm emissive strips run along both bridge edges; nearby lights softly illuminate the paving. `CascadeBridgeLightPool` follows the recycled bridge sections and enables at most eight local lights, without real-time shadow maps or runtime object spawning. Phone performance still needs device profiling.

The evening materials, sky and colour-grading profile are separate assets under `Assets/Cascade/Art/Evening`; the earlier standalone Dreamworld assets remain available. The initial evening preview is `Builds/EveningReview/Cascade.exe`.

## Clear evening refinement — 11 November 2026

Revisited the approved [Sunset gameplay frame in Figma](https://www.figma.com/design/YY1ORKTNJom7iOTRzw8RCb?node-id=40007724-529). The integrated world now uses a clear periwinkle/pink evening gradient, sparse light clouds and less distance fog. The gameplay wordmark remains removed.

Lantern glass uses a dedicated HDR shader: the previous URP material was serialized without an enabled emission keyword, making the bulbs appear unlit. The pooled point lights now coincide with the actual lantern centres (rather than positions near the floor), and illuminate the passing paving and runner. A separate camera-side fill on the character render layer reveals hair and clothing without flattening the bridge lighting. Warm edge strips remain secondary to the lamps.

`CascadeSoftScenery` bakes separate mesh assets for the integrated scene. Rounded paving has wider bevels and smaller gaps; foliage crowns have reduced small lobes, and welded smooth normals remove surface seams on foliage and cliffs. Triangle counts and material batching are retained. Original Dreamworld source meshes and prefab are unchanged. This is a visual refinement, not a mobile performance certification.

Play Mode checks passed with both runners, all four companion colours, input, pause/resume, three aspect ratios and bounded bridge lights through 80 simulated seconds of scenery recycling. Review captures are `IntegrationReview/clear-evening-mi.png`, `clear-evening-fa.png` and `clear-evening-compact.png`. The new Windows preview is built into `Builds/ClearEveningReview/Cascade.exe`.

The new Windows player built successfully (0 errors, 12 warnings) and was launched. Verified the actual running player with Fa, moving scenery, glowing lanterns and a progressing track timer. Mi was also visually verified in Play Mode. No Git commit or push was performed.

## Glass menus and balanced lighting — 11 November 2026

Approved menu buttons, captions and dialogs now use translucent plum surfaces, a narrow light rim and restrained reflections. These are native vector UI layers, without added drop shadows or screen-capture blur. The gold primary action remains more opaque for readability.

The pause panel is 564×572 in the legacy 720×1280 reference canvas, down from 660×1100. It sits below the runner, preserving the view of the bridge, and uses a lighter backdrop shade. Restart confirmation has its own smaller arrangement; result screens retain room for statistics. Existing confirmation and result-input guards remain in place.

Settings opened from pause hide the home backdrop and retain the actual paused world behind a compact 348×520 panel. Language and information views return through settings to the same paused attempt. Closing settings refreshes the pause labels in the selected language.

Lighting is a measured step between the previous versions: the key light is 0.95, the sky fill 0.56, and exposure 0.15. Ground and ambient midtones are slightly lighter; lantern emission is unchanged. The character still has a separate soft fill.

The latest local preview is `Builds/GlassMenuReview/Cascade.exe`. Screenshots include `IntegrationReview/glass-home.png`, `glass-pause.png`, `glass-pause-settings.png`, `glass-pause-compact.png` and `balanced-light-mi.png`.

Verified: all Play Mode checks passed, including settings/language navigation during pause, cancellation of restart without changing the board or clock, and refreshed pause labels. Windows build succeeded with 0 errors and 2 warnings. The standalone player was checked using actual clicks through play → pause → settings → pause → continue, then left paused for review. No Git commit or push was performed.

## Lantern-lit dusk and luminous rhythm guide — 11 November 2026

Supersedes the brighter GlassMenuReview lighting. The world now uses a lower sunset key (0.42), cooler sky fill (0.14), and darker ambient colours. Lanterns have warm HDR glass and 5.8-intensity local lights; bridge edge strips remain secondary. Dedicated rendering layers separate the character's camera fill and rim from the scenery, so a readable runner does not flatten the bridge's local illumination.

Nearby lantern intensities fade at the front/back of the camera's lighting range while the existing pool retains its limit of eight active lights. The PC and mobile URP assets allow eight additional lights per object to reduce discontinuities across the batched paving. Device performance still needs profiling; this change was validated on Windows.

Canopy and garden-stone meshes are baked into separate rounded copies, with smoother normals and unchanged topology. The integrated foliage uses a softer wrapped-light material with reduced texture grain. Runner materials are copied before reducing normal-map strength and gloss. The original Dreamworld prefab and source materials remain intact.

The rhythm guide now uses a luminous rail, a fixed gold receptor ring, and approaching round beads. Quarter notes are larger with a halo; eighths and sixteenths use smaller beads. Position still comes directly from the same DSP song clock and sixteenth grid. The ring's subtle highlight follows the existing hit window. The instruction is visible in Russian and English, with no overlap against the board. Free subdivision changes, muted play, glass menus and the single matching companion sphere remain unchanged.

All Play Mode checks passed, including readable guide text, isolated hero lighting, muted rhythm progression, board taps, all four companion appearances, both runners, pause/settings/language/confirmation, compact composition, and bounded lights through 80 simulated seconds. The Windows player built successfully with 0 errors and 14 warnings. The standalone player was launched and inspected during a Fa run: the timer and rhythm beads advanced, lamps and character were visible, and the compact glass pause menu worked. It was left paused for review.

Current preview: `Builds/LanternRhythmReview/Cascade.exe`.
Review captures: `IntegrationReview/lantern-rhythm-mi.png`, `lantern-rhythm-fa.png`, `lantern-rhythm-pause.png`.
Validation: `IntegrationReview/lantern-rhythm-checks.txt` and `lantern-player-build.txt`.
No Git commit or push was performed.


## Continuous rhythm light, growing to x10 — 11 November 2026

The integrated gameplay now has a continuous quarter-note light breath before the first tap, throughout active play and musical rests. The waveform uses the same calibrated DSP position and quarter anchors as the rhythm guide; switching between quarter/eighth/sixteenth taps affects amplitude, not pulse frequency or phase. Muting the track does not stop it.

Each successful tap increases the visual progress by 1 / (9 * hitsPerStep), reaching the cap when the actual multiplier first reaches x10 (27 hits with the current three-hit steps). Normal-mode peak light gain grows from +10% to +50%. Soft effects retains the same progression at one quarter of the normal amplitude. Increases are eased; a miss or streak timeout fades back to the baseline instead of disabling the pulse.

The shared signal drives lantern emission, bridge edge emission, the existing local-light pool and the board/orb outlines. The character's dedicated fill, sky and global exposure stay constant. Light intensity is always calculated from cached base values, so gains cannot accumulate. Pausing freezes both phase and growth; leaving the run clears the shared shader signal. Effects settings can be changed while paused without advancing the frozen phase.

Validation: core rhythm/integration checks passed, including every multiplier threshold, each-hit amplitude growth, cap, misses, transitions, rests, pause and soft mode. Play Mode checks verified a nonzero moving pulse with muted music and zero hits; an isolated real 27-hit Rhythm streak reached x10 and drove the bridge pool above +44% near its peak. Pause, menus, both runners, companion colours and light recycling also passed. Review fixtures do not alter the user's saved progression or effects preference.

Preview: `Builds/ContinuousPulseReview/Cascade.exe`. Windows build succeeded with 0 errors and 2 warnings. The standalone build was launched, checked during a no-input Fa run, and left paused for review.
Evidence: `IntegrationReview/continuous-pulse-checks.txt`, `continuous-pulse-play-checks.txt`, `pulse-player-build.txt`, `pulse-x10-peak.png` and `pulse-x10-trough.png` (isolated review streak).
No Git commit or push was performed.


## Restored screen and board aura — 11 November 2026

The approved screen was skipping the old V3 aura builder. Added an approved-layout implementation that restores two separate native UI meshes: a broad gold/pink halo beneath the board and a blue/violet aura at the physical display perimeter. The screen aura sits above the opaque world viewport, with a fully transparent centre; it does not depend on the safe-area inset. Both meshes ignore pointer events and sit below menu canvases.

Both halos use the existing continuous quarter-note pulse and per-hit x10 growth. Peak alpha grows from 0.16 to 0.42 for the screen edge and 0.28 to 0.95 for the board halo. Soft effects uses lower but still growing amplitudes. The existing thin rim and world lighting remain coordinated with the same clock. Pause freezes the effect and returning home clears it.

Play Mode checks passed for baseline halo activity without hits or music, real x10 growth, transparent centre, non-blocking input, pause, home cleanup, 828×1700 and 750×1334 physical screen coverage, and the existing gameplay/menu/world checks. Visually compared peak and trough renders. Evidence: `IntegrationReview/screen-field-aura-checks.txt`, `screen-field-aura-x10.png`, `screen-field-aura-trough.png`.

Current Windows preview: `Builds/ScreenFieldAuraReview/Cascade.exe` (0 errors, 2 warnings). The standalone version was opened and checked in gameplay, then paused. The user's in-progress attempt in the older ContinuousPulseReview window was preserved; that window was not closed. No Git commit or push.


## Escaping companion and musical energy absorption — 12 November 2026

The single companion now follows a smooth 4.2-second escape/return loop: a faster forward departure and a slower return, with gentle vertical and lateral motion. Its distance ahead of the runner varies from 3.8 to 6.0 world units; soft effects reduces the excursion to 65%. The position is calculated absolutely from the reset offset and scene clock, avoiding accumulated drift. The existing next-piece artwork, colour and projected contact halo follow the new position.

Every accepted manual placement launches a short curved ribbon with a small musical note from that cell toward the active runner's chest (spine fallback). It retains the placed charge colour even after the next piece changes. Travel takes 0.65–0.95 seconds, followed by a 0.30-second shrinking absorption ring and soft local light restricted to the runner's render layer. Rhythm hits make the stream brighter. Soft effects lowers brightness and removes the small motes. Occupied-cell taps and automatic board additions do not emit streams.

A fixed pool holds at most 16 streams. The shared effect clock freezes travel and absorption on pause; restart and leaving gameplay clear the effects. Existing board/screen aura, continuous x10 light growth, lighting, rhythm rules and menu layout remain intact.

Validation: core checks cover 180 simulated seconds of flight continuity, bounds, soft movement, charge preservation, live target tracking, expiry and stream reuse. All Play Mode checks passed, including actual placements, rejected occupied-cell input, mesh generation, pause/resume, hero-only light, both Mi and Fa, the existing rhythm/aura checks and compact layouts. Reviewed flight and absorption images; reduced the initial arrival light after visual inspection to preserve hair and clothing colour.

Current Windows preview: `Builds/CompanionEnergyReview/Cascade.exe` — succeeded with 0 errors and 2 warnings. Launched the standalone player, verified the moving companion and an actual placed sphere's absorption on Fa, and left this new window paused. Older player windows, including the user's unfinished attempt, were preserved. No Git commit or push.

Evidence: `IntegrationReview/companion-energy-checks.txt`, `companion-energy-play-checks.txt`, `energy-player-build.txt`, `music-energy-flight.png`, `music-energy-absorption.png` and `music-energy-fa.png`.

## Correction: 3D current from the companion to the runner — 12 November 2026

Supersedes the previous board-to-hero energy implementation. The user clarified that placing a sphere should only trigger the effect: its visible source must be the flying companion, and the flow must have volume. Removed `CascadeMusicEnergyGraphic` and its HUD overlay. No cell coordinates are used by the new effect.

`CascadeMusicEnergy3D` renders a pooled world-space mesh in the scene camera: a soft outer tube, luminous core, winding filaments and small 3D particles. The transparent additive shader retains depth testing, so the runner naturally occludes the flow as it enters the torso. The stream follows the live companion and chest/spine anchors along a curved path, and its trailing end continues entering after the leading end arrives. A gentle hero-only light accompanies absorption. The existing accepted-placement trigger, on-beat emphasis, soft-effects mode and moving companion are retained.

The old cell-to-character trails are entirely removed. The new geometry has no UI graphic or input collider. The fixed 16-flight pool and reusable mesh cap rapid-input load. Pause freezes the shared effect clock; restart and leaving gameplay clear geometry and light. Shader is included through Resources for standalone builds.

Validation: core checks passed, including 180-second companion motion, 3D mesh depth, supported shader, live anchors, expiry and bounded pool/mesh. All Play Mode checks passed for the companion as the exact emission origin, absence of the former HUD overlay, accepted/rejected board input, pause/resume, both Mi and Fa and existing rhythm/aura/menu checks. Visually reviewed flight and absorption; widened and bent the current to remain visible beside the orb and runner, and kept the trailing flow after arrival.

Current preview: `Builds/CompanionVolumeReview/Cascade.exe`, built successfully with 0 errors and 2 warnings. Opened the standalone version and verified an actual board tap. The user interacted with the new player during review; its resulting attempt was left untouched on the result screen. Older player windows were preserved. No Git commit or push.

Evidence: `IntegrationReview/companion-volume-checks.txt`, `companion-volume-play-checks.txt`, `volume-player-build.txt`, `companion-volume-flight.png`, `companion-volume-midflight.png`, `companion-volume-absorption.png`, `companion-volume-fa-flight.png`, `companion-volume-fa-absorption.png`.

## Mac / TestFlight source preparation — 12 November 2026

Prepared iOS candidate 1.0 (15), retaining fitness.quokka.caskade, Quokka team RUT46C38YA, portrait iPhone/iPad, IL2CPP Release and the approved CascadeGame scene. Added a Mac export script, source checkout verification, Unity release dependency validation and a current Russian build/upload guide. Old scene preview exporters now use project-relative Builds directories. Local Unity recovery scenes are excluded from Git.

Release checks passed: 667 scene/Resources dependencies with stable metadata; both character models and iOS plugins are present; the existing rhythm, audio, save and effect checks passed. The standalone source verifier passed for 1342 required files and 1070 unique GUIDs with exact path case. No Git LFS placeholders are required. Actual iOS export, Metal/IL2CPP compilation, signing and upload remain Mac-side steps; no iOS module is installed on this Windows editor.

Entry point for the Mac: Documentation/BUILD_ON_MAC.md and bash Tools/build-ios-mac.sh 15 from Updates/V3. The last documented published TestFlight build remains 1.0 (14).
