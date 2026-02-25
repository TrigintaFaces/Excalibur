// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics;
using System.Text;

using Excalibur.Dispatch.Patterns.ClaimCheck;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Excalibur.Dispatch.CloudNativePatterns.Examples.ClaimCheck;

/// <summary>
/// Demo service that demonstrates various claim check scenarios.
/// </summary>
public partial class ClaimCheckDemoService(
	IClaimCheckProvider claimCheckProvider,
	ILogger<ClaimCheckDemoService> logger) : BackgroundService
{
	private readonly IClaimCheckProvider _claimCheckProvider = claimCheckProvider;
	private readonly ILogger<ClaimCheckDemoService> _logger = logger;

	/// <inheritdoc />
	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		LogStartingDemonstrations(_logger);

		// Demo 1: Small message (no claim check)
		await DemoSmallMessage(stoppingToken).ConfigureAwait(false);

		// Demo 2: Large message (automatic claim check)
		await DemoLargeMessage(stoppingToken).ConfigureAwait(false);

		// Demo 3: Chunked large message
		await DemoChunkedMessage(stoppingToken).ConfigureAwait(false);

		// Demo 4: Compressed message
		await DemoCompressedMessage(stoppingToken).ConfigureAwait(false);

		// Demo 5: Performance test
		await DemoPerformanceTest(stoppingToken).ConfigureAwait(false);

		LogDemonstrationsCompleted(_logger);
	}

	private static byte[] GenerateLargePayload(int size)
	{
		var payload = new byte[size];
		var random = new Random();

		// Fill with semi-random data (compressible)
		for (var i = 0; i < size; i++)
		{
			payload[i] = (byte)(random.Next(0, 26) + 65); // A-Z
		}

		return payload;
	}

	[LoggerMessage(
		EventId = 8001,
		Level = LogLevel.Information,
		Message = "Starting Claim Check pattern demonstrations...")]
	private static partial void LogStartingDemonstrations(ILogger logger);

	[LoggerMessage(
		EventId = 8002,
		Level = LogLevel.Information,
		Message = "Claim Check demonstrations completed!")]
	private static partial void LogDemonstrationsCompleted(ILogger logger);

	[LoggerMessage(
		EventId = 8003,
		Level = LogLevel.Information,
		Message = "=== Demo 1: Small Message (No Claim Check) ===")]
	private static partial void LogDemo1Start(ILogger logger);

	[LoggerMessage(
		EventId = 8004,
		Level = LogLevel.Information,
		Message = "Small message serialized. Size: {Size} bytes. Claim check used: {ClaimCheckUsed}")]
	private static partial void LogSmallMessageSerialized(ILogger logger, int size, bool claimCheckUsed);

	[LoggerMessage(
		EventId = 8005,
		Level = LogLevel.Information,
		Message = "Message deserialized successfully. Content: {Content}")]
	private static partial void LogMessageDeserialized(ILogger logger, string content);

	[LoggerMessage(
		EventId = 8006,
		Level = LogLevel.Information,
		Message = "=== Demo 2: Large Message (Automatic Claim Check) ===")]
	private static partial void LogDemo2Start(ILogger logger);

	[LoggerMessage(
		EventId = 8007,
		Level = LogLevel.Information,
		Message = "Large message serialized in {ElapsedMs}ms. Serialized size: {Size} bytes (vs original {OriginalSize} bytes)")]
	private static partial void LogLargeMessageSerialized(ILogger logger, long elapsedMs, int size, int originalSize);

	[LoggerMessage(
		EventId = 8008,
		Level = LogLevel.Information,
		Message = "Message deserialized in {ElapsedMs}ms. Payload size: {Size} bytes")]
	private static partial void LogMessageDeserializedWithSize(ILogger logger, long elapsedMs, int size);

	[LoggerMessage(
		EventId = 8009,
		Level = LogLevel.Information,
		Message = "=== Demo 3: Chunked Large Message ===")]
	private static partial void LogDemo3Start(ILogger logger);

	[LoggerMessage(
		EventId = 8010,
		Level = LogLevel.Information,
		Message = "Very large payload stored in {ElapsedMs}ms. Reference: {ClaimId}, Chunks: {ChunkCount}")]
	private static partial void LogVeryLargePayloadStored(ILogger logger, long elapsedMs, string claimId, string chunkCount);

	[LoggerMessage(
		EventId = 8011,
		Level = LogLevel.Information,
		Message = "Payload retrieved in {ElapsedMs}ms. Size verified: {SizeMatch}")]
	private static partial void LogPayloadRetrieved(ILogger logger, long elapsedMs, bool sizeMatch);

	[LoggerMessage(
		EventId = 8012,
		Level = LogLevel.Information,
		Message = "=== Demo 4: Compressed Message ===")]
	private static partial void LogDemo4Start(ILogger logger);

	[LoggerMessage(
		EventId = 8013,
		Level = LogLevel.Information,
		Message =
			"Compressible payload stored in {ElapsedMs}ms. Original size: {OriginalSize} bytes, Compressed: {Compressed}, Final size: ~{CompressedSize} bytes")]
	private static partial void LogCompressiblePayloadStored(ILogger logger, long elapsedMs, int originalSize, string compressed,
		long compressedSize);

	[LoggerMessage(
		EventId = 8014,
		Level = LogLevel.Information,
		Message = "Compression ratio: {Ratio:F1}%")]
	private static partial void LogCompressionRatio(ILogger logger, double ratio);

	[LoggerMessage(
		EventId = 8015,
		Level = LogLevel.Information,
		Message = "=== Demo 5: Performance Test ===")]
	private static partial void LogDemo5Start(ILogger logger);

	[LoggerMessage(
		EventId = 8016,
		Level = LogLevel.Information,
		Message =
			"Stored {Count} messages ({TotalSizeMB:F1} MB) in {ElapsedMs}ms. Throughput: {Throughput:F1} MB/s, {MessagesPerSec:F1} messages/sec")]
	private static partial void LogPerformanceTestCompleted(ILogger logger, int count, double totalSizeMB, long elapsedMs,
		double throughput, double messagesPerSec);

	private async Task DemoSmallMessage(CancellationToken cancellationToken)
	{
		LogDemo1Start(_logger);

		var smallPayload = "This is a small message that won't trigger claim check.";
		var message = new SampleMessage
		{
			Id = Guid.NewGuid(),
			Payload = Encoding.UTF8.GetBytes(smallPayload),
			Timestamp = DateTimeOffset.UtcNow
		};

		// Serialize message (claim check not triggered for small payloads)
		var serializer = new ClaimCheckMessageSerializer(_claimCheckProvider);
		var serialized = await serializer.SerializeAsync(message, cancellationToken).ConfigureAwait(false);

		LogSmallMessageSerialized(_logger, serialized.Length, false);

		// Deserialize message
		var deserialized = await serializer.DeserializeAsync<SampleMessage>(serialized, cancellationToken).ConfigureAwait(false);
		LogMessageDeserialized(_logger, Encoding.UTF8.GetString(deserialized.Payload));
	}

	private async Task DemoLargeMessage(CancellationToken cancellationToken)
	{
		LogDemo2Start(_logger);

		// Create a large payload (100KB)
		var largePayload = GenerateLargePayload(100 * 1024);
		var message = new SampleMessage { Id = Guid.NewGuid(), Payload = largePayload, Timestamp = DateTimeOffset.UtcNow };

		// Serialize message (claim check triggered for large payloads)
		var serializer = new ClaimCheckMessageSerializer(_claimCheckProvider);
		var sw = Stopwatch.StartNew();
		var serialized = await serializer.SerializeAsync(message, cancellationToken).ConfigureAwait(false);
		sw.Stop();

		LogLargeMessageSerialized(_logger, sw.ElapsedMilliseconds, serialized.Length, largePayload.Length);

		// Deserialize message
		sw.Restart();
		var deserialized = await serializer.DeserializeAsync<SampleMessage>(serialized, cancellationToken).ConfigureAwait(false);
		sw.Stop();

		LogMessageDeserializedWithSize(_logger, sw.ElapsedMilliseconds, deserialized.Payload.Length);
	}

	private async Task DemoChunkedMessage(CancellationToken cancellationToken)
	{
		LogDemo3Start(_logger);

		// Create a very large payload (5MB)
		var veryLargePayload = GenerateLargePayload(5 * 1024 * 1024);
		var message = new SampleMessage { Id = Guid.NewGuid(), Payload = veryLargePayload, Timestamp = DateTimeOffset.UtcNow };

		// Store using chunked provider for better performance
		var sw = Stopwatch.StartNew();
		var reference = await _claimCheckProvider.StoreAsync(
			veryLargePayload,
			cancellationToken,
			new ClaimCheckMetadata
			{
				MessageId = message.Id.ToString(),
				MessageType = nameof(SampleMessage),
				ContentType = "application/octet-stream"
			}).ConfigureAwait(false);
		sw.Stop();

		LogVeryLargePayloadStored(
			_logger,
			sw.ElapsedMilliseconds,
			reference.Id,
			reference.Metadata?.Properties?.GetValueOrDefault("ChunkCount") ?? "1");

		// Retrieve the payload
		sw.Restart();
		var retrieved = await _claimCheckProvider.RetrieveAsync(reference, cancellationToken).ConfigureAwait(false);
		sw.Stop();

		LogPayloadRetrieved(_logger, sw.ElapsedMilliseconds, retrieved.Length == veryLargePayload.Length);
	}

	private async Task DemoCompressedMessage(CancellationToken cancellationToken)
	{
		LogDemo4Start(_logger);

		// Create a compressible payload (repeated text)
		var compressibleText = string.Join("", Enumerable.Repeat("This is highly compressible text. ", 5000));
		var compressiblePayload = Encoding.UTF8.GetBytes(compressibleText);

		var message = new SampleMessage { Id = Guid.NewGuid(), Payload = compressiblePayload, Timestamp = DateTimeOffset.UtcNow };

		// Store with compression
		var metadata = new ClaimCheckMetadata
		{
			MessageId = message.Id.ToString(),
			MessageType = nameof(SampleMessage),
			ContentType = "text/plain"
			// Compression is handled automatically based on options
		};

		var sw = Stopwatch.StartNew();
		var reference = await _claimCheckProvider.StoreAsync(compressiblePayload, cancellationToken, metadata).ConfigureAwait(false);
		sw.Stop();

		LogCompressiblePayloadStored(
			_logger,
			sw.ElapsedMilliseconds,
			compressiblePayload.Length,
			reference.Metadata?.Properties?.GetValueOrDefault("Compressed") ?? "false",
			reference.Size);

		// Calculate compression ratio
		if (reference.Size > 0)
		{
			var compressionRatio = (1 - (double)reference.Size / compressiblePayload.Length) * 100;
			LogCompressionRatio(_logger, compressionRatio);
		}
	}

	private async Task DemoPerformanceTest(CancellationToken cancellationToken)
	{
		LogDemo5Start(_logger);

		const int MessageCount = 100;
		const int PayloadSize = 256 * 1024; // 256KB per message

		var sw = Stopwatch.StartNew();
		var tasks = new Task[MessageCount];

		// Parallel store operations
		for (var i = 0; i < MessageCount; i++)
		{
			var index = i;
			tasks[i] = Task.Run(async () =>
			{
				var payload = GenerateLargePayload(PayloadSize);
				var metadata = new ClaimCheckMetadata
				{
					MessageId = $"perf-test-{index}",
					MessageType = "PerformanceTest",
					ContentType = "application/octet-stream"
				};

				_ = await _claimCheckProvider.StoreAsync(payload, cancellationToken, metadata).ConfigureAwait(false);
			}, cancellationToken);
		}

		await Task.WhenAll(tasks).ConfigureAwait(false);
		sw.Stop();

		var throughputMBps = MessageCount * PayloadSize / 1024.0 / 1024.0 / (sw.ElapsedMilliseconds / 1000.0);
		LogPerformanceTestCompleted(
			_logger,
			MessageCount,
			MessageCount * PayloadSize / 1024.0 / 1024.0,
			sw.ElapsedMilliseconds,
			throughputMBps,
			MessageCount / (sw.ElapsedMilliseconds / 1000.0));
	}
}
