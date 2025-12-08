# 📦 Install IPCameraViewer on Intel/AMD (x64) Computers

## ✅ What You've Got

Your **x64 (Intel/AMD) release build** is ready!

**Location**: 
```
D:\ActiveProjs\IPCameraViewer\IPCameraViewer\bin\Release\net8.0-windows10.0.19041.0\win-x64\AppPackages\IPCameraViewer_1.0.0.1_Test\
```

## 🚀 Installation Steps

### ⚠️ IMPORTANT: Certificate Fix (If Needed)

If you get **"publisher certificate could not be verified"** error:

1. **Right-click** on `Install_Certificate.ps1` → **Run as Administrator**
   - This installs the test certificate to your system
   - Click "Yes" when prompted

2. Then proceed with installation below

---

### Option 1: Install Using PowerShell Script (Easiest)

1. **Navigate to the folder**:
   ```
   D:\ActiveProjs\IPCameraViewer\IPCameraViewer\bin\Release\net8.0-windows10.0.19041.0\win-x64\AppPackages\IPCameraViewer_1.0.0.1_Test\
   ```

2. **Right-click** on `Install.ps1` → **Run with PowerShell**
   - This will install the app package and all dependencies

3. **Find the app** in your Start Menu: "IP Camera Viewer"

### Option 2: Manual Installation

1. **Double-click** on:
   ```
   IPCameraViewer_1.0.0.1_x64.msix
   ```

2. **Click "Install"** when prompted

3. **Launch** from Start Menu

### Option 3: Enable Developer Mode (Alternative)

If you don't want to install the certificate:

1. Open **Settings** → **Privacy & Security** → **For developers**
2. Turn on **"Developer Mode"**
3. Then install the `.msix` file

## 📥 Required: Download Tesseract Data

After installation, the app needs the Tesseract language data:

1. **Download** `eng.traineddata`:
   https://github.com/tesseract-ocr/tessdata/raw/main/eng.traineddata

2. **Find the app's installation folder**:
   ```
   C:\Users\YourUsername\AppData\Local\Packages\com.companyname.ipcameraviewer_<random>\LocalCache\Local\
   ```
   (The exact path will vary)

3. **Create a folder** called `tessdata` inside the app folder

4. **Copy** `eng.traineddata` into the `tessdata` folder

**OR** just run the app once and check the debug output - it will tell you exactly where to put the file!

## 🎯 What Works on Intel/AMD x64:

- ✅ **Full ANPR** with OpenCV plate detection
- ✅ **Tesseract OCR** (after downloading trained data)
- ✅ **Automatic plate region detection** (ignores watermarks)
- ✅ **Motion detection recording** (GIF, PNG, MP4)
- ✅ **Per-camera sound alerts**
- ✅ **Multi-camera streaming**

## 📋 System Requirements

- **OS**: Windows 10 version 1809 (build 17763) or later
- **Architecture**: x64 (Intel/AMD 64-bit processors)
- **RAM**: 4GB minimum, 8GB recommended
- **Disk Space**: ~200MB (including dependencies)

## 🔧 Troubleshooting

### "This app package's publisher certificate could not be verified" (0x800B010A)
**Solution 1** - Install certificate (recommended):
- Right-click `Install_Certificate.ps1` → **Run as Administrator**
- Then install the app

**Solution 2** - Enable Developer Mode:
- Settings → Privacy & Security → For developers → Turn on "Developer Mode"
- Then install the app

**Solution 3** - Manual certificate install:
1. Right-click the `.msix` file → Properties
2. Digital Signatures tab → Select signature → Details
3. View Certificate → Install Certificate
4. Install to "Local Machine" → "Trusted Root Certification Authorities"

### "This app package is not signed"
- Click "More info" → "Install anyway"
- This is a test/debug build, not production-signed

### "Tesseract not initialized"
- Download `eng.traineddata` as described above
- Check the debug console for the exact path

### "OpenCV error"
- Ensure you're on an Intel/AMD processor (not ARM)
- Update Windows to the latest version

## 🎉 You're Ready!

Once installed:
1. Launch "IP Camera Viewer"
2. Add your camera stream
3. Watch ANPR detect license plates automatically!

The app will use:
- **OpenCV** to find plate regions (ignores watermarks)
- **Tesseract** for accurate OCR
- **Smart corrections** (O→0, Q→2, etc.)

---

**Note**: To transfer to another Intel computer, just copy the entire folder:
```
D:\ActiveProjs\IPCameraViewer\IPCameraViewer\bin\Release\net8.0-windows10.0.19041.0\win-x64\AppPackages\IPCameraViewer_1.0.0.1_Test\
```

And follow the same installation steps!


