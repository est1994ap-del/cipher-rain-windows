# Cipher Rain for Windows 1.0.0

Native digital rain for the Windows desktop, with a live settings preview, notification-area controls, and a matching screen saver.

## Included

- Independent streams with six character styles, four palettes, four presets, and layered glow.
- Nine procedural effects, an incoming multiline title, and a separate console boot sequence.
- Independent playback on multiple displays, 24/30/60 fps choices, covered-window pausing, and battery conservation.
- Automatic settings saving and optional startup in the background when signing in to Windows.
- Startup preferences stay intact when changing appearance presets. Pending changes save on settings close, app quit, and Windows sign-out.
- Windows screen saver and matching still-image export for the lock screen.
- Per-user installer, desktop shortcut named **Cipher Rain**, and a portable package containing the .NET runtime.

## Downloads

**Cipher.Rain.Setup.exe** installs the app. **Cipher.Rain.Portable.zip** runs after extracting the whole archive. Both target Windows 11 x64 and use the same local settings folder. GitHub replaces spaces in uploaded filenames with dots; local builds use spaced filenames.

The public source independently built on a GitHub-hosted Windows runner on October 7, 2026, including all 39 core checks. See [the successful build](https://github.com/est1994ap-del/cipher-rain-windows/actions/runs/37668793076). These automated checks do not replace clean-PC installation or real hardware testing.

This is an unsigned build. The live animation runs on the desktop and as a screen saver; Windows' secure sign-in background uses a static image. See [QA Report](docs/QA%20Report.md) for observed checks and remaining hardware verification.
