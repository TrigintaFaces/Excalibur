namespace Examples.CloudNative.Serverless.GoogleCloudFunctions.CloudStorageTriggers;

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Excalibur.Dispatch.CloudNative.Serverless.Google;
using Excalibur.Dispatch.CloudNative.Serverless.Google.Triggers;
using Google.Cloud.Functions.Framework;
using Google.Cloud.Storage.V1;
using Microsoft.Extensions.Logging;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Formats.Jpeg;

/// <summary>
/// Example function that processes uploaded images by creating thumbnails and resized versions.
/// Demonstrates real-world image processing scenario with Cloud Storage triggers.
/// </summary>
[FunctionsStartup(typeof(ImageProcessingStartup))]
public class ImageProcessingFunction : CloudStorageFunction
{
 private readonly StorageClient _storageClient;
 private readonly ILogger<ImageProcessingFunction> _logger;

 // Configuration
 private const string PROCESSED_BUCKET_SUFFIX = "-processed";
 private const int THUMBNAIL_SIZE = 200;
 private const int MEDIUM_SIZE = 800;
 private const int LARGE_SIZE = 1600;
 private const int JPEG_QUALITY = 85;

 /// <summary>
 /// Initializes a new instance of the <see cref="ImageProcessingFunction"/> class.
 /// </summary>
 public ImageProcessingFunction(ILogger<ImageProcessingFunction> logger)
 {
 _logger = logger;
 _storageClient = StorageClient.Create();
 }

 /// <summary>
 /// Processes the Cloud Storage event for image files.
 /// </summary>
 protected override async Task ProcessEventAsync(
 CloudStorageEvent storageEvent,
 GoogleCloudFunctionExecutionContext context,
 CancellationToken cancellationToken)
 {
 // Only process image uploads
 if (storageEvent.EventType != CloudStorageEventType.ObjectFinalized)
 {
 _logger.LogInformation("Skipping non-finalized event: {EventType}", storageEvent.EventType);
 return;
 }

 // Check if it's an image file
 if (!IsImageFile(storageEvent.Name, storageEvent.ContentType))
 {
 _logger.LogInformation("Skipping non-image file: {FileName}", storageEvent.Name);
 return;
 }

 _logger.LogInformation(
 "Processing image: {Bucket}/{Object} (Size: {Size} bytes)",
 storageEvent.Bucket,
 storageEvent.Name,
 storageEvent.Size);

 try
 {
 // Download the original image
 using var imageStream = await DownloadObjectAsync(
 storageEvent.Bucket,
 storageEvent.Name,
 cancellationToken);

 if (imageStream == null)
 {
 _logger.LogError("Failed to download image: {Bucket}/{Object}",
 storageEvent.Bucket, storageEvent.Name);
 return;
 }

 // Load the image
 using var image = await Image.LoadAsync(imageStream, cancellationToken);

 _logger.LogInformation(
 "Loaded image: {Width}x{Height}, Format: {Format}",
 image.Width,
 image.Height,
 image.Metadata.DecodedImageFormat?.Name);

 // Generate different sizes
 var processingTasks = new[]
 {
 ProcessImageSizeAsync(image, storageEvent, "thumbnail", THUMBNAIL_SIZE, cancellationToken),
 ProcessImageSizeAsync(image, storageEvent, "medium", MEDIUM_SIZE, cancellationToken),
 ProcessImageSizeAsync(image, storageEvent, "large", LARGE_SIZE, cancellationToken)
 };

 await Task.WhenAll(processingTasks);

 // Add completion metadata
 await UpdateOriginalMetadataAsync(storageEvent, context, cancellationToken);

 _logger.LogInformation(
 "Successfully processed image: {Bucket}/{Object}",
 storageEvent.Bucket,
 storageEvent.Name);
 }
 catch (Exception ex)
 {
 _logger.LogError(ex,
 "Failed to process image: {Bucket}/{Object}",
 storageEvent.Bucket,
 storageEvent.Name);
 throw;
 }
 }

 /// <summary>
 /// Processes and uploads a specific size variant of the image.
 /// </summary>
 private async Task ProcessImageSizeAsync(
 Image image,
 CloudStorageEvent storageEvent,
 string sizeName,
 int maxDimension,
 CancellationToken cancellationToken)
 {
 // Skip if image is already smaller
 if (image.Width <= maxDimension && image.Height <= maxDimension)
 {
 _logger.LogDebug("Skipping {Size} - image is already smaller", sizeName);
 return;
 }

 // Clone and resize the image
 using var resizedImage = image.Clone(ctx => ctx
 .Resize(new ResizeOptions
 {
 Mode = ResizeMode.Max,
 Size = new Size(maxDimension, maxDimension),
 Sampler = KnownResamplers.Lanczos3
 }));

 // Generate output path
 var outputBucket = $"{storageEvent.Bucket}{PROCESSED_BUCKET_SUFFIX}";
 var outputPath = GenerateOutputPath(storageEvent.Name, sizeName);

 // Upload to Cloud Storage
 using var outputStream = new MemoryStream();
 await resizedImage.SaveAsJpegAsync(outputStream, new JpegEncoder
 {
 Quality = JPEG_QUALITY
 }, cancellationToken);

 outputStream.Position = 0;

 await _storageClient.UploadObjectAsync(
 outputBucket,
 outputPath,
 "image/jpeg",
 outputStream,
 new UploadObjectOptions
 {
 Metadata = new Dictionary<string, string>
 {
 ["original-bucket"] = storageEvent.Bucket,
 ["original-object"] = storageEvent.Name,
 ["original-size"] = storageEvent.Size.ToString(),
 ["variant"] = sizeName,
 ["dimensions"] = $"{resizedImage.Width}x{resizedImage.Height}",
 ["processed-at"] = DateTime.UtcNow.ToString("O")
 }
 },
 cancellationToken);

 _logger.LogInformation(
 "Created {Size} variant: {Output} ({Width}x{Height})",
 sizeName,
 outputPath,
 resizedImage.Width,
 resizedImage.Height);
 }

 /// <summary>
 /// Updates the metadata of the original object to indicate processing completion.
 /// </summary>
 private async Task UpdateOriginalMetadataAsync(
 CloudStorageEvent storageEvent,
 GoogleCloudFunctionExecutionContext context,
 CancellationToken cancellationToken)
 {
 try
 {
 var obj = await _storageClient.GetObjectAsync(
 storageEvent.Bucket,
 storageEvent.Name,
 cancellationToken: cancellationToken);

 obj.Metadata ??= new Dictionary<string, string>();
 obj.Metadata["processed"] = "true";
 obj.Metadata["processed-at"] = DateTime.UtcNow.ToString("O");
 obj.Metadata["processing-duration-ms"] = context.Metrics.HandlerDuration.TotalMilliseconds.ToString("F0");
 obj.Metadata["variants-created"] = "thumbnail,medium,large";

 await _storageClient.UpdateObjectAsync(obj, cancellationToken: cancellationToken);
 }
 catch (Exception ex)
 {
 _logger.LogWarning(ex, "Failed to update original object metadata");
 // Non-critical error, don't fail the function
 }
 }

 /// <summary>
 /// Checks if the file is an image based on name and content type.
 /// </summary>
 private static bool IsImageFile(string fileName, string? contentType)
 {
 // Check content type
 if (!string.IsNullOrEmpty(contentType) && contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
 return true;

 // Check file extension
 var extension = Path.GetExtension(fileName)?.ToLowerInvariant();
 return extension switch
 {
 ".jpg" or ".jpeg" or ".png" or ".gif" or ".bmp" or ".webp" => true,
 _ => false
 };
 }

 /// <summary>
 /// Generates the output path for a processed image variant.
 /// </summary>
 private static string GenerateOutputPath(string originalPath, string variant)
 {
 var directory = Path.GetDirectoryName(originalPath)?.Replace('\\', '/') ?? "";
 var fileNameWithoutExt = Path.GetFileNameWithoutExtension(originalPath);

 if (!string.IsNullOrEmpty(directory))
 directory += "/";

 return $"{directory}{fileNameWithoutExt}_{variant}.jpg";
 }

 /// <summary>
 /// Configures the trigger options for image processing.
 /// </summary>
 protected override CloudStorageTriggerOptions ConfigureTriggerOptions()
 {
 return new CloudStorageTriggerOptions
 {
 // Enable content download for image processing
 DownloadContent = true,

 // Only process specific buckets
 BucketPatterns = new[] { "user-uploads-*", "content-images-*" },

 // File patterns for images
 FilePatterns = new[] { "*.jpg", "*.jpeg", "*.png", "*.gif", "*.bmp", "*.webp" },

 // Only process finalized objects
 EventTypes = new[] { CloudStorageEventType.ObjectFinalized },

 // Size limits (max 50MB)
 MaxFileSizeBytes = 50 * 1024 * 1024,

 // Validation
 ValidateEvent = true,

 // Metrics
 TrackMetrics = true
 };
 }
}

/// <summary>
/// Startup configuration for the image processing function.
/// </summary>
public class ImageProcessingStartup : FunctionsStartup
{
 public override void ConfigureServices(IServiceCollection services)
 {
 // Add Cloud Storage trigger support
 services.AddGoogleCloudStorageTriggers();

 // Configure image processing settings
 services.Configure<ImageProcessingOptions>(options =>
 {
 options.MaxConcurrentProcessing = 10;
 options.ProcessingTimeout = TimeSpan.FromMinutes(5);
 });

 // Add health checks
 services.AddHealthChecks()
 .AddCheck("storage", new StorageHealthCheck());
 }
}

/// <summary>
/// Configuration options for image processing.
/// </summary>
public class ImageProcessingOptions {
 public int MaxConcurrentProcessing { get; set; } = 10;
 public TimeSpan ProcessingTimeout { get; set; } = TimeSpan.FromMinutes(5);
}

/// <summary>
/// Health check for Cloud Storage connectivity.
/// </summary>
public class StorageHealthCheck : IHealthCheck
{
 public async Task<HealthCheckResult> CheckHealthAsync(
 HealthCheckContext context,
 CancellationToken cancellationToken = default)
 {
 try
 {
 var client = StorageClient.Create();
 await client.ListBucketsAsync(Environment.GetEnvironmentVariable("GOOGLE_CLOUD_PROJECT"))
 .FirstOrDefaultAsync(cancellationToken);

 return HealthCheckResult.Healthy("Cloud Storage is accessible");
 }
 catch (Exception ex)
 {
 return HealthCheckResult.Unhealthy("Cloud Storage is not accessible", ex);
 }
 }
}
