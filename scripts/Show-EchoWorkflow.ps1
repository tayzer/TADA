param(
    [string]$Dotnet = 'dotnet',
    [string]$Cli = (Join-Path $PSScriptRoot '../src/Tada.Cli/bin/Release/net10.0/Tada.Cli.dll')
)
$ErrorActionPreference = 'Stop'
$repository = Split-Path $PSScriptRoot -Parent
$Cli = (Resolve-Path -LiteralPath $Cli).Path
$Dotnet = (Get-Command $Dotnet -CommandType Application -ErrorAction Stop).Source
$workspace = Join-Path $repository ('.artifacts/echo-demo/' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $workspace -Force | Out-Null
$transcript = [Collections.Generic.List[string]]::new()
$transcript.Add('Automated demonstration control approval. Human business acceptance remains separate.')
$transcript.Add('Results are local advisory L1.')

function Invoke-DemoTada([string]$Name, [string[]]$Arguments, [int]$ExpectedExit = 0) {
    $output = & $Dotnet $Cli @Arguments --repo $workspace --json 2>&1
    $exitCode = $LASTEXITCODE
    $json = $output -join "`n"
    [IO.File]::WriteAllText((Join-Path $workspace "$Name.json"), $json + "`n")
    $displayArguments = $Arguments | ForEach-Object { "'" + $_.Replace("'", "''") + "'" }
    $transcript.Add('> tada ' + ($displayArguments -join ' ') + ' --json')
    $transcript.Add($json)
    $transcript.Add("Exit $exitCode")
    [IO.File]::WriteAllLines((Join-Path $workspace 'transcript.txt'), $transcript)
    if ($exitCode -ne $ExpectedExit) { throw "$Name expected exit $ExpectedExit, got $exitCode. Evidence retained in $workspace" }
    Write-Host "$Name (exit $exitCode)"
    return $json | ConvertFrom-Json
}

$version = Invoke-DemoTada 'version' @('--version')
Invoke-DemoTada 'init' @('init','--acceptance') | Out-Null
Copy-Item -LiteralPath (Join-Path $repository 'examples/Echo/echo.feature') -Destination "$workspace/specifications/functional/echo.feature"
Copy-Item -LiteralPath (Join-Path $repository 'examples/Echo/EchoBindings.cs') -Destination "$workspace/tests/Acceptance/Bindings.cs"
New-Item -ItemType Directory -Path "$workspace/src/Api" -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $repository 'examples/Echo/Api/Echo.Api.csproj') -Destination "$workspace/src/Api/Echo.Api.csproj"
$programPath = Join-Path $workspace 'src/Api/Program.cs'
[IO.File]::WriteAllText($programPath, 'var builder = WebApplication.CreateBuilder(args); var app = builder.Build(); app.MapPost("/echo", () => Results.StatusCode(501)); app.Run();')
$acceptancePath = Join-Path $workspace 'tests/Acceptance/Acceptance.csproj'
$acceptanceProject = [IO.File]::ReadAllText($acceptancePath).Replace('</Project>', '<ItemGroup><ProjectReference Include="../../src/Api/Echo.Api.csproj" ReferenceOutputAssembly="false" Private="false" OutputItemType="TadaSutOutput" /></ItemGroup></Project>')
[IO.File]::WriteAllText($acceptancePath, $acceptanceProject)
$configPath = Join-Path $workspace 'tada.json'
$config = Get-Content -LiteralPath $configPath -Raw | ConvertFrom-Json
$config.execution | Add-Member -NotePropertyName sutProject -NotePropertyValue 'src/Api/Echo.Api.csproj' -Force
$config.execution | Add-Member -NotePropertyName sutAssembly -NotePropertyValue 'Echo.Api.dll' -Force
$config | Add-Member -NotePropertyName implementationRoots -NotePropertyValue @('src') -Force
[IO.File]::WriteAllText($configPath, ($config | ConvertTo-Json -Depth 8))

Push-Location $workspace
try {
    & $Dotnet restore tests/Acceptance/Acceptance.csproj -p:NuGetAudit=false 2>&1 |
        Tee-Object -FilePath (Join-Path $workspace 'restore.log') | Out-Host
    if ($LASTEXITCODE) { throw "Harness restore failed. Evidence retained in $workspace" }
} finally { Pop-Location }
$baseline = (Invoke-DemoTada 'prepare' @('baseline', 'prepare')).baseline
$review = Invoke-DemoTada 'review' @('baseline', 'review', '--baseline', $baseline)
Invoke-DemoTada 'approval' @('baseline', 'approve', '--review', $review.review.id, '--by', 'automated Echo demonstration control; not human acceptance') | Out-Null
$red = Invoke-DemoTada 'fail' @('run', '--baseline', $baseline, '--dotnet', $Dotnet) 1
if ($red.outcome -ne 'FAIL' -or !($red.reasons | Where-Object code -eq 'requirement-failure')) {
    throw 'HTTP 501 did not establish the intended assertion failure.'
}
Invoke-DemoTada 'verify-fail' @('verify', '--run', $red.runId) 1 | Out-Null
Copy-Item -LiteralPath (Join-Path $repository 'examples/Echo/Api/Program.cs') -Destination $programPath
$green = Invoke-DemoTada 'pass' @('run', '--baseline', $baseline, '--dotnet', $Dotnet)
$verified = Invoke-DemoTada 'verify-pass' @('verify', '--run', $green.runId)
if ($verified.outcome -ne 'PASS' -or $verified.passedCases.Count -ne 2) { throw 'Both Echo cases must pass.' }

# Change the working assertion after approval and attempt a new run.
$bindingPath = Join-Path $workspace 'tests/Acceptance/Bindings.cs'
$originalBinding = [IO.File]::ReadAllBytes($bindingPath)
try {
    $binding = [IO.File]::ReadAllText($bindingPath)
    $changed = $binding.Replace('Assert.AreEqual(code, (int)response!.StatusCode);', 'Assert.IsTrue(true);')
    if ($changed -eq $binding) { throw 'The assertion-edit control did not change its intended binding.' }
    [IO.File]::WriteAllText($bindingPath, $changed)
    $transcript.Add('> Replace status assertion in tests/Acceptance/Bindings.cs with Assert.IsTrue(true);')
    $blocked = Invoke-DemoTada 'oracle-edit' @('run', '--baseline', $baseline, '--dotnet', $Dotnet) 2
    if ($blocked.outcome -ne 'INDETERMINATE' -or !($blocked.reasons | Where-Object { $_.code -eq 'execution-invalid' -and $_.detail.StartsWith('Oracle inputs changed') })) {
        throw 'The edited assertion was not rejected for changed oracle inputs.'
    }
    Invoke-DemoTada 'verify-history-after-edit' @('verify', '--run', $green.runId) | Out-Null
} finally { [IO.File]::WriteAllBytes($bindingPath, $originalBinding) }

# Corrupt the captured assertion in this isolated control, then restore exact bytes.
$snapshotBinding = Join-Path $workspace ('.tada/snapshots/' + $baseline.Substring(7) + '/tests/Acceptance/Bindings.cs')
$originalSnapshot = [IO.File]::ReadAllBytes($snapshotBinding)
try {
    [IO.File]::WriteAllText($snapshotBinding, $changed)
    $transcript.Add('> Apply the same assertion edit to the captured snapshot in this disposable control.')
    $tampered = Invoke-DemoTada 'tamper' @('verify', '--run', $green.runId) 2
    if ($tampered.outcome -ne 'INDETERMINATE' -or !($tampered.reasons | Where-Object code -eq 'integrity')) {
        throw 'The captured assertion edit was not rejected for integrity.'
    }
} finally { [IO.File]::WriteAllBytes($snapshotBinding, $originalSnapshot) }
Invoke-DemoTada 'verify-restored' @('verify', '--run', $green.runId) | Out-Null
$report = [ordered]@{
    sourceCommit = (& git -C $repository rev-parse HEAD)
    verifier = $version
    verifierSha256 = (Get-FileHash -LiteralPath $Cli -Algorithm SHA256).Hash.ToLowerInvariant()
    baseline = $baseline
    failedRun = $red.runId
    passingRun = $green.runId
    workspace = $workspace
    approval = 'Automated demonstration control; not human business acceptance.'
    evidence = 'Complete local evidence is under .tada/ in this workspace. Result JSON and transcript are excerpts for readers.'
}
[IO.File]::WriteAllText((Join-Path $workspace 'demo.json'), ($report | ConvertTo-Json -Depth 8))
Write-Host "Echo demo and complete local evidence: $workspace"
