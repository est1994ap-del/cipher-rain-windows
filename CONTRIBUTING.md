# Contributing

Open an issue with a clear description of the change or problem. For a bug, include Windows version, display count, GPU model if known, and short steps to reproduce it. Keep personal titles, settings files, local paths, and private assets out of public reports.

Build with the .NET 8 SDK on Windows x64. Run `Build-Release.ps1`, then the noninteractive core checks:

```powershell
$test = Start-Process './artifacts/Portable/CipherRain.exe' -ArgumentList '--test "artifacts/Core Tests.json"' -WindowStyle Hidden -Wait -PassThru
if ($test.ExitCode -ne 0) { throw 'Core tests failed' }
Get-Content './artifacts/Core Tests.json'
```

The desktop/resource/startup scripts affect a locally running wallpaper and are manual workstation checks. They are not run in the GitHub build workflow.

Keep platform-specific desktop code isolated, preserve saved settings, and change only app-owned processes and files. New assets must have clear redistribution rights. Source changes and matching release notes belong together in a pull request. Generated downloads, build directories, private reference files, and settings do not belong in commits.
