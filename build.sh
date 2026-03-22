#!/bin/bash
# Blob3D CLI Build Script
# Usage:
#   ./build.sh android          Build Android APK
#   ./build.sh ios              Build iOS Xcode project
#   ./build.sh all              Build both platforms
#   ./build.sh android --dev    Development build
#   ./build.sh setup            Run project setup only (no build)

set -euo pipefail

PROJECT_DIR="$(cd "$(dirname "$0")" && pwd)"
PLATFORM="${1:-help}"
shift || true
EXTRA_ARGS="$*"

# ========================================
# Find Unity installation
# ========================================
find_unity() {
    local unity_path=""

    # macOS: Unity Hub default location
    if [ -d "/Applications/Unity/Hub/Editor" ]; then
        unity_path=$(ls -d /Applications/Unity/Hub/Editor/*/Unity.app/Contents/MacOS/Unity 2>/dev/null | sort -rV | head -1)
    fi

    # macOS: Direct install
    if [ -z "$unity_path" ] && [ -f "/Applications/Unity/Unity.app/Contents/MacOS/Unity" ]; then
        unity_path="/Applications/Unity/Unity.app/Contents/MacOS/Unity"
    fi

    # Check UNITY_PATH env var
    if [ -z "$unity_path" ] && [ -n "${UNITY_PATH:-}" ]; then
        unity_path="$UNITY_PATH"
    fi

    # mdfind fallback (macOS Spotlight)
    if [ -z "$unity_path" ]; then
        unity_path=$(mdfind "kMDItemFSName == 'Unity.app'" 2>/dev/null | head -1)
        if [ -n "$unity_path" ]; then
            unity_path="$unity_path/Contents/MacOS/Unity"
        fi
    fi

    if [ -z "$unity_path" ] || [ ! -f "$unity_path" ]; then
        echo "ERROR: Unity not found."
        echo ""
        echo "Options:"
        echo "  1. Install Unity Hub: https://unity.com/download"
        echo "  2. Install Unity 2022.3 LTS via Unity Hub"
        echo "  3. Set UNITY_PATH environment variable:"
        echo "     export UNITY_PATH=/path/to/Unity"
        exit 1
    fi

    echo "$unity_path"
}

# ========================================
# Main
# ========================================
case "$PLATFORM" in
    android)
        UNITY=$(find_unity)
        echo "=== Blob3D Android Build ==="
        echo "Unity: $UNITY"
        echo "Project: $PROJECT_DIR"
        echo ""
        "$UNITY" -batchmode -nographics -quit \
            -projectPath "$PROJECT_DIR" \
            -buildTarget Android \
            -executeMethod BuildScript.BuildAndroid \
            -logFile - \
            $EXTRA_ARGS
        echo ""
        echo "=== Build complete: Builds/Android/Blob3D.apk ==="
        ;;

    ios)
        UNITY=$(find_unity)
        echo "=== Blob3D iOS Build ==="
        echo "Unity: $UNITY"
        echo "Project: $PROJECT_DIR"
        echo ""
        "$UNITY" -batchmode -nographics -quit \
            -projectPath "$PROJECT_DIR" \
            -buildTarget iOS \
            -executeMethod BuildScript.BuildiOS \
            -logFile - \
            $EXTRA_ARGS
        echo ""
        echo "=== Build complete: Builds/iOS/ (open in Xcode) ==="
        ;;

    all)
        UNITY=$(find_unity)
        echo "=== Blob3D Full Build ==="
        echo "Unity: $UNITY"
        echo ""
        "$UNITY" -batchmode -nographics -quit \
            -projectPath "$PROJECT_DIR" \
            -buildTarget Android \
            -executeMethod BuildScript.BuildAll \
            -logFile - \
            $EXTRA_ARGS
        echo ""
        echo "=== All builds complete ==="
        ;;

    setup)
        UNITY=$(find_unity)
        echo "=== Blob3D Project Setup ==="
        "$UNITY" -batchmode -nographics -quit \
            -projectPath "$PROJECT_DIR" \
            -executeMethod BuildScript.RunSetupIfNeeded \
            -logFile -
        echo "=== Setup complete ==="
        ;;

    help|*)
        echo "Blob3D Build Script"
        echo ""
        echo "Usage: ./build.sh <command> [options]"
        echo ""
        echo "Commands:"
        echo "  android     Build Android APK"
        echo "  ios         Build iOS Xcode project"
        echo "  all         Build both platforms"
        echo "  setup       Run project auto-setup only"
        echo "  help        Show this help"
        echo ""
        echo "Options:"
        echo "  --dev       Development build with debugging"
        echo "  --output <path>  Custom output directory"
        echo ""
        echo "Environment:"
        echo "  UNITY_PATH  Path to Unity executable (auto-detected if not set)"
        echo ""
        echo "Examples:"
        echo "  ./build.sh android"
        echo "  ./build.sh ios --dev"
        echo "  UNITY_PATH=/path/to/Unity ./build.sh android"
        ;;
esac
