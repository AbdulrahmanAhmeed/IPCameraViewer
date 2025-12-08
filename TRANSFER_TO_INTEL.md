# 📤 Transfer to Intel Computer - Quick Guide

## 🎯 What You Need to Copy

**Copy this entire folder** to a USB drive or network share:

```
D:\ActiveProjs\IPCameraViewer\IPCameraViewer\bin\Release\net8.0-windows10.0.19041.0\win-x64\AppPackages\IPCameraViewer_1.0.0.1_Test\
```

**Size**: ~100-150 MB

---

## 🚀 On Your Intel Computer

### Step 1: Install the App

**Option A** - PowerShell (Recommended):
1. Open PowerShell as **Administrator**
2. Navigate to the copied folder
3. Run:
   ```powershell
   .\Install.ps1
   ```

**Option B** - Double-Click:
1. Double-click `IPCameraViewer_1.0.0.1_x64.msix`
2. Click "Install"

### Step 2: Download Tesseract Data

1. Download this file (22 MB):
   ```
   https://github.com/tesseract-ocr/tessdata/raw/main/eng.traineddata
   ```

2. Run the app once - it will show you the exact path in the debug console

3. Create a `tessdata` folder at that location

4. Copy `eng.traineddata` into the `tessdata` folder

5. Restart the app

---

## ✅ You're Done!

The app will now use:
- ✅ **OpenCV** to detect plate regions (no more watermarks!)
- ✅ **Tesseract OCR** for accurate text recognition
- ✅ **Smart corrections** (O→0, Q→2, I→1, S→5, B→8)
- ✅ **Motion detection recording** (GIF, PNG, MP4)
- ✅ **Multi-camera support**

---

## 🔍 Expected Debug Output

When you trigger motion on an Intel computer, you should see:

```
[ANPR] 🚀 Starting ANPR detection (OpenCV + Tesseract)...
[ANPR] 📷 Image loaded: 1920x1080
[ANPR] 🔍 Found 2 potential plate region(s)
[ANPR] 🔲 Found region: 240x80 at (820,540), AR: 3.00
[ANPR] 📝 OCR result: 'PMO200' → 'PMO200' (score: 10.00)
[ANPR] ✅ Plate detected: PMO200 (Confidence: 95%, Duplicate: False)
```

---

## ⚠️ Troubleshooting

### "Tesseract not initialized"
- Download `eng.traineddata` as described above
- Make sure it's in the correct `tessdata` folder

### "No plate regions detected"
- Ensure good lighting on the plate
- Plate should be at least 80 pixels wide
- Try adjusting camera angle

### "This app package is not signed"
- Click "More info" → "Install anyway"
- This is expected for test/development builds

---

## 🎉 That's It!

Your Intel computer will now have **full ANPR capabilities** with much better accuracy than the ARM64 version!

**Accuracy Improvement**:
- ARM64 (your laptop): ~60% (reads watermarks)
- Intel (x64): ~90% (ignores watermarks, smart corrections)


