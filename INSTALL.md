# Install and use

1. Open **Cipher Rain Setup.exe** and choose **Install & open**.
2. The app installs under `%LOCALAPPDATA%\Programs\CipherRain`, creates a Start-menu entry, and offers the desktop shortcut **Cipher Rain**.
3. Open the shortcut, adjust settings, and use **Start desktop**, **Pause**, **Resume**, and **Stop**. Changes appear immediately and save automatically.
4. To start at sign-in, turn on **Windows → Start Cipher Rain when I sign in**.
5. To use the screen saver, choose **Windows → Use as Windows screen saver**, then set the wait time in Windows. Cipher Rain preserves Windows' existing password and sign-in policy.

The portable ZIP contains the complete runtime. Extract the whole folder and open `CipherRain.exe`. The portable and installed editions share `%LOCALAPPDATA%\CipherRain` settings. Only one settings/tray controller runs at a time.

Uninstall through **Windows Settings → Apps → Installed apps → Cipher Rain**, or open **Uninstall Cipher Rain.exe** in the install folder. The uninstaller offers to preserve settings, removes its own shortcuts and optional startup entry, restores the previous screen-saver selection if it still owns that selection, and removes only files listed in its installation manifest. Source projects and handoff files remain untouched.

## Compatibility and Windows warnings

Requires **Windows 11 x64** (Intel/AMD) and Direct3D 11-compatible graphics. The downloads include the .NET runtime. Native ARM, macOS and Linux are not supported by this package.

This is an unsigned first release. Windows can report an unknown publisher, and Smart App Control or an administrator's policy may block it. Do not turn off antivirus, Smart App Control, Secure Boot, or other security protections. If Windows blocks the app, leave those protections enabled and wait for a compatible trusted release. No certificate or signing secret is included.

## Verify your download

Download `SHA256SUMS.txt` from the same release. In PowerShell, run:

```powershell
Get-FileHash -Algorithm SHA256 -LiteralPath '.\Cipher Rain Setup.exe'
```

Compare the result with the installer entry in `SHA256SUMS.txt`. Use the portable ZIP's filename instead to verify that download. A matching checksum detects a changed file; it is not a publisher signature or a guarantee of security.

## Common questions

- **I downloaded code instead of an app.** Use the files attached to [Releases](https://github.com/est1994ap-del/cipher-rain-windows/releases), not **Code → Download ZIP**.
- **I closed the window, but the wallpaper is still running.** This is intentional. Open its notification-area icon to pause, stop, or quit.
- **Where are my settings?** Both editions use `%LOCALAPPDATA%\CipherRain`; portable mode does not create a separate profile.
- **Can it animate the secure sign-in screen?** No. Export a matching still image instead.
- **Does it need the internet?** The installed app works locally and has no telemetry. Building from source downloads development dependencies.
