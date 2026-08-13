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
