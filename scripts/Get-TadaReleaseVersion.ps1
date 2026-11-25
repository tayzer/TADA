param([string]$BaseCommit)

$ErrorActionPreference = 'Stop'
$repository = Split-Path $PSScriptRoot -Parent
$number = '(?:0|[1-9][0-9]*)'
$corePattern = "($number)\.($number)\.($number)"
$version = [string]([xml](Get-Content -LiteralPath "$repository/Directory.Build.props" -Raw)).Project.PropertyGroup.Version
if ($version -cnotmatch "^$corePattern-alpha\.1$") {
    throw "Expected an exact MAJOR.MINOR.PATCH-alpha.1 release version, found '$version'."
}
foreach ($manifest in @('plugin.json', '.codex-plugin/plugin.json', '.claude-plugin/plugin.json', '.cursor-plugin/plugin.json')) {
    $declared = (Get-Content -LiteralPath "$repository/plugins/tada/$manifest" -Raw | ConvertFrom-Json).version
    if ($declared -cne $version) { throw "plugins/tada/$manifest declares '$declared'. Expected '$version'." }
}

if ($BaseCommit) {
    $baseProperties = git -C $repository show "${BaseCommit}:Directory.Build.props"
    if ($LASTEXITCODE) { throw "Cannot read the release version at $BaseCommit." }
    $baseVersion = [string]([xml]($baseProperties -join "`n")).Project.PropertyGroup.Version
    if ($baseVersion -cnotmatch "^$corePattern-alpha\.[1-9][0-9]*$") {
        throw "Cannot determine the previous release version from '$baseVersion'."
    }
    $major = [long]$Matches[1]
    $minor = [long]$Matches[2]
    $patch = [long]$Matches[3]
    $allowed = @(
        "$major.$minor.$($patch + 1)-alpha.1"
        "$major.$($minor + 1).0-alpha.1"
        "$($major + 1).0.0-alpha.1"
    )
    if ($version -cnotin $allowed) {
        throw "Select one SemVer bump from $baseVersion. Patch: $($allowed[0]), minor: $($allowed[1]), major: $($allowed[2]). Run scripts/Set-TadaVersion.ps1 -Version with the chosen version."
    }
}
$version
