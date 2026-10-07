param([string]$OutputDirectory)
$ErrorActionPreference='Stop'
function Send([string]$Command) {
 $pipe=[IO.Pipes.NamedPipeClientStream]::new('.', 'CipherRain.Control.v1', [IO.Pipes.PipeDirection]::InOut)
 try { $pipe.Connect(10000);$writer=[IO.StreamWriter]::new($pipe,[Text.UTF8Encoding]::new($false),1024,$true);$reader=[IO.StreamReader]::new($pipe,[Text.Encoding]::UTF8,$true,1024,$true);$writer.AutoFlush=$true;$writer.WriteLine($Command);$r=$reader.ReadLine();$writer.Dispose();$reader.Dispose();return $r }finally{$pipe.Dispose()}
}
$saved=Get-Content -LiteralPath (Join-Path $env:LOCALAPPDATA 'CipherRain/Settings.json') -Raw
$settings=$saved | ConvertFrom-Json
$report=[Collections.Generic.List[object]]::new()
try {
 Send 'hide' | Out-Null
 foreach($fps in @(60,30,24)) {
  $settings.frameRate=$fps;$settings.pauseWhenCovered=$false
  Send ('set:'+($settings | ConvertTo-Json -Depth 30 -Compress)) | Out-Null
  Start-Sleep -Seconds 4
  Send 'reset-metrics' | Out-Null
  $before=Get-Process CipherRain | Select-Object Id,CPU,WorkingSet64,PrivateMemorySize64,Handles
  $ids=@($before.Id)
  $start=Get-Date
  try {$counter=Get-Counter '\GPU Engine(*)\Utilization Percentage' -SampleInterval 1 -MaxSamples 10 -ErrorAction Stop;$gpu=@($counter.CounterSamples | Where-Object {$_.InstanceName -match 'pid_(\d+)_' -and [int]$Matches[1] -in $ids -and $_.InstanceName -match 'engtype_3D'});$gpuMean=($gpu | Measure-Object CookedValue -Sum).Sum/10}catch{$gpuMean=$null;Start-Sleep -Seconds 10}
  $seconds=((Get-Date)-$start).TotalSeconds;$after=Get-Process CipherRain
  $cpu=0;foreach($p in $after){$old=$before | Where-Object Id -eq $p.Id;if($old){$cpu+=$p.CPU-$old.CPU}}
  $status=Send 'status' | ConvertFrom-Json
  $report.Add([pscustomobject]@{requestedFps=$fps;sampleSeconds=$seconds;cpuMachinePercent=100*$cpu/$seconds/[Environment]::ProcessorCount;totalWorkingSetMB=($after | Measure-Object WorkingSet64 -Sum).Sum/1MB;totalPrivateMemoryMB=($after | Measure-Object PrivateMemorySize64 -Sum).Sum/1MB;totalHandles=($after | Measure-Object Handles -Sum).Sum;sumGpu3DPercent=$gpuMean;status=$status})
 }
 Send 'pause' | Out-Null
 $before=(Get-Process CipherRain | Measure-Object CPU -Sum).Sum
 Start-Sleep -Seconds 5
 $cpu=(Get-Process CipherRain | Measure-Object CPU -Sum).Sum-$before
 $report.Add([pscustomobject]@{mode='Paused with settings hidden';cpuMachinePercent=100*$cpu/5/[Environment]::ProcessorCount;status=(Send 'status' | ConvertFrom-Json)})
} finally {Send ('set:'+(($saved | ConvertFrom-Json) | ConvertTo-Json -Depth 30 -Compress)) | Out-Null;Send 'resume' | Out-Null;Send 'show' | Out-Null}
$report | ConvertTo-Json -Depth 30 | Set-Content -LiteralPath (Join-Path $OutputDirectory 'Resource Measurements.json') -Encoding utf8
$report | Select-Object requestedFps,mode,cpuMachinePercent,totalWorkingSetMB,totalPrivateMemoryMB,sumGpu3DPercent
