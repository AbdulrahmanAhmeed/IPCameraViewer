# OCR Integration Script
# This script automatically adds all the necessary code for license plate recognition

Write-Host "=== OCR Integration Script ===" -ForegroundColor Cyan
Write-Host ""

$projectPath = "d:\ActiveProjs\IPCameraViewer\IPCameraViewer"
$viewModelFile = Join-Path $projectPath "Models\CameraStreamViewModel.cs"
$mainPageFile = Join-Path $projectPath "MainPage.xaml.cs"

# Step 1: Add CurrentFrameBytes to CameraStreamViewModel.cs
Write-Host "Step 1: Adding CurrentFrameBytes property to CameraStreamViewModel..." -ForegroundColor Yellow
$content = Get-Content $viewModelFile -Raw
if ($content -notmatch "CurrentFrameBytes")
{
    $content = $content -replace '(// Frame buffer for recording\r?\n\s+public IPCameraViewer\.Services\.FrameBuffer\? FrameBuffer \{ get; set; \}\r?\n\s+\r?\n\s+// Recording state)', 
        '$1' + "`r`n        `r`n        // Current frame bytes for OCR processing`r`n        public byte[]? CurrentFrameBytes { get; set; }`r`n        "
    Set-Content $viewModelFile -Value $content -NoNewline
    Write-Host "  ✓ Added CurrentFrameBytes property" -ForegroundColor Green
}
else
{
    Write-Host "  ✓ CurrentFrameBytes already exists" -ForegroundColor Green
}

# Step 2: Add OCR service field to MainPage.xaml.cs
Write-Host "Step 2: Adding plateRecognitionService field to MainPage..." -ForegroundColor Yellow
$content = Get-Content $mainPageFile -Raw
if ($content -notmatch "plateRecognitionService")
{
    $content = $content -replace '(private readonly SettingsService settingsService = new\(\);)', 
        '$1' + "`r`n`tprivate readonly LicensePlateRecognitionService plateRecognitionService = new();"
    Set-Content $mainPageFile -Value $content -NoNewline
    Write-Host "  ✓ Added plateRecognitionService field" -ForegroundColor Green
}
else
{
    Write-Host "  ✓ plateRecognitionService already exists" -ForegroundColor Green
}

# Step 3: Add plate log format constant
Write-Host "Step 3: Adding MotionDetectedWithPlateLogFormat constant..." -ForegroundColor Yellow
$content = Get-Content $mainPageFile -Raw
if ($content -notmatch "MotionDetectedWithPlateLogFormat")
{
    $content = $content -replace '(private const string MotionDetectedLogFormat = "\[{0}\] Motion detected \(ratio={1:0\.000}\)";)', 
        '$1' + "`r`n`tprivate const string MotionDetectedWithPlateLogFormat = `"[{0}] Motion detected (ratio={1:0.000}) - Plate: {2}`";"
    Set-Content $mainPageFile -Value $content -NoNewline
    Write-Host "  ✓ Added MotionDetectedWithPlateLogFormat constant" -ForegroundColor Green
}
else
{
    Write-Host "  ✓ MotionDetectedWithPlateLogFormat already exists" -ForegroundColor Green
}

# Step 4: Store frame bytes in OnFrameReceived
Write-Host "Step 4: Adding frame bytes storage in OnFrameReceived..." -ForegroundColor Yellow
$content = Get-Content $mainPageFile -Raw
if ($content -notmatch "streamViewModel\.CurrentFrameBytes = jpegBytes")
{
    $content = $content -replace '(private void OnFrameReceived\(CameraStreamViewModel streamViewModel, byte\[\] jpegBytes\)\r?\n\s+{\r?\n\s+long timestampMs = Environment\.TickCount64;)', 
        '$1' + "`r`n`r`n            // Store current frame bytes for OCR processing`r`n            streamViewModel.CurrentFrameBytes = jpegBytes;"
    Set-Content $mainPageFile -Value $content -NoNewline
    Write-Host "  ✓ Added frame bytes storage" -ForegroundColor Green
}
else
{
    Write-Host "  ✓ Frame bytes storage already exists" -ForegroundColor Green
}

# Step 5: Add OCR to OnMotion method
Write-Host "Step 5: Adding OCR recognition to OnMotion..." -ForegroundColor Yellow
$content = Get-Content $mainPageFile -Raw
if ($content -notmatch "plateRecognitionService\.RecognizePlateAsync")
{
    # Add OCR code before MainThread.BeginInvokeOnMainThread
    $ocrCode = @"
`r`n`r`n            // Try to recognize license plate from the current frame
            string? plateNumber = null;
            if (streamViewModel.CurrentFrameBytes != null)
            {
                try
                {
                    plateNumber = await this.plateRecognitionService.RecognizePlateAsync(streamViewModel.CurrentFrameBytes);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine(`$"[OCR] Error recognizing plate: {ex.Message}");
                }
            }
"@
    
    # Make OnMotion async
    $content = $content -replace '(private void OnMotion\(CameraStreamViewModel streamViewModel\))', 
        'private async void OnMotion(CameraStreamViewModel streamViewModel)'

    $content = $content -replace '(private async void OnMotion\(CameraStreamViewModel streamViewModel\)\r?\n\s+{\r?\n\s+var detectionTime = DateTime\.Now;)\r?\n\s+\r?\n\s+(MainThread\.BeginInvokeOnMainThread)', 
        '$1' + $ocrCode + "`r`n            `r`n            " + '$2'
    
    # Update log format to include plate number
    $content = $content -replace '(streamViewModel\.DetectionLogs\.Add\(string\.Format\(MainPage\.MotionDetectedLogFormat, timestamp, streamViewModel\.LastRatio\)\);)', 
        @"
// Add log with or without plate number
                if (!string.IsNullOrEmpty(plateNumber))
                {
                    streamViewModel.DetectionLogs.Add(string.Format(MainPage.MotionDetectedWithPlateLogFormat, timestamp, streamViewModel.LastRatio, plateNumber));
                }
                else
                {
                    streamViewModel.DetectionLogs.Add(string.Format(MainPage.MotionDetectedLogFormat, timestamp, streamViewModel.LastRatio));
                }
"@
    
    Set-Content $mainPageFile -Value $content -NoNewline
    Write-Host "  ✓ Added OCR recognition code" -ForegroundColor Green
}
else
{
    Write-Host "  ✓ OCR recognition already exists" -ForegroundColor Green
}

# Step 6: Add disposal in OnDisappearing
Write-Host "Step 6: OCR service disposal check..." -ForegroundColor Yellow
# Windows OCR service doesn't need disposal in the same way, skipping this step or keeping it for cleanup if needed
Write-Host "  ✓ OCR service disposal handled (none needed for Windows OCR)" -ForegroundColor Green

Write-Host ""
Write-Host "=== Integration Complete! ===" -ForegroundColor Green
Write-Host ""
Write-Host "Next steps:" -ForegroundColor Cyan
Write-Host "1. Build the project: dotnet build"
Write-Host "2. Run the app"
Write-Host "3. Add camera stream (http://192.168.1.2:8080/video)"
Write-Host "4. Show 'ABC 123' written on paper to camera"
Write-Host "5. Check Debug Output for [OCR] messages"
Write-Host ""
