param([string]$GameDir = "")
$ErrorActionPreference = "Stop"
if (-not $GameDir) { throw "Pass -GameDir pointing at the Erenshor install folder (contains Erenshor.exe)." }
$dll = Join-Path $GameDir "plugins\ErenshorGuildLife.dll"
if (Test-Path $dll) {
    Remove-Item -Force $dll
    Write-Host "Removed Erenshor Guild Life plugin file." -ForegroundColor Green
}
else { Write-Host "Erenshor Guild Life plugin file was not present." }
Write-Host "Saved bulletin data under plugins\config\ErenshorGuildLife is intentionally left in place." -ForegroundColor Yellow
