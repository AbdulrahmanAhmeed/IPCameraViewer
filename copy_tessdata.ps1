# Quick fix: Copy tessdata to all build output directories
# This script finds all AppX folders and copies tessdata into them

Write-Host "================================================" -ForegroundColor Cyan
Write-Host "  Tesseract Data Deployment Script" -ForegroundColor Cyan
Write-Host "================================================" -ForegroundColor Cyan
Write-Host ""

# Check if tessdata source exists
$sourceTessdata = "tessdata"
if (-Not (Test-Path $sourceTessdata)) {
    Write-Host "ERROR: tessdata folder not found in current directory!" -ForegroundColor Red
    Write-Host "Expected: $((Get-Location).Path)\tessdata" -ForegroundColor Yellow
    Write-Host ""
    Write-Host "Please run this script from the project root directory." -ForegroundColor Yellow
    Read-Host "Press Enter to exit"
    exit 1
}

# Check if eng.traineddata exists
$trainedDataFile = Join-Path $sourceTessdata "eng.traineddata"
if (-Not (Test-Path $trainedDataFile)) {
    Write-Host "ERROR: eng.traineddata not found!" -ForegroundColor Red
    Write-Host "Expected: $trainedDataFile" -ForegroundColor Yellow
    Write-Host ""
    Write-Host "Download it from:" -ForegroundColor Yellow
    Write-Host "https://github.com/tesseract-ocr/tessdata/raw/main/eng.traineddata" -ForegroundColor Cyan
    Write-Host ""
    Read-Host "Press Enter to exit"
    exit 1
}

$fileSize = (Get-Item $trainedDataFile).Length / 1MB
Write-Host "Found eng.traineddata ($([math]::Round($fileSize, 2)) MB)" -ForegroundColor Green
Write-Host ""

# Find all possible output directories
$searchPaths = @(
    "bin\Debug\**\AppX",
    "bin\Release\**\AppX",
    "..\IPCameraViewer\bin\Debug\**\AppX",
    "..\IPCameraViewer\bin\Release\**\AppX"
)

# Also check the GitHub location mentioned in the error
$githubPath = "C:\Users\$env:USERNAME\Documents\GitHub\IPCameraViewer\bin\Debug"
if (Test-Path $githubPath) {
    $searchPaths += "$githubPath\**\AppX"
}

Write-Host "Searching for build output directories..." -ForegroundColor Yellow
Write-Host ""

$foundDirectories = @()
foreach ($searchPath in $searchPaths) {
    try {
        $dirs = Get-ChildItem -Path $searchPath -Directory -ErrorAction SilentlyContinue
        if ($dirs) {
            $foundDirectories += $dirs
        }
    } catch {
        # Ignore errors for paths that don't exist
    }
}

if ($foundDirectories.Count -eq 0) {
    Write-Host "No AppX build directories found." -ForegroundColor Yellow
    Write-Host ""
    Write-Host "Please build your project first, then run this script again." -ForegroundColor Yellow
    Write-Host ""
    Write-Host "Build command: dotnet build" -ForegroundColor Cyan
    Write-Host ""
    Read-Host "Press Enter to exit"
    exit 0
}

Write-Host "Found $($foundDirectories.Count) build directory(ies):" -ForegroundColor Green
Write-Host ""

$copiedCount = 0
$skippedCount = 0

foreach ($dir in $foundDirectories) {
    $targetTessdata = Join-Path $dir.FullName "tessdata"
    $targetFile = Join-Path $targetTessdata "eng.traineddata"
    
    # Check if already exists
    if (Test-Path $targetFile) {
        $existingSize = (Get-Item $targetFile).Length
        $sourceSize = (Get-Item $trainedDataFile).Length
        
        if ($existingSize -eq $sourceSize) {
            Write-Host "SKIP: $($dir.FullName)" -ForegroundColor Gray
            Write-Host "      (already up to date)" -ForegroundColor Gray
            $skippedCount++
            continue
        }
    }
    
    # Create tessdata folder if it doesn't exist
    if (-Not (Test-Path $targetTessdata)) {
        New-Item -ItemType Directory -Path $targetTessdata -Force | Out-Null
    }
    
    # Copy the file
    try {
        Copy-Item -Path $trainedDataFile -Destination $targetFile -Force
        Write-Host "COPY: $($dir.FullName)" -ForegroundColor Green
        $copiedCount++
    } catch {
        Write-Host "FAIL: $($dir.FullName)" -ForegroundColor Red
        Write-Host "      Error: $($_.Exception.Message)" -ForegroundColor Red
    }
}

Write-Host ""
Write-Host "================================================" -ForegroundColor Cyan
Write-Host "Deployment complete!" -ForegroundColor Green
Write-Host ""
Write-Host "Copied:  $copiedCount" -ForegroundColor Green
Write-Host "Skipped: $skippedCount (already existed)" -ForegroundColor Gray
Write-Host ""
Write-Host "You can now run your application." -ForegroundColor Cyan
Write-Host "Expected: [ANPR] Tesseract engine initialized" -ForegroundColor Yellow
Write-Host ""
Write-Host "================================================" -ForegroundColor Cyan
Write-Host ""
