param([string]$BepInExRoot = "")
$ErrorActionPreference = "Stop"
if (-not $BepInExRoot) { throw "Pass -BepInExRoot pointing at the profile/root that contains BepInEx." }
$pluginDir = Join-Path $BepInExRoot "BepInEx\plugins\ErenshorGuildLife"
if (Test-Path $pluginDir) {
    Remove-Item -Recurse -Force $pluginDir
    Write-Host "Removed Erenshor Guild Life plugin files." -ForegroundColor Green
}
else { Write-Host "Erenshor Guild Life plugin folder was not present." }
Write-Host "Saved bulletin data under BepInEx\config\ErenshorGuildLife is intentionally left in place." -ForegroundColor Yellow
