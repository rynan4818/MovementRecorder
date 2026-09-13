param([string]$Configuration = 'Release')
$ErrorActionPreference = 'Stop'
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$project = Join-Path $repo 'MovementRecorder'
$manifest = Get-Content -LiteralPath (Join-Path $project 'manifest.json') -Raw | ConvertFrom-Json
$commit = & git -C $repo rev-parse --short=7 HEAD
if ($LASTEXITCODE -ne 0) { throw 'Cannot read Git revision.' }
$name = 'MovementRecorder-{0}-bs{1}-{2}.zip' -f $manifest.version, $manifest.gameVersion, $commit
$zip = Join-Path $project "bin\$Configuration\zip\$name"
$expected = [ordered]@{
    'Plugins/MovementRecorder.dll' = Join-Path $project "bin\$Configuration\MovementRecorder.dll"
    'Docs/MovementRecorder/Replay-ja.md' = Join-Path $repo 'docs\Replay-ja.md'
    'Docs/MovementRecorder/LICENSE' = Join-Path $repo 'LICENSE'
    'Docs/MovementRecorder/THIRD-PARTY-NOTICES.md' = Join-Path $repo 'THIRD-PARTY-NOTICES.md'
    'Docs/MovementRecorder/licenses/LiteDB-LICENSE.txt' = Join-Path $repo 'licenses\LiteDB-LICENSE.txt'
    'Docs/MovementRecorder/licenses/System.Buffers-LICENSE.txt' = Join-Path $repo 'licenses\System.Buffers-LICENSE.txt'
    'Docs/MovementRecorder/licenses/System.Buffers-THIRD-PARTY-NOTICES.txt' = Join-Path $repo 'licenses\System.Buffers-THIRD-PARTY-NOTICES.txt'
}
$archive = [IO.Compression.ZipFile]::OpenRead($zip)
try {
    $files = @($archive.Entries | Where-Object { $_.Name -ne '' })
    if ($files.Count -ne $expected.Count) { throw 'Unexpected files in release ZIP.' }
    foreach ($entry in $files) {
        $key = $entry.FullName.Replace('\', '/')
        if (!$expected.Contains($key)) { throw "Unexpected ZIP entry: $key" }
        $stream = $entry.Open()
        try { $hash = (Get-FileHash -InputStream $stream -Algorithm SHA256).Hash } finally { $stream.Dispose() }
        if ($hash -ne (Get-FileHash -LiteralPath $expected[$key] -Algorithm SHA256).Hash) { throw "Packaged content differs: $key" }
    }
    [pscustomobject]@{ Result = 'Passed'; Entries = $files.Count; ZIP = $zip; SHA256 = (Get-FileHash -LiteralPath $zip).Hash }
} finally { $archive.Dispose() }
