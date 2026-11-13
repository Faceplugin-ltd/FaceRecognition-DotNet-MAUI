#import "FaceWorkerBridge.h"
#import <Foundation/Foundation.h>
#import <UIKit/UIKit.h>

@interface FaceRecognitionActiveLivenessConfig : NSObject
@property (nonatomic, assign) BOOL enabled;
+ (instancetype)defaultConfig;
@end

@interface FaceRecognitionVideoWorkerConfig : NSObject
@property (nonatomic, assign) float matchThreshold;
@property (nonatomic, strong) FaceRecognitionActiveLivenessConfig *activeLiveness;
+ (instancetype)configWithMatchThreshold:(float)threshold;
@end

@interface FaceRecognitionSDK : NSObject
+ (void)stopVideoWorker;
+ (void)setVideoWorkerEventHandler:(void (^_Nullable)(NSString *json))handler;
+ (int)startVideoWorkerWithConfig:(FaceRecognitionVideoWorkerConfig *)config;
+ (int)startVideoWorkerWithMatchThreshold:(float)threshold;
+ (nullable NSString *)detectImage:(UIImage *)image crop:(BOOL)crop flags:(NSUInteger)flags;
+ (nullable NSString *)extractFeatureFromImage:(UIImage *)image;
+ (int)addVideoWorkerFrame:(UIImage *)image;
@end

static FaceWorkerEventCallback s_callback;
static UIImage *s_lastLiveImage;
static NSInteger s_lastLiveW;
static NSInteger s_lastLiveH;
static BOOL s_videoFrameBusy;
static dispatch_queue_t s_workerQueue;

static dispatch_queue_t FRSWorkerQueue(void)
{
  static dispatch_once_t once;
  dispatch_once(&once, ^{
    s_workerQueue = dispatch_queue_create("com.faceplugin.maui.videoworker", DISPATCH_QUEUE_SERIAL);
  });
  return s_workerQueue;
}

/** Same as RN FRSFixOrientation — bake to scale-1 CGImage before SDK calls. */
static UIImage *FRSFixOrientation(UIImage *image)
{
  if (image == nil) {
    return nil;
  }
  if (image.imageOrientation == UIImageOrientationUp && image.scale == 1.0 && image.CGImage != nil) {
    return image;
  }
  CGSize pixelSize = CGSizeMake(image.size.width * image.scale, image.size.height * image.scale);
  if (pixelSize.width < 1 || pixelSize.height < 1) {
    return nil;
  }
  UIGraphicsBeginImageContextWithOptions(pixelSize, YES, 1);
  [image drawInRect:CGRectMake(0, 0, pixelSize.width, pixelSize.height)];
  UIImage *out = UIGraphicsGetImageFromCurrentImageContext();
  UIGraphicsEndImageContext();
  return out ?: image;
}

static UIImage *FRSImageFromBytes(const void *bytes, int length)
{
  if (bytes == NULL || length <= 0) {
    return nil;
  }
  NSData *data = [NSData dataWithBytes:bytes length:(NSUInteger)length];
  return FRSFixOrientation([UIImage imageWithData:data]);
}

static NSInteger FRSPixelW(UIImage *image)
{
  return image ? (NSInteger)llround(image.size.width * image.scale) : 0;
}

static NSInteger FRSPixelH(UIImage *image)
{
  return image ? (NSInteger)llround(image.size.height * image.scale) : 0;
}

static UIImage *FRSRotateDegrees(UIImage *image, float degrees)
{
  if (image == nil) {
    return nil;
  }
  float deg = fmodf(degrees, 360.f);
  if (fabsf(deg) < 0.01f) {
    return image;
  }
  CGFloat radians = deg * (CGFloat)M_PI / 180.f;
  CGFloat w = (CGFloat)FRSPixelW(image);
  CGFloat h = (CGFloat)FRSPixelH(image);
  if (w < 1 || h < 1) {
    return image;
  }
  CGFloat cosA = fabs(cos(radians));
  CGFloat sinA = fabs(sin(radians));
  CGFloat outW = floor(w * cosA + h * sinA);
  CGFloat outH = floor(w * sinA + h * cosA);
  if (outW < 1 || outH < 1) {
    return image;
  }
  UIGraphicsBeginImageContextWithOptions(CGSizeMake(outW, outH), YES, 1);
  CGContextRef ctx = UIGraphicsGetCurrentContext();
  CGContextTranslateCTM(ctx, outW * 0.5, outH * 0.5);
  CGContextRotateCTM(ctx, radians);
  [image drawInRect:CGRectMake(-w * 0.5, -h * 0.5, w, h)];
  UIImage *rotated = UIGraphicsGetImageFromCurrentImageContext();
  UIGraphicsEndImageContext();
  return rotated ?: image;
}

static UIImage *FRSScaleMax(UIImage *image, int maxEdge)
{
  if (image == nil) {
    return nil;
  }
  CGFloat w = (CGFloat)FRSPixelW(image);
  CGFloat h = (CGFloat)FRSPixelH(image);
  CGFloat edge = MAX(w, h);
  if (edge <= maxEdge || maxEdge < 1) {
    return image;
  }
  CGFloat scale = (CGFloat)maxEdge / edge;
  CGFloat nw = MAX(1, floor(w * scale));
  CGFloat nh = MAX(1, floor(h * scale));
  UIGraphicsBeginImageContextWithOptions(CGSizeMake(nw, nh), YES, 1);
  [image drawInRect:CGRectMake(0, 0, nw, nh)];
  UIImage *scaled = UIGraphicsGetImageFromCurrentImageContext();
  UIGraphicsEndImageContext();
  return scaled ?: image;
}

/** RN FRSApplyLiveTransform: bake orientation, rotate, scale long edge, force owned CGImage. */
static UIImage *FRSApplyLiveTransform(UIImage *src, float rotateDegrees, int maxEdge)
{
  UIImage *frame = FRSFixOrientation(src);
  if (frame == nil) {
    return nil;
  }
  frame = FRSRotateDegrees(frame, rotateDegrees);
  frame = FRSScaleMax(frame, MAX(1, maxEdge));
  // Own a scale-1 copy so callers can release the source JPEG UIImage.
  CGFloat w = (CGFloat)FRSPixelW(frame);
  CGFloat h = (CGFloat)FRSPixelH(frame);
  if (w < 1 || h < 1) {
    return frame;
  }
  UIGraphicsBeginImageContextWithOptions(CGSizeMake(w, h), YES, 1);
  [frame drawInRect:CGRectMake(0, 0, w, h)];
  UIImage *owned = UIGraphicsGetImageFromCurrentImageContext();
  UIGraphicsEndImageContext();
  return owned ?: frame;
}

static char *FRSDupNSString(NSString *value)
{
  const char *utf8 = [(value ?: @"") UTF8String];
  if (utf8 == NULL) {
    return strdup("");
  }
  return strdup(utf8);
}

static char *FRSDupCString(const char *value)
{
  return strdup(value ? value : "");
}

void FaceWorkerBridge_SetCallback(FaceWorkerEventCallback callback)
{
  s_callback = callback;
}

void FaceWorkerBridge_InstallHandler(void)
{
  [FaceRecognitionSDK setVideoWorkerEventHandler:^(NSString *json) {
    FaceWorkerEventCallback cb = s_callback;
    if (cb == NULL || json == nil) {
      return;
    }
    // Copy immediately — UTF8String lifetime ends with the autorelease pool.
    const char *utf8 = [json UTF8String];
    if (utf8 == NULL) {
      return;
    }
    char *owned = strdup(utf8);
    if (owned == NULL) {
      return;
    }
    cb(owned);
    free(owned);
  }];
}

void FaceWorkerBridge_ClearHandler(void)
{
  [FaceRecognitionSDK setVideoWorkerEventHandler:nil];
}

int FaceWorkerBridge_Start(float matchThreshold)
{
  [FaceRecognitionSDK stopVideoWorker];
  s_videoFrameBusy = NO;
  FaceWorkerBridge_InstallHandler();

  // Always prefer config + activeLiveness OFF (RN/Flutter Capture+Identify).
  Class configCls = NSClassFromString(@"FaceRecognitionVideoWorkerConfig");
  Class alCls = NSClassFromString(@"FaceRecognitionActiveLivenessConfig");
  if (configCls != nil && alCls != nil) {
    FaceRecognitionVideoWorkerConfig *config =
        [FaceRecognitionVideoWorkerConfig configWithMatchThreshold:matchThreshold];
    FaceRecognitionActiveLivenessConfig *al =
        [FaceRecognitionActiveLivenessConfig defaultConfig];
    al.enabled = NO;
    config.activeLiveness = al;
    return [FaceRecognitionSDK startVideoWorkerWithConfig:config];
  }

  return [FaceRecognitionSDK startVideoWorkerWithMatchThreshold:matchThreshold];
}

void FaceWorkerBridge_Stop(void)
{
  [FaceRecognitionSDK stopVideoWorker];
  FaceWorkerBridge_ClearHandler();
  s_videoFrameBusy = NO;
}

char *FaceWorkerBridge_Detect(const void *bytes, int length, unsigned long long flags)
{
  UIImage *image = FRSImageFromBytes(bytes, length);
  if (image == nil) {
    return strdup("");
  }
  NSString *json = [FaceRecognitionSDK detectImage:image crop:NO flags:(NSUInteger)flags];
  return FRSDupNSString(json);
}

char *FaceWorkerBridge_Extract(const void *bytes, int length)
{
  UIImage *image = FRSImageFromBytes(bytes, length);
  if (image == nil) {
    return strdup("");
  }
  NSString *json = [FaceRecognitionSDK extractFeatureFromImage:image];
  return FRSDupNSString(json);
}

static BOOL FRSFeatureLooksEmpty(NSString *json)
{
  if (json == nil || json.length == 0 || [json isEqualToString:@"{}"] || [json isEqualToString:@"[]"]) {
    return YES;
  }
  return [json rangeOfString:@"\"feature\""].location == NSNotFound;
}

/** Match RN/Android Utils.cropFace: 1.4× square window scaled to 200×200. */
static UIImage *FRSCropFace(UIImage *image, int x1, int y1, int x2, int y2)
{
  if (image == nil) {
    return nil;
  }
  CGFloat iw = image.size.width * image.scale;
  CGFloat ih = image.size.height * image.scale;
  if (iw < 2 || ih < 2) {
    return nil;
  }
  CGFloat centerX = (x1 + x2) / 2.0;
  CGFloat centerY = (y1 + y2) / 2.0;
  NSInteger cropWidth = (NSInteger)((x2 - x1) * 1.4f);
  if (cropWidth < 2) {
    cropWidth = MAX(2, MAX((NSInteger)(x2 - x1), (NSInteger)(y2 - y1)));
  }
  NSInteger cropX1 = (NSInteger)MAX(0, centerX - cropWidth / 2.0);
  NSInteger cropY1 = (NSInteger)MAX(0, centerY - cropWidth / 2.0);
  NSInteger cropX2 = (NSInteger)MIN(iw - 1, centerX + cropWidth / 2.0);
  NSInteger cropY2 = (NSInteger)MIN(ih - 1, centerY + cropWidth / 2.0);
  NSInteger w = cropX2 - cropX1 + 1;
  NSInteger h = cropY2 - cropY1 + 1;
  if (w <= 1 || h <= 1 || image.CGImage == NULL) {
    return nil;
  }
  CGRect rect = CGRectMake(cropX1, cropY1, w, h);
  CGImageRef cg = CGImageCreateWithImageInRect(image.CGImage, rect);
  if (cg == NULL) {
    return nil;
  }
  UIImage *cropped = [UIImage imageWithCGImage:cg scale:1.0 orientation:UIImageOrientationUp];
  CGImageRelease(cg);
  if (cropped == nil) {
    return nil;
  }
  UIGraphicsBeginImageContextWithOptions(CGSizeMake(200, 200), YES, 1.0);
  [cropped drawInRect:CGRectMake(0, 0, 200, 200)];
  UIImage *scaled = UIGraphicsGetImageFromCurrentImageContext();
  UIGraphicsEndImageContext();
  return scaled ?: cropped;
}

char *FaceWorkerBridge_ExtractCropped(const void *bytes, int length, int x1, int y1, int x2, int y2)
{
  UIImage *image = FRSImageFromBytes(bytes, length);
  if (image == nil) {
    return strdup("");
  }
  NSString *json = [FaceRecognitionSDK extractFeatureFromImage:image] ?: @"";
  if (FRSFeatureLooksEmpty(json)) {
    UIImage *cropped = FRSCropFace(image, x1, y1, x2, y2);
    if (cropped != nil) {
      json = [FaceRecognitionSDK extractFeatureFromImage:cropped] ?: @"";
    }
  }
  return FRSDupNSString(json);
}

char *FaceWorkerBridge_CropFace(const void *bytes, int length, int x1, int y1, int x2, int y2)
{
  UIImage *image = FRSImageFromBytes(bytes, length);
  UIImage *cropped = FRSCropFace(image, x1, y1, x2, y2);
  if (cropped == nil) {
    return strdup("");
  }
  NSData *data = UIImageJPEGRepresentation(cropped, 0.85);
  if (data == nil) {
    return strdup("");
  }
  NSString *b64 = [data base64EncodedStringWithOptions:0];
  return FRSDupNSString(b64);
}

char *FaceWorkerBridge_ApplyLiveFrame(
    const void *bytes,
    int length,
    float rotateDegrees,
    int maxEdge,
    int feedWorker)
{
  if (feedWorker && s_videoFrameBusy) {
    // RN: return last size with ingested=false while previous add is in flight.
    char buf[64];
    snprintf(buf, sizeof(buf), "0|%ld|%ld", (long)s_lastLiveW, (long)s_lastLiveH);
    return FRSDupCString(buf);
  }

  UIImage *raw = FRSImageFromBytes(bytes, length);
  UIImage *prepared = FRSApplyLiveTransform(raw, rotateDegrees, maxEdge);
  if (prepared == nil) {
    return FRSDupCString("0|0|0");
  }

  s_lastLiveImage = prepared;
  s_lastLiveW = FRSPixelW(prepared);
  s_lastLiveH = FRSPixelH(prepared);

  if (feedWorker) {
    s_videoFrameBusy = YES;
    UIImage *toFeed = prepared;
    dispatch_async(FRSWorkerQueue(), ^{
      @try {
        (void)[FaceRecognitionSDK addVideoWorkerFrame:toFeed];
      } @catch (__unused NSException *e) {
      }
      s_videoFrameBusy = NO;
    });
  }

  char buf[64];
  snprintf(buf, sizeof(buf), "1|%ld|%ld", (long)s_lastLiveW, (long)s_lastLiveH);
  return FRSDupCString(buf);
}

char *FaceWorkerBridge_ExportLastLiveFrame(void)
{
  UIImage *image = s_lastLiveImage;
  if (image == nil) {
    return strdup("");
  }
  NSData *data = UIImageJPEGRepresentation(image, 0.85);
  if (data == nil) {
    return strdup("");
  }
  return FRSDupNSString([data base64EncodedStringWithOptions:0]);
}

char *FaceWorkerBridge_LastLiveSize(void)
{
  char buf[64];
  snprintf(buf, sizeof(buf), "%ld|%ld", (long)s_lastLiveW, (long)s_lastLiveH);
  return FRSDupCString(buf);
}

void FaceWorkerBridge_FreeString(char *value)
{
  if (value != NULL) {
    free(value);
  }
}
