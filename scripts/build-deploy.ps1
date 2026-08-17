# Build and deploy MK-88 Hydra (Release)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
Set-Location -LiteralPath $root

dotnet build ".\MK88Hydra\MK88Hydra.csproj" -c Release
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

$deploy = if ($env:NuclearOptionRoot) {
  Join-Path $env:NuclearOptionRoot "BepInEx\plugins\MK-88-Hydra"
} else {
  'C:\Program Files (x86)\Steam\steamapps\common\Nuclear Option\BepInEx\plugins\MK-88-Hydra'
}
New-Item -ItemType Directory -Force -Path $deploy | Out-Null
Copy-Item -LiteralPath ".\MK88Hydra\bin\Release\MK88Hydra.dll" -Destination $deploy -Force

$nobpCandidates = @(
  ".\UnityBake\Build\MK88Hydra.nobp",
  ".\MK88Hydra\Resources\MK88Hydra.nobp"
)
foreach ($n in $nobpCandidates) {
  if (Test-Path -LiteralPath $n) {
    $len = (Get-Item -LiteralPath $n).Length
    if ($len -lt 4096) { Write-Warning "Skip tiny nobp ($len bytes): $n"; continue }
    Copy-Item -LiteralPath $n -Destination (Join-Path $deploy "MK88Hydra.nobp") -Force
    Write-Host "Deployed nobp ($len bytes) from $n"
    break
  }
}

$texSrc = ".\UnityBake\Assets\MissilePack\Textures"
if (Test-Path -LiteralPath $texSrc) {
  Copy-Item -LiteralPath $texSrc -Destination (Join-Path $deploy "Textures") -Recurse -Force
}
$kozuch = ".\UnityBake\Assets\MissilePack\KozuchTorpedoTexture.png"
if (Test-Path -LiteralPath $kozuch) {
  Copy-Item -LiteralPath $kozuch -Destination $deploy -Force
}

Write-Host "Deployed to $deploy"
Get-ChildItem -LiteralPath $deploy | Format-Table Name, Length
