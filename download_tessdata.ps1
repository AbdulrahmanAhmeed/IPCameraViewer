# Download Tesseract Trained Data for ANPR
# Run this PowerShell script to automatically download eng.traineddata

Write-Host "ANPR Tesseract Data Downloader" -ForegroundColor Cyan
Write-Host "==============================" -ForegroundColor Cyan
Write-Host ""

# Find the output directory
$outputDir = "bin\Debug\net8.0-windows10.0.19041.0\win10-arm64\AppX"
if (-Not (Test-Path $outputDir)) {
    Write-Host "Output directory not found. Please build the project first." -ForegroundColor Yellow
    Write-Host "Run: dotnet build" -ForegroundColor Yellow
    exit 1
}

# Create tessdata folder
$tessDataDir = Join-Path $outputDir "tessdata"
if (-Not (Test-Path $tessDataDir)) {
    Write-Host "Creating tessdata folder..." -ForegroundColor Green
    New-Item -ItemType Directory -Path $tessDataDir | Out-Null
}

# Download eng.traineddata
$url = "https://github.com/tesseract-ocr/tessdata/raw/main/eng.traineddata"
$outputFile = Join-Path $tessDataDir "eng.traineddata"

if (Test-Path $outputFile) {
    Write-Host "eng.traineddata already exists!" -ForegroundColor Green
    $fileSize = (Get-Item $outputFile).Length / 1MB
    $fileSizeRounded = [math]::Round($fileSize, 2)
    Write-Host "File size: $fileSizeRounded MB" -ForegroundColor Gray
    Write-Host ""
    Write-Host "Setup complete! You can now run the application." -ForegroundColor Cyan
    exit 0
}

Write-Host "Downloading eng.traineddata (23 MB)..." -ForegroundColor Yellow
Write-Host "From: $url" -ForegroundColor Gray
Write-Host ""

try {
    Invoke-WebRequest -Uri $url -OutFile $outputFile -UseBasicParsing
    Write-Host "Download complete!" -ForegroundColor Green
    
    $fileSize = (Get-Item $outputFile).Length / 1MB
    $fileSizeRounded = [math]::Round($fileSize, 2)
    Write-Host "File size: $fileSizeRounded MB" -ForegroundColor Gray
    Write-Host ""
    Write-Host "Setup complete! You can now run the application." -ForegroundColor Cyan
    Write-Host ""
    Write-Host "The ANPR system is ready to detect license plates!" -ForegroundColor Green
}
catch {
    Write-Host "Download failed: $_" -ForegroundColor Red
    Write-Host ""
    Write-Host "Please download manually from:" -ForegroundColor Yellow
    Write-Host "$url" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "And save it to:" -ForegroundColor Yellow
    Write-Host "$outputFile" -ForegroundColor Cyan
    exit 1
}
