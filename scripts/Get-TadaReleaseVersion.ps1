[CmdletBinding(DefaultParameterSetName = 'Validate')]
param(
    [Parameter(ParameterSetName = 'Validate')]
    [Parameter(Mandatory, ParameterSetName = 'Bump')]
    [string]$BaseCommit,

    [Parameter(Mandatory, ParameterSetName = 'Bump')]
    [ValidateSet('patch', 'minor', 'major')]
    [string]$Bump
)

$ErrorActionPreference = 'Stop'
$repository = Split-Path $PSScriptRoot -Parent
$number = '(?:0|[1-9][0-9]*)'
$corePattern = "($number)\.($number)\.($number)"
if (!$Bump) {
    $version = [string]([xml](Get-Content -LiteralPath "$repository/Directory.Build.props" -Raw)).Project.PropertyGroup.Version
    if ($version -cnotmatch "^$corePattern\z") {
        throw "Expected an exact MAJOR.MINOR.PATCH release version, found '$version'."
    }
}

if ($BaseCommit) {
    $baseProperties = git -C $repository show "${BaseCommit}:Directory.Build.props"
    if ($LASTEXITCODE) { throw "Cannot read the release version at $BaseCommit." }
    $baseVersion = [string]([xml]($baseProperties -join "`n")).Project.PropertyGroup.Version
    if ($baseVersion -cnotmatch "^$corePattern(?:-alpha\.[1-9][0-9]*)?\z") {
        throw "Cannot determine the previous release version from '$baseVersion'."
    }
    $major = [long]$Matches[1]
    $minor = [long]$Matches[2]
    $patch = [long]$Matches[3]
    $allowed = @{
        patch = "$major.$minor.$($patch + 1)"
        minor = "$major.$($minor + 1).0"
        major = "$($major + 1).0.0"
    }
    if ($Bump) { return $allowed[$Bump] }
    if ($version -cnotin $allowed.Values) {
        throw "Select one SemVer bump from $baseVersion. Patch: $($allowed.patch), minor: $($allowed.minor), major: $($allowed.major). Run scripts/Set-TadaVersion.ps1 -Bump with patch, minor or major."
    }
}
$version
