# ANPR Setup Guide

## ✅ What's Already Installed

- ✅ OpenCvSharp4 (Computer Vision for plate detection)
- ✅ Tesseract OCR (Text recognition engine)
- ✅ Integration complete and built successfully

## 📥 Required: Download Tesseract Trained Data

The application needs Tesseract language data to perform OCR. Follow these steps:

### Step 1: Download eng.traineddata

**Option A: Direct Download**
1. Download this file:  
   https://github.com/tesseract-ocr/tessdata/raw/main/eng.traineddata
2. Save it to your Downloads folder

**Option B: Using Browser**
1. Visit: https://github.com/tesseract-ocr/tessdata
2. Click on `eng.traineddata`
3. Click the "Download" button

### Step 2: Create tessdata Folder

Create a `tessdata` folder in your application's output directory:

```
D:\ActiveProjs\IPCameraViewer\IPCameraViewer\bin\Debug\net8.0-windows10.0.19041.0\win10-arm64\AppX\tessdata\
```

### Step 3: Copy the File

Copy `eng.traineddata` into the `tessdata` folder you just created.

## 🚀 How to Use

1. Run the application
2. Add your camera stream
3. When motion is detected, the ANPR system will:
   - ✅ Find license plate regions using edge detection
   - ✅ Crop and preprocess the plate image
   - ✅ Run Tesseract OCR on the plate
   - ✅ Apply smart character corrections (O→0, Q→2, etc.)
   - ✅ Filter out watermarks and non-plate text
   - ✅ Display the detected plate number with confidence score

## 🔍 Debug Output

Watch the debug console for:
- `[ANPR] 📷 Image loaded` - Image processing started
- `[ANPR] 🔍 Found N potential plate region(s)` - Regions detected
- `[ANPR] 📝 OCR result` - Text recognized
- `[ANPR] ✅ License plate` - Final result with confidence

## 🎯 Expected Improvements

The new ANPR system should:
- ✅ **Ignore watermarks** (by detecting plate regions, not the whole image)
- ✅ **Better accuracy** (preprocessed images work better with OCR)
- ✅ **Smart corrections** (O→0, Q→2, I→1, S→5, B→8)
- ✅ **Confidence scoring** (only show high-confidence results)

## ⚠️ Troubleshooting

**If you see**: `[ANPR] ⚠️ Tesseract tessdata not found`
- You need to download and install the trained data file (see above)

**If plates are not detected**:
- Check lighting - plates need good contrast
- Try adjusting the camera angle
- Ensure the plate is large enough in the frame (at least 80x20 pixels)

**If wrong characters are read**:
- This is expected with poor image quality
- The system automatically corrects common mistakes (O→0, Q→2)
- Higher confidence scores = more accurate readings

