package com.faceplugin.facerecognition.maui;

import android.content.Context;
import android.graphics.Bitmap;
import android.graphics.Matrix;
import android.util.Base64;

import com.faceplugin.facerecognitionsdk.FaceRecognitionSDK;

import java.io.ByteArrayOutputStream;
import java.util.ArrayDeque;
import java.util.ArrayList;
import java.util.List;

/** Thin Java entry the MAUI app calls. The AAR stays the Face Recognition SDK. */
public final class FaceBridge {
    private static final ArrayDeque<String> EVENTS = new ArrayDeque<>();
    private static Bitmap lastLiveBitmap;

    private FaceBridge() {}

    public static void installListener() {
        // Anonymous class: Android javac cannot emit lambdas (LambdaMetafactory is stubbed).
        FaceRecognitionSDK.setVideoWorkerEventHandler(new FaceRecognitionSDK.VideoWorkerEventListener() {
            @Override
            public void onVideoWorkerEvent(String json) {
                if (json == null) {
                    return;
                }
                synchronized (EVENTS) {
                    EVENTS.addLast(json);
                    while (EVENTS.size() > 8) {
                        EVENTS.removeFirst();
                    }
                }
            }
        });
    }

    public static String pollEvent() {
        synchronized (EVENTS) {
            return EVENTS.isEmpty() ? "" : EVENTS.removeFirst();
        }
    }

    public static String machineCode(Context context) {
        String value = FaceRecognitionSDK.getMachineCode(context);
        return value == null ? "" : value;
    }

    public static int activate(Context context, String license) {
        return FaceRecognitionSDK.setActivation(context, license);
    }

    public static int init(Context context) throws java.io.IOException {
        return FaceRecognitionSDK.init(context);
    }

    public static String lastError() {
        String value = FaceRecognitionSDK.lastLicenseError();
        return value == null ? "" : value;
    }

    public static String licenseStatus() {
        String value = FaceRecognitionSDK.getLicenseStatus();
        return value == null ? "" : value;
    }

    public static int setLandmarkMode(int mode) {
        return FaceRecognitionSDK.setLandmarkMode(mode);
    }

    public static String detect(Bitmap bitmap, int flags) {
        String value = FaceRecognitionSDK.detect(bitmap, false, flags);
        return value == null ? "" : value;
    }

    public static String extractFeature(Bitmap bitmap) {
        String value = FaceRecognitionSDK.extractFeature(bitmap);
        return value == null ? "" : value;
    }

    /** RN templateExtraction: full-frame extract first, then 1.4× crop fallback. */
    public static String extractCropped(Bitmap src, int x1, int y1, int x2, int y2) {
        if (src == null) {
            return "";
        }
        String json = extractFeature(src);
        if (json != null && json.contains("\"feature\"")) {
            return json;
        }
        Bitmap crop = cropFaceBitmap(src, x1, y1, x2, y2);
        if (crop == null) {
            return json == null ? "" : json;
        }
        try {
            String cropped = extractFeature(crop);
            return (cropped == null || cropped.isEmpty()) ? (json == null ? "" : json) : cropped;
        } finally {
            if (crop != src && !crop.isRecycled()) {
                crop.recycle();
            }
        }
    }

    /** Flutter/RN cropFace — 1.4× square scaled to 200×200 JPEG base64. */
    public static String cropFaceBase64(Bitmap src, int x1, int y1, int x2, int y2) {
        Bitmap crop = cropFaceBitmap(src, x1, y1, x2, y2);
        if (crop == null) {
            return "";
        }
        Bitmap scaled = null;
        try {
            scaled = Bitmap.createScaledBitmap(crop, 200, 200, true);
            ByteArrayOutputStream out = new ByteArrayOutputStream();
            scaled.compress(Bitmap.CompressFormat.JPEG, 85, out);
            return Base64.encodeToString(out.toByteArray(), Base64.NO_WRAP);
        } finally {
            if (scaled != null && scaled != crop && !scaled.isRecycled()) {
                scaled.recycle();
            }
            if (crop != src && !crop.isRecycled()) {
                crop.recycle();
            }
        }
    }

    private static Bitmap cropFaceBitmap(Bitmap src, int x1, int y1, int x2, int y2) {
        if (src == null) {
            return null;
        }
        int centerX = (x1 + x2) / 2;
        int centerY = (y1 + y2) / 2;
        int cropWidth = (int) ((x2 - x1) * 1.4f);
        if (cropWidth < 2) {
            cropWidth = Math.max(2, Math.max(x2 - x1, y2 - y1));
        }
        int cropX1 = Math.max(0, centerX - cropWidth / 2);
        int cropY1 = Math.max(0, centerY - cropWidth / 2);
        int cropX2 = Math.min(src.getWidth() - 1, centerX + cropWidth / 2);
        int cropY2 = Math.min(src.getHeight() - 1, centerY + cropWidth / 2);
        int w = cropX2 - cropX1 + 1;
        int h = cropY2 - cropY1 + 1;
        if (w <= 1 || h <= 1) {
            return null;
        }
        return Bitmap.createBitmap(src, cropX1, cropY1, w, h);
    }

    public static String quality(Bitmap bitmap) {
        String value = FaceRecognitionSDK.quality(bitmap, false);
        return value == null ? "" : value;
    }

    public static float similarity(byte[] a, byte[] b) {
        return FaceRecognitionSDK.similarity(a, b);
    }

    public static int startVideoWorker(float threshold) {
        // Flutter/RN: stop any previous worker, then install listener and start.
        FaceRecognitionSDK.stopVideoWorker();
        synchronized (EVENTS) {
            EVENTS.clear();
        }
        installListener();
        return FaceRecognitionSDK.startVideoWorker(threshold);
    }

    public static void stopVideoWorker() {
        FaceRecognitionSDK.stopVideoWorker();
        FaceRecognitionSDK.setVideoWorkerEventHandler(null);
        synchronized (EVENTS) {
            EVENTS.clear();
        }
    }

    public static int syncDatabase(String packedBase64, float threshold) {
        List<byte[]> list = new ArrayList<>();
        if (packedBase64 != null && !packedBase64.isEmpty()) {
            for (String part : packedBase64.split("\n")) {
                if (!part.isEmpty()) {
                    list.add(Base64.decode(part, Base64.DEFAULT));
                }
            }
        }
        return FaceRecognitionSDK.syncVideoWorkerDatabase(list, threshold);
    }

    public static int addFrame(Bitmap bitmap) {
        return FaceRecognitionSDK.addVideoWorkerFrame(bitmap);
    }

    /**
     * Same as RN/Flutter applyLiveFrame: rotate + scale long edge, cache, optionally feed worker.
     * Always owns a copy so C# can recycle the decode bitmap without killing the worker frame.
     * Returns "1|width|height" or "0|0|0".
     */
    public static String applyLiveFrame(Bitmap src, float rotateDegrees, int maxEdge, boolean feedWorker) {
        if (src == null) {
            return "0|0|0";
        }
        Bitmap owned = src.copy(src.getConfig() != null ? src.getConfig() : Bitmap.Config.ARGB_8888, false);
        if (owned == null) {
            return "0|0|0";
        }
        Bitmap prepared = applyLiveTransform(owned, rotateDegrees, Math.max(1, maxEdge));
        if (prepared != owned && !owned.isRecycled()) {
            owned.recycle();
        }
        if (lastLiveBitmap != null && lastLiveBitmap != prepared && !lastLiveBitmap.isRecycled()) {
            lastLiveBitmap.recycle();
        }
        lastLiveBitmap = prepared;
        if (feedWorker) {
            FaceRecognitionSDK.addVideoWorkerFrame(prepared);
        }
        return "1|" + prepared.getWidth() + "|" + prepared.getHeight();
    }

    /** JPEG bytes of last prepared live frame (Base64), or empty. */
    public static String exportLastLiveFrameBase64() {
        Bitmap prepared = lastLiveBitmap;
        if (prepared == null) {
            return "";
        }
        ByteArrayOutputStream out = new ByteArrayOutputStream();
        prepared.compress(Bitmap.CompressFormat.JPEG, 85, out);
        return Base64.encodeToString(out.toByteArray(), Base64.NO_WRAP);
    }

    public static String lastLiveSize() {
        Bitmap prepared = lastLiveBitmap;
        if (prepared == null) {
            return "0|0";
        }
        return prepared.getWidth() + "|" + prepared.getHeight();
    }

    /** Rotate by degrees then scale long edge ≤ maxEdge. No front/back branching. */
    private static Bitmap applyLiveTransform(Bitmap src, float rotateDegrees, int maxEdge) {
        Bitmap frame = src;
        float deg = rotateDegrees % 360f;
        if (Math.abs(deg) > 0.01f) {
            Matrix matrix = new Matrix();
            matrix.postRotate(deg);
            Bitmap rotated = Bitmap.createBitmap(frame, 0, 0, frame.getWidth(), frame.getHeight(), matrix, true);
            if (rotated != frame && frame != src && !frame.isRecycled()) {
                frame.recycle();
            }
            frame = rotated;
        }
        return scaleMax(frame, maxEdge);
    }

    private static Bitmap scaleMax(Bitmap src, int maxEdge) {
        int w = src.getWidth();
        int h = src.getHeight();
        int edge = Math.max(w, h);
        if (edge <= maxEdge) {
            return src;
        }
        float scale = maxEdge / (float) edge;
        Bitmap scaled = Bitmap.createScaledBitmap(
            src, Math.max(1, (int) (w * scale)), Math.max(1, (int) (h * scale)), true);
        if (scaled != src && !src.isRecycled()) {
            src.recycle();
        }
        return scaled;
    }

    public static int deinit() {
        return FaceRecognitionSDK.deinit();
    }
}
