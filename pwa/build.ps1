$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$dist = Join-Path $root 'dist'

if (Test-Path -LiteralPath $dist) {
    Remove-Item -LiteralPath $dist -Recurse -Force
}

New-Item -ItemType Directory -Path $dist | Out-Null
New-Item -ItemType Directory -Path (Join-Path $dist 'icons') | Out-Null

$topLevelFiles = @(
    'index.html',
    'styles.css',
    'app.js',
    'sw.js',
    'manifest.webmanifest',
    'runtime-config.js',
    'version.json'
)

foreach ($file in $topLevelFiles) {
    Copy-Item -LiteralPath (Join-Path $root $file) -Destination (Join-Path $dist $file)
}

Copy-Item -LiteralPath (Join-Path $root 'icons\icon.svg') -Destination (Join-Path $dist 'icons\icon.svg')
Copy-Item -LiteralPath (Join-Path $root 'icons\icon-maskable.svg') -Destination (Join-Path $dist 'icons\icon-maskable.svg')

$sourceRoot = Join-Path $root 'src'
$sourceFiles = Get-ChildItem -Path $sourceRoot -Recurse -File -Include *.js |
    Where-Object { $_.FullName -notmatch '\\src\\todos\\' }
foreach ($sourceFile in $sourceFiles) {
    $relativePath = $sourceFile.FullName.Substring($sourceRoot.Length).TrimStart('\')
    $targetPath = Join-Path $dist "src\$relativePath"
    $targetDirectory = Split-Path -Parent $targetPath

    if (-not (Test-Path -LiteralPath $targetDirectory)) {
        New-Item -ItemType Directory -Path $targetDirectory -Force | Out-Null
    }

    Copy-Item -LiteralPath $sourceFile.FullName -Destination $targetPath
}

Write-Host "Built static PWA to $dist"
