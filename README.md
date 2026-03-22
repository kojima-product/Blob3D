# Blob3D

A 3D blob-eating action game inspired by Agar.io. Grow your blob by consuming feed and smaller blobs while avoiding larger opponents. Available on mobile (iOS/Android) and PC.

## Screenshots

<!-- Add screenshots here -->
_Coming soon_

## Features

- Smooth 3D blob physics with custom surface shader
- AI-controlled opponent blobs with dynamic spawning
- Power-up system (speed boost, shield, magnet, etc.)
- Feed spawning and absorption mechanics
- Mobile-optimized virtual joystick controls
- Real-time leaderboard and score tracking
- Skin customization system
- URP-based grid ground shader
- Object pooling for performance
- Field boundary system with auto-rotation camera

## How to Build

### Requirements

- **Unity 2022.3 LTS** (install via [Unity Hub](https://unity.com/download))
- Android SDK (API level 29+) for Android builds
- Xcode 14+ for iOS builds

### Setup

1. Clone this repository
2. Open the project in Unity 2022.3 LTS
3. Unity will auto-import all assets on first open
4. Open `Assets/Scenes/GameScene.unity`
5. Press Play to test in the editor

### Building

Use the included build script:

```bash
# Build Android APK
./build.sh android

# Build iOS Xcode project
./build.sh ios

# Build both platforms
./build.sh all

# Development build with debugging
./build.sh android --dev
```

Or set `UNITY_PATH` if Unity is not auto-detected:

```bash
UNITY_PATH=/path/to/Unity ./build.sh android
```

Output locations:
- Android: `Builds/Android/Blob3D.apk`
- iOS: `Builds/iOS/` (open in Xcode to deploy)

## Controls

### PC (Editor)
- **WASD / Arrow Keys** — Move the blob
- **Mouse** — Camera rotation

### Mobile
- **Virtual Joystick** — Touch and drag to move
- **Auto-rotation** — Landscape left/right supported

## Tech Stack

- **Engine:** Unity 2022.3 LTS
- **Rendering:** Universal Render Pipeline (URP)
- **Language:** C#
- **Shaders:** Custom HLSL (BlobSurface, GridGround)
- **Platforms:** iOS 14.0+, Android 10+ (API 29)

## Project Structure

```
Assets/
├── Materials/          # Materials and URP assets
├── Prefabs/            # Reusable game object prefabs
├── Resources/          # Runtime-loaded assets (skins, etc.)
├── Scenes/             # GameScene
├── Scripts/
│   ├── AI/             # AI blob controller and spawner
│   ├── Core/           # GameManager, AudioManager, BlobBase
│   ├── Data/           # ScoreManager, SkinData, SkinManager
│   ├── Editor/         # Build scripts and project setup
│   ├── Gameplay/       # Feed, PowerUps, Absorption, VFX
│   ├── Player/         # BlobController, Camera, Input
│   ├── UI/             # HUD, Leaderboard, UIManager, Joystick
│   └── Utils/          # ObjectPool
└── Shaders/            # BlobSurface.shader, GridGround.shader
```

## License

MIT License. See [LICENSE](LICENSE) for details.
