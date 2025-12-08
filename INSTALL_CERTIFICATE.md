# 🔒 Fix Certificate Trust Issue

## ⚠️ Error You're Seeing

```
This app package's publisher certificate could not be verified.
The root certificate and all immediate certificates of the signature in the app package must be verified (0x800B010A)
```

## ✅ Solution: Install the Test Certificate

### **Method 1: Right-Click Install (Easiest)**

1. **Right-click** on `IPCameraViewer_1.0.0.1_x64.msix`

2. Select **"Properties"**

3. Go to the **"Digital Signatures"** tab

4. Select the signature → Click **"Details"**

5. Click **"View Certificate"**

6. Click **"Install Certificate..."**

7. Select **"Local Machine"** (requires admin rights)

8. Choose **"Place all certificates in the following store"**

9. Click **"Browse"** → Select **"Trusted Root Certification Authorities"**

10. Click **"OK"** → **"Next"** → **"Finish"**

11. Click **"Yes"** to the security warning

12. Now double-click the `.msix` file again to install

---

### **Method 2: Using PowerShell (Alternative)**

Open **PowerShell as Administrator** and run:

```powershell
# Navigate to the folder
cd "D:\ActiveProjs\IPCameraViewer\IPCameraViewer\bin\Release\net8.0-windows10.0.19041.0\win-x64\AppPackages\IPCameraViewer_1.0.0.1_Test\"

# Extract and install the certificate
Add-AppxPackage -Path "IPCameraViewer_1.0.0.1_x64.msix" -Register AppxManifest.xml
```

If that doesn't work, use the installation script:

```powershell
.\Install.ps1
```

The script should handle certificate installation automatically.

---

### **Method 3: Enable Developer Mode (Windows 11)**

1. Open **Settings** → **Privacy & Security** → **For developers**

2. Turn on **"Developer Mode"**

3. Try installing the `.msix` again

**Note**: This allows installation of unsigned/test packages.

---

## 🏗️ **Better Solution: Sign the Package Properly**

If you want a properly signed package (for distribution), you need to:

1. **Create a self-signed certificate** (for testing)
2. **Re-build the package** with that certificate
3. **Install the certificate** on target machines

Would you like me to create a script to do this?

---

## ⚡ **Quick Workaround: Developer Sideloading**

If you just want to test the app quickly:

### **Step 1: Enable Sideloading**

1. Open **Settings** → **Apps** → **Apps & features**
2. Under "Choose where to get apps", select **"Anywhere"**

### **Step 2: Use the Install Script**

```powershell
# Run as Administrator
.\Install.ps1
```

This script should bypass some certificate checks.

---

## 🎯 **Recommended for Your Intel Laptop:**

Since this is a test/development build for your own use:

1. ✅ **Enable Developer Mode** (easiest)
2. ✅ **OR install the test certificate** (Methods 1 or 2 above)
3. ✅ Then install the `.msix` file

---

## 📝 **Why This Happens**

- The `.msix` package is signed with a **test certificate** (auto-generated during build)
- Windows doesn't trust this certificate by default
- You need to either:
  - Trust the certificate manually
  - Enable Developer Mode
  - Use a proper code-signing certificate (costs money)

---

## 🚀 **After Fixing the Certificate:**

The app will install normally and you'll have full ANPR capabilities!

Let me know which method you'd like to use, or if you want me to create a properly signed package!


