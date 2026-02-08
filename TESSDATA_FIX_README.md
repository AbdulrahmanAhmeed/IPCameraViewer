# 🔧 Tesseract Not Initialized - Fix Guide

## ⚠️ Problem
Your application shows:
```
[ANPR] ⚠️ Tesseract not available - eng.traineddata not found
[ANPR] ⚠️ Tesseract engine not initialized
```

## 📍 Root Cause
The application is looking for `eng.traineddata` file at:
```
C:\Users\AbdelrahmanSaleh\Documents\GitHub\IPCameraViewer\bin\Debug\net9.0-windows10.0.19041.0\win10-x64\AppX\tessdata\eng.traineddata
```

But the file only exists in your source folder at:
```
D:\ActiveProjs\IPCameraViewer\IPCameraViewer\tessdata\eng.traineddata
```

## ✅ Solution Options

### **Option 1: Use the PowerShell Script (QUICKEST)**

I've created a script that automatically copies `tessdata` to all build output directories.

1. **Open PowerShell** in the project directory:
   ```powershell
   cd D:\ActiveProjs\IPCameraViewer\IPCameraViewer
   ```

2. **Run the script**:
   ```powershell
   .\copy_tessdata.ps1
   ```

3. **Restart your application** from Visual Studio

---

### **Option 2: Manual Copy**

1. **Copy this folder**:
   ```
   D:\ActiveProjs\IPCameraViewer\IPCameraViewer\tessdata\
   ```

2. **To this location**:
   ```
   C:\Users\AbdelrahmanSaleh\Documents\GitHub\IPCameraViewer\bin\Debug\net9.0-windows10.0.19041.0\win10-x64\AppX\tessdata\
   ```

3. **Restart your application**

---

### **Option 3: Rebuild with Updated Project File (PERMANENT FIX)**

The `.csproj` file has been updated to automatically copy `tessdata` on every build.

**If your GitHub location is your main development folder:**

1. **Copy the updated `IPCameraViewer.csproj`** from:
   ```
   D:\ActiveProjs\IPCameraViewer\IPCameraViewer\IPCameraViewer.csproj
   ```
   
   To:
   ```
   C:\Users\AbdelrahmanSaleh\Documents\GitHub\IPCameraViewer\IPCameraViewer.csproj
   ```

2. **Copy the `tessdata` folder** to the GitHub location:
   ```
   Copy from: D:\ActiveProjs\IPCameraViewer\IPCameraViewer\tessdata\
   Copy to:   C:\Users\AbdelrahmanSaleh\Documents\GitHub\IPCameraViewer\tessdata\
   ```

3. **Rebuild in Visual Studio**:
   - Right-click solution → "Clean Solution"
   - Right-click solution → "Rebuild Solution"

4. **Run the application**

---

## ✅ Expected Success Output

After applying the fix, you should see:
```
[ANPR] ✅ Tesseract engine initialized successfully
[ANPR] 🚀 Starting ANPR detection...
[ANPR] 📷 Image loaded: 1920x1080
[ANPR] 🔍 Found potential plate region(s)
[ANPR] ✅ Plate detected: ABC123 (Confidence: 95%)
```

---

## 📝 Additional Notes

### About the Two Project Locations

You seem to have two copies of the project:
- **D:\ActiveProjs\IPCameraViewer\** (Cursor workspace)
- **C:\Users\AbdelrahmanSaleh\Documents\GitHub\IPCameraViewer\** (Visual Studio build location)

**Recommendation:** Decide which one is your primary development location and work from there consistently.

### Verifying the Fix

After applying any solution, check if the file exists:
```powershell
Test-Path "C:\Users\AbdelrahmanSaleh\Documents\GitHub\IPCameraViewer\bin\Debug\net9.0-windows10.0.19041.0\win10-x64\AppX\tessdata\eng.traineddata"
```

Should return: `True`

---

## 🆘 Still Not Working?

If you still see the warning after trying the above:

1. **Check the exact path** where your app is running from the debug output
2. **Navigate to that path** in File Explorer
3. **Manually create** a `tessdata` folder there
4. **Copy** `eng.traineddata` into it
5. **Restart** the application

---

## 📦 Download eng.traineddata (If Missing)

If you don't have `eng.traineddata` at all:

**Direct download:**
```
https://github.com/tesseract-ocr/tessdata/raw/main/eng.traineddata
```

**Size:** ~23 MB

**Save to:** `tessdata\eng.traineddata` in your project folder


