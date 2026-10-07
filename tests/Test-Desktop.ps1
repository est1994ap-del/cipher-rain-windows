param([string]$AppPath, [string]$OutputDirectory, [int]$Cycles=10)
$ErrorActionPreference='Stop'
function Send-CipherRain([string]$Command) {
    $pipe=[IO.Pipes.NamedPipeClientStream]::new('.', 'CipherRain.Control.v1', [IO.Pipes.PipeDirection]::InOut)
    try { $pipe.Connect(10000); $writer=[IO.StreamWriter]::new($pipe,[Text.UTF8Encoding]::new($false),1024,$true); $writer.AutoFlush=$true; $reader=[IO.StreamReader]::new($pipe,[Text.Encoding]::UTF8,$true,1024,$true); $writer.WriteLine($Command); $result=$reader.ReadLine(); $writer.Dispose(); $reader.Dispose(); return $result } finally {$pipe.Dispose()}
}
New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null
$results=[Collections.Generic.List[object]]::new()
function Check([string]$Name,[bool]$Passed){$results.Add([pscustomobject]@{name=$Name;passed=$Passed});if(!$Passed){throw "Failed: $Name"}}
Send-CipherRain 'start' | Out-Null
Start-Sleep -Seconds 3
$initial=Send-CipherRain 'status' | ConvertFrom-Json
$monitors=$initial.hosts.Count
Check 'One live host per display' ($monitors -gt 0 -and @($initial.hosts | Where-Object {$_.error -ne ''}).Count -eq 0)
Send-CipherRain 'start' | Out-Null
$repeat=Send-CipherRain 'status' | ConvertFrom-Json
Check 'Repeated Start does not create extra hosts' ($repeat.hosts.Count -eq $monitors)
Send-CipherRain 'pause' | Out-Null
$before=Send-CipherRain 'status' | ConvertFrom-Json
Start-Sleep -Milliseconds 400
$after=Send-CipherRain 'status' | ConvertFrom-Json
Check 'Pause freezes every display clock' (($before.hosts.elapsed -join ',') -eq ($after.hosts.elapsed -join ','))
Send-CipherRain 'resume' | Out-Null
Send-CipherRain 'recover-shell' | Out-Null
Start-Sleep -Seconds 2
$recovered=Send-CipherRain 'status' | ConvertFrom-Json
Check 'Shell adapter reattachment preserves live hosts' ($recovered.hosts.Count -eq $monitors -and @($recovered.hosts | Where-Object {$_.error -ne ''}).Count -eq 0)
for($i=1;$i -le $Cycles;$i++) {
    Send-CipherRain 'stop' | Out-Null
    Send-CipherRain 'stop' | Out-Null
    $stopped=Send-CipherRain 'status' | ConvertFrom-Json
    Check "Stop cycle $i removes all desktop hosts" ($stopped.hosts.Count -eq 0 -and $stopped.state -eq 'Stopped')
    Send-CipherRain 'start' | Out-Null
    Start-Sleep -Milliseconds 1100
    $started=Send-CipherRain 'status' | ConvertFrom-Json
    Check "Start cycle $i recreates all displays" ($started.hosts.Count -eq $monitors)
}
Send-CipherRain 'hide' | Out-Null
Start-Sleep -Milliseconds 400
$hidden=Send-CipherRain 'status' | ConvertFrom-Json
Check 'Hidden settings suspend preview while wallpaper continues' ($hidden.preview.suspended -and $hidden.state -eq 'Playing')
Send-CipherRain 'show' | Out-Null
$results | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $OutputDirectory 'Desktop Lifecycle Tests.json') -Encoding utf8
Send-CipherRain 'status' | Set-Content -LiteralPath (Join-Path $OutputDirectory 'After Lifecycle Tests.json') -Encoding utf8
"Passed $($results.Count) checks across $monitors displays."
