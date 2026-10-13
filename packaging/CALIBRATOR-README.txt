Calibration Suite for OpenTabletDriver

Requirements: Windows x64, .NET 8 Windows Desktop Runtime, and OpenTabletDriver 0.6.7.
Install the separate CalibrationSuite.zip filter through OTD Plugin Manager and restart OTD.
Extract this entire archive and launch CalibrationSuite.Calibrator.exe.

Connect one display tablet. Its full digitizer must map to its full display,
with X right and Y down. Rotated/cropped mappings and non-display tablets are outside scope.
Move the setup window onto the tablet screen and choose Calibrate.
Aim with the physical nib; Space captures, Backspace revisits, R restarts, Esc returns.
If tilt is reported, the first stable pose at each target sets a reference;
later poses spread around that reported value. The arrow is a reported-tilt plot,
not a physical-angle measurement. Reduce Tilt range if targets are hard to reach.
If OTD pen reports contain no tilt, tilt capture is blocked for the session.
Capture upright positions only: hold the pen upright at each crosshair.
The app cannot verify posture without tilt measurements. If reported tilt is
unusable, set Tilt directions or Tilt levels to 0 to choose position-only mode.
Support uses OTD report interfaces and geometry, with no per-model settings.

Review and accept the measurements, Copy Calibration, paste into the intended
OTD profile's Calibration data property, and Save. The Calibrator does not save
measurements after closing. Use OTD settings backups and presets for persistence.
Disable the filter to compare, or clear its data and Save to remove the correction.

Update/remove the filter through OTD Plugin Manager. For Calibrator updates,
close the app and extract the new archive into a fresh directory.
To remove the Calibrator, delete its extracted directory; saved OTD data stays in OTD.

Source, instructions, and issues: https://github.com/ThiagoRangel7/OTD.CalibrationSuite
License: MIT for Calibration Suite. Third-party components retain their own licenses.
See notices/ and the accompanying CalibrationSuite.Source.zip for LGPL source/build inputs.
