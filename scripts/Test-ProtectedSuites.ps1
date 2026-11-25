param(
    [string]$Dotnet = 'dotnet',
    [string]$Cli = (Join-Path $PSScriptRoot '../src/Tada.Cli/bin/Release/net10.0/Tada.Cli.dll')
)
$ErrorActionPreference = 'Stop'
$repository = Split-Path $PSScriptRoot -Parent
$Dotnet = (Get-Command $Dotnet -CommandType Application).Source
$Cli = (Resolve-Path -LiteralPath $Cli).Path
$workspace = Join-Path $repository ('.artifacts/protected-suites/' + [guid]::NewGuid().ToString('N'))
$sample = Join-Path $repository 'examples/ProtectedSuites'
$results = [Collections.Generic.List[object]]::new()
New-Item -ItemType Directory -Path $workspace | Out-Null

function Copy-Example([bool]$NewSuite) {
    foreach ($file in Get-ChildItem -LiteralPath $sample -Recurse -File -Force) {
        $relative = [IO.Path]::GetRelativePath($sample, $file.FullName).Replace('\','/')
        if ($relative -match '(^|/)(bin|obj|\.tada)(/|$)' -or $relative -eq 'README.md') { continue }
        $isNew = $relative.StartsWith('tests/') -or $relative -eq 'fitness.verification.json'
        if ($isNew -ne $NewSuite) { continue }
        $destination = Join-Path $workspace $relative
        New-Item -ItemType Directory -Path (Split-Path $destination -Parent) -Force | Out-Null
        Copy-Item -LiteralPath $file.FullName -Destination $destination
    }
}

function Check([string]$Name, [string[]]$Arguments, [int]$ExpectedExit = 0) {
    $output = & $Dotnet $Cli @Arguments --repo $workspace --json 2>&1
    $exitCode = $LASTEXITCODE
    $json = $output -join [Environment]::NewLine
    $results.Add([pscustomobject]@{ control=$Name; exit=$exitCode; output=$json | ConvertFrom-Json })
    $results | ConvertTo-Json -Depth 35 | Set-Content -LiteralPath (Join-Path $workspace 'validation.json') -Encoding utf8
    if ($exitCode -ne $ExpectedExit) { throw "$Name expected exit $ExpectedExit, got $exitCode. $json Evidence: $workspace" }
    Write-Host "$Name (exit $exitCode)"
    return $results[$results.Count - 1].output
}

function Assert-Control([bool]$Condition, [string]$Message) {
    if (!$Condition) { throw "$Message Evidence: $workspace" }
}

function Approve-Fixture([string]$Id) {
    $review = Check "Review $Id" @('baseline','review','--baseline',$Id)
    Check 'Isolated approval control' @('baseline','approve','--review',$review.review.id,'--by',
        'isolated protected-suite demonstration control; not requester acceptance') | Out-Null
}

function Write-Json([string]$Path, $Value) {
    [IO.File]::WriteAllText((Join-Path $workspace $Path), ($Value | ConvertTo-Json -Depth 20))
}

Copy-Example $false
Push-Location -LiteralPath $workspace
try {
    $contractDigest = (Get-FileHash -LiteralPath interfaces/openapi.json).Hash
    $initial = Check 'Discover legacy contract without scaffolding' @('init')
    Assert-Control ($initial.layout -eq 'existing' -and $initial.discovered -contains 'interfaces/openapi.json' -and
        $initial.discovered -contains 'legacy/ContractChecks/Legacy.Contract.csproj') 'Contract infrastructure was not discovered.'
    Assert-Control (!(Test-Path tests) -and !(Test-Path tada.json)) 'Default initialisation added competing verification infrastructure.'
    Check 'Opt in to recommended conventions' @('init','--layout','recommended') | Out-Null
    Copy-Example $true
    $repeat = Check 'Reuse persisted preference' @('init')
    Assert-Control ($repeat.layout -eq 'recommended' -and $repeat.created.Count -eq 0 -and $repeat.updated.Count -eq 0) 'Layout was not idempotent.'
    Assert-Control ((Get-FileHash -LiteralPath interfaces/openapi.json).Hash -eq $contractDigest) 'Initialisation changed the existing OpenAPI file.'

    foreach ($project in @('legacy/ContractChecks/Legacy.Contract.csproj','tests/Fitness/Echo.Fitness.csproj')) {
        & $Dotnet restore $project --locked-mode -p:NuGetAudit=false --disable-build-servers | Out-Host
        if ($LASTEXITCODE) { throw "Locked restore failed for $project." }
    }
    $contract = (Check 'Prepare legacy contract' @('baseline','prepare','--file','contract.verification.json')).baseline
    $fitness = (Check 'Prepare executable allocation budget' @('baseline','prepare','--file','fitness.verification.json')).baseline
    Check 'Draft cannot execute as protected' @('run','--baseline',$fitness,'--dotnet',$Dotnet) 2 | Out-Null
    Approve-Fixture $contract
    Approve-Fixture $fitness
    $ledger = @{ schemaVersion=2; baseline=$null; active=@(); candidates=@(); protectedSuites=@($contract,$fitness) }
    Write-Json '.tada/enforcement.json' $ledger
    $hooks = Join-Path $workspace '.tada/control-hooks'
    New-Item -ItemType Directory -Path $hooks | Out-Null
    function FixtureGit([string[]]$Arguments) {
        & git -c core.longpaths=true -c "core.hooksPath=$hooks" -c commit.gpgsign=false -c 'user.name=TADA integration control' -c 'user.email=tada-control@example.invalid' @Arguments
        if ($LASTEXITCODE) { throw 'Local fixture Git command failed.' }
    }
    FixtureGit @('init','--quiet','--initial-branch=main',"--template=$hooks")
    FixtureGit @('add','--','.tada/enforcement.json')
    FixtureGit @('commit','--quiet','-m','Record mandatory verification suites')
    $plan = [ordered]@{
        schemaVersion=1; objective='Verify response shape and the warmed allocation budget.'
        categories=@('feat'); businessChange=$false; impact=@('Existing response contract and allocation budget')
        checks=@(
            @{id='echo-contract'; level='contract'; purpose='contract'; obligations=@('OpenAPI response shape'); rationale='Check the existing OpenAPI response.'; baseline=$contract},
            @{id='echo-fitness'; level='fitness'; purpose='fitness'; obligations=@('Warmed batch allocation budget'); rationale='Measure current-thread managed allocations.'; baseline=$fitness}
        )
        omissions=@('Functional journey and HTTP transport are outside this library example.')
    }
    Write-Json 'plan.json' $plan
    $selected = (Check 'Coordinate two independent approved suites' @('plan','prepare','--file','plan.json')).plan
    $green = Check 'Both suite runners pass' @('run','--plan',$selected,'--dotnet',$Dotnet)
    Check 'Reconcile both retained TRX reports offline' @('verify','--run',$green.runId) | Out-Null
    Check 'Both mandatory suites satisfy the gate' @('gate','--run',$green.runId,'--base','HEAD') | Out-Null

    $contractRun = ($green.checks | Where-Object id -eq 'echo-contract').result.runId
    $report = Join-Path $workspace ".tada/runs/$contractRun/harness/contract.trx"
    $reportBytes = [IO.File]::ReadAllBytes($report)
    try {
        [IO.File]::Delete($report)
        Check 'Missing contract TRX blocks verification' @('verify','--run',$green.runId) 2 | Out-Null
        Check 'Missing contract TRX blocks merge gate' @('gate','--run',$green.runId,'--base','HEAD') 2 | Out-Null
        [IO.File]::WriteAllText($report, '<invalid/>')
        Check 'Tampered contract TRX blocks verification' @('verify','--run',$green.runId) 2 | Out-Null
    }
    finally { [IO.File]::WriteAllBytes($report, $reportBytes) }

    $implementation = Join-Path $workspace 'legacy/Echo/Echo.cs'
    $source = [IO.File]::ReadAllText($implementation)
    try {
        [IO.File]::WriteAllText($implementation, $source.Replace('return value + 3;', 'GC.KeepAlive(new byte[16_384]); return value + 3;'))
        Check 'Old green evidence is stale for the merge gate' @('gate','--run',$green.runId,'--base','HEAD') 2 | Out-Null
        $red = Check 'Implementation exceeds approved allocated bytes' @('run','--plan',$selected,'--dotnet',$Dotnet) 1
        $failed = ($red.checks | Where-Object id -eq 'echo-fitness').result
        Assert-Control ($failed.reasons[0].code -eq 'protected-test-failure' -and $failed.reasons[0].detail -like '*Allocated*bytes*') 'Fitness RED did not come from its intended assertion.'
        Check 'Verified fitness failure blocks merge gate' @('gate','--run',$red.runId,'--base','HEAD') 1 | Out-Null
        # A suite can be selected before enrolment. The plan still requires its result.
        $ledger.protectedSuites = @($contract)
        try {
            Write-Json '.tada/enforcement.json' $ledger
            $selectedOnly = Check 'Selected fitness failure blocks the gate before enrolment' @('gate','--run',$red.runId) 1
            Assert-Control ($selectedOnly.reasons.code -contains 'protected-test-failure') 'Gate discarded the selected fitness failure.'
        }
        finally {
            $ledger.protectedSuites = @($contract,$fitness)
            Write-Json '.tada/enforcement.json' $ledger
        }
    }
    finally { [IO.File]::WriteAllText($implementation, $source) }

    $threshold = Join-Path $workspace 'tests/Fitness/AllocationChecks.cs'
    $thresholdText = [IO.File]::ReadAllText($threshold)
    try {
        [IO.File]::WriteAllText($threshold, $thresholdText.Replace('2_048', '20_000_000'))
        Check 'Unapproved threshold edit blocks fresh run' @('run','--baseline',$fitness,'--dotnet',$Dotnet) 2 | Out-Null
        Check 'Unapproved threshold edit blocks gate' @('gate','--run',$green.runId,'--base','HEAD') 2 | Out-Null
        Check 'Historical evidence retains its approved threshold' @('verify','--run',$green.runId) | Out-Null
    }
    finally { [IO.File]::WriteAllText($threshold, $thresholdText) }

    $ledger.protectedSuites = @($fitness)
    Write-Json '.tada/enforcement.json' $ledger
    $removed = Check 'Removing the contract from the ledger blocks the gate' @('gate','--run',$green.runId,'--base','HEAD') 2
    Assert-Control ($removed.reasons.code -contains 'protected-suite-removed') 'Gate failed to identify a removed contract.'
    $ledger.protectedSuites = @($contract,$fitness)
    Write-Json '.tada/enforcement.json' $ledger
    $plan.previous = $selected
    $plan.changeReason = 'Control of a plan that omits a mandatory suite.'
    $plan.checks = @($plan.checks[1])
    Write-Json 'plan.json' $plan
    $omission = (Check 'Prepare scoped fitness plan' @('plan','prepare','--file','plan.json')).plan
    $fitnessOnly = Check 'Scoped fitness plan passes' @('run','--plan',$omission,'--dotnet',$Dotnet)
    $blocked = Check 'Omitting the contract from a plan blocks the gate' @('gate','--run',$fitnessOnly.runId,'--base','HEAD') 2
    Assert-Control ($blocked.reasons.code -contains 'protected-suite-not-executed') 'Gate did not identify missing coverage.'
    Check 'Restored current implementation satisfies the original gate' @('gate','--run',$green.runId,'--base','HEAD') | Out-Null
    Write-Host "Protected-suite controls saved at $workspace/validation.json"
}
finally { Pop-Location }
