param(
    [Parameter(Mandatory, Position=0)]
    [string[]]$Source,
    [string]$ReferenceRoot = (Join-Path (Split-Path $PSScriptRoot -Parent) 'artifacts\hero-identity\reference-bundle'),
    [switch]$UseMove,
    [switch]$ShowTree
)

$ErrorActionPreference = 'Stop'

$photoExtensions = @('.png', '.jpg', '.jpeg', '.webp', '.bmp', '.tga', '.gif', '.tiff', '.exr')
$modelExtensions = @('.blend', '.fbx', '.obj', '.glb', '.gltf', '.usd', '.usdc', '.usda', '.abc', '.ply', '.stl', '.dae', '.3ds')

function Is-Photo([string]$ext) {
    return $photoExtensions.Contains($ext.ToLowerInvariant())
}

function Is-Model([string]$ext) {
    return $modelExtensions.Contains($ext.ToLowerInvariant())
}

$resolvedSources = @($Source | ForEach-Object { (Resolve-Path -LiteralPath $_ -ErrorAction Stop).Path })
$items = @($resolvedSources | ForEach-Object {
    if ((Get-Item -LiteralPath $_).PSIsContainer) {
        Get-ChildItem -LiteralPath $_ -Recurse -File | ForEach-Object { $_.FullName }
    } else {
        $_
    }
} | Select-Object -Unique)
if ($items.Count -eq 0) { throw 'No files found in the supplied sources.' }

if (-not (Test-Path -LiteralPath $ReferenceRoot)) {
    $null = New-Item -ItemType Directory -Path $ReferenceRoot -Force
}

$stamp = (Get-Date -Format 'yyyyMMdd-HHmmss-fff') + '-' + [guid]::NewGuid().ToString('N').Substring(0, 8)
$sessionDir = Join-Path $ReferenceRoot $stamp
$photosDir = Join-Path $sessionDir 'photos'
$modelsDir = Join-Path $sessionDir 'models'
$otherDir = Join-Path $sessionDir 'other'
$null = New-Item -ItemType Directory -Path $photosDir, $modelsDir, $otherDir -Force

function Get-SafeDestination {
    param([string]$TargetDir, [System.IO.FileInfo]$Item)
    $dest = Join-Path $TargetDir $Item.Name
    $base = [System.IO.Path]::GetFileNameWithoutExtension($Item.Name)
    $ext = $Item.Extension
    $counter = 1
    while (Test-Path -LiteralPath $dest) {
        $dest = Join-Path $TargetDir ($base + '-' + $stamp + '-' + $counter + $ext)
        $counter++
    }
    return $dest
}

function Add-ManifestEntry {
    param([string]$source, [string]$destination)
    return [pscustomobject]@{ source = $source; destination = $destination }
}

$manifest = [pscustomobject]@{
    session = $stamp
    source_paths = $resolvedSources
    created_utc = (Get-Date).ToUniversalTime().ToString('o')
    photo_count = 0
    model_count = 0
    other_count = 0
    photos = @()
    models = @()
    others = @()
}

foreach ($path in $items) {
    $item = Get-Item -LiteralPath $path
    $ext = $item.Extension.ToLowerInvariant()
    if (Is-Photo $ext) {
        $target = Get-SafeDestination -TargetDir $photosDir -Item $item
        if ($UseMove) { Move-Item -LiteralPath $item.FullName -Destination $target } else { Copy-Item -LiteralPath $item.FullName -Destination $target }
        $manifest.photo_count++
        $manifest.photos += Add-ManifestEntry $item.FullName $target
    } elseif (Is-Model $ext) {
        $target = Get-SafeDestination -TargetDir $modelsDir -Item $item
        if ($UseMove) { Move-Item -LiteralPath $item.FullName -Destination $target } else { Copy-Item -LiteralPath $item.FullName -Destination $target }
        $manifest.model_count++
        $manifest.models += Add-ManifestEntry $item.FullName $target
    } else {
        $target = Get-SafeDestination -TargetDir $otherDir -Item $item
        if ($UseMove) { Move-Item -LiteralPath $item.FullName -Destination $target } else { Copy-Item -LiteralPath $item.FullName -Destination $target }
        $manifest.other_count++
        $manifest.others += Add-ManifestEntry $item.FullName $target
    }
}

$manifestPath = Join-Path $sessionDir 'manifest.json'
$manifest | ConvertTo-Json -Depth 10 | Set-Content -Encoding utf8 -Path $manifestPath

Write-Host "REFERENCE_BUNDLE=$sessionDir"
Write-Host "PHOTOS=$($manifest.photo_count), MODELS=$($manifest.model_count), OTHER=$($manifest.other_count)"
if ($ShowTree) {
    Get-ChildItem -LiteralPath $sessionDir -Recurse
}
