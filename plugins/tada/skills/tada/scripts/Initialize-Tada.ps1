param(
    [string]$Repository = (Get-Location).Path,
    [string]$Dotnet = 'dotnet',
    [ValidateSet('existing', 'recommended')][string]$Layout
)

$ErrorActionPreference = 'Stop'

# Automatically upgrade alpha builds within the same major/minor line.
# Older published builds can have a second numeric prerelease identifier.
function Test-OlderAlpha([string]$Installed, [string]$Target) {
    $pattern = '^((?:0|[1-9][0-9]*)\.(?:0|[1-9][0-9]*))\.(0|[1-9][0-9]*)-alpha\.([1-9][0-9]*)(?:\.([1-9][0-9]*))?$'
    $from = [regex]::Match($Installed, $pattern)
    $to = [regex]::Match($Target, $pattern)
    if (!$from.Success -or !$to.Success -or $from.Groups[1].Value -ne $to.Groups[1].Value) { return $false }
    foreach ($index in 2..4) {
        $fromPart = [long]$from.Groups[$index].Value
        $toPart = [long]$to.Groups[$index].Value
        if ($fromPart -ne $toPart) { return $fromPart -lt $toPart }
    }
    return $false
}
$skillDirectory = Split-Path $PSScriptRoot -Parent
$skillsDirectory = Split-Path $skillDirectory -Parent
$pluginRoot = Split-Path $skillsDirectory -Parent
$manifest = Get-Content -LiteralPath (Join-Path $pluginRoot 'plugin.json') -Raw | ConvertFrom-Json
$feed = Join-Path $pluginRoot 'feed'
$package = Join-Path $feed "Tada.Cli.$($manifest.version).nupkg"
if (!(Test-Path -LiteralPath $package)) { throw 'The packaged TADA feed is missing. Use the distribution built by New-TadaPlugin.ps1.' }

$projectRoot = (Resolve-Path -LiteralPath $Repository).Path
$previousPathSetup = $env:DOTNET_ADD_GLOBAL_TOOLS_TO_PATH
$env:DOTNET_ADD_GLOBAL_TOOLS_TO_PATH = 'false'
Push-Location -LiteralPath $projectRoot
try {
    $sdk = & $Dotnet --version
    if ($LASTEXITCODE -or $sdk -notmatch '^10\.0\.[1-9][0-9]{2,}$') {
        throw 'Select a stable .NET 10.0 SDK with -Dotnet that satisfies the application repository SDK policy.'
    }
    if (!(Test-Path -LiteralPath 'dotnet-tools.json') -and !(Test-Path -LiteralPath '.config/dotnet-tools.json')) {
        & $Dotnet new tool-manifest | Out-Host
        if ($LASTEXITCODE) { throw 'Local tool manifest creation failed.' }
    }
    $tools = (& $Dotnet tool list --local --format json | ConvertFrom-Json).data
    if ($LASTEXITCODE) { throw 'Cannot inspect the local tool manifest.' }
    $installed = @($tools | Where-Object packageId -eq 'tada.cli')
    $configuration = Join-Path $projectRoot ('.tada-feed-' + [guid]::NewGuid().ToString('N') + '.config')
    try {
        $escapedFeed = [Security.SecurityElement]::Escape($feed)
        [IO.File]::WriteAllText($configuration, '<configuration><packageSources><clear/><add key="tada" value="' + $escapedFeed + '"/></packageSources></configuration>')
        if ($installed.Count -eq 0) {
            & $Dotnet tool install Tada.Cli --version $manifest.version --configfile $configuration | Out-Host
        }
        else {
            if ($installed[0].version -ne $manifest.version -and !(Test-OlderAlpha $installed[0].version $manifest.version)) {
                throw "This project selects Tada.Cli $($installed[0].version). Select a compatible TADA plugin for that version."
            }
            & $Dotnet tool update Tada.Cli --version $manifest.version --configfile $configuration | Out-Host
        }
        if ($LASTEXITCODE) { throw 'TADA local tool installation or restore failed.' }
    }
    finally { if (Test-Path -LiteralPath $configuration) { Remove-Item -LiteralPath $configuration } }
    $initArguments = @('init')
    if ($Layout) { $initArguments += @('--layout', $Layout) }
    & $Dotnet tool run tada @initArguments
    if ($LASTEXITCODE) { throw 'TADA project preparation failed.' }
}
finally {
    Pop-Location
    $env:DOTNET_ADD_GLOBAL_TOOLS_TO_PATH = $previousPathSetup
}
