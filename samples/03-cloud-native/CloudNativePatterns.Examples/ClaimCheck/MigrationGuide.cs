// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Text;

using Azure.Storage.Blobs;

using Excalibur.Dispatch.Abstractions.Diagnostics;
using Excalibur.Dispatch.Abstractions.Serialization;
using Excalibur.Dispatch.Patterns.ClaimCheck;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Excalibur.Dispatch.CloudNativePatterns.Examples.ClaimCheck;

/// <summary>
/// Migration guide showing how to migrate from existing patterns to the new Claim Check implementation.
/// </summary>
#pragma warning disable CA1034 // Nested types are intentionally public for documentation and example clarity
public static partial class MigrationGuide
{
	/// <summary>
	/// Example 1: Migrating from direct Azure Blob Storage usage.
	/// </summary>
	public static class FromDirectBlobStorage
	{
		// Migration helper
		public static void ConfigureServices(IServiceCollection services) => services.AddClaimCheck<AzureBlobClaimCheckProvider>(static
			options =>
		{
			options.ConnectionString = "UseDevelopmentStorage=true";
			options.ContainerName = "messages"; // Same container
			options.BlobNamePrefix = "migrated"; // Separate namespace

			// Benefits over direct usage:
			options.EnableCompression = true; // Automatic compression
			options.Storage.ChunkSize = 1024 * 1024; // Automatic chunking
			options.ValidateChecksum = true; // Data integrity
			options.EnableCleanup = true; // Automatic cleanup
		});

		// ❌ OLD WAY: Direct blob storage manipulation
		public class OldBlobStoragePattern(string connectionString)
		{
			private readonly BlobServiceClient _blobServiceClient = new(connectionString);
			private readonly string _containerName = "messages";

			public async Task<string> StoreMessageAsync(byte[] messageData, string messageId)
			{
				var containerClient = _blobServiceClient.GetBlobContainerClient(_containerName);
				_ = await containerClient.CreateIfNotExistsAsync().ConfigureAwait(false);

				var blobName = $"{DateTime.UtcNow:yyyy/MM/dd}/{messageId}";
				var blobClient = containerClient.GetBlobClient(blobName);

				using var stream = new MemoryStream(messageData);
				_ = await blobClient.UploadAsync(stream, overwrite: true).ConfigureAwait(false);

				return blobName;
			}

			public async Task<byte[]> RetrieveMessageAsync(string blobName)
			{
				var containerClient = _blobServiceClient.GetBlobContainerClient(_containerName);
				var blobClient = containerClient.GetBlobClient(blobName);

				using var stream = new MemoryStream();
				_ = await blobClient.DownloadToAsync(stream).ConfigureAwait(false);
				return stream.ToArray();
			}
		}

		// ✅ NEW WAY: Using Claim Check provider
		public class NewClaimCheckPattern(IClaimCheckProvider claimCheckProvider)
		{
			public async Task<ClaimCheckReference> StoreMessageAsync(byte[] messageData, string messageId)
			{
				var metadata = new ClaimCheckMetadata { MessageId = messageId, MessageType = "MigratedMessage" };

				// Automatic blob naming, compression, chunking
				return await claimCheckProvider.StoreAsync(messageData, CancellationToken.None, metadata).ConfigureAwait(false);
			}

			public async Task<byte[]> RetrieveMessageAsync(ClaimCheckReference reference) =>
				// Automatic decompression, chunk assembly, validation
				await claimCheckProvider.RetrieveAsync(reference, CancellationToken.None).ConfigureAwait(false);
		}
	}

	/// <summary>
	/// Example 2: Migrating from custom claim check implementation.
	/// </summary>
	public static partial class FromCustomImplementation
	{
		// ❌ OLD WAY: Custom claim check with manual everything
		public interface IOldClaimCheckService
		{
			Task<string> StoreAsync(object message);

			Task<T> RetrieveAsync<T>(string claimId);
		}

		public partial class OldCustomClaimCheck(BlobServiceClient blobClient, ILogger<OldCustomClaimCheck> logger)
			: IOldClaimCheckService
		{
			private readonly BlobServiceClient _blobClient = blobClient ?? throw new ArgumentNullException(nameof(blobClient));
			private readonly ILogger<OldCustomClaimCheck> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

			/// <inheritdoc />
			public async Task<string> StoreAsync(object message)
			{
				try
				{
					// Manual serialization
					var json = System.Text.Json.JsonSerializer.Serialize(message);
					var bytes = Encoding.UTF8.GetBytes(json);

					// Manual compression
					if (bytes.Length > 1024)
					{
						using var output = new MemoryStream();
						await using (var gzip = new System.IO.Compression.GZipStream(output,
							             System.IO.Compression.CompressionMode.Compress))
						{
							await gzip.WriteAsync(bytes).ConfigureAwait(false);
						}

						bytes = output.ToArray();
					}

					// Manual blob operations
					var claimId = Guid.NewGuid().ToString();
					var container = _blobClient.GetBlobContainerClient("claims");
					var blob = container.GetBlobClient(claimId);

					using var stream = new MemoryStream(bytes);
					_ = await blob.UploadAsync(stream).ConfigureAwait(false);

					return claimId;
				}
				catch (Exception ex)
				{
					LogStoreFailed(_logger, ex);
					throw;
				}
			}

			/// <inheritdoc />
			public async Task<T> RetrieveAsync<T>(string claimId)
			{
				try
				{
					var container = _blobClient.GetBlobContainerClient("claims");
					var blob = container.GetBlobClient(claimId);

					using var stream = new MemoryStream();
					_ = await blob.DownloadToAsync(stream).ConfigureAwait(false);
					var bytes = stream.ToArray();

					// Manual decompression (assume compressed if larger than threshold)
					if (bytes.Length > 100)
					{
						try
						{
							using var input = new MemoryStream(bytes);
							using var gzip = new System.IO.Compression.GZipStream(input,
								System.IO.Compression.CompressionMode.Decompress);
							using var output = new MemoryStream();
							await gzip.CopyToAsync(output).ConfigureAwait(false);
							bytes = output.ToArray();
						}
						catch
						{
							// If decompression fails, use original bytes (might not be compressed)
						}
					}

					// Manual deserialization
					var json = Encoding.UTF8.GetString(bytes);
					return System.Text.Json.JsonSerializer.Deserialize<T>(json) ??
					       throw new InvalidOperationException("Deserialization returned null");
				}
				catch (Exception ex)
				{
					LogRetrieveFailed(_logger, ex, claimId);
					throw;
				}
			}

			[LoggerMessage(
				EventId = 7001,
				Level = LogLevel.Error,
				Message = "Failed to store claim check")]
			private static partial void LogStoreFailed(ILogger logger, Exception exception);

			[LoggerMessage(
				EventId = 7011,
				Level = LogLevel.Error,
				Message = "Failed to retrieve claim check {ClaimId}")]
			private static partial void LogRetrieveFailed(ILogger logger, Exception exception, string claimId);
		}

		// ✅ NEW WAY: Simplified with built-in features
		public class NewClaimCheckService
		{
			private readonly IClaimCheckProvider _provider;
			private readonly IBinaryMessageSerializer _serializer;

			public NewClaimCheckService(IServiceProvider services)
			{
				_provider = services.GetRequiredService<IClaimCheckProvider>();
				_serializer = new ClaimCheckMessageSerializer(_provider);
			}

			public async Task<ClaimCheckReference> StoreAsync<T>(T message, string messageId)
			{
				// All serialization, compression, chunking handled automatically
				var bytes = await _serializer.SerializeAsync(message, CancellationToken.None).ConfigureAwait(false);

				var metadata = new ClaimCheckMetadata { MessageId = messageId, MessageType = typeof(T).Name };

				return await _provider.StoreAsync(bytes, CancellationToken.None, metadata).ConfigureAwait(false);
			}

			public async Task<T> RetrieveAsync<T>(ClaimCheckReference reference)
			{
				// All retrieval, decompression, deserialization handled
				var bytes = await _provider.RetrieveAsync(reference, CancellationToken.None).ConfigureAwait(false);
				return await _serializer.DeserializeAsync<T>(bytes, CancellationToken.None).ConfigureAwait(false);
			}
		}
	}

	/// <summary>
	/// Example 3: Migrating from file system storage.
	/// </summary>
	public static partial class FromFileSystemStorage
	{
		// ✅ NEW WAY: Cloud-native with built-in features
		public static void MigrateToClaimCheck(IServiceCollection services)
		{
			_ = services.AddClaimCheck<AzureBlobClaimCheckProvider>(static options =>
			{
				// Use blob storage instead of file system
				options.ConnectionString = "DefaultEndpointsProtocol=https;...";
				options.ContainerName = "migrated-messages";

				// Automatic hierarchical organization
				options.BlobNamePrefix = "messages";

				// Built-in cleanup - no manual job needed
				options.EnableCleanup = true;
				options.RetentionPeriod = TimeSpan.FromDays(7);

				// Additional benefits
				options.EnableCompression = true;
				options.ValidateChecksum = true;
				options.Storage.MaxConcurrency = 10; // Parallel operations
			});

			// Optional: Migration tool to copy existing files
			_ = services.AddHostedService<FileSystemMigrationService>();
		}

		// ❌ OLD WAY: Local file system storage
		public class OldFileSystemPattern
		{
			private readonly string _basePath = @"C:\MessageStore";

			public async Task<string> StoreMessageAsync(byte[] data, string messageId)
			{
				var directory = Path.Combine(_basePath,
					DateTime.UtcNow.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture));
				_ = Directory.CreateDirectory(directory);

				var filePath = Path.Combine(directory, $"{messageId}.dat");
				await File.WriteAllBytesAsync(filePath, data).ConfigureAwait(false);

				return filePath;
			}

			public async Task<byte[]> RetrieveMessageAsync(string filePath)
			{
				if (!File.Exists(filePath))
				{
					throw new FileNotFoundException($"Message file not found: {filePath}");
				}

				return await File.ReadAllBytesAsync(filePath).ConfigureAwait(false);
			}

			// Manual cleanup job needed
			public async Task CleanupOldFilesAsync()
			{
				var cutoffDate = DateTime.UtcNow.AddDays(-7);
				var directories = Directory.GetDirectories(_basePath);

				foreach (var dir in directories)
				{
					var dirInfo = new DirectoryInfo(dir);
					if (dirInfo.CreationTime < cutoffDate)
					{
						Directory.Delete(dir, recursive: true);
					}
				}
			}
		}

		public partial class FileSystemMigrationService(
			IClaimCheckProvider claimCheckProvider,
			ILogger<FileSystemMigrationService> logger)
			: BackgroundService
		{
			private readonly string _sourcePath = @"C:\MessageStore";

			/// <inheritdoc />
			protected override async Task ExecuteAsync(CancellationToken stoppingToken)
			{
				LogMigrationStarting(logger);

				var files = Directory.GetFiles(_sourcePath, "*.dat", SearchOption.AllDirectories);
				var migrated = 0;

				foreach (var file in files)
				{
					if (stoppingToken.IsCancellationRequested)
					{
						break;
					}

					try
					{
						var messageId = Path.GetFileNameWithoutExtension(file);
						var data = await File.ReadAllBytesAsync(file, stoppingToken).ConfigureAwait(false);

						var metadata = new ClaimCheckMetadata
						{
							MessageId = messageId,
							MessageType = "MigratedFromFileSystem",
							Properties = { ["OriginalPath"] = file, ["MigrationDate"] = DateTime.UtcNow.ToString("O") }
						};

						_ = await claimCheckProvider.StoreAsync(data, stoppingToken, metadata).ConfigureAwait(false);

						// Delete after successful migration
						File.Delete(file);
						migrated++;

						if (migrated % 100 == 0)
						{
							LogMigratedFiles(logger, migrated);
						}
					}
					catch (Exception ex)
					{
						LogMigrationFailed(logger, ex, file);
					}
				}

				LogMigrationCompleted(logger, migrated);
			}

			[LoggerMessage(
				EventId = 7002,
				Level = LogLevel.Information,
				Message = "Starting file system migration...")]
			private static partial void LogMigrationStarting(ILogger logger);

			[LoggerMessage(
				EventId = 7003,
				Level = LogLevel.Information,
				Message = "Migrated {Count} files...")]
			private static partial void LogMigratedFiles(ILogger logger, int count);

			[LoggerMessage(
				EventId = 7004,
				Level = LogLevel.Error,
				Message = "Failed to migrate file {File}")]
			private static partial void LogMigrationFailed(ILogger logger, Exception exception, string file);

			[LoggerMessage(
				EventId = 7005,
				Level = LogLevel.Information,
				Message = "Migration completed. Migrated {Count} files.")]
			private static partial void LogMigrationCompleted(ILogger logger, int count);
		}
	}

	/// <summary>
	/// Example 4: Performance comparison.
	/// </summary>
	public static partial class PerformanceComparison
	{
		public static async Task ComparePerformanceAsync(IServiceProvider services)
		{
			var loggerFactory = services.GetRequiredService<ILoggerFactory>();
			var logger = loggerFactory.CreateLogger("PerformanceComparison");
			var claimCheckProvider = services.GetRequiredService<IClaimCheckProvider>();

			// Test data
			var testData = new byte[5 * 1024 * 1024]; // 5MB
			new Random().NextBytes(testData);

			// Old way - direct blob storage
			var blobClient = new BlobServiceClient("UseDevelopmentStorage=true");
			var container = blobClient.GetBlobContainerClient("perf-test");
			_ = await container.CreateIfNotExistsAsync().ConfigureAwait(false);

			var sw = ValueStopwatch.StartNew();

			// Upload with old method
			var blob = container.GetBlobClient($"old-{Guid.NewGuid()}");
			using (var stream = new MemoryStream(testData))
			{
				_ = await blob.UploadAsync(stream).ConfigureAwait(false);
			}

			var oldUploadTime = sw.Elapsed.TotalMilliseconds;

			// Download with old method
			sw = ValueStopwatch.StartNew();
			using (var stream = new MemoryStream())
			{
				_ = await blob.DownloadToAsync(stream).ConfigureAwait(false);
			}

			var oldDownloadTime = sw.Elapsed.TotalMilliseconds;

			// New way - claim check provider
			sw = ValueStopwatch.StartNew();

			// Upload with new method (includes compression, chunking)
			var reference = await claimCheckProvider.StoreAsync(testData,
					CancellationToken.None,
					new ClaimCheckMetadata { MessageId = Guid.NewGuid().ToString(), MessageType = "PerformanceTest" })
				.ConfigureAwait(false);

			var newUploadTime = sw.Elapsed.TotalMilliseconds;

			// Download with new method
			sw = ValueStopwatch.StartNew();

			_ = await claimCheckProvider.RetrieveAsync(reference, CancellationToken.None).ConfigureAwait(false);
			var newDownloadTime = sw.Elapsed.TotalMilliseconds;

			// Results
			LogPerformanceResults(logger);
			LogOldMethodPerformance(logger, oldUploadTime, oldDownloadTime);
			LogNewMethodPerformance(logger, newUploadTime, newDownloadTime);
			LogImprovementPercentage(logger,
				1.0 - newUploadTime / oldUploadTime,
				1.0 - newDownloadTime / oldDownloadTime);

			// Storage comparison
			var blobProperties = await blob.GetPropertiesAsync().ConfigureAwait(false);
			LogStorageComparison(logger, blobProperties.Value.ContentLength, reference.Size,
				1.0 - (double)reference.Size / blobProperties.Value.ContentLength);
		}

		[LoggerMessage(
			EventId = 7006,
			Level = LogLevel.Information,
			Message = "Performance Comparison Results:")]
		private static partial void LogPerformanceResults(ILogger logger);

		[LoggerMessage(
			EventId = 7007,
			Level = LogLevel.Information,
			Message = "Old Method - Upload: {Time}ms, Download: {Download}ms")]
		private static partial void LogOldMethodPerformance(ILogger logger, double time, double download);

		[LoggerMessage(
			EventId = 7008,
			Level = LogLevel.Information,
			Message = "New Method - Upload: {Time}ms, Download: {Download}ms")]
		private static partial void LogNewMethodPerformance(ILogger logger, double time, double download);

		[LoggerMessage(
			EventId = 7009,
			Level = LogLevel.Information,
			Message = "Improvement - Upload: {Percent:P}, Download: {DownloadPercent:P}")]
		private static partial void LogImprovementPercentage(ILogger logger, double percent, double downloadPercent);

		[LoggerMessage(
			EventId = 7010,
			Level = LogLevel.Information,
			Message = "Storage - Old: {Old:N0} bytes, New: {New:N0} bytes, Saved: {Saved:P}")]
		private static partial void LogStorageComparison(ILogger logger, long old, long @new, double saved);
	}

	/// <summary>
	/// Migration checklist.
	/// </summary>
	public static class MigrationChecklist
	{
		public const string Checklist = """

		                                # Claim Check Pattern Migration Checklist

		                                ## Pre-Migration
		                                - [ ] Inventory existing claim check or large message handling code
		                                - [ ] Identify all storage locations (blob, file system, database)
		                                - [ ] Document current retention policies
		                                - [ ] Measure current performance metrics
		                                - [ ] Plan migration timeline and rollback strategy

		                                ## Migration Steps
		                                1. [ ] Add Excalibur.Dispatch.CloudNativePatterns NuGet package
		                                2. [ ] Configure claim check services in DI
		                                3. [ ] Update message serialization to use ClaimCheckMessageSerializer
		                                4. [ ] Replace direct storage calls with IClaimCheckProvider
		                                5. [ ] Configure retention and cleanup policies
		                                6. [ ] Enable compression and chunking if beneficial
		                                7. [ ] Update error handling for new exception types
		                                8. [ ] Add metrics and monitoring

		                                ## Testing
		                                - [ ] Unit test message serialization/deserialization
		                                - [ ] Integration test with real storage
		                                - [ ] Load test with production-like message sizes
		                                - [ ] Test cleanup job execution
		                                - [ ] Verify compression ratios
		                                - [ ] Test concurrent operations

		                                ## Deployment
		                                - [ ] Deploy with feature flag if possible
		                                - [ ] Monitor error rates and performance
		                                - [ ] Verify cleanup is working
		                                - [ ] Check storage costs
		                                - [ ] Validate message throughput

		                                ## Post-Migration
		                                - [ ] Remove old claim check code
		                                - [ ] Update documentation
		                                - [ ] Train team on new patterns
		                                - [ ] Set up alerts for failures
		                                - [ ] Schedule regular reviews

		                                """;
	}
}

#pragma warning restore CA1034
