$ErrorActionPreference = 'Stop'
Set-Location -LiteralPath $PSScriptRoot
$artifacts = Join-Path $PSScriptRoot 'artifacts'
$portable = Join-Path $artifacts 'Portable'
# Package only this build's output, never leftovers from an older build.
if (Test-Path -LiteralPath $portable) {
    $resolved = [IO.Path]::GetFullPath($portable)
    $expected = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot 'artifacts/Portable'))
    if ($resolved -ne $expected -or ((Get-Item -LiteralPath $portable).Attributes -band [IO.FileAttributes]::ReparsePoint)) {
        throw 'Refusing to clean an unexpected release directory.'
    }
    Remove-Item -LiteralPath $portable -Recurse -Force
}
New-Item -ItemType Directory -Force -Path $artifacts | Out-Null
dotnet publish src/CipherRain/CipherRain.csproj -c Release -r win-x64 --self-contained true -o $portable -p:DebugType=None -p:DebugSymbols=false
if ($LASTEXITCODE -ne 0) { throw 'App publish failed.' }
Copy-Item -LiteralPath (Join-Path $portable 'CipherRain.exe') -Destination (Join-Path $portable 'CipherRain.scr') -Force
Copy-Item -LiteralPath README.md,INSTALL.md,PRIVACY.md,LICENSES.md,RELEASE-NOTES.md -Destination $portable -Force
Copy-Item -LiteralPath LICENSE.txt -Destination (Join-Path $portable 'Cipher Rain License.txt') -Force
Copy-Item -LiteralPath LICENSE.txt -Destination (Join-Path $portable 'LICENSE.txt') -Force
Copy-Item -LiteralPath docs -Destination $portable -Recurse -Force
$zip = Join-Path $artifacts 'Cipher Rain Portable.zip'
if (Test-Path -LiteralPath $zip) { Remove-Item -LiteralPath $zip }
[IO.Compression.ZipFile]::CreateFromDirectory($portable,$zip,[IO.Compression.CompressionLevel]::Optimal,$false)
dotnet publish src/Setup/Setup.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=None -p:DebugSymbols=false -o artifacts/Setup
if ($LASTEXITCODE -ne 0) { throw 'Installer publish failed.' }
$setup = Join-Path $artifacts 'Cipher Rain Setup.exe'
Copy-Item -LiteralPath 'artifacts/Setup/Cipher Rain Setup.exe' -Destination $setup -Force
$hashes = foreach ($file in @($setup,$zip)) {
    $item = Get-Item -LiteralPath $file
    [pscustomobject]@{file=$item.Name; bytes=$item.Length; sha256=(Get-FileHash -Algorithm SHA256 -LiteralPath $file).Hash.ToLowerInvariant()}
}
$hashes | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $artifacts 'Release SHA256.json') -Encoding utf8
$hashes | ForEach-Object { $_.sha256 + '  ' + $_.file } | Set-Content -LiteralPath (Join-Path $artifacts 'SHA256SUMS.txt') -Encoding utf8
'Ready: artifacts/Cipher Rain Setup.exe and artifacts/Cipher Rain Portable.zip'
