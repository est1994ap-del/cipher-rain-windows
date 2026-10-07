# Privacy

Cipher Rain works offline. It has no account, telemetry, analytics, advertising, subscription, network client, privileged service, or background download.

Settings, private glyphs, per-display simulation checkpoints, and bounded local diagnostic logs are stored under `%LOCALAPPDATA%\CipherRain`. Logs record local errors and omit title/profile content. A log rotates at approximately 512 KB, retaining one prior file. Rendering does not write files each frame. Checkpoints are written on transitions and orderly shutdown.

The screen saver never asks for a password. Windows handles all secure sign-in. Import/export and matching still-image export write only to the paths you choose. Nothing is uploaded or published.
