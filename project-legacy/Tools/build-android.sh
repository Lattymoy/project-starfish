#!/usr/bin/env bash
# Project Legacy — Android Build Helper
# Validates environment, checks dependencies, and builds an APK via Unity batch mode.

set -euo pipefail

# ============================================================================
# Configuration
# ============================================================================
REQUIRED_UNITY_VERSION="2022.3.30f1"
PROJECT_ROOT="${PROJECT_ROOT:-$(cd "$(dirname "$0")/.." && pwd)}"
BUILD_DIR="${PROJECT_ROOT}/Builds"
VERSION="${VERSION:-0.1.0-dev}"
OUTPUT_APK="${BUILD_DIR}/ProjectLegacy-${VERSION}.apk"
BUILD_METHOD="BuildScript.BuildAndroid"

# Unity editor paths (checked in order)
UNITY_PATHS=(
    "/Applications/Unity/Hub/Editor/${REQUIRED_UNITY_VERSION}/Unity.app/Contents/MacOS/Unity"
    "/opt/unity/editors/${REQUIRED_UNITY_VERSION}/Editor/Unity"
    "${HOME}/Unity/Hub/Editor/${REQUIRED_UNITY_VERSION}/Editor/Unity"
    "C:/Program Files/Unity/Hub/Editor/${REQUIRED_UNITY_VERSION}/Editor/Unity.exe"
)

# ============================================================================
# Helper Functions
# ============================================================================
log() {
    echo "[ProjectLegacy] $1"
}

error() {
    echo "[ProjectLegacy] ERROR: $1" >&2
    exit 1
}

warn() {
    echo "[ProjectLegacy] WARNING: $1" >&2
}

# ============================================================================
# Step 1: Find Unity Editor
# ============================================================================
find_unity() {
    log "Looking for Unity ${REQUIRED_UNITY_VERSION}..."

    # Check UNITY_PATH environment variable first
    if [[ -n "${UNITY_PATH:-}" ]] && [[ -x "${UNITY_PATH}" ]]; then
        log "Using Unity from UNITY_PATH: ${UNITY_PATH}"
        echo "${UNITY_PATH}"
        return 0
    fi

    # Check common installation paths
    for path in "${UNITY_PATHS[@]}"; do
        if [[ -x "${path}" ]]; then
            log "Found Unity at: ${path}"
            echo "${path}"
            return 0
        fi
    done

    # Try to find via 'which' or 'unity-editor' command
    if command -v unity-editor &>/dev/null; then
        log "Found Unity via unity-editor command"
        echo "unity-editor"
        return 0
    fi

    error "Unity ${REQUIRED_UNITY_VERSION} not found. Set UNITY_PATH environment variable or install via Unity Hub."
}

# ============================================================================
# Step 2: Validate Unity Version
# ============================================================================
validate_unity_version() {
    local unity_path="$1"
    log "Validating Unity version..."

    # Try to get version from Unity
    local version_output
    version_output=$("${unity_path}" -version 2>/dev/null || true)

    if [[ -n "${version_output}" ]]; then
        if [[ "${version_output}" == *"${REQUIRED_UNITY_VERSION}"* ]]; then
            log "Unity version verified: ${REQUIRED_UNITY_VERSION}"
        else
            warn "Unity version mismatch. Expected ${REQUIRED_UNITY_VERSION}, got: ${version_output}"
            warn "Build may fail or produce unexpected results."
        fi
    else
        warn "Could not verify Unity version. Proceeding anyway."
    fi
}

# ============================================================================
# Step 3: Check for Arena2 Data
# ============================================================================
check_arena2_data() {
    log "Checking for Daggerfall arena2 data..."

    # Common locations to check
    local arena2_paths=(
        "${PROJECT_ROOT}/arena2"
        "${PROJECT_ROOT}/Assets/StreamingAssets/arena2"
        "${HOME}/.daggerfall/arena2"
        "${HOME}/DaggerfallGameFiles/arena2"
    )

    for path in "${arena2_paths[@]}"; do
        if [[ -d "${path}" ]]; then
            # Verify it contains expected files
            if [[ -f "${path}/ARCH3D.BSA" ]] || [[ -f "${path}/arch3d.bsa" ]]; then
                log "Found arena2 data at: ${path}"
                return 0
            fi
        fi
    done

    warn "arena2 data folder not found. The build will succeed but the game"
    warn "won't run without Daggerfall data files. Obtain them from Steam"
    warn "or Bethesda.net (free) and place the arena2 folder in the project."
}

# ============================================================================
# Step 4: Prepare Build Directory
# ============================================================================
prepare_build_dir() {
    log "Preparing build directory..."

    mkdir -p "${BUILD_DIR}"

    # Clean previous build artifacts for this version
    if [[ -f "${OUTPUT_APK}" ]]; then
        log "Removing previous build: ${OUTPUT_APK}"
        rm -f "${OUTPUT_APK}"
    fi
}

# ============================================================================
# Step 5: Build Android APK
# ============================================================================
build_apk() {
    local unity_path="$1"

    log "Starting Android APK build..."
    log "  Project: ${PROJECT_ROOT}"
    log "  Output:  ${OUTPUT_APK}"
    log "  Version: ${VERSION}"

    local log_file="${BUILD_DIR}/build-${VERSION}.log"

    "${unity_path}" \
        -batchmode \
        -nographics \
        -quit \
        -projectPath "${PROJECT_ROOT}" \
        -buildTarget Android \
        -executeMethod "${BUILD_METHOD}" \
        -logFile "${log_file}" \
        -outputPath "${OUTPUT_APK}" \
        -buildVersion "${VERSION}" \
        2>&1 | tee -a "${log_file}" || {
            error "Build failed. Check log: ${log_file}"
        }

    if [[ -f "${OUTPUT_APK}" ]]; then
        local size
        size=$(du -h "${OUTPUT_APK}" | cut -f1)
        log "Build successful!"
        log "  APK: ${OUTPUT_APK}"
        log "  Size: ${size}"
    else
        error "Build completed but APK not found at ${OUTPUT_APK}. Check log: ${log_file}"
    fi
}

# ============================================================================
# Main
# ============================================================================
main() {
    log "================================================"
    log "  Project Legacy — Android Build"
    log "  Version: ${VERSION}"
    log "================================================"

    local unity_path
    unity_path=$(find_unity)

    validate_unity_version "${unity_path}"
    check_arena2_data
    prepare_build_dir
    build_apk "${unity_path}"

    log "================================================"
    log "  Build Complete!"
    log "================================================"
}

main "$@"
