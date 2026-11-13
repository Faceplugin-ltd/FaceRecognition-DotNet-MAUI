#!/bin/bash
# Re-sign nested FaceRecognitionEngine inside embedded facerecognitionsdk.framework,
# then re-seal the outer frameworks with the app identity.
set -euo pipefail

APP_BUNDLE="${1:?app bundle path required}"
SIGN_IDENTITY="${2:?codesign identity required}"

if [ -z "${SIGN_IDENTITY}" ] || [ "${SIGN_IDENTITY}" = "-" ]; then
  echo "warning: codesign identity unset; skipping nested facerecognitionsdk codesign" >&2
  exit 0
fi

sign_framework() {
  local fw="$1"
  if [ ! -d "${fw}" ]; then
    return 0
  fi
  echo "Signing $(basename "${fw}")"
  /usr/bin/codesign --remove-signature "${fw}" 2>/dev/null || true
  /usr/bin/codesign --force --sign "${SIGN_IDENTITY}" \
    --preserve-metadata=identifier,entitlements,flags \
    --timestamp=none \
    "${fw}"
}

FRAMEWORKS="${APP_BUNDLE}/Frameworks"
SDK_FW="${FRAMEWORKS}/facerecognitionsdk.framework"
NESTED=""
for candidate in FaceRecognitionEngine.framework facerecognitionengine.framework; do
  if [ -d "${SDK_FW}/Frameworks/${candidate}" ]; then
    NESTED="${SDK_FW}/Frameworks/${candidate}"
    break
  fi
done

if [ -n "${NESTED}" ]; then
  sign_framework "${NESTED}"
fi

# Also sign top-level native refs that ship beside the app.
sign_framework "${SDK_FW}"
sign_framework "${FRAMEWORKS}/FaceRecognitionEngine.framework"
sign_framework "${FRAMEWORKS}/onnxruntime.framework"
