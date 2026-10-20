param(
    [string]$IdentityName,
    [string]$Publisher,
    [string]$PublisherDisplayName,
    [string]$DisplayName = 'Realmly Companion',
    [string]$IdentityFile = (Join-Path $PSScriptRoot 'StoreIdentity.json'),
    [version]$Version = '0.2.0.0',
    [string]$MakeAppxPath,
    [switch]$ValidationOnly
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
if (!$ValidationOnly -and !$IdentityName -and !$Publisher -and !$PublisherDisplayName -and (Test-Path -LiteralPath $IdentityFile)) {
    $identity = Get-Content -LiteralPath $IdentityFile -Raw | ConvertFrom-Json
    $IdentityName = $identity.identityName
    $Publisher = $identity.publisher
    $PublisherDisplayName = $identity.publisherDisplayName
    if (!$PSBoundParameters.ContainsKey('DisplayName')) { $DisplayName = $identity.displayName }
}
if ($Version.Revision -ne 0 -or $Version.Build -lt 0) {
    throw 'Use a four-part Store version with revision zero, for example 0.2.0.0.'
}
if ($ValidationOnly) {
    if ($IdentityName -or $Publisher -or $PublisherDisplayName) {
        throw 'ValidationOnly uses a deliberately fake identity. Omit identity arguments or omit ValidationOnly.'
    }
    $IdentityName = 'Realmly.Companion.PackagingTest'
    $Publisher = 'CN=Realmly Packaging Test'
    $PublisherDisplayName = 'Realmly Packaging Test - not for submission'
} elseif ([string]::IsNullOrWhiteSpace($IdentityName) -or [string]::IsNullOrWhiteSpace($Publisher) -or [string]::IsNullOrWhiteSpace($PublisherDisplayName)) {
    throw 'Supply IdentityName, Publisher and PublisherDisplayName exactly as shown in Partner Center > Product management > Product identity. Use ValidationOnly only for a non-submittable test package.'
}
if (!$MakeAppxPath) {
    $sdkRoot = Join-Path ${env:ProgramFiles(x86)} 'Windows Kits/10/bin'
    $sdkCandidates = @(Get-ChildItem -LiteralPath $sdkRoot -Directory | Where-Object { $_.Name -match '^10\.0\.\d+\.0$' } |
        Sort-Object { [version]$_.Name } -Descending | ForEach-Object { Join-Path $_.FullName 'x64/makeappx.exe' } | Where-Object { Test-Path -LiteralPath $_ })
    if ($sdkCandidates.Count -eq 0) { throw 'Install the Windows 10/11 SDK (MakeAppx.exe is required).' }
    $MakeAppxPath = $sdkCandidates[0]
}
if (!(Test-Path -LiteralPath $MakeAppxPath -PathType Leaf)) { throw 'MakeAppx.exe was not found.' }
$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$buildRoot = Join-Path $repoRoot ('artifacts/msix/' + [guid]::NewGuid().ToString('N'))
$payload = Join-Path $buildRoot 'payload'
New-Item -ItemType Directory -Path $payload -Force | Out-Null

# A folder-based self-contained build avoids extracting a single-file executable
# into the user's temp directory. Never use the portable updater in this build.
& dotnet publish (Join-Path $repoRoot 'companion/RealmlyCompanion.csproj') -c Release -r win-x64 --self-contained true `
    '-p:StoreDistribution=true' '-p:PublishSingleFile=false' '-p:DebugType=None' '-p:DebugSymbols=false' `
    ('-p:Version=' + $Version.ToString(3)) ('-p:AssemblyVersion=' + $Version) ('-p:FileVersion=' + $Version) -o $payload
if ($LASTEXITCODE -ne 0) { throw 'Store publish failed.' }
Copy-Item -LiteralPath (Join-Path $repoRoot 'LICENSE') -Destination (Join-Path $payload 'LICENSE.txt')

# Preserve the licence/notices shipped with the self-contained Microsoft runtimes.
$dependencies = Get-Content -LiteralPath (Join-Path $payload 'RealmlyCompanion.deps.json') -Raw | ConvertFrom-Json
$restore = Get-Content -LiteralPath (Join-Path $repoRoot 'companion/obj/project.assets.json') -Raw | ConvertFrom-Json
foreach ($runtime in @($dependencies.libraries.PSObject.Properties | Where-Object { $_.Name -like 'runtimepack.*/*' })) {
    $packageRelative = $runtime.Name.Substring('runtimepack.'.Length).ToLowerInvariant()
    $packageRoots = @($restore.packageFolders.PSObject.Properties.Name)
    $packageSources = @($packageRoots | ForEach-Object { Join-Path $_ $packageRelative } | Where-Object { Test-Path -LiteralPath $_ -PathType Container })
    if ($packageSources.Count -eq 0) { throw ('Cannot find runtime licence files for ' + $packageRelative) }
    $notices = @(Get-ChildItem -LiteralPath $packageSources[0] -File | Where-Object { $_.Name -match '^(LICENSE(\.TXT)?|THIRD-PARTY-NOTICES\.TXT)$' })
    if (!($notices | Where-Object { $_.Name -match '^LICENSE' })) { throw ('Missing runtime licence: ' + $packageRelative) }
    $licenceFolder = Join-Path $payload ('ThirdPartyLicenses/' + $packageRelative)
    New-Item -ItemType Directory -Path $licenceFolder -Force | Out-Null
    foreach ($notice in $notices) { Copy-Item -LiteralPath $notice.FullName -Destination (Join-Path $licenceFolder $notice.Name) }
}

[xml]$manifest = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'AppxManifest.xml') -Raw
$manifest.Package.Identity.Name = $IdentityName
$manifest.Package.Identity.Publisher = $Publisher
$manifest.Package.Identity.Version = $Version.ToString()
$manifest.Package.Properties.PublisherDisplayName = $PublisherDisplayName
$manifest.Package.Properties.DisplayName = $DisplayName
$manifest.Package.Applications.Application.VisualElements.DisplayName = $DisplayName
$manifest.Save((Join-Path $payload 'AppxManifest.xml'))

# Derive the required Windows package logos from the existing app artwork.
Add-Type -AssemblyName System.Drawing
$assets = Join-Path $payload 'Assets'
New-Item -ItemType Directory -Path $assets -Force | Out-Null
$icon = [Drawing.Image]::FromFile((Join-Path $repoRoot 'companion/Assets/realmly-compass-v1.png'))
try {
    foreach ($logo in @(@('StoreLogo.png',50), @('Square44x44Logo.png',44), @('Square150x150Logo.png',150))) {
        $size = [int]$logo[1]
        $bitmap = [Drawing.Bitmap]::new($size, $size, [Drawing.Imaging.PixelFormat]::Format32bppArgb)
        $graphics = [Drawing.Graphics]::FromImage($bitmap)
        try {
            $graphics.Clear([Drawing.Color]::Transparent)
            $graphics.CompositingQuality = [Drawing.Drawing2D.CompositingQuality]::HighQuality
            $graphics.InterpolationMode = [Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
            $padding = [Math]::Max(1, [int]($size * 0.06))
            $graphics.DrawImage($icon, [Drawing.Rectangle]::new($padding, $padding, $size - 2*$padding, $size - 2*$padding))
            $bitmap.Save((Join-Path $assets $logo[0]), [Drawing.Imaging.ImageFormat]::Png)
        } finally { $graphics.Dispose(); $bitmap.Dispose() }
    }
} finally { $icon.Dispose() }

$suffix = if ($ValidationOnly) { '-validation-only' } else { '' }
$package = Join-Path $buildRoot ('RealmlyCompanion-' + $Version + '-x64' + $suffix + '.msix')
# Do not skip MakeAppx validation; each run uses a fresh output directory.
& $MakeAppxPath pack /d $payload /p $package /v *> (Join-Path $buildRoot 'makeappx.log')
if ($LASTEXITCODE -ne 0) { throw ('MakeAppx validation/packaging failed. See ' + (Join-Path $buildRoot 'makeappx.log')) }
Get-FileHash -LiteralPath $package -Algorithm SHA256 | Format-List
if ($ValidationOnly) { Write-Warning 'FAKE IDENTITY: packaging validation only. Do not install, submit or distribute this MSIX.' }
Write-Output ('MSIX: ' + $package)
Write-Output 'Unsigned submission package. Microsoft signs accepted Store packages; this is not a signed standalone download.'
