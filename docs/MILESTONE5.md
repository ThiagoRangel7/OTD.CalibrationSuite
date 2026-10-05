# Milestone 5 — visual guidance and UI usability

**Status: planned; implementation not started.** Requested on 2026-10-04 after reviewing the milestone 4 interface. The current implementation is checkpointed at `2ec8a32`. This brief records the next visual milestone; the current development UI still uses the milestone 4 presentation.

## User feedback and design intent

The setup window's instructions at the top are too long, and the full-screen capture window is too text-heavy to follow while aiming and tilting the pen. The user wants shorter instructions, clearer visual hierarchy, and a better layout. They explicitly clarified that this does **not** mean removing options, reducing the number of controls, or hiding relevant inputs. Preserve configuration access while making the screen easier to read.

## Setup window

- Keep the opening instruction brief and actionable. Place necessary setup reminders where they are relevant instead of stacking paragraphs above the controls.
- Give connection readiness, grid layout, pose settings, sampling/quality settings, capture count, and primary actions distinct visual roles. Use readable typography, grouped controls, aligned values, consistent units, and adequate spacing.
- Keep relevant calibration controls visible and discoverable in the setup layout, including the sampling/quality inputs currently grouped under Advanced. Do not replace them with a few presets or bury them in more collapsed sections. Smaller windows may scroll; controls must remain reachable and clearly labeled.
- Place validation feedback near the affected setting and keep the next action apparent. Differentiate Calibrate, Resume, Review, and Copy through hierarchy and current state while retaining all applicable actions.

## Full-screen guidance

- Give the physical aiming crosshair and orientation guide priority. Keep the aiming point clear of instructions, geometry, and status panels.
- Develop the user's concept of an arrow continuing from the pen tip toward a target point/sphere. The guide should show how the actual pen orientation differs from the requested pose without making the user translate angle numbers mentally.
- Define the scene in a concrete mockup/prototype: nib anchor, live arrow, requested target, view/camera, depth cues, and how alignment appears. A reference plane, shadow, or other depth cue may help; these details are design decisions to evaluate, not an already approved scene.
- Explicitly resolve arrow direction. An arrow continuing beyond the nib follows eraser-to-tip, whereas spec azimuth describes tip-to-eraser. Document and test that visual reversal so moving the real pen produces an intuitive response. Use the established guidance projection and retain raw tilt for measurements and correction.
- Make ready, capturing, accepted, waiting, and retry states distinct with shape/icon/progress cues as well as color. A ready pose must not look like an accepted measurement. Show compact burst and overall progress, brief keyboard hints, and one short contextual failure reason.
- Keep required live angles/position/tilt/error legible in a secondary region. Long explanatory prose should not compete with the guide. Existing keyboard actions, capture gates, retries, and data-preservation behavior remain the functional baseline.

## Implementation and verification

Use the existing WPF presentation and capture snapshots where practical. Justify any rendering dependency; do not introduce a second orientation model or move acquisition/sampling onto the UI thread. Carry the same hierarchy into review/accept/copy feedback where useful.

Retain the milestone 4 regression tests. Add focused tests for new visual projection/state behavior where they catch real errors. Inspect normal and small/high-DPI layouts for target occlusion, text clipping, visible controls, contrast, and ambiguous 3D poses. Check upright, all cardinal directions, diagonals, and behavior near upright. Verify keyboard responsiveness during contact and that stylus/mouse/touch input does not activate capture UI controls.

User review should establish that settings are easy to find, instructions are short enough to act on, and the arrow/target scene makes the requested motion clear. Revisit the [milestone 4 review checklist](MILESTONE4.md#user-review) through the improved UI. Known-angle scale, diagonal physical conversion, capture defaults, and actual corrected-pointer behavior still need device evidence; the checkpoint commit and visual plan do not establish them.

Final requirement audit, package distribution, licensing, installation/update/removal, and settings/preset verification move to **milestone 6** with their existing scope.
