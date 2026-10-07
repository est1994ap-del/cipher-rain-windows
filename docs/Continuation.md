# Maintenance and release builds

Use this repository as the complete Windows source. It includes both app and installer projects, the shader, original icon, dependency notices, and verification scripts. No original Mac handoff or private conversion script is required to build it.

Install the .NET 8 SDK on Windows x64, then run `Build-Release.ps1` from the repository root. The script creates a fresh portable payload before embedding it in the installer. Outputs and filename-only checksums are written to `artifacts`. These generated files belong in GitHub Releases, rather than in source history.

Settings are saved to `%LOCALAPPDATA%\CipherRain`; installation is under `%LOCALAPPDATA%\Programs\CipherRain`. Sign-in startup uses a per-user Run entry with `--start --tray`. Appearance presets preserve the user's startup and desktop preferences. App updates should preserve settings.

The GitHub workflow builds packages and runs the core checks. It does not install the app, register startup, or publish a release. The desktop, resource, and startup scripts operate on a running local app and are intentionally manual checks; read them before running them on a workstation.

Review `QA Report.md` before making broader release claims. Real hardware transitions, long-duration stability, full accessibility coverage, clean-machine installation, and code signing remain useful future release work.
