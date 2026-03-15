# Project Legacy — Developer Setup Guide

## Prerequisites

- **Unity 2022.3.30f1** with the following modules:
  - Android Build Support
  - OpenJDK (included with Android module)
  - Android SDK & NDK Tools (included with Android module)
- **Git** for version control
- **DOS Daggerfall data files** — the `arena2` folder from the original game
  - Free from [Steam](https://store.steampowered.com/app/1812390/The_Elder_Scrolls_II_Daggerfall/)
  - Free from [Bethesda.net](https://elderscrolls.bethesda.net/en/daggerfall)
- **Android device** (or emulator) running Android 8.0+ (API 26) for testing

---

## Step 1 — Clone dfu-android

Clone Vwing's DFU Android fork, which is the base project:

```bash
git clone https://github.com/Vwing/daggerfall-unity-android.git
cd daggerfall-unity-android
```

---

## Step 2 — Add Project Legacy

Clone the Project Legacy repository into the Unity project's Assets folder:

```bash
cd Assets
git clone <project-legacy-repo-url> ProjectLegacy
```

This places all Project Legacy code under `Assets/ProjectLegacy/`, keeping it isolated from core DFU code.

---

## Step 3 — Open in Unity

1. Open **Unity Hub**
2. Click **Add** and select the `daggerfall-unity-android` folder
3. Ensure the project is set to open with Unity **2022.3.30f1**
   - If you don't have this version, install it via Unity Hub → Installs → Install Editor
   - When installing, make sure **Android Build Support** is checked (with OpenJDK and SDK/NDK)
4. Open the project — first import may take several minutes

---

## Step 4 — Configure Daggerfall Data

1. Obtain the DOS Daggerfall data files (install from Steam or Bethesda.net)
2. Locate the `arena2` folder in the install directory
3. In Unity, DFU will prompt for the `arena2` path on first run, or you can set it in:
   - **DaggerfallUnity** → **Inspector** → **Arena2 Path**
4. Verify the path is correct — DFU needs this data to run

---

## Step 5 — Configure Player Settings

In Unity, go to **Edit → Project Settings → Player**:

1. **Default Orientation**: Portrait
2. **Allowed Orientations**: Portrait only (uncheck all others)
3. **Minimum API Level**: Android 8.0 (API level 26)
4. **Target API Level**: Automatic (highest installed)
5. **Scripting Backend**: IL2CPP (recommended for release builds; Mono is fine for development)
6. **Target Architectures**: ARM64 (ARMv7 optional for older devices)

---

## Step 6 — Verify Setup

1. Enter Play Mode in the Unity Editor
2. `LegacyBootstrapper` should auto-detect the environment and log initialization:
   ```
   [ProjectLegacy] LegacyBootstrapper initializing...
   [ProjectLegacy] Platform: Android detected
   [ProjectLegacy] Portrait mode activated
   ```
   - In the editor (non-Android), the bootstrapper runs in editor mode for testing
3. No manual scene changes should be needed — the bootstrapper hooks into DFU's existing startup

---

## Building an APK

### From Unity Editor

1. Go to **File → Build Settings**
2. Select **Android** platform
3. Click **Switch Platform** if not already on Android
4. Click **Build** or **Build and Run**
5. Select output location for the APK

### Using the Build Script

A helper script is provided for command-line builds:

```bash
cd Tools
chmod +x build-android.sh
./build-android.sh
```

The script validates Unity version, checks for `arena2` data, and builds to `Builds/ProjectLegacy-{version}.apk`.

---

## Testing on Device

### Via USB

1. Enable **Developer Options** on your Android device
2. Enable **USB Debugging**
3. Connect via USB
4. In Unity: **Build and Run** will install and launch on the connected device

### Via APK

1. Build the APK (see above)
2. Transfer to device and install
3. On first run, the Setup Wizard will guide you through placing the `arena2` data

---

## Project Structure

```
daggerfall-unity-android/        # Base DFU Android project
├── Assets/
│   ├── Scripts/                 # DFU core code (do not modify)
│   ├── Game/                    # DFU game assets
│   └── ProjectLegacy/          # ← Our code lives here
│       ├── Scripts/
│       │   ├── Core/            # Bootstrapping, portrait mode, settings
│       │   ├── Input/           # Touch zones, joystick, gestures
│       │   ├── Combat/          # Auto-aim, lock-on, swipe combat
│       │   ├── UI/              # All UI (HUD, startup, panels, common)
│       │   └── Util/            # Haptics, device profiling
│       ├── Prefabs/             # UI prefabs
│       └── Resources/           # ScriptableObject defaults
```

---

## Troubleshooting

### "LegacyBootstrapper not found" or no initialization logs

- Ensure `Assets/ProjectLegacy/` exists and Unity has imported the scripts
- Check the Console for compilation errors
- Verify the bootstrapper's script execution order is set to -100

### DFU can't find arena2 data

- Check the path in DaggerfallUnity inspector
- Ensure the folder contains the actual data files (ARCH3D.BSA, etc.)
- On device: data should be placed in the app's persistent data path

### Build fails with Android errors

- Verify Android Build Support is installed in Unity Hub
- Check minimum API level is set to 26
- Ensure IL2CPP or Mono backend is configured correctly
- Check that NDK path is set in **Preferences → External Tools**

### Performance issues on device

- Start with **Mono** scripting backend for faster build iteration
- Lower render resolution scale in Project Legacy settings
- Use `DeviceProfile` to check detected performance tier
- Target 30fps — DFU is CPU-bound on mobile
