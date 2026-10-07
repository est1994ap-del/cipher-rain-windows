param([string]$OutputDirectory = (Join-Path $PSScriptRoot '../artifacts'))
$ErrorActionPreference = 'Stop'
$app = Join-Path $env:LOCALAPPDATA 'Programs/CipherRain/CipherRain.exe'
$settingsPath = Join-Path $env:LOCALAPPDATA 'CipherRain/Settings.json'
function Send-CipherRain([string]$Command) {
    $pipe = [IO.Pipes.NamedPipeClientStream]::new('.', 'CipherRain.Control.v1', [IO.Pipes.PipeDirection]::InOut)
    try {
        $pipe.Connect(10000)
        $writer = [IO.StreamWriter]::new($pipe, [Text.UTF8Encoding]::new($false), 1024, $true)
        $reader = [IO.StreamReader]::new($pipe, [Text.Encoding]::UTF8, $true, 1024, $true)
        $writer.AutoFlush = $true
        $writer.WriteLine($Command)
        $result = $reader.ReadLine()
        $writer.Dispose()
        $reader.Dispose()
        return $result
    } finally { $pipe.Dispose() }
}
$results = [Collections.Generic.List[object]]::new()
function Check([string]$Name, [bool]$Passed) {
    $results.Add([pscustomobject]@{name=$Name; passed=$Passed})
    if (!$Passed) { throw "Failed: $Name" }
}
$before = [IO.File]::ReadAllText($settingsPath)
Check 'Startup preference saved on disk' (($before | ConvertFrom-Json).launchAtLogin -eq $true)
$expectedCommand = '"' + $app + '" --start --tray'
$run = (Get-ItemProperty -LiteralPath 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run' -Name CipherRain).CipherRain
Check 'Windows sign-in runs installed app in background' ($run -eq $expectedCommand)
$initial = Send-CipherRain 'status' | ConvertFrom-Json
$processes = @(Get-Process -Name CipherRain | Where-Object { $_.Path -eq $app })
try {
    Send-CipherRain 'quit' | Out-Null
    foreach ($process in $processes) {
        Check "Previous app process $($process.Id) exits cleanly" ($process.WaitForExit(15000))
    }
    Start-Process -FilePath $app -ArgumentList '--start --tray' -WindowStyle Hidden | Out-Null
    Start-Sleep -Seconds 4
    $restarted = Send-CipherRain 'status' | ConvertFrom-Json
    Check 'Saved appearance survives quit and restart' ([IO.File]::ReadAllText($settingsPath) -eq $before)
    Check 'Wallpaper starts on every previous display' ($restarted.state -eq 'Playing' -and $restarted.hosts.Count -eq $initial.hosts.Count -and $restarted.hosts.Count -gt 0)
    Check 'All restarted displays have healthy renderers' (@($restarted.hosts | Where-Object { $_.error -ne '' -or $_.parentClass -ne 'WorkerW' }).Count -eq 0)
    Check 'Automatic startup leaves settings hidden' ($null -eq $restarted.preview)
    $settingsStillEnabled = (Get-Content -LiteralPath $settingsPath -Raw | ConvertFrom-Json).launchAtLogin
    Check 'Automatic startup remains enabled after restart' ($settingsStillEnabled -and (Get-ItemProperty -LiteralPath 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run' -Name CipherRain).CipherRain -eq $expectedCommand)
} finally {
    if (@(Get-Process -Name CipherRain -ErrorAction SilentlyContinue | Where-Object { $_.Path -eq $app }).Count -eq 0) {
        Start-Process -FilePath $app -ArgumentList '--start --tray' -WindowStyle Hidden | Out-Null
    }
    New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null
    $results | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $OutputDirectory 'Startup Persistence Tests.json') -Encoding utf8
}
Send-CipherRain 'status' | Set-Content -LiteralPath (Join-Path $OutputDirectory 'After Startup Test.json') -Encoding utf8
"Passed $($results.Count) checks; preserved all settings and restarted $($restarted.hosts.Count) displays."
