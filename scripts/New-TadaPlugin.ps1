param([Parameter(Mandatory)][string]$Package)

$ErrorActionPreference = 'Stop'
$repository = Split-Path $PSScriptRoot -Parent
$source = Join-Path $repository 'plugins/tada'
$version = & "$PSScriptRoot/Get-TadaReleaseVersion.ps1"
$marketplaceName = 'tada'
$marketplaceDescription = 'Test-Assured Development Assistance plugins for Codex, Claude Code and Cursor.'
$manifest = Get-Content -LiteralPath (Join-Path $source 'plugin.json') -Raw | ConvertFrom-Json
$packagePath = (Resolve-Path -LiteralPath $Package).Path
$packageZip = [IO.Compression.ZipFile]::OpenRead($packagePath)
try {
    $reader = [IO.StreamReader]::new($packageZip.GetEntry('Tada.Cli.nuspec').Open())
    try { [xml]$metadata = $reader.ReadToEnd() } finally { $reader.Dispose() }
    if ($metadata.package.metadata.id -ne 'Tada.Cli' -or $metadata.package.metadata.version -cne $version) {
        throw "Expected verifier package Tada.Cli $version, found $($metadata.package.metadata.id) $($metadata.package.metadata.version)."
    }
}
finally { $packageZip.Dispose() }

$output = Join-Path $repository ('.artifacts/plugin-bundles/' + [guid]::NewGuid().ToString('N'))
$marketplaceRoot = Join-Path $output 'marketplace'
$plugin = Join-Path $marketplaceRoot 'tada'
New-Item -ItemType Directory -Path $marketplaceRoot -Force | Out-Null
Copy-Item -LiteralPath $source -Destination $plugin -Recurse
# Each host reads its own manifest. Stamp the copies shared by both archives.
foreach ($relativePath in @('plugin.json', '.codex-plugin/plugin.json', '.claude-plugin/plugin.json', '.cursor-plugin/plugin.json')) {
    $path = Join-Path $plugin $relativePath
    $stagedManifest = Get-Content -LiteralPath $path -Raw | ConvertFrom-Json -AsHashtable
    if ($stagedManifest.name -ne $manifest.name) {
        throw "$relativePath must use the plugin name from plugin.json."
    }
    $stagedManifest['version'] = $version
    $stagedManifest | ConvertTo-Json -Depth 30 | Set-Content -LiteralPath $path -Encoding utf8
}
New-Item -ItemType Directory -Path (Join-Path $plugin 'feed') | Out-Null
Copy-Item -LiteralPath $packagePath -Destination (Join-Path $plugin "feed/Tada.Cli.$version.nupkg")
Copy-Item -LiteralPath (Join-Path $repository 'LICENSE') -Destination $plugin
Copy-Item -LiteralPath (Join-Path $repository 'THIRD_PARTY_NOTICES.md') -Destination $plugin
$catalogDirectory = Join-Path $marketplaceRoot '.agents/plugins'
New-Item -ItemType Directory -Path $catalogDirectory -Force | Out-Null
$catalog = [ordered]@{
    name = $marketplaceName
    plugins = @([ordered]@{
        name = 'tada'
        source = @{ source = 'local'; path = './tada' }
        policy = @{ installation = 'AVAILABLE'; authentication = 'ON_INSTALL' }
        category = 'Productivity'
    })
}
$catalog | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $catalogDirectory 'marketplace.json') -Encoding utf8
$standaloneCatalogDirectory = Join-Path $plugin '.agents/plugins'
New-Item -ItemType Directory -Path $standaloneCatalogDirectory -Force | Out-Null
$catalog.plugins[0].source.path = './'
$catalog | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $standaloneCatalogDirectory 'marketplace.json') -Encoding utf8
$claudeCatalogDirectory = Join-Path $marketplaceRoot '.claude-plugin'
New-Item -ItemType Directory -Path $claudeCatalogDirectory -Force | Out-Null
$claudeCatalog = [ordered]@{
    name = $marketplaceName
    description = $marketplaceDescription
    owner = @{ name = 'TADA contributors' }
    plugins = @(@{ name = $manifest.name; source = './tada' })
}
$claudeCatalog | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $claudeCatalogDirectory 'marketplace.json') -Encoding utf8
# The standalone ZIP also registers as a Claude Code marketplace that resolves its own root.
$claudeCatalog.plugins[0].source = './'
$claudeCatalog | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $plugin '.claude-plugin/marketplace.json') -Encoding utf8
$cursorCatalogDirectory = Join-Path $marketplaceRoot '.cursor-plugin'
New-Item -ItemType Directory -Path $cursorCatalogDirectory -Force | Out-Null
$cursorCatalog = [ordered]@{
    name = $marketplaceName
    owner = @{ name = 'TADA contributors' }
    metadata = @{ description = $marketplaceDescription }
    plugins = @([ordered]@{ name = $manifest.name; source = 'tada'; description = $manifest.description })
}
$cursorCatalog | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $cursorCatalogDirectory 'marketplace.json') -Encoding utf8

# The marketplace directory is published as the content of the Git marketplace repository.
Copy-Item -LiteralPath (Join-Path $repository 'LICENSE') -Destination $marketplaceRoot
$marketplaceGuide = (Get-Content -LiteralPath (Join-Path $PSScriptRoot 'marketplace/README.md') -Raw).Replace('VERSION_PLACEHOLDER', $version)
[IO.File]::WriteAllText((Join-Path $marketplaceRoot 'README.md'), $marketplaceGuide, [Text.UTF8Encoding]::new($false))
[IO.File]::WriteAllText((Join-Path $marketplaceRoot '.gitattributes'), "# Keep packaged plugin bytes identical to the audited release.`n* -text`n", [Text.UTF8Encoding]::new($false))

$archive = Join-Path $output "TADA-plugin-$version.zip"
[IO.Compression.ZipFile]::CreateFromDirectory($plugin, $archive)
$marketplaceArchive = Join-Path $output "TADA-marketplace-$version.zip"
[IO.Compression.ZipFile]::CreateFromDirectory($marketplaceRoot, $marketplaceArchive)
$result = [pscustomobject]@{
    version = $version
    sourceCommit = [string]$metadata.package.metadata.repository.commit
    marketplace = $marketplaceName
    archive = $archive
    archiveSha256 = (Get-FileHash -LiteralPath $archive).Hash.ToLowerInvariant()
    marketplaceArchive = $marketplaceArchive
    marketplaceArchiveSha256 = (Get-FileHash -LiteralPath $marketplaceArchive).Hash.ToLowerInvariant()
    packageSha256 = (Get-FileHash -LiteralPath $packagePath).Hash.ToLowerInvariant()
    marketplaceRoot = $marketplaceRoot
    pluginRoot = $plugin
    hosts = @('Codex', 'Claude Code', 'Cursor')
    status = 'Local plugin prepared for host installation and enable/disable testing'
}
$result | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $output 'plugin.json') -Encoding utf8
$result
