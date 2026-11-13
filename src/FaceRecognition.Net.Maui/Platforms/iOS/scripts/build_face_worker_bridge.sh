#!/usr/bin/env bash
# Compile FaceWorkerBridge.m into a device arm64 static library for the MAUI app.
set -euo pipefail

SRC_DIR="$(cd "$(dirname "$0")/.." && pwd)"
NATIVE="$SRC_DIR/Native"
OUT_DIR="${1:?usage: build_face_worker_bridge.sh <output-dir>}"
mkdir -p "$OUT_DIR"

SDKROOT="$(xcrun --sdk iphoneos --show-sdk-path)"
OBJ="$OUT_DIR/FaceWorkerBridge.o"
LIB="$OUT_DIR/libFaceWorkerBridge.a"

xcrun clang -c "$NATIVE/FaceWorkerBridge.m" \
  -o "$OBJ" \
  -fobjc-arc \
  -isysroot "$SDKROOT" \
  -arch arm64 \
  -miphoneos-version-min=15.0 \
  -I "$NATIVE"

rm -f "$LIB"
xcrun ar rcs "$LIB" "$OBJ"
echo "$LIB"
