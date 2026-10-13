# Milestone 4: capture and review

Implemented after the user's **2026-10-04** request and checkpointed as `2ec8a32`. The user then requested the visual work in milestone 5. Device workflow review remains pending.

The app captures one keyboard-triggered burst per pose. Sampling runs on the serialized report consumer; WPF displays snapshots. Per-report eligibility/tilt gates, duration/report minima, medians, stability, gap limits, and timeouts reject bad attempts without losing progress.

Unfinished drafts, completed candidates, and accepted data remain separate. Recapture copies a completed result and clears only the chosen pose/node. Only acceptance replaces accepted data. Copy produces its accepted snapshot and asks the user to paste/Save in OTD; closing loses app-session measurements.

Current capture supports absolute pen reports with optional tilt, using reported-coordinate targets. The earlier Huion-only physical-angle adapter and upright/origin constraints were removed. [SPEC](../SPEC.md#capture-and-review) contains the current rules and keyboard actions.

## Evidence

The initial warning-free Debug/Release builds passed 133 non-live cases each. Coverage includes pre-trigger exclusion, repeat events, pressure independence, duration/sample minima, timeout/stability boundaries, raw medians, recapture preservation, candidate/accepted ownership, and copied-data evaluation. Offscreen WPF setup/capture/review renders were inspected; later visual checks cover input handling and small/high-DPI layouts.

Synthetic results do not establish actual pen-contact responsiveness, target reachability, or correction accuracy. Use the [current device checklist](RELEASE.md#final-device-check) for resume, recapture, discard/accept/copy, Save, and recovery checks.
