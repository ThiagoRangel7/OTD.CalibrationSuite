# Calibration Suite - Specification (OpenTabletDriver Plugin, PreTransform)

This document specifies **Calibration Suite**, an OpenTabletDriver (OTD) plugin for **precision-first calibration of display tablets**. It calibrates both **position** and **tilt**, then applies those calibration results in real time via a lightweight filter.

Calibration Suite has two parts that must work together:

- **Calibrator**: the calibration component that runs the workflow (sampling, gating, solving) and owns the full-screen WPF **Calibration Window**.
- **Correction Filter**: an OTD pipeline element (**Filter plugin**) that runs on every pen report and applies only the real-time correction math.

Terminology is intentionally strict to avoid ambiguity:

- **Correction Filter** = OTD *Filter plugin* that transforms pen reports in real time.
- **Calibrator** = calibration component (workflow + sampling + solve + calibration JSON generation).
- **Calibration Window** = full-screen WPF window shown during calibration.
- **External calibrator app** = a separate process that hosts the Calibrator.

---

## 1. Overview

Calibration Suite performs two end-to-end processes:

**Position Calibration**  
Capture an $R\times C$ grid of position error across the tablet surface, then apply an interpolated correction field at runtime.

**Tilt Calibration**  
Using the current working position stage as the baseline, capture tilt errors for a set of tilt poses at each node, solve per-node tilt coefficients, then apply interpolated tilt correction at runtime.

Every pen report must be corrected in this order:

1. Position correction (interpolated grid error field)
2. Tilt correction (interpolated coefficients)
3. Output corrected position

The two stages are independent in data layout but always present at runtime. Calibration Suite therefore works with one complete calibration data object that contains both a position section and a tilt section. When a section has not yet been calibrated, its stored values are zero, so that section becomes a no-op.

This gives four practical states without changing the runtime data model:

- default position + default tilt
- calibrated position + default tilt
- default position + calibrated tilt
- calibrated position + calibrated tilt

Completing Position Calibration updates the current working calibration immediately. The position section is replaced with the newly computed values, and the tilt section is reset to zero so that no previous tilt stage remains associated with the new position stage. If the user later chooses to run Tilt Calibration, the new tilt stage is computed on top of that updated position stage.

### PreTransform-first design

This specification uses **pre-transform coordinates** (raw tablet units) as the authoritative space for both calibration data and runtime correction. The Correction Filter runs at `PipelinePosition.PreTransform` and modifies `ITabletReport.Position` before OTD applies the output-mode transform (tablet → screen).

The position stage always computes a corrected position $p_1$ from the raw position $p_0$. The tilt stage always computes a final corrected position $p_2$ from $p_1$. When either stage is still in its default all-zero state, that stage produces zero correction and therefore becomes a no-op.

The Calibration Window is still drawn in pixels on a display because the user must see targets and pose guidance. Pixels are **UI-only**. Samples, calibration data, and runtime correction are all defined in pre-transform digitizer units.

---

## 2. Components

The Calibrator is responsible for:

- showing the main screen and the Calibration Window
- guiding the user through nodes and poses
- collecting samples at high frequency using raw tablet reports
- validating captures with gates and improving robustness via burst + median
- solving tilt coefficients once per calibration
- maintaining the current **working calibration**
- producing finalized calibration JSON

The Correction Filter is responsible for:

- loading persisted calibration JSON from OTD settings
- applying it per report with minimal overhead
- treating zero/default data as identity correction

### Hosting model

The Calibrator is hosted as an **external WPF calibrator app** (separate process). The Correction Filter remains an in-process OTD Filter plugin element inside the daemon pipeline.

The Calibrator communicates with OTD through the daemon RPC surface and the tablet debug stream. The final calibration JSON is copied by the user into the Calibration Suite plugin settings in OTD. OTD remains the owner of normal settings persistence.

### Why the Calibrator must sample raw reports

OTD’s daemon debug stream provides clones of **raw device reports**. Therefore:

- the Calibrator samples **raw** positions and tilt inputs
- the Correction Filter applies corrections in **raw** (PreTransform)
- screen pixels are used only for rendering the UI

### Working calibration

The Calibrator always operates on one complete **working calibration** in memory.

At startup, the Calibrator loads the current Calibration Suite calibration JSON from OTD settings. If no calibration JSON exists yet, or if it is empty, the Calibrator constructs full default calibration data with zero position offsets and zero tilt coefficients.

Every calibration action updates this working calibration:

- **Calibrate Position** replaces the position section and resets the tilt section to zero
- **Calibrate Tilt** replaces only the tilt section and preserves the current position section

The user may therefore calibrate position, return to the main screen, then calibrate tilt later in the same session without first persisting the intermediate JSON to OTD.

---

## 3. Glossary

The terms below are used consistently throughout this document. They are defined here once so later sections can stay concise.

**Working calibration**: the complete calibration data object currently held by the Calibrator in memory. It always contains both a position section and a tilt section, and it is updated immediately when a calibration step completes successfully.

**Calibration JSON**: the JSON representation of the current calibration data. It is copied into OTD settings and then consumed by the Correction Filter at runtime.

**Default section**: a position or tilt section whose stored values are all zero. At runtime, a default section behaves as a no-op.

**$p_0, p_1, p_2$**: pointer positions at successive runtime stages, in **pre-transform (raw)** units. $p_0$ is raw (no correction), $p_1$ is after the position correction stage, and $p_2$ is after both position and tilt correction.

**Position stage**: the runtime stage that subtracts interpolated position error from $p_0$ to produce $p_1$.

**Tilt stage**: the runtime stage that subtracts predicted tilt error from $p_1$ to produce $p_2$.

**Origin Point ($T_i$)**: the target position currently shown by the Calibrator for the active node (the point the user aims at). In formulas the same concept is written as $T_i$.

**Node**: one point in the $R\times C$ grid across the tablet surface. Each node stores one position error vector $e_{\mathrm{pos},r,c}$ and one set of tilt coefficients $(K_{x2},K_{y2},K_x,K_y,K_{xy})$.

**Position error ($e_{\mathrm{pos}}$)**: the measured position error at a node during Position Calibration:

$$
e_{\mathrm{pos}} = p_0 - T_i
$$

Runtime subtracts an interpolated position error field to produce $p_1$.

**Tilt error ($e_{\mathrm{tilt}}$)**: the measured error used for Tilt Calibration:

$$
e_{\mathrm{tilt}} = p_1 - T_i
$$

This is the error observed at the node target after the position correction stage has already been accounted for, and is the quantity the tilt model learns to predict.

**Predicted tilt error ($\hat e_{\mathrm{tilt}}$)**: the tilt model's predicted tilt error from raw tilt inputs (and coefficients interpolated at the current position). Runtime subtracts it from $p_1$ to produce $p_2$.

**Pose**: a target pen orientation used during Tilt Calibration, defined by a pair of angles $(\mathrm{Azimuth}, \mathrm{Altitude})$.

**Azimuth**: angle (in the screen plane) of the pen axis vector (tip→top) projected onto the screen; $0^\circ$ points to $+X$ (right), and angles increase clockwise (toward $+Y$ down).

**Altitude**: angle from the screen plane toward vertical. $90^\circ$ is vertical; smaller means more tilt.

**Capture**: one user-triggered acquisition (via `Space`) that validates pose (gate), collects a burst of raw reports, and stores medians.

**Gate**: a boolean predicate on pose that must remain true during a capture. If it fails during capture, the capture is discarded and must be retried.

**Sample**: a single raw device report reading (position/tilt) taken from the same report stream used by the Correction Filter.

### Derived tilt angles

The raw tilt inputs used by Calibration Suite are $\mathrm{TiltX}$ and $\mathrm{TiltY}$ in degrees. The Calibrator derives azimuth and altitude from those values for display, gating, and tilt-pose guidance.

Let

$$
t_x^{(r)} = \mathrm{TiltX}\cdot\frac{\pi}{180}, \qquad
t_y^{(r)} = \mathrm{TiltY}\cdot\frac{\pi}{180}
$$

where the superscript $(r)$ denotes radians.

#### Azimuth

Azimuth is computed from the tangent of the two tilt axes:

$$
\mathrm{Azimuth} =
\operatorname{atan2}\!\bigl(\tan(t_y^{(r)}), \tan(t_x^{(r)})\bigr)\cdot\frac{180}{\pi}
$$

If the resulting angle is negative, add $360^\circ$.

The final azimuth range is therefore:

$$
0^\circ \le \mathrm{Azimuth} < 360^\circ
$$

#### Altitude

First define

$$
r = \sqrt{\tan^2(t_x^{(r)}) + \tan^2(t_y^{(r)})}
$$

Then altitude is:

$$
\mathrm{Altitude} =
\begin{cases}
90^\circ, & r = 0 \\
\operatorname{atan}\!\left(\frac{1}{r}\right)\cdot\frac{180}{\pi}, & r > 0
\end{cases}
$$

The final altitude range is:

$$
0^\circ \le \mathrm{Altitude} \le 90^\circ
$$

A perfectly vertical pen therefore has:

$$
\mathrm{TiltX}=0,\quad \mathrm{TiltY}=0,\quad \mathrm{Altitude}=90^\circ
$$

In that special case, azimuth is set to $0^\circ$.

Whenever azimuth is compared against a target pose, the comparison must use circular wraparound. For example, $359^\circ$ and $1^\circ$ differ by $2^\circ$, not $358^\circ$.

---

## 4. Targets

### Coordinate space

All targets, samples, and corrections are defined in **pre-transform coordinates** (raw tablet units). This includes target coordinates, sampled pointer coordinates, interpolation math, and corrected output.

The Calibration Window is rendered in pixels, but pixels are UI-only.

### Calibration condition

Calibration is performed for the **full tablet screen and full tablet surface** under **absolute mapping**. The target grid is therefore defined over the full raw digitizer range, while still using a physical inset for usability on the tablet's screen.

Because runtime correction happens in PreTransform, later OTD mapping changes such as rotation, output-region changes, or use of only a sub-area still occur after the correction stage.

### Node placement

Position and tilt share the same $R\times C$ Origin Points. Targets must not be placed on the physical border. The default grid is still $3\times3$, but the data model and runtime interpolation are defined for general $R\times C$ grids.

Let $W_{mm},H_{mm}$ be the physical digitizer width and height in millimeters. Define the inset margin:

$$
m_{mm} = \mathrm{GridInsetMm}
$$

Let $W_{raw},H_{raw}$ be the full digitizer range in pre-transform digitizer units. Define the corresponding pre-transform digitizer margins:

$$
m_x=\frac{m_{mm}}{W_{mm}}\,W_{raw},\qquad m_y=\frac{m_{mm}}{H_{mm}}\,H_{raw}
$$

Define column and row lines for $c\in\{0,\dots,C-1\}$ and $r\in\{0,\dots,R-1\}$:

$$
X_c = m_x + \frac{c}{C-1}\left(W_{raw}-2m_x\right),\qquad
Y_r = m_y + \frac{r}{R-1}\left(H_{raw}-2m_y\right)
$$

Nodes are visited in deterministic order top-left to bottom-right. Only the active node target is visible.

The inset is intentional: it reduces the chance of off-screen targets when raw cursor offset is large, and it avoids fitting extreme edge distortion that a low-order interpolation field cannot represent well. Defining the inset in millimeters keeps margin behavior consistent across devices with different DPI but similar physical size.

---

## 5. Calibrator

The Calibrator owns the main screen and the full-screen WPF Calibration Window.

The window must be fullscreen opaque, update many times per second with minimal latency, and stay responsive regardless of pen contact.

### Main screen

The main screen is intentionally simple. It shows the current working calibration state and whether that working state matches what is currently stored in OTD.

It displays:

```text
CALIBRATION STATUS
  Position: Default / Calibrated
  Tilt: Default / Calibrated

OTD SETTINGS
  Up to date / Pending changes
```

The two calibration-state labels are derived entirely from the current working calibration:

- **Position: Default** if all stored position offsets are zero
- **Tilt: Default** if all stored tilt coefficients are zero

Otherwise the corresponding state is **Calibrated**.

`OTD SETTINGS` compares the current working calibration against the calibration JSON currently stored in OTD:

- **Up to date** when they are equal
- **Pending changes** when they differ

The main screen provides these actions:

- **Calibrate Position**
- **Calibrate Tilt**
- **Copy Calibration**
- **Check OTD Status**

When `OTD SETTINGS` is `Pending changes`, the main screen shows helper text instructing the user to copy the updated calibration JSON into OTD.

`Check OTD Status` compares the current working calibration against the calibration JSON currently stored in OTD and updates only the status line. It does not overwrite the working calibration.

The Calibrator may also perform this same comparison automatically at appropriate times, such as when the window regains focus or the main screen becomes active again.

### Calibration Window

Only the current active node target is drawn. During tilt steps, the window must also show the target pose and the current pose (azimuth/altitude).

### Rendering

The Calibrator samples raw reports and renders target points computed from $T_i$ in raw coordinates.

When rendering to pixels, the Calibrator must apply the same absolute mapping used by OTD for the current full-screen tablet configuration. Rendering must not use OS cursor APIs as the source of truth for sampling.

### Info Box

The Info Box must remain visible while the window is open. It displays, with 1 decimal place:

- pointer position: $p_{\mathrm{step}}$
- raw tilt axes: $\mathrm{TiltX}, \mathrm{TiltY}$
- derived angles: $\mathrm{Azimuth}, \mathrm{Altitude}$
- error: $e_x, e_y$, defined as

  $$
  (e_x, e_y) = p_{\mathrm{step}} - T_i
  $$

where $T_i$ is the current Origin Point. $p_{\mathrm{step}}$ is the pointer position used by the current step: $p_0$ during Position Calibration, and $p_1$ during Tilt Calibration.

### Input rules

The Calibration Window must ignore stylus, mouse, and touch interactions. Pen contact must not be interpreted as click, drag, or hold, and must not freeze or throttle sampling or rendering. Keyboard is the only control surface.

Keyboard mappings:

- `Space`: attempt capture (subject to capture rules)
- `Backspace`: redo previous step (go back one node/pose and recapture)
- `R`: soft reset the current calibration section only
  - if calibrating position: restart Position Calibration at the first node
  - if calibrating tilt: restart Tilt Calibration at the first node or pose
- `Esc`: close the window without modifying the current working calibration unless the session completed successfully

A reset-persisted-calibration action must exist in the OTD UI.

---

## 6. Position Calibration

Position Calibration is a tablet-surface correction of **where the cursor lands after mapping**. It measures how far the raw pointer position is from known targets across the configured $R\times C$ grid, treats those measurements as samples of a smooth error field, and then subtracts an interpolated error from every report at runtime.

In other words: the Correction Filter applies **pointer position minus the estimated position error**.

Position Calibration includes both capturing position errors in the Calibrator and applying them in the Correction Filter.

### What is measured

For each grid node, the Calibrator displays the current target point $T_i$ and instructs the user to hold the pen upright (altitude near $90^\circ$). When the user triggers a capture (Section 8), the Calibrator records one position-calibration sample for that node.

Define the measured position error at that node as:

$$
e_{\mathrm{pos}} = (e_x,e_y) = p_0 - T_i
$$

For an $R\times C$ grid, we store one error vector per node:

$$
e_{\mathrm{pos},r,c},\qquad r\in\{0,\dots,R-1\},\ c\in\{0,\dots,C-1\}
$$

### Runtime

At runtime, the Correction Filter must estimate a position error for any raw position, but calibration only provides errors at discrete grid nodes. We treat the node errors $e_{\mathrm{pos},r,c}$ as samples of a smooth **position error field** and reconstruct the error at an arbitrary point by **bilinear interpolation** between surrounding nodes.

Because nodes are inset by a margin, the grid covers only the rectangle $[X_0, X_{C-1}]\times[Y_0, Y_{R-1}]$. When the pen is near physical edges (outside that inset), the filter clamps into the grid domain before interpolating. This avoids extrapolation and keeps behavior stable.

Let $p_0=(x,y)$. Clamp into the grid domain:

$$
x_c=\mathrm{clamp}(x,\ X_0,\ X_{C-1}),\qquad y_c=\mathrm{clamp}(y,\ Y_0,\ Y_{R-1})
$$

Choose the cell indices $a,b$ such that:

$$
X_a \le x_c \le X_{a+1},\qquad
Y_b \le y_c \le Y_{b+1}
$$

Normalized coordinates inside the selected cell:

$$
u=\frac{x_c-X_a}{X_{a+1}-X_a},\qquad
v=\frac{y_c-Y_b}{Y_{b+1}-Y_b}
$$

Bilinear interpolation of the position error:

$$
e_{\mathrm{pos}}(u,v)=(1-u)(1-v)e_{\mathrm{pos},a,b}+u(1-v)e_{\mathrm{pos},a+1,b}+(1-u)v\,e_{\mathrm{pos},a,b+1}+uv\,e_{\mathrm{pos},a+1,b+1}
$$

Apply Position Calibration:

$$
p_1 = p_0 - e_{\mathrm{pos}}(u,v)
$$

This $p_1$ is the input position used by Tilt Calibration and by runtime tilt correction.

### Working-calibration effect

When Position Calibration completes successfully, the Calibrator updates the position section in the current working calibration and resets all tilt coefficients to zero. Returning to the main screen therefore yields:

- `Position: Calibrated`
- `Tilt: Default`
- `OTD SETTINGS: Pending changes`

until the user updates OTD with the new calibration JSON.

---

## 7. Tilt Calibration

Tilt Calibration is a tablet-surface correction of tilt-induced **tilt error**. It measures the remaining error at many tilt poses across all grid nodes, fits per-node coefficients for a shared tilt model, and subtracts the model-predicted tilt error at runtime.

In other words: the Correction Filter applies **$p_2 = p_1 - \hat e_{\mathrm{tilt}}$**, where $\hat e_{\mathrm{tilt}}$ depends on raw tilt inputs and on position.

Tilt Calibration includes capturing pose samples in the Calibrator, solving coefficients once, and applying tilt correction in the Correction Filter.

Tilt Calibration always runs against the current working position calibration. If the current position field is still default zero, then the position stage is a no-op and tilt is calibrated on top of $p_1=p_0$.

### What is measured

During tilt collection, the tilt stage remains zero while the Calibrator forms the residual against the current working position stage. The goal is to measure **tilt error** at many poses and then learn how that error depends on raw tilt inputs.

At a given node target $T_i$, define the tilt error for a captured pose sample as:

$$
e_{\mathrm{tilt}} = (e_x,e_y) = p_1 - T_i
$$

Each pose sample stores the pair of raw tilt inputs $(t_x,t_y)=(\mathrm{TiltX},\mathrm{TiltY})$ together with the measured tilt error $(e_x,e_y)$.

Because the Calibrator samples raw reports, it computes $p_1$ locally from $p_0$ using the position field currently present in the working calibration. In other words, the Calibrator first removes the error already explained by the current position stage, then fits the tilt model to the remaining error. If the current position field is still default zero, then this step leaves $p_1=p_0$.

### Poses

At each node, capture 16 poses:

- altitude $= 65^\circ$, azimuth $\in \{0,45,90,135,180,225,270,315\}$
- altitude $= 45^\circ$, azimuth $\in \{0,45,90,135,180,225,270,315\}$

The Calibrator shows both target and current azimuth and altitude. Capture gating for these poses uses $\mathrm{AngleToleranceDeg}$ and is defined in Section 8.

### Model

We capture tilt error at only a finite set of poses (16 per node). At runtime, however, the pen can take essentially any azimuth and altitude. The tilt correction model is a compact function fitted from those sparse samples so the filter can predict tilt error continuously across orientations instead of correcting only the exact poses that were measured.

We use the same model form everywhere; only the coefficients vary across the tablet surface.

Runtime applies tilt correction by subtracting predicted tilt error:

$$
p_2 = p_1 - \hat e_{\mathrm{tilt}}(t_x,t_y)
$$

Each node has 5 coefficients:

$$
K_{x2},\ K_{y2},\ K_x,\ K_y,\ K_{xy}
$$

Given raw tilt $(t_x,t_y)$, the model predicts the tilt error components:

$$
\hat e_{\mathrm{tilt},x}(t_x,t_y) = K_{x2}\,t_x|t_x| + K_x\,t_x - K_{xy}\,t_x|t_y|
$$

$$
\hat e_{\mathrm{tilt},y}(t_x,t_y) = K_{y2}\,t_y|t_y| + K_y\,t_y - K_{xy}\,t_y|t_x|
$$

### Solve

We solve one coefficient set per grid node using all captured samples, while also encouraging coefficients to vary smoothly across the tablet surface.

There are $5RC$ unknowns total (5 per node across an $R\times C$ grid). Choose them so that:

1) **Data fit**: for every captured pose sample, the model prediction $\hat e_{\mathrm{tilt}}$ is close to the measured $e_{\mathrm{tilt}}$.  
2) **Neighbor similarity**: adjacent nodes have similar coefficients.  
3) **Small-coefficient preference**: coefficients stay small unless the data clearly requires otherwise.

This is a single linear least-squares solve, because the model is linear in the coefficients.

Each captured sample contributes two linear equations (one for $e_x$, one for $e_y$) for the node where it was captured:

$$
e_x \approx K_{x2}\,t_x|t_x| + K_x\,t_x - K_{xy}\,t_x|t_y|
$$

$$
e_y \approx K_{y2}\,t_y|t_y| + K_y\,t_y - K_{xy}\,t_y|t_x|
$$

To add neighbor similarity, include additional equations that penalize differences between adjacent nodes. For each adjacent node pair $(i,j)$ and each coefficient type, add a row:

$$
K^{(i)} - K^{(j)} \approx 0
$$

All neighbor-difference rows are weighted by $\sqrt{\lambda}$.

To add small-coefficient preference, include additional equations that penalize coefficient magnitude. For each node and each coefficient type, add a row:

$$
K^{(i)} \approx 0
$$

All magnitude rows are weighted by $\sqrt{\mu}$.

Solve using a numerically stable method such as QR or SVD.

#### System

Stack all unknown coefficients (all nodes) into one vector $\mathbf{k}$ ($5RC$ unknowns total). Stack all sample equations into:

$$
A\,\mathbf{k} \approx \mathbf{b}
$$

where:

- $\mathbf{k}$ is the vector of **unknown coefficients** (all $5RC$ values: 5 coefficients per node across the full grid).
- $A$ is a large matrix of **known numbers** built from the captured data. Each row represents one of the equations above, filled with terms computed from the measured tilt inputs (things like $t_x|t_x|$, $t_x$, $t_x|t_y|$) and with columns corresponding to the coefficients for the correct node.
- $\mathbf{b}$ is the vector of **measured outputs** (the stacked measured errors $e_x$ and $e_y$ from the samples).

To stabilize the solve, we add two additional penalties:

- **Neighbor similarity**: adjacent nodes should have similar coefficients. Let $D$ be a sparse difference matrix such that $D\,\mathbf{k}$ is the list of coefficient differences between grid neighbors. Penalizing $\lVert D\,\mathbf{k} \rVert^2$ encourages smooth coefficient variation across the screen.
- **Small-coefficient preference**: keep coefficients small unless data forces them larger. This is the standard ridge term $\lVert \mathbf{k} \rVert^2$.

The final objective is:

$$
\min_{\mathbf{k}}\; \lVert A\,\mathbf{k} - \mathbf{b} \rVert^2 
+ \lambda\, \lVert D\,\mathbf{k} \rVert^2 
+ \mu\, \lVert \mathbf{k} \rVert^2
$$

In code, this is implemented by appending extra rows and solving a single augmented least-squares system:

$$
\begin{bmatrix}
A \\
\sqrt{\lambda}\,D \\
\sqrt{\mu}\,I
\end{bmatrix}
\mathbf{k}
\approx
\begin{bmatrix}
\mathbf{b} \\
\mathbf{0} \\
\mathbf{0}
\end{bmatrix}
$$

Here $I$ is the identity matrix (it selects each coefficient directly), and $\mathbf{0}$ are zero vectors of matching size.

### Runtime

At runtime, tilt correction is applied after position correction.

For each report, the Correction Filter does:

1) Compute $p_1$ using the position error field.  
2) Interpolate the 5 tilt coefficients at the position $p_1$ using the same cell selection and $(u,v)$ logic as position.  
3) Evaluate the tilt model to obtain the predicted tilt error.  
4) Subtract the predicted error to produce $p_2$.

Interpolate coefficients at $p_1$ to obtain:

$$
K_{x2}(p_1),\ K_{y2}(p_1),\ K_x(p_1),\ K_y(p_1),\ K_{xy}(p_1)
$$

Evaluate the predicted tilt error:

$$
\hat e_{\mathrm{tilt},x} = K_{x2}(p_1)\,t_x|t_x| + K_x(p_1)\,t_x - K_{xy}(p_1)\,t_x|t_y|
$$

$$
\hat e_{\mathrm{tilt},y} = K_{y2}(p_1)\,t_y|t_y| + K_y(p_1)\,t_y - K_{xy}(p_1)\,t_y|t_x|
$$

Apply Tilt Calibration:

$$
p_2 = p_1 - (\hat e_{\mathrm{tilt},x},\hat e_{\mathrm{tilt},y})
$$

### Working-calibration effect

When Tilt Calibration completes successfully, the Calibrator replaces only the tilt section in the current working calibration and preserves the current position section. Returning to the main screen therefore yields:

- `Position: Default` or `Calibrated`, depending on the working position field
- `Tilt: Calibrated`
- `OTD SETTINGS: Pending changes`

until the user updates OTD with the new calibration JSON.

---

## 8. Capture

A **capture** is the atomic data-collection action used by both Position Calibration and Tilt Calibration. It is triggered only by the keyboard (`Space`). Each successful capture yields exactly one sample for the current step (position node or tilt pose) that is:

- valid (passes the gate for the current step), and
- robust to noise (computed as the median of a short burst of raw reports)

If the gate fails at any point during the capture, the capture is discarded and the Calibrator prompts the user to retry.

### Trigger

- `Space`: start a capture attempt for the current node or pose.

### Gate

A **gate** is a condition on pen pose that must remain true during the entire capture window.

- Position Calibration gate: altitude within $\mathrm{AngleToleranceDeg}$ of $90^\circ$.
- Tilt Calibration gate: azimuth and altitude each within $\mathrm{AngleToleranceDeg}$ of their pose targets.

Gate checks use raw sensor values and the derived-angle formulas defined in Section 3 (no smoothing/filtering/prediction).

### Sampling

While the capture is active, the Calibrator collects a burst of raw tablet reports. For each quantity needed by the current step (for example $x$, $y$, $t_x$, $t_y$, azimuth, altitude), the captured value is the **median** over the burst.

The live Info Box continues to display the latest raw values. Only the stored sample uses the median.

A capture ends only when both conditions are satisfied:

- elapsed time $\ge \mathrm{CaptureWindowMs}$, and
- samples collected $\ge \mathrm{MinSamplesPerCapture}$

This means that, depending on report rate:

- if reports arrive **slowly**, the burst may run **longer than** $\mathrm{CaptureWindowMs}$ until $\mathrm{MinSamplesPerCapture}$ samples are collected
- if reports arrive **quickly**, the burst may collect **more than** $\mathrm{MinSamplesPerCapture}$ samples, but it still continues until elapsed time $\ge \mathrm{CaptureWindowMs}$

### Responsiveness

Samples must be taken from the same raw report stream the Correction Filter uses, to preserve precision and avoid UI or input throttling side effects.

The Calibration Window must remain responsive and keep updating even while the pen touches the screen. Pen contact must not be interpreted as UI input.

---

## 9. Persistence

Calibration Suite persists one complete calibration JSON document in OTD settings. This calibration JSON is the runtime source of truth for the Correction Filter.

The calibration JSON always contains both a position section and a tilt section. A section that has not yet been calibrated is represented by all-zero values, not by an omitted block.

### Calibration JSON structure

The persisted calibration JSON has this structure:

```json
{
  "format": "CalibrationSuite",
  "version": 1,
  "grid": {
    "preTransformRowCoordinates": [37.5, 250.0, 462.5],
    "preTransformColumnCoordinates": [50.0, 500.0, 950.0]
  },
  "positionOffsets": [
    [{ "x": 0.0, "y": 0.0 }, { "x": 0.0, "y": 0.0 }, { "x": 0.0, "y": 0.0 }],
    [{ "x": 0.0, "y": 0.0 }, { "x": 0.0, "y": 0.0 }, { "x": 0.0, "y": 0.0 }],
    [{ "x": 0.0, "y": 0.0 }, { "x": 0.0, "y": 0.0 }, { "x": 0.0, "y": 0.0 }]
  ],
  "tiltCoefficients": [
    [{ "kx2": 0.0, "ky2": 0.0, "kx": 0.0, "ky": 0.0, "kxy": 0.0 },
     { "kx2": 0.0, "ky2": 0.0, "kx": 0.0, "ky": 0.0, "kxy": 0.0 },
     { "kx2": 0.0, "ky2": 0.0, "kx": 0.0, "ky": 0.0, "kxy": 0.0 }],
    [{ "kx2": 0.0, "ky2": 0.0, "kx": 0.0, "ky": 0.0, "kxy": 0.0 },
     { "kx2": 0.0, "ky2": 0.0, "kx": 0.0, "ky": 0.0, "kxy": 0.0 },
     { "kx2": 0.0, "ky2": 0.0, "kx": 0.0, "ky": 0.0, "kxy": 0.0 }],
    [{ "kx2": 0.0, "ky2": 0.0, "kx": 0.0, "ky": 0.0, "kxy": 0.0 },
     { "kx2": 0.0, "ky2": 0.0, "kx": 0.0, "ky": 0.0, "kxy": 0.0 },
     { "kx2": 0.0, "ky2": 0.0, "kx": 0.0, "ky": 0.0, "kxy": 0.0 }]
  ]
}
```

### Meaning

`format` and `version` identify the calibration data format and schema version.

`grid.preTransformRowCoordinates` and `grid.preTransformColumnCoordinates` store the resolved grid line coordinates directly in pre-transform units. Grid shape is inferred from their lengths.

The calibration JSON does not need to persist generation inputs such as `GridInsetMm` or digitizer geometry (`MaxX`, `MaxY`, `Width`, `Height`), because runtime consumes the resolved pre-transform coordinates directly.

`positionOffsets` stores position error vectors in pre-transform units. Its matrix dimensions must match `preTransformRowCoordinates × preTransformColumnCoordinates`.

`tiltCoefficients` stores tilt-model coefficient sets with the same matrix dimensions, using names that match the model terms:

- `kx2` for $K_{x2}$
- `ky2` for $K_{y2}$
- `kx` for $K_x$
- `ky` for $K_y$
- `kxy` for $K_{xy}$

### Default interpretation

Calibration JSON is interpreted as follows:

- position is **Default** when all `positionOffsets` values are zero
- tilt is **Default** when all `tiltCoefficients` values are zero

Otherwise the corresponding section is **Calibrated**.

If no calibration JSON is present in OTD settings, the Calibrator and the Correction Filter must synthesize the default all-zero calibration data in memory and behave exactly as if it had been stored explicitly.

### User workflow

The Calibrator reads the current calibration JSON from OTD and uses it as the initial working calibration.

When the user chooses **Copy Calibration**, the Calibrator copies the complete current working calibration to the clipboard as calibration JSON.

The user then pastes that JSON into OTD's Calibration Suite settings. OTD handles normal settings persistence.

`Copy Calibration` does not change the `OTD SETTINGS` status by itself. That status changes to `Up to date` only after the Calibrator compares the working calibration against the calibration JSON currently stored in OTD and finds them equal.

### Reset, import, and export

A reset-persisted-calibration action in the OTD UI must replace both sections with default all-zero values.

Import and export actions in the OTD UI read and write the same calibration JSON structure described above.

---

## 10. Performance

The Correction Filter must do only:

1. compute $p_1$ by subtracting interpolated position error from $p_0$
2. compute $p_2$ by interpolating tilt coefficients at $p_1$, predicting tilt error, and subtracting it
3. output $p_2$

It must not do per-report file I/O, coefficient solving, large allocations, or long-lived locks.

---

## 11. Parameters

Sampling:

- $\mathrm{CaptureWindowMs}$
- $\mathrm{MinSamplesPerCapture}$

Gates:

- $\mathrm{AngleToleranceDeg}$ (single tolerance for all gates; default $3^\circ$)

Solver:

- $\lambda$ (neighbor similarity weight)
- $\mu$ (small-coefficient weight)

Grid:

- $\mathrm{RowCount}$ and $\mathrm{ColumnCount}$ (default $3\times3$)
- $\mathrm{GridInsetMm}$, a fixed physical inset margin used for target placement
