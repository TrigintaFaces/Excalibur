// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Text;

using Azure.Messaging.ServiceBus;

using Excalibur.Dispatch.Abstractions.Serialization;
using Excalibur.Dispatch.Patterns.ClaimCheck;

using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;

namespace Excalibur.Dispatch.CloudNativePatterns.Examples.ClaimCheck;

// Mock interfaces for examples

/// <summary>
/// Examples showing integration of Claim Check pattern with messaging systems.
/// </summary>
#pragma warning disable CA1034 // Nested types are intentionally public for documentation and example clarity

public sealed partial class IntegrationExamples
{
	/// <summary>
	/// Example 1: Integration with Azure Service Bus.
	/// </summary>
	public partial class ServiceBusIntegration(
		ServiceBusClient serviceBusClient,
		IClaimCheckProvider claimCheckProvider,
		ILogger<ServiceBusIntegration> logger)
	{
		private readonly ServiceBusClient _serviceBusClient = serviceBusClient;
		private readonly IClaimCheckProvider _claimCheckProvider = claimCheckProvider;
		private readonly ILogger<ServiceBusIntegration> _logger = logger;

		/// <summary>
		/// Send a large message using claim check pattern.
		/// </summary>
		public async Task SendLargeMessageAsync(string queueName, LargeOrder order, CancellationToken cancellationToken = default)
		{
			ArgumentNullException.ThrowIfNull(order);

			var sender = _serviceBusClient.CreateSender(queueName);

			try
			{
				// Serialize the order
				var orderJson = System.Text.Json.JsonSerializer.Serialize(order);
				var orderBytes = Encoding.UTF8.GetBytes(orderJson);

				ServiceBusMessage message;

				// Check if payload exceeds Service Bus limit (256KB for standard tier)
				if (orderBytes.Length > 256 * 1024)
				{
					LogMessageExceedsLimit(_logger, orderBytes.Length / 1024);

					// Store payload in blob storage
					var metadata = new ClaimCheckMetadata
					{
						MessageId = order.OrderId,
						MessageType = nameof(LargeOrder),
						ContentType = "application/json",
						CorrelationId = order.CustomerId
					};

					var reference = await _claimCheckProvider.StoreAsync(orderBytes, cancellationToken, metadata).ConfigureAwait(false);

					// Create a small message with just the reference
					var claimCheckMessage = new ClaimCheckEnvelope
					{
						ClaimCheckReference = reference,
						MessageType = nameof(LargeOrder),
						Timestamp = DateTimeOffset.UtcNow
					};

					var envelopeJson = System.Text.Json.JsonSerializer.Serialize(claimCheckMessage);
					message = new ServiceBusMessage(envelopeJson)
					{
						ContentType = "application/vnd.claimcheck+json",
						MessageId = order.OrderId,
						CorrelationId = order.CustomerId,
						ApplicationProperties =
						{
							// Add custom properties for routing
							["ClaimCheck"] = true, ["OriginalSize"] = orderBytes.Length
						}
					};
				}
				else
				{
					// Small message - send directly
					message = new ServiceBusMessage(orderBytes)
					{
						ContentType = "application/json",
						MessageId = order.OrderId,
						CorrelationId = order.CustomerId,
						ApplicationProperties = { ["ClaimCheck"] = false }
					};
				}

				// Send the message
				await sender.SendMessageAsync(message, cancellationToken).ConfigureAwait(false);
				LogMessageSentSuccessfully(_logger, order.OrderId);
			}
			finally
			{
				await sender.DisposeAsync().ConfigureAwait(false);
			}
		}

		/// <summary>
		/// Receive and process large messages.
		/// </summary>
		public async Task StartMessageProcessorAsync(string queueName, CancellationToken cancellationToken = default)
		{
			var processor = _serviceBusClient.CreateProcessor(queueName,
				new ServiceBusProcessorOptions { AutoCompleteMessages = false, MaxConcurrentCalls = 10, PrefetchCount = 20 });

			processor.ProcessMessageAsync += async args =>
			{
				try
				{
					byte[] messageBody;

					// Check if this is a claim check message
					if (args.Message.ApplicationProperties.TryGetValue("ClaimCheck", out var isClaimCheck) &&
						(bool)isClaimCheck)
					{
						LogProcessingClaimCheckMessage(_logger, args.Message.MessageId);

						// Deserialize the envelope
						var envelopeJson = args.Message.Body.ToString();
						var envelope = System.Text.Json.JsonSerializer.Deserialize<ClaimCheckEnvelope>(envelopeJson);

						// Retrieve the actual payload
						messageBody = await _claimCheckProvider.RetrieveAsync(
							envelope.ClaimCheckReference,
							cancellationToken).ConfigureAwait(false);

						LogRetrievedFromClaimCheck(_logger, messageBody.Length / 1024);
					}
					else
					{
						// Direct message
						messageBody = args.Message.Body.ToArray();
					}

					// Process the order
					var orderJson = Encoding.UTF8.GetString(messageBody);
					var order = System.Text.Json.JsonSerializer.Deserialize<LargeOrder>(orderJson);

					await ProcessOrderAsync(order).ConfigureAwait(false);

					// Complete the message
					await args.CompleteMessageAsync(args.Message, cancellationToken).ConfigureAwait(false);
				}
				catch (Exception ex)
				{
					LogErrorProcessingMessage(_logger, ex, args.Message.MessageId);

					// Abandon the message for retry
					await args.AbandonMessageAsync(args.Message, null, cancellationToken).ConfigureAwait(false);
				}
			};

			processor.ProcessErrorAsync += args =>
			{
				LogMessageProcessorError(_logger, args.Exception);
				return Task.CompletedTask;
			};

			await processor.StartProcessingAsync(cancellationToken).ConfigureAwait(false);

			// Keep processing until cancelled
			await Task.Delay(Timeout.Infinite, cancellationToken).ConfigureAwait(false);
		}

		private async Task ProcessOrderAsync(LargeOrder order)
		{
			LogProcessingOrder(_logger, order.OrderId, order.Items.Count);

			// Simulate order processing
			await Task.Delay(100).ConfigureAwait(false);
		}

		[LoggerMessage(
			EventId = 9001,
			Level = LogLevel.Information,
			Message = "Message size {Size}KB exceeds limit, using claim check")]
		private static partial void LogMessageExceedsLimit(ILogger logger, int size);

		[LoggerMessage(
			EventId = 9002,
			Level = LogLevel.Information,
			Message = "Message {MessageId} sent successfully")]
		private static partial void LogMessageSentSuccessfully(ILogger logger, string messageId);

		[LoggerMessage(
			EventId = 9003,
			Level = LogLevel.Information,
			Message = "Processing claim check message {MessageId}")]
		private static partial void LogProcessingClaimCheckMessage(ILogger logger, string messageId);

		[LoggerMessage(
			EventId = 9004,
			Level = LogLevel.Information,
			Message = "Retrieved {Size}KB from claim check")]
		private static partial void LogRetrievedFromClaimCheck(ILogger logger, int size);

		[LoggerMessage(
			EventId = 9005,
			Level = LogLevel.Error,
			Message = "Error processing message {MessageId}")]
		private static partial void LogErrorProcessingMessage(ILogger logger, Exception exception, string messageId);

		[LoggerMessage(
			EventId = 9006,
			Level = LogLevel.Error,
			Message = "Error in message processor")]
		private static partial void LogMessageProcessorError(ILogger logger, Exception exception);

		[LoggerMessage(
			EventId = 9007,
			Level = LogLevel.Information,
			Message = "Processing order {OrderId} with {ItemCount} items")]
		private static partial void LogProcessingOrder(ILogger logger, string orderId, int itemCount);
	}

	/// <summary>
	/// Example 2: Integration with message processor pipeline.
	/// </summary>
	public class MessageProcessorIntegration(IClaimCheckProvider claimCheckProvider, IBinaryMessageSerializer baseSerializer)
	{
		private readonly IClaimCheckProvider _claimCheckProvider = claimCheckProvider;
		private readonly IBinaryMessageSerializer _baseSerializer = baseSerializer;

		/// <summary>
		/// Create a message processor with automatic claim check handling.
		/// </summary>
		public IMessageProcessor CreateClaimCheckProcessor()
		{
			// Wrap the serializer with claim check support
			var claimCheckSerializer = new ClaimCheckMessageSerializer(
				_claimCheckProvider,
				_baseSerializer);

			// Create processor pipeline
			return new MessageProcessorBuilder()
				.UseSerializer(claimCheckSerializer)
				.UseMiddleware<LoggingMiddleware>()
				.UseMiddleware<MetricsMiddleware>()
				.UseMiddleware<RetryMiddleware>()
				.Build();
		}
	}

	/// <summary>
	/// Example 3: Distributed system with claim check.
	/// </summary>
	public partial class DistributedSystemExample(
		IClaimCheckProvider claimCheckProvider,
		IDistributedCache cache,
		ILogger<DistributedSystemExample> logger)
	{
		private readonly IClaimCheckProvider _claimCheckProvider = claimCheckProvider;
		private readonly IDistributedCache _cache = cache;
		private readonly ILogger<DistributedSystemExample> _logger = logger;

		/// <summary>
		/// Process large data in a distributed system with caching.
		/// </summary>
		public async Task<ProcessingResult> ProcessLargeDataAsync(
			string dataId,
			CancellationToken cancellationToken = default)
		{
			// Check cache first
			var cacheKey = $"claim-check:{dataId}";
			var cachedReference = await _cache.GetStringAsync(cacheKey, cancellationToken).ConfigureAwait(false);

			byte[] data;
			if (!string.IsNullOrEmpty(cachedReference))
			{
				LogFoundCachedReference(_logger, dataId);

				// Deserialize reference from cache
				var reference = System.Text.Json.JsonSerializer.Deserialize<ClaimCheckReference>(cachedReference);

				// Retrieve data
				data = await _claimCheckProvider.RetrieveAsync(reference, cancellationToken).ConfigureAwait(false);
			}
			else
			{
				LogFetchingData(_logger, dataId);

				// Fetch data from source
				data = await FetchDataFromSourceAsync(dataId, cancellationToken).ConfigureAwait(false);

				// Store in claim check if large
				if (data.Length > 1024 * 1024) // 1MB
				{
					var metadata = new ClaimCheckMetadata
					{
						MessageId = dataId,
						MessageType = "LargeData",
						Properties = { ["source"] = "distributed-system" }
					};

					var reference = await _claimCheckProvider.StoreAsync(data, cancellationToken, metadata).ConfigureAwait(false);

					// Cache the reference
					var referenceJson = System.Text.Json.JsonSerializer.Serialize(reference);
					await _cache.SetStringAsync(
						cacheKey,
						referenceJson,
						new DistributedCacheEntryOptions { SlidingExpiration = TimeSpan.FromHours(1) },
						cancellationToken).ConfigureAwait(false);
				}
			}

			// Process the data
			return await ProcessDataAsync(data, cancellationToken).ConfigureAwait(false);
		}

		private async Task<byte[]> FetchDataFromSourceAsync(string dataId, CancellationToken cancellationToken)
		{
			// Simulate fetching large data
			await Task.Delay(100, cancellationToken).ConfigureAwait(false);
			return Encoding.UTF8.GetBytes($"Large data for {dataId}..." + new string('x', 2 * 1024 * 1024));
		}

		private static async Task<ProcessingResult> ProcessDataAsync(byte[] data, CancellationToken cancellationToken)
		{
			// Simulate data processing
			await Task.Delay(50, cancellationToken).ConfigureAwait(false);
			return new ProcessingResult { Success = true, ProcessedBytes = data.Length };
		}

		[LoggerMessage(
			EventId = 10001,
			Level = LogLevel.Information,
			Message = "Found cached claim check reference for {DataId}")]
		private static partial void LogFoundCachedReference(ILogger logger, string dataId);

		[LoggerMessage(
			EventId = 10002,
			Level = LogLevel.Information,
			Message = "No cached reference, fetching data for {DataId}")]
		private static partial void LogFetchingData(ILogger logger, string dataId);
	}

	/// <summary>
	/// Example 4: Error handling and retry scenarios.
	/// </summary>
	public partial class ErrorHandlingExample(
		IClaimCheckProvider claimCheckProvider,
		ILogger<ErrorHandlingExample> logger)
	{
		private readonly IClaimCheckProvider _claimCheckProvider = claimCheckProvider;
		private readonly ILogger<ErrorHandlingExample> _logger = logger;

		/// <summary>
		/// Process with comprehensive error handling.
		/// </summary>
		public async Task<T> ProcessWithClaimCheckAsync<T>(
			string messageId,
			Func<byte[], Task<T>> processor,
			CancellationToken cancellationToken = default)
		{
			ArgumentNullException.ThrowIfNull(processor);

			const int MaxRetries = 3;
			var retryCount = 0;

			while (retryCount < MaxRetries)
			{
				try
				{
					// Try to get claim check reference from somewhere
					var reference = await GetClaimCheckReferenceAsync(messageId).ConfigureAwait(false);

					if (reference == null)
					{
						throw new InvalidOperationException($"No claim check reference found for message {messageId}");
					}

					// Retrieve with timeout
					using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
					cts.CancelAfter(TimeSpan.FromMinutes(5));

					var data = await _claimCheckProvider.RetrieveAsync(reference, cts.Token).ConfigureAwait(false);

					// Process the data
					var result = await processor(data).ConfigureAwait(false);

					// Clean up if configured
					if (reference.Metadata?.Properties?.GetValueOrDefault("DeleteAfterRead") == "true")
					{
						try
						{
							_ = await _claimCheckProvider.DeleteAsync(reference, cancellationToken).ConfigureAwait(false);
							LogDeletedClaimCheck(_logger, reference.Id);
						}
						catch (Exception ex)
						{
							LogFailedToDeleteClaimCheck(_logger, ex, reference.Id);
							// Don't fail the operation
						}
					}

					return result;
				}
				catch (ClaimCheckNotFoundException ex)
				{
					LogClaimCheckNotFound(_logger, ex, messageId);
					throw; // Don't retry for not found
				}
				catch (Exception ex) when (retryCount < MaxRetries - 1)
				{
					retryCount++;
					LogClaimCheckRetry(_logger, ex, messageId, retryCount);

					// Exponential backoff
					await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, retryCount)), cancellationToken).ConfigureAwait(false);
				}
			}

			throw new InvalidOperationException($"Failed to process claim check for message {messageId} after {MaxRetries} retries");
		}

		private async Task<ClaimCheckReference?> GetClaimCheckReferenceAsync(string messageId)
		{
			// Simulate getting reference from a database or message
			await Task.Delay(10).ConfigureAwait(false);
			return new ClaimCheckReference { Id = Guid.NewGuid().ToString(), BlobName = $"claims/{messageId}", Size = 1024 * 1024 };
		}

		[LoggerMessage(
			EventId = 11001,
			Level = LogLevel.Information,
			Message = "Deleted claim check {ClaimId} after successful processing")]
		private static partial void LogDeletedClaimCheck(ILogger logger, string claimId);

		[LoggerMessage(
			EventId = 11002,
			Level = LogLevel.Warning,
			Message = "Failed to delete claim check {ClaimId}")]
		private static partial void LogFailedToDeleteClaimCheck(ILogger logger, Exception exception, string claimId);

		[LoggerMessage(
			EventId = 11003,
			Level = LogLevel.Error,
			Message = "Claim check not found for message {MessageId}")]
		private static partial void LogClaimCheckNotFound(ILogger logger, Exception exception, string messageId);

		[LoggerMessage(
			EventId = 11004,
			Level = LogLevel.Warning,
			Message = "Error processing claim check for message {MessageId}, retry {RetryCount}")]
		private static partial void LogClaimCheckRetry(ILogger logger, Exception exception, string messageId, int retryCount);
	}
}

#pragma warning restore CA1034

// Supporting classes for examples
