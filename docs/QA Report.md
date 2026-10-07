# Windows verification

This report describes observed results from the Windows implementation and the clean upload-package build. It does not claim pixel-for-pixel equivalence to a simultaneously running Mac or certify hardware tests that were not performed.

## Observed checks

- Release builds completed with zero warnings or errors. The core suite passed **39 checks**, including settings validation, unknown-field preservation, Windows preference preservation through presets, deterministic timing at 24 and 60 fps, all nine effects, bounded effect storage, title completion, and checkpoint recovery.
- Four desktop renderers attached to Explorer's WorkerW background surface with healthy graphics renderers. This is the Windows surface that places the animation behind desktop icons.
- Start/Stop cycles, repeated Start, paused simulation clocks, hidden-preview suspension, and safe background-surface reattachment were exercised. Start did not create duplicate renderers.
- Actual Windows settings controls and preview were inspected. GPU image captures covered all effects, title, console boot, native-font rendering, and glow variation. Private reference captures are not distributed.
- Embedded screen-saver preview and a timed full-screen saver worked across the four displays, with desktop animation continuing afterward.
- Per-user install, shortcut creation, uninstall, and reinstall were exercised. Saved settings and ordinary wallpaper were preserved.
- The startup/persistence suite passed **12 checks**: registered sign-in command, clean app exit, unchanged saved settings after restart, four healthy desktop renderers, and hidden settings during background launch. A physical reboot or sign-out was not forced.
- The upload package is rebuilt from clean public source with public documentation. It contains no saved profile, private symbols, credentials, original handoff, local diagnostic logs, or private captures.

Core results and package integrity evidence are included in the upload folder's **Verification** directory. Hardware observations above were made during implementation; those intrusive tests were not repeated during packaging.

## Measured workstation sample

Four active renderers, settings hidden, about 12 seconds per sample after warm-up. Covered-window pausing was temporarily disabled during measurement and restored afterward.

| Requested rate | Observed frame-loop rate | Total CPU | Total working memory |
| --- | --- | --- | --- |
| 60 fps | 59.99–60.05 | 0.81% | 846 MB |
| 30 fps | 30.00–30.01 | 0.53% | 830 MB |
| 24 fps | 24.00 | 0.60% | 806 MB |
| Paused, settings hidden | 0 | 0.026% | Not recorded |

These are measurements from one workstation, not promises for every computer. Memory totals include shared runtime and graphics pages. Frame-loop rates are not independent measurements of physical display output.

## Remaining verification

- Eight-hour stability and uninterrupted long visual review.
- Physical sleep/wake, monitor hot-plug, GPU failure, Remote Desktop, mixed display scaling, and battery transitions.
- Comprehensive high-contrast, light-theme, screen-reader, and keyboard coverage.
- Clean Windows virtual-machine install and publisher signing.

Windows' secure sign-in background cannot host this live animation; the app provides a matching still-image export. The desktop uses an isolated Explorer compatibility layer that may need adaptation after a future Windows change.
