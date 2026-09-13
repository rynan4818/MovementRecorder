param(
    [Parameter(Mandatory = $true)][string]$GameDirectory,
    [ValidateSet('Debug', 'Release')][string]$Configuration = 'Release',
    [string]$MSBuildPath,
    [string]$NuGetSource = (Join-Path $env:USERPROFILE '.nuget\packages'),
    [switch]$Package
)
$ErrorActionPreference = 'Stop'
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$game = [IO.Path]::GetFullPath($GameDirectory)
if (!(Test-Path -LiteralPath (Join-Path $game 'Beat Saber_Data\Managed\Main.dll'))) { throw 'Beat Saber reference DLLs were not found.' }
if (!$MSBuildPath) {
    $vswhere = Join-Path ([Environment]::GetEnvironmentVariable('ProgramFiles(x86)')) 'Microsoft Visual Studio\Installer\vswhere.exe'
    $MSBuildPath = & $vswhere -latest -products '*' -requires Microsoft.Component.MSBuild -find 'MSBuild\**\Bin\MSBuild.exe' | Select-Object -First 1
}
if (!$MSBuildPath -or !(Test-Path -LiteralPath $MSBuildPath)) { throw 'Specify MSBuildPath for a Visual Studio installation with .NET Framework 4.7.2 targeting support.' }
$packages = Join-Path $env:USERPROFILE '.nuget\packages'
$arguments = @((Join-Path $repo 'MovementRecorder\MovementRecorder.csproj'), '/restore', "/p:Configuration=$Configuration",
    "/p:BeatSaberDir=$game", "/p:GameDirectory=$game", "/p:RestoreSources=$NuGetSource", "/p:RestorePackagesPath=$packages",
    "/p:NuGetPackagesDirectory=$packages", '/p:NuGetAudit=false', '/p:DisableCopyToGame=True', '/p:DisableCopyToPlugins=True',
    '/p:DisableZipRelease=True', '/p:ImportBSMTTargets=False', '/nologo', '/verbosity:minimal')
& $MSBuildPath @arguments
if ($LASTEXITCODE -ne 0) { throw "MSBuild failed: $LASTEXITCODE" }
$dll = Join-Path $repo "MovementRecorder\bin\$Configuration\MovementRecorder.dll"
Get-FileHash -LiteralPath $dll -Algorithm SHA256
if ($Package) {
    $artifacts = Join-Path $repo 'artifacts'
    $stage = Join-Path $artifacts ('package-' + [Guid]::NewGuid().ToString('N'))
    $plugins = Join-Path $stage 'Plugins'
    $documentation = Join-Path $stage 'Docs\MovementRecorder-Replay'
    $null = New-Item -ItemType Directory -Path $plugins, $documentation
    Copy-Item -LiteralPath $dll -Destination $plugins
    Copy-Item -LiteralPath (Join-Path $repo 'docs\Replay-ja.md'), (Join-Path $repo 'docs\Replay-Implementation-ja.md'),
        (Join-Path $repo 'docs\Replay-FileList-Fix-ja.md'), (Join-Path $repo 'docs\Replay-HDT-Startup-Fix-ja.md'),
        (Join-Path $repo 'docs\Replay-Model-Startup-Fix-ja.md'),
        (Join-Path $repo 'docs\Replay-Native-Sabers-Fix-ja.md'),
        (Join-Path $repo 'docs\Replay-Pause-UI-Fix-ja.md'),
        (Join-Path $repo 'THIRD-PARTY-NOTICES.md'), (Join-Path $repo 'LICENSE') -Destination $documentation
    Copy-Item -LiteralPath (Join-Path $repo 'licenses') -Destination $documentation -Recurse
    $zip = Join-Path $artifacts 'MovementRecorder-Replay-BS1.29.1-preview.zip'
    Compress-Archive -LiteralPath (Join-Path $stage 'Plugins'), (Join-Path $stage 'Docs') -DestinationPath $zip -Force
    Get-FileHash -LiteralPath $zip -Algorithm SHA256
}
# This script deliberately has no game deployment or git operation.
