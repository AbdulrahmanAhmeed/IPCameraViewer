using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
#if WINDOWS
using Windows.Media.Ocr;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;
using System.Runtime.InteropServices.WindowsRuntime;
#endif

namespace IPCameraViewer.Services
{
    public class LicensePlateRecognitionService
    {
#if WINDOWS
        private OcrEngine? ocrEngine;
        private readonly Dictionary<string, DateTime> recentDetections = new();
        private readonly object detectionLock = new object();
        private const int DuplicateWindowSeconds = 5; // Suppress duplicates within 5 seconds
        
        /// <summary>
        /// Result of plate recognition including plate number and confidence
        /// </summary>
        public class PlateRecognitionResult
        {
            public string? PlateNumber { get; set; }
            public double Confidence { get; set; }
            public bool IsDuplicate { get; set; }
        }
        
        // Common license plate patterns (can be customized for different countries)
        private static readonly Regex[] PlatePatterns = new[]
        {
            new Regex(@"[A-Z]{1,3}\s*\d{1,4}", RegexOptions.Compiled), // ABC 123, AB 1234
            new Regex(@"\d{1,4}\s*[A-Z]{1,3}", RegexOptions.Compiled), // 123 ABC, 1234 AB
            new Regex(@"[A-Z]{2,3}[-\s]*\d{2,4}", RegexOptions.Compiled),   // ABC-123, AB-1234, ABC 123
            new Regex(@"\d{2,4}[-\s]*[A-Z]{2,3}", RegexOptions.Compiled),   // 123-ABC, 1234-AB, 123 ABC
            new Regex(@"[A-Z0-9]{1,6}[-\s][A-Z0-9]{1,6}", RegexOptions.Compiled), // Generic hyphenated or spaced
            new Regex(@"[A-Z]{2,4}\d{2,4}", RegexOptions.Compiled),         // BXJ823 (no space)
            new Regex(@"\d{2,4}[A-Z]{2,4}", RegexOptions.Compiled),         // 123ABC (no space)
            new Regex(@"[A-Z0-9]{5,8}", RegexOptions.Compiled)              // Generic alphanumeric (5-8 chars)
        };

        public LicensePlateRecognitionService()
        {
            try
            {
                // Initialize Windows OCR engine
                var currentLang = new Windows.Globalization.Language(Windows.Globalization.Language.CurrentInputMethodLanguageTag);
                if (OcrEngine.IsLanguageSupported(currentLang))
                {
                    this.ocrEngine = OcrEngine.TryCreateFromLanguage(currentLang);
                }
                
                if (this.ocrEngine == null)
                {
                    // Fallback to English if current language is not supported or TryCreate failed
                    var lang = new Windows.Globalization.Language("en-US");
                    if (OcrEngine.IsLanguageSupported(lang))
                    {
                        this.ocrEngine = OcrEngine.TryCreateFromLanguage(lang);
                    }
                }

                if (this.ocrEngine == null)
                {
                     // Fallback to any available language
                     if (OcrEngine.AvailableRecognizerLanguages.Count > 0)
                     {
                         this.ocrEngine = OcrEngine.TryCreateFromLanguage(OcrEngine.AvailableRecognizerLanguages[0]);
                     }
                }

                if (this.ocrEngine != null)
                {
                    System.Diagnostics.Debug.WriteLine($"[OCR] Windows OCR engine initialized. Language: {this.ocrEngine.RecognizerLanguage.DisplayName}");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"[OCR] WARNING: Could not initialize Windows OCR engine. No supported languages found.");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[OCR] Failed to initialize Windows OCR: {ex.Message}");
                this.ocrEngine = null;
            }
        }

        /// <summary>
        /// Recognizes a license plate from JPEG image bytes with confidence and duplicate detection
        /// </summary>
        public async Task<PlateRecognitionResult?> RecognizePlateWithConfidenceAsync(byte[] jpegBytes)
        {
            if (this.ocrEngine == null)
            {
                return null;
            }

            try
            {
                // Load image from JPEG bytes into SoftwareBitmap
                using (InMemoryRandomAccessStream stream = new InMemoryRandomAccessStream())
                {
                    await stream.WriteAsync(jpegBytes.AsBuffer());
                    stream.Seek(0);
                    
                    BitmapDecoder decoder;
                    try
                    {
                        decoder = await BitmapDecoder.CreateAsync(stream);
                    }
                    catch (Exception)
                    {
                        // Invalid image data, ignore
                        return null;
                    }

                    using (SoftwareBitmap softwareBitmap = await decoder.GetSoftwareBitmapAsync())
                    {
                        // OcrEngine requires Bgra8 or Gray8
                        SoftwareBitmap ocrBitmap = softwareBitmap;
                        bool needsDispose = false;

                        if (softwareBitmap.BitmapPixelFormat != BitmapPixelFormat.Bgra8 && 
                            softwareBitmap.BitmapPixelFormat != BitmapPixelFormat.Gray8)
                        {
                             ocrBitmap = SoftwareBitmap.Convert(softwareBitmap, BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied);
                             needsDispose = true;
                        }
                        
                        System.Diagnostics.Debug.WriteLine($"[OCR] Image size: {ocrBitmap.PixelWidth}x{ocrBitmap.PixelHeight}");

                        // Try OCR on full image first (simpler, works for poor quality streams)
                        try 
                        {
                            System.Diagnostics.Debug.WriteLine($"[OCR] Attempting OCR on full image...");
                            OcrResult result = await this.ocrEngine.RecognizeAsync(ocrBitmap);
                            
                            string text = result.Text;
                            
                            if (string.IsNullOrWhiteSpace(text))
                            {
                                System.Diagnostics.Debug.WriteLine($"[OCR] ⚠️ NO TEXT DETECTED - OCR returned empty result (Image quality too poor or no readable text)");
                                return null;
                            }

                            System.Diagnostics.Debug.WriteLine($"[OCR] ✓ Raw text detected: '{text}' (Length: {text.Length}, Lines: {result.Lines.Count})");

                            // Extract potential license plate from text using lines (more precise)
                            string? plateNumber = null;
                            double confidence = 0.0;
                            
                            // Collect all plate candidates with scores
                            var plateCandidates = new List<(string plate, double score, string originalText)>();
                            
                            foreach (var line in result.Lines)
                            {
                                string lineText = line.Text;
                                System.Diagnostics.Debug.WriteLine($"[OCR] Line text: '{lineText}'");
                                
                                string? candidatePlate = ExtractPlateNumber(lineText);
                                if (!string.IsNullOrEmpty(candidatePlate))
                                {
                                    // Score this candidate based on how plate-like it is
                                    double score = ScorePlateCandidate(lineText, candidatePlate);
                                    System.Diagnostics.Debug.WriteLine($"[OCR] ✓ Candidate '{candidatePlate}' from '{lineText}' scored {score:F2}");
                                    
                                    if (score > 0)
                                    {
                                        plateCandidates.Add((candidatePlate, score, lineText));
                                    }
                                    else
                                    {
                                        System.Diagnostics.Debug.WriteLine($"[OCR] ✗ Rejected (score <= 0)");
                                    }
                                }
                                else
                                {
                                    System.Diagnostics.Debug.WriteLine($"[OCR] ✗ No plate pattern match");
                                }
                            }
                            
                            // Choose the best candidate (highest score)
                            if (plateCandidates.Count > 0)
                            {
                                var bestCandidate = plateCandidates.OrderByDescending(c => c.score).First();
                                plateNumber = bestCandidate.plate;
                                confidence = Math.Min(0.95, bestCandidate.score / 10.0); // Normalize score to confidence
                                System.Diagnostics.Debug.WriteLine($"[OCR] Best plate candidate: {plateNumber} (score: {bestCandidate.score:F2}, confidence: {confidence:P0})");
                            }
                            
                            // Fallback: try full text if no line matched
                            if (string.IsNullOrEmpty(plateNumber))
                            {
                                plateNumber = ExtractPlateNumber(text);
                                if (string.IsNullOrEmpty(plateNumber))
                                {
                                    return null;
                                }
                                confidence = 0.6; // Lower confidence for full-text extraction
                            }
                            
                            // Filter out obvious non-plates (too many words, common phrases)
                            if (IsLikelyNonPlate(text))
                            {
                                System.Diagnostics.Debug.WriteLine($"[OCR] Filtered out non-plate text: {text}");
                                return null;
                            }

                            // Check for duplicates
                            bool isDuplicate = false;
                            lock (this.detectionLock)
                            {
                                // Clean up old detections
                                var now = DateTime.Now;
                                var expiredKeys = this.recentDetections
                                    .Where(kvp => (now - kvp.Value).TotalSeconds > DuplicateWindowSeconds)
                                    .Select(kvp => kvp.Key)
                                    .ToList();
                                
                                foreach (var key in expiredKeys)
                                {
                                    this.recentDetections.Remove(key);
                                }

                                // Check if this plate was recently detected
                                if (this.recentDetections.ContainsKey(plateNumber))
                                {
                                    isDuplicate = true;
                                }
                                else
                                {
                                    this.recentDetections[plateNumber] = now;
                                }
                            }

                            System.Diagnostics.Debug.WriteLine($"[OCR] License plate recognized: {plateNumber} (Confidence: {confidence:P0}, Duplicate: {isDuplicate})");

                            return new PlateRecognitionResult
                            {
                                PlateNumber = plateNumber,
                                Confidence = confidence,
                                IsDuplicate = isDuplicate
                            };
                        }
                        finally
                        {
                            if (needsDispose && ocrBitmap != softwareBitmap)
                            {
                                ocrBitmap.Dispose();
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[OCR] Error during recognition: {ex.Message}");
                return null;
            }
        }

        public async Task<string?> RecognizePlateAsync(byte[] jpegBytes)
        {
            if (this.ocrEngine == null)
            {
                return null;
            }

            try
            {
                // Load image from JPEG bytes into SoftwareBitmap
                using (InMemoryRandomAccessStream stream = new InMemoryRandomAccessStream())
                {
                    await stream.WriteAsync(jpegBytes.AsBuffer());
                    stream.Seek(0);
                    
                    BitmapDecoder decoder;
                    try
                    {
                        decoder = await BitmapDecoder.CreateAsync(stream);
                    }
                    catch (Exception)
                    {
                        // Invalid image data, ignore
                        return null;
                    }

                    using (SoftwareBitmap softwareBitmap = await decoder.GetSoftwareBitmapAsync())
                    {
                        // OcrEngine requires Bgra8 or Gray8
                        // If the image is not in one of these formats, convert it
                        SoftwareBitmap ocrBitmap = softwareBitmap;
                        bool needsDispose = false;

                        if (softwareBitmap.BitmapPixelFormat != BitmapPixelFormat.Bgra8 && 
                            softwareBitmap.BitmapPixelFormat != BitmapPixelFormat.Gray8)
                        {
                             ocrBitmap = SoftwareBitmap.Convert(softwareBitmap, BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied);
                             needsDispose = true;
                        }

                        try 
                        {
                            // Perform OCR
                            OcrResult result = await this.ocrEngine.RecognizeAsync(ocrBitmap);
                            
                            string text = result.Text;
                            
                            if (string.IsNullOrWhiteSpace(text))
                            {
                                return null;
                            }

                            System.Diagnostics.Debug.WriteLine($"[OCR] Raw text detected: {text}");

                            // Extract potential license plate from text
                            string? plateNumber = ExtractPlateNumber(text);
                            
                            if (!string.IsNullOrEmpty(plateNumber))
                            {
                                System.Diagnostics.Debug.WriteLine($"[OCR] License plate recognized: {plateNumber}");
                            }

                            return plateNumber;
                        }
                        finally
                        {
                            if (needsDispose)
                            {
                                ocrBitmap.Dispose();
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[OCR] Error during recognition: {ex.Message}");
                return null;
            }
        }

        private static string? ExtractPlateNumber(string text)
        {
            // Clean up text - remove extra whitespace and newlines
            text = Regex.Replace(text, @"\s+", " ").Trim().ToUpperInvariant();
            
            // Common OCR misreads: Fix letter O <-> digit 0, letter I <-> digit 1
            string cleanedText = text;
            System.Diagnostics.Debug.WriteLine($"[OCR ExtractPlate] Processing text: '{text}'");

            // Try each pattern
            int patternIndex = 0;
            foreach (var pattern in PlatePatterns)
            {
                var match = pattern.Match(cleanedText);
                if (match.Success)
                {
                    // Normalize the plate number (uppercase, single space)
                    string plate = match.Value.ToUpperInvariant();
                    plate = Regex.Replace(plate, @"\s+", ""); // Remove spaces for consistency
                    
                    // Fix common OCR errors in the matched plate
                    plate = FixCommonOcrErrors(plate);
                    
                    System.Diagnostics.Debug.WriteLine($"[OCR ExtractPlate] Pattern {patternIndex} matched: '{plate}'");
                    return plate;
                }
                patternIndex++;
            }

            // If no pattern matched but we have text that looks like it could be a plate
            // (mix of letters and numbers OR all Os/0s, reasonable length)
            string noSpaces = cleanedText.Replace(" ", "");
            if (noSpaces.Length >= 4 && noSpaces.Length <= 10)
            {
                // Check if it has letters and numbers, or is mostly O/0 (common OCR issue)
                bool hasLetters = Regex.IsMatch(noSpaces, @"[A-Z]");
                bool hasDigits = Regex.IsMatch(noSpaces, @"\d");
                bool hasManyOs = noSpaces.Count(c => c == 'O') >= 2;
                
                if ((hasLetters && hasDigits) || (hasLetters && hasManyOs))
                {
                    string corrected = FixCommonOcrErrors(noSpaces);
                    System.Diagnostics.Debug.WriteLine($"[OCR ExtractPlate] Fallback match: '{corrected}' (from '{noSpaces}')");
                    return corrected;
                }
            }

            System.Diagnostics.Debug.WriteLine($"[OCR ExtractPlate] No match found");
            return null;
        }
        
        /// <summary>
        /// Fix common OCR errors in license plates
        /// </summary>
        private static string FixCommonOcrErrors(string plate)
        {
            // For plates like "PMO200", "PMOOOO", "PMOQOO" etc., we need smart correction
            // Common OCR confusions: O/0, Q/0, I/1, S/5, B/8
            
            // Pattern: Letters at start, then digits/Os/Qs at end
            var match = Regex.Match(plate, @"^([A-Z]{2,4})([OQ0-9]+)$");
            if (match.Success)
            {
                string letterPart = match.Groups[1].Value;
                string numberPart = match.Groups[2].Value;
                
                // In the number section, convert letter-like chars to digits
                string fixedNumberPart = numberPart
                    .Replace('O', '0')  // Letter O → Digit 0
                    .Replace('Q', '2'); // Letter Q → Digit 2 (looks similar when blurry)
                
                if (fixedNumberPart != numberPart)
                {
                    string corrected = letterPart + fixedNumberPart;
                    System.Diagnostics.Debug.WriteLine($"[OCR Fix] Corrected '{plate}' → '{corrected}' (O→0, Q→2)");
                    return corrected;
                }
            }
            
            // Pattern: digits/Os/Qs at start, then letters
            match = Regex.Match(plate, @"^([OQ0-9]+)([A-Z]{2,4})$");
            if (match.Success)
            {
                string numberPart = match.Groups[1].Value;
                string letterPart = match.Groups[2].Value;
                
                string fixedNumberPart = numberPart
                    .Replace('O', '0')
                    .Replace('Q', '2');
                
                if (fixedNumberPart != numberPart)
                {
                    string corrected = fixedNumberPart + letterPart;
                    System.Diagnostics.Debug.WriteLine($"[OCR Fix] Corrected '{plate}' → '{corrected}' (O→0, Q→2)");
                    return corrected;
                }
            }
            
            return plate;
        }
        
        /// <summary>
        /// Scores a plate candidate - higher score = more likely to be a real plate
        /// </summary>
        private static double ScorePlateCandidate(string originalText, string extractedPlate)
        {
            double score = 0;
            
            originalText = originalText.Trim().ToUpperInvariant();
            extractedPlate = extractedPlate.Trim().ToUpperInvariant();
            
            // 1. Length scoring (plates are typically 5-8 characters)
            int plateLength = extractedPlate.Replace(" ", "").Replace("-", "").Length;
            if (plateLength >= 5 && plateLength <= 8)
            {
                score += 3.0; // Strong indicator
            }
            else if (plateLength >= 4 && plateLength <= 10)
            {
                score += 1.5;
            }
            else
            {
                score -= 2.0; // Too short or too long
            }
            
            // 2. Character composition (good mix of letters and numbers)
            int letterCount = extractedPlate.Count(c => char.IsLetter(c));
            int digitCount = extractedPlate.Count(c => char.IsDigit(c));
            int oCount = extractedPlate.Count(c => c == 'O'); // Letter O might be misread 0
            
            if (letterCount >= 2 && digitCount >= 2)
            {
                score += 4.0; // Excellent mix (e.g., PMO200)
            }
            else if (letterCount >= 2 && oCount >= 2)
            {
                // Pattern like "PMOOOO" - likely OCR mistook digits for O
                score += 3.5; // High score, likely a plate with OCR errors
            }
            else if (letterCount >= 1 && digitCount >= 1)
            {
                score += 1.5; // Some mix
            }
            else if (letterCount >= 2 && digitCount == 0 && oCount == 0)
            {
                score -= 3.0; // Only letters, no numbers = probably not a plate
            }
            else
            {
                score -= 2.0; // Unlikely to be a plate
            }
            
            // 3. Penalize if original text is much longer (likely a sentence)
            int wordCount = originalText.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries).Length;
            if (wordCount > 5)
            {
                score -= 5.0; // Strong penalty for long phrases
            }
            else if (wordCount > 3)
            {
                score -= 2.0;
            }
            else if (wordCount <= 3 && wordCount >= 2)
            {
                score += 1.0; // Bonus for 2-3 words (typical plate format: "PMO 200")
            }
            else if (wordCount == 1)
            {
                score += 2.5; // High bonus for single "word"
            }
            
            // 4. Penalize common non-plate words in original text
            string[] badKeywords = { "IMAGE", "PHOTO", "ROYALTY", "FREE", "STOCK", "COPYRIGHT", "WATERMARK" };
            foreach (var keyword in badKeywords)
            {
                if (originalText.Contains(keyword))
                {
                    score -= 10.0; // Major penalty
                    break;
                }
            }
            
            // 5. Bonus for typical plate patterns
            if (System.Text.RegularExpressions.Regex.IsMatch(extractedPlate, @"^[A-Z]{2,3}\s?\d{2,4}$"))
            {
                score += 2.0; // ABC 123 pattern
            }
            else if (System.Text.RegularExpressions.Regex.IsMatch(extractedPlate, @"^\d{2,4}\s?[A-Z]{2,3}$"))
            {
                score += 2.0; // 123 ABC pattern
            }
            
            // 6. Penalize if too many non-alphanumeric characters
            int specialCharCount = extractedPlate.Count(c => !char.IsLetterOrDigit(c) && c != ' ' && c != '-');
            if (specialCharCount > 2)
            {
                score -= 3.0;
            }
            
            return score;
        }
        
        /// <summary>
        /// Checks if the text line is likely to contain a license plate
        /// </summary>
        private static bool IsLikelyPlate(string text)
        {
            text = text.Trim().ToUpperInvariant();
            
            // Plates are typically short (3-10 characters)
            if (text.Length < 3 || text.Length > 15)
            {
                return false;
            }
            
            // Plates have a good mix of letters and numbers
            int letterCount = text.Count(c => char.IsLetter(c));
            int digitCount = text.Count(c => char.IsDigit(c));
            
            if (letterCount == 0 || digitCount == 0)
            {
                return false;
            }
            
            // Should not have too many spaces or special characters
            int wordCount = text.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries).Length;
            if (wordCount > 3)
            {
                return false;
            }
            
            return true;
        }
        
        /// <summary>
        /// Checks if the text is likely NOT a license plate (common phrases, URLs, etc.)
        /// </summary>
        private static bool IsLikelyNonPlate(string text)
        {
            text = text.ToUpperInvariant();
            
            // Filter out common non-plate phrases
            string[] nonPlateKeywords = {
                "IMAGE", "PHOTO", "STOCK", "ROYALTY", "FREE", "COPYRIGHT",
                "ALAMY", "SHUTTERSTOCK", "GETTY", "WATERMARK", "PLATE",
                "NUMBER", "HTTP", "WWW", "COM", "SEARCH", "GOOGLE",
                "AUSTRALIAN", "LICENSE", "REGISTRATION"
            };
            
            foreach (var keyword in nonPlateKeywords)
            {
                if (text.Contains(keyword))
                {
                    return true;
                }
            }
            
            // If text has more than 4 words, it's likely not a plate
            int wordCount = text.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries).Length;
            if (wordCount > 4)
            {
                return true;
            }
            
            return false;
        }

#else
        // Non-Windows platforms - OCR not available
        public LicensePlateRecognitionService()
        {
            System.Diagnostics.Debug.WriteLine("[OCR] License plate recognition is only available on Windows");
        }

        public class PlateRecognitionResult
        {
            public string? PlateNumber { get; set; }
            public double Confidence { get; set; }
            public bool IsDuplicate { get; set; }
        }

        public Task<string?> RecognizePlateAsync(byte[] jpegBytes)
        {
            // OCR not available on this platform
            return Task.FromResult<string?>(null);
        }

        public Task<PlateRecognitionResult?> RecognizePlateWithConfidenceAsync(byte[] jpegBytes)
        {
            // OCR not available on this platform
            return Task.FromResult<PlateRecognitionResult?>(null);
        }
#endif
    }
}
