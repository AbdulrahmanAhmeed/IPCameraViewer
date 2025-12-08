using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using OpenCvSharp;
using Tesseract;

namespace IPCameraViewer.Services
{
    /// <summary>
    /// Advanced ANPR service using OpenCV for plate detection and Tesseract for OCR
    /// </summary>
    public class AnprService
    {
        private TesseractEngine? tesseractEngine;
        private readonly Dictionary<string, DateTime> recentDetections = new();
        private readonly object detectionLock = new object();
        private const int DuplicateWindowSeconds = 5;

        public class PlateRecognitionResult
        {
            public string? PlateNumber { get; set; }
            public double Confidence { get; set; }
            public bool IsDuplicate { get; set; }
            public Mat? PlateImage { get; set; }
        }

        public AnprService()
        {
            InitializeTesseract();
        }

        private void InitializeTesseract()
        {
            try
            {
                // Tesseract needs tessdata folder with trained data files
                // For now, we'll use the default English trained data
                string tessDataPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tessdata");
                
                if (!Directory.Exists(tessDataPath))
                {
                    System.Diagnostics.Debug.WriteLine($"[ANPR] Creating tessdata directory: {tessDataPath}");
                    Directory.CreateDirectory(tessDataPath);
                    System.Diagnostics.Debug.WriteLine("[ANPR] ⚠️ Tesseract tessdata not found. Please download eng.traineddata from:");
                    System.Diagnostics.Debug.WriteLine("[ANPR]    https://github.com/tesseract-ocr/tessdata/raw/main/eng.traineddata");
                    System.Diagnostics.Debug.WriteLine($"[ANPR]    and place it in: {tessDataPath}");
                }

                if (File.Exists(Path.Combine(tessDataPath, "eng.traineddata")))
                {
                    this.tesseractEngine = new TesseractEngine(tessDataPath, "eng", EngineMode.Default);
                    
                    // Configure for license plate recognition
                    this.tesseractEngine.SetVariable("tessedit_char_whitelist", "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-");
                    this.tesseractEngine.SetVariable("classify_bln_numeric_mode", "1");
                    
                    System.Diagnostics.Debug.WriteLine("[ANPR] ✅ Tesseract engine initialized successfully");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("[ANPR] ⚠️ Tesseract not available - eng.traineddata not found");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ANPR] ❌ Failed to initialize Tesseract: {ex.Message}");
            }
        }

        public async Task<PlateRecognitionResult?> RecognizePlateAsync(byte[] jpegBytes)
        {
            if (this.tesseractEngine == null)
            {
                System.Diagnostics.Debug.WriteLine("[ANPR] ⚠️ Tesseract engine not initialized");
                return null;
            }

            return await Task.Run(() =>
            {
                try
                {
                    // Step 1: Load image with OpenCV
                    Mat originalImage = Cv2.ImDecode(jpegBytes, ImreadModes.Color);
                    if (originalImage.Empty())
                    {
                        System.Diagnostics.Debug.WriteLine("[ANPR] ❌ Failed to decode image");
                        return null;
                    }

                    System.Diagnostics.Debug.WriteLine($"[ANPR] 📷 Image loaded: {originalImage.Width}x{originalImage.Height}");

                    // Step 2: Find potential license plate regions
                    var plateRegions = FindPlateRegions(originalImage);
                    
                    if (plateRegions.Count == 0)
                    {
                        System.Diagnostics.Debug.WriteLine("[ANPR] ⚠️ No plate regions detected");
                        return null;
                    }

                    System.Diagnostics.Debug.WriteLine($"[ANPR] 🔍 Found {plateRegions.Count} potential plate region(s)");

                    // Step 3: Try OCR on each region
                    PlateRecognitionResult? bestResult = null;
                    double bestScore = 0;

                    foreach (var region in plateRegions)
                    {
                        Mat plateImage = new Mat(originalImage, region);
                        Mat processedPlate = PreprocessPlateImage(plateImage);

                        string? plateText = PerformOcr(processedPlate);
                        
                        if (!string.IsNullOrEmpty(plateText))
                        {
                            string cleanedPlate = CleanPlateText(plateText);
                            double score = ScorePlateCandidate(cleanedPlate);

                            System.Diagnostics.Debug.WriteLine($"[ANPR] 📝 OCR result: '{plateText}' → '{cleanedPlate}' (score: {score:F2})");

                            if (score > bestScore)
                            {
                                bestScore = score;
                                bestResult = new PlateRecognitionResult
                                {
                                    PlateNumber = cleanedPlate,
                                    Confidence = Math.Min(0.95, score / 10.0),
                                    PlateImage = processedPlate.Clone()
                                };
                            }
                        }

                        processedPlate.Dispose();
                        plateImage.Dispose();
                    }

                    originalImage.Dispose();

                    if (bestResult == null || string.IsNullOrEmpty(bestResult.PlateNumber))
                    {
                        System.Diagnostics.Debug.WriteLine("[ANPR] ❌ No valid plate text recognized");
                        return null;
                    }

                    // Check for duplicates
                    bool isDuplicate = false;
                    lock (this.detectionLock)
                    {
                        var now = DateTime.Now;
                        var expiredKeys = this.recentDetections
                            .Where(kvp => (now - kvp.Value).TotalSeconds > DuplicateWindowSeconds)
                            .Select(kvp => kvp.Key)
                            .ToList();

                        foreach (var key in expiredKeys)
                        {
                            this.recentDetections.Remove(key);
                        }

                        if (this.recentDetections.ContainsKey(bestResult.PlateNumber))
                        {
                            isDuplicate = true;
                        }
                        else
                        {
                            this.recentDetections[bestResult.PlateNumber] = now;
                        }
                    }

                    bestResult.IsDuplicate = isDuplicate;

                    System.Diagnostics.Debug.WriteLine($"[ANPR] ✅ License plate: {bestResult.PlateNumber} (Confidence: {bestResult.Confidence:P0}, Duplicate: {isDuplicate})");

                    return bestResult;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[ANPR] ❌ Error: {ex.Message}");
                    return null;
                }
            });
        }

        private List<OpenCvSharp.Rect> FindPlateRegions(Mat image)
        {
            var regions = new List<OpenCvSharp.Rect>();

            try
            {
                // Convert to grayscale
                Mat gray = new Mat();
                Cv2.CvtColor(image, gray, ColorConversionCodes.BGR2GRAY);

                // Apply bilateral filter to reduce noise while keeping edges sharp
                Mat filtered = new Mat();
                Cv2.BilateralFilter(gray, filtered, 11, 17, 17);

                // Edge detection
                Mat edges = new Mat();
                Cv2.Canny(filtered, edges, 30, 200);

                // Find contours
                Cv2.FindContours(edges, out OpenCvSharp.Point[][] contours, out HierarchyIndex[] hierarchy, 
                    RetrievalModes.Tree, ContourApproximationModes.ApproxSimple);

                // Filter contours that could be license plates
                foreach (var contour in contours)
                {
                    var rect = Cv2.BoundingRect(contour);
                    double aspectRatio = (double)rect.Width / rect.Height;

                    // License plates typically have aspect ratio between 2:1 and 5:1
                    // And occupy 2-10% of image area
                    double areaRatio = (double)(rect.Width * rect.Height) / (image.Width * image.Height);

                    if (aspectRatio >= 1.5 && aspectRatio <= 6.0 && 
                        areaRatio >= 0.01 && areaRatio <= 0.15 &&
                        rect.Width > 80 && rect.Height > 20)
                    {
                        // Add some padding
                        int padding = 5;
                        var paddedRect = new OpenCvSharp.Rect(
                            Math.Max(0, rect.X - padding),
                            Math.Max(0, rect.Y - padding),
                            Math.Min(image.Width - rect.X + padding, rect.Width + padding * 2),
                            Math.Min(image.Height - rect.Y + padding, rect.Height + padding * 2)
                        );

                        regions.Add(paddedRect);
                        System.Diagnostics.Debug.WriteLine($"[ANPR] 🔲 Found region: {rect.Width}x{rect.Height} at ({rect.X},{rect.Y}), AR: {aspectRatio:F2}");
                    }
                }

                // If no regions found, use center region as fallback
                if (regions.Count == 0)
                {
                    System.Diagnostics.Debug.WriteLine("[ANPR] ⚠️ No contours matched, using center crop fallback");
                    int cropTop = (int)(image.Height * 0.35);
                    int cropLeft = (int)(image.Width * 0.15);
                    int cropWidth = (int)(image.Width * 0.7);
                    int cropHeight = (int)(image.Height * 0.4);
                    
                    regions.Add(new OpenCvSharp.Rect(cropLeft, cropTop, cropWidth, cropHeight));
                }

                gray.Dispose();
                filtered.Dispose();
                edges.Dispose();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ANPR] ❌ Error finding plate regions: {ex.Message}");
            }

            return regions;
        }

        private Mat PreprocessPlateImage(Mat plateImage)
        {
            // Convert to grayscale
            Mat gray = new Mat();
            Cv2.CvtColor(plateImage, gray, ColorConversionCodes.BGR2GRAY);

            // Resize to improve OCR (Tesseract works better at 300 DPI equivalent)
            Mat resized = new Mat();
            double scale = Math.Max(2.0, 300.0 / plateImage.Width);
            Cv2.Resize(gray, resized, new OpenCvSharp.Size(0, 0), scale, scale, InterpolationFlags.Cubic);

            // Apply adaptive threshold
            Mat thresh = new Mat();
            Cv2.AdaptiveThreshold(resized, thresh, 255, AdaptiveThresholdTypes.GaussianC, 
                ThresholdTypes.Binary, 11, 2);

            // Denoise
            Mat denoised = new Mat();
            Cv2.FastNlMeansDenoising(thresh, denoised, 10, 7, 21);

            gray.Dispose();
            resized.Dispose();
            thresh.Dispose();

            return denoised;
        }

        private string? PerformOcr(Mat image)
        {
            try
            {
                // Convert OpenCV Mat to byte array
                Cv2.ImEncode(".png", image, out byte[] imageBytes);

                using var pix = Pix.LoadFromMemory(imageBytes);
                using var page = this.tesseractEngine!.Process(pix);
                
                string text = page.GetText().Trim();
                float confidence = page.GetMeanConfidence();

                if (confidence < 0.3f) // Low confidence threshold
                {
                    return null;
                }

                return text;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ANPR] ❌ OCR error: {ex.Message}");
                return null;
            }
        }

        private static string CleanPlateText(string text)
        {
            // Remove all whitespace and special characters
            text = Regex.Replace(text, @"[^A-Z0-9-]", "");
            
            // Common OCR corrections
            text = text.Replace('O', '0');
            text = text.Replace('Q', '0');
            text = text.Replace('I', '1');
            text = text.Replace('S', '5');
            text = text.Replace('B', '8');
            
            return text.ToUpperInvariant();
        }

        private static double ScorePlateCandidate(string plate)
        {
            double score = 0;

            // Length check (typical plates are 5-8 characters)
            if (plate.Length >= 5 && plate.Length <= 8)
                score += 5.0;
            else if (plate.Length >= 4 && plate.Length <= 9)
                score += 2.0;
            else
                return 0; // Too short/long

            // Must have both letters and numbers
            bool hasLetters = Regex.IsMatch(plate, @"[A-Z]");
            bool hasDigits = Regex.IsMatch(plate, @"\d");
            
            if (hasLetters && hasDigits)
                score += 3.0;
            else
                return 0; // Not a valid plate pattern

            // Common plate patterns
            if (Regex.IsMatch(plate, @"^[A-Z]{3}\d{3}$")) // ABC123
                score += 2.0;
            else if (Regex.IsMatch(plate, @"^[A-Z]{2}\d{4}$")) // AB1234
                score += 2.0;
            else if (Regex.IsMatch(plate, @"^\d{3}[A-Z]{3}$")) // 123ABC
                score += 2.0;

            return score;
        }

        public void Dispose()
        {
            this.tesseractEngine?.Dispose();
        }
    }
}

