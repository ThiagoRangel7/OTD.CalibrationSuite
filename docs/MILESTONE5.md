# Milestone 5: guidance and usability

Implemented after the user's **2026-10-04** request, with revisions on October 5 and 12. Device usability review remains pending.

## Current presentation

Setup keeps all 13 inputs visible in responsive groups, with nearby errors, capture totals, and clear primary actions. Smaller windows scroll. Review shows requested/measured values and preserves the accept/recapture/copy workflow.

At the physical crosshair, a one-DIP cyan arrow plots current reported tilt relative to the target's measured reference: X right, Y up. An unfilled circle shows the requested offset and tolerance. The guide scales to avoid screen edges/cards without changing the tolerance or moving the nib. Native WPF drawing reuses target geometry between changes.

Green/end caps indicate readiness; cyan and a progress arc indicate capture; gray/dotted lines indicate waiting. Local success/retry symbols and short hints keep the next action beside the crosshair. Success follows stored measurements, and the next pose retains its own readiness. Raw numbers and keyboard reference stay in corner cards.

Current defaults are 8 reported units of tilt tolerance, 200 ms, 20 reports, 0.5 mm position stability, and 3 reported units of tilt stability. Position-only capture explicitly asks users to hold upright and explains why tilt is blocked.

## Evidence and limits

The WPF verifier renders setup, capture cues/directions, and review at normal, small, and high-DPI sizes. Assertions cover the circle against the sampling gate, precise strokes, unobscured landmarks, reused-view resizing, keyboard capture, and mouse/touch suppression. Local synthetic update/layout measurements were around one millisecond or less; they do not measure hardware/GPU latency.

The user reported approximately `(5, -10)` tilt at perceived perpendicularity and near-zero readings with a slight lean. Parser/RPC checks found no added software offset. Current guidance uses the measured reference rather than assuming physical upright is raw zero. The device's physical zero and angular scale remain unverified and are not release gates.

Run the [development checks](../README.md#development-checks) and inspect PNGs in `artifacts/visual-checks`. Complete the [device workflow](RELEASE.md#final-device-check), especially target clarity/reachability, local cues, input responsiveness during contact, and enabled/bypassed correction. Historical 3D/annular-sector designs are superseded.
