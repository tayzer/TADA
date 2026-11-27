[CmdletBinding(DefaultParameterSetName = 'Version', PositionalBinding = $false)]
param(
    [Parameter(Mandatory, ParameterSetName = 'Bump')]
    [ValidateSet('patch', 'minor', 'major')]
    [string]$Bump,

    [Parameter(ParameterSetName = 'Bump')]
    [string]$BaseCommit = 'origin/main',

    [Parameter(Mandatory, Position = 0, ParameterSetName = 'Version')]
    [ValidatePattern('^(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\z')]
    [string]$Version
)

# Record the selected release version in the build properties for review in a pull request.
$ErrorActionPreference = 'Stop'
$repository = Split-Path $PSScriptRoot -Parent
$path = Join-Path $repository 'Directory.Build.props'
if ($Bump) {
    $Version = & "$PSScriptRoot/Get-TadaReleaseVersion.ps1" -BaseCommit $BaseCommit -Bump $Bump
}

$text = [IO.File]::ReadAllText($path)
$pattern = '<Version>[^<]+</Version>'
$found = [regex]::Matches($text, $pattern).Count
if ($found -ne 1) { throw "Expected exactly one version in $path, found $found." }
[IO.File]::WriteAllText($path, [regex]::Replace($text, $pattern, "<Version>$Version</Version>"), [Text.UTF8Encoding]::new($false))
$Version
