# Windows architecture and feature map

The Windows implementation adapts the author's macOS rain behavior using native Windows graphics and a Windows settings interface. Public builds use local system-font symbols.

| Feature | Source | Verification |
| --- | --- | --- |
| Independent streams and changing symbols | `Simulation.cs` | Deterministic timing and bounds |
| Layered light, glow, and composition | `Renderer.cs`, `Shaders/Rain.hlsl` | GPU captures and glow comparison |
| Saved settings and presets | `Settings.cs` | Validation, migration, and preference preservation |
| Nine effects, incoming title, boot | `Simulation.cs`, `Sequences.cs`, `Storm.cs` | Effects and continuity checks |
| Settings and separate live preview | `SettingsWindow.cs` | Windows UI inspection |
| Desktop, tray, Start/Pause/Stop | `Controller.cs`, `Native.cs` | Lifecycle and owned-window checks |
| Screen saver and checkpoints | `Program.cs`, `RenderHost.cs` | Embedded/full-screen and checkpoint tests |
| Sign-in startup and saving at exit | `Controller.cs`, `Program.cs`, `SettingsWindow.cs` | Restart and saved-settings checks |
| Installation and shortcuts | `src/Setup`, `Build-Release.ps1` | Per-user installation and removal |

Wallpaper processes are separate for each monitor, allowing graphics resources to follow that display's adapter. The settings preview runs on a separate graphics thread. Windows secure sign-in remains managed by Windows.
