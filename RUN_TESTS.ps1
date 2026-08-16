$ErrorActionPreference = "Stop"
$ScriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path

function Find-Csc {
    foreach ($path in @(
        "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe",
        "$env:WINDIR\Microsoft.NET\Framework\v4.0.30319\csc.exe"
    )) {
        if (Test-Path $path) { return $path }
    }
    throw "csc.exe not found. Install the .NET Framework Developer Pack or Visual Studio Build Tools."
}

$csc = Find-Csc
$out = Join-Path $env:TEMP "ErenshorGuildLifeCoreTests.exe"
& $csc /nologo /target:exe /out:$out `
    (Join-Path $ScriptRoot "src\GuildModels.cs") `
    (Join-Path $ScriptRoot "src\GuildLifeCore.cs") `
    (Join-Path $ScriptRoot "src\GuildStore.cs") `
    (Join-Path $ScriptRoot "tests\GuildLifeCoreTests.cs")
if ($LASTEXITCODE -ne 0) { throw "Guild Life core tests did not compile." }
& $out
if ($LASTEXITCODE -ne 0) { throw "Guild Life core tests failed." }

# Unity-free retained-UI visibility/fallback, action routing, strict bool mutation parsing, gesture cleanup, and normalized-position recovery policy.
$suiteUiOut = Join-Path $env:TEMP "ErenshorGuildLife.SuiteUiPolicyTests.exe"
& $csc /nologo /target:exe /out:$suiteUiOut `
    (Join-Path $ScriptRoot "src\SuiteUiPolicies.cs") `
    (Join-Path $ScriptRoot "tests\SuiteUiPolicyTests.cs")
if ($LASTEXITCODE -ne 0) { throw "Suite UI policy tests did not compile." }
& $suiteUiOut
if ($LASTEXITCODE -ne 0) { throw "Suite UI policy tests failed." }

# Source-level authority boundary guard. Guild Life may inspect native guild state but must not
# expose or call native guild mutation operations in this read-only product.
$guildSource = (Get-ChildItem (Join-Path $ScriptRoot "src") -Filter "*.cs" | ForEach-Object { Get-Content $_.FullName -Raw }) -join "`n"
$forbiddenPatterns = @(
    '\.Invite\s*\(',
    '\.Kick\s*\(',
    '\.Recruit\s*\(',
    '\.CreateGuild\s*\(',
    '\.LeaveGuild\s*\(',
    '\.StartRaid\s*\(',
    '\.StartGuildQuest\s*\(',
    '\.SetValue\s*\(',
    'HarmonyPatch'
)
foreach ($pattern in $forbiddenPatterns) {
    if ($guildSource -match $pattern) { throw "Read-only boundary test failed on forbidden source pattern: $pattern" }
}
if ($guildSource -match 'LunarisPermission\.Network') { throw "Read-only boundary test failed: Guild Life requests network permission." }
Write-Host "PASS: Guild Life read-only authority source guard"

$readerSource = Get-Content (Join-Path $ScriptRoot "src\GuildReader.cs") -Raw
$pluginSource = Get-Content (Join-Path $ScriptRoot "src\ErenshorGuildLifePlugin.cs") -Raw
if ($readerSource -match 'gameObject\.name') { throw "Guild snapshot mapping guard failed: scene GameObject name fallback returned." }
$memberNameMatch = [regex]::Match($readerSource, 'private\s+static\s+string\s+MemberName\(object\s+value\)[\s\S]*?(?=private\s+static\s+bool\s+Contains)')
if (-not $memberNameMatch.Success) { throw "Guild snapshot mapping guard failed: MemberName mapper was not found." }
if ($memberNameMatch.Value -match 'Convert\.ToString\(value\)') { throw "Guild snapshot mapping guard failed: unverified member-object ToString fallback returned." }
if ($readerSource -notmatch 'Read\(string\s+verifiedPlayerName\)') { throw "Guild snapshot mapping guard failed: reader is not bound to verified character identity." }
if ($pluginSource -notmatch 'private\s+void\s+UnloadCharacter\(\)[\s\S]*?_snapshot\s*=\s*null;') { throw "Guild lifecycle guard failed: character unload no longer clears guild snapshot." }
if ($pluginSource -notmatch 'Instance\s*!=\s*null\s*&&\s*Instance\s*!=\s*this') { throw "Guild lifecycle guard failed: duplicate plugin initialization is not rejected." }
Write-Host "PASS: Guild Life snapshot/lifecycle source guard"

# Header collapse uses an owned Image-chevron; it must not depend on TMP triangle coverage.
$guildWindowSource = Get-Content (Join-Path $ScriptRoot "src\GuildWindow.cs") -Raw
$guildUiSource = Get-Content (Join-Path $ScriptRoot "src\RetainedUiKit.cs") -Raw
if ($guildWindowSource -notmatch 'AddVerticalChevron\(_collapseChevron,\s*!_collapsed\)' -or
    $guildUiSource -notmatch 'internal\s+static\s+void\s+AddVerticalChevron') {
    throw "Guild Life release polish guard failed: glyph-safe collapse chevron is missing."
}
Write-Host "Guild Life release polish collapse-icon guard: PASS" -ForegroundColor Green
$launcherVisual = Get-Content (Join-Path $ScriptRoot "src\StandaloneLauncherVisual.cs") -Raw
$launcherSource = Get-Content (Join-Path $ScriptRoot "src\GuildLauncher.cs") -Raw
if ($launcherVisual -notmatch 'Width\s*=\s*154f' -or $launcherVisual -notmatch 'Height\s*=\s*32f' -or
    $launcherVisual -notmatch 'GripWidth\s*=\s*20f' -or $launcherVisual -notmatch '"GripDot"' -or
    $launcherSource -notmatch 'StyleGrip\(grip\)' -or $launcherSource -notmatch 'StyleButton\(button, _label\)') {
    throw "Guild Life Forgotten Roads launcher visual contract failed."
}
Write-Host "Guild Life Forgotten Roads launcher visual contract: PASS" -ForegroundColor Green
