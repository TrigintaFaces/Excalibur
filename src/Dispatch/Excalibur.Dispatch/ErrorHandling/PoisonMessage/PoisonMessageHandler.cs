// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Serialization;
using Excalibur.Dispatch.Diagnostics;
using Excalibur.Dispatch.Messaging;
using Excalibur.Dispatch.Options.ErrorHandling;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using DispatchMessageMetadata = Excalibur.Dispatch.Messaging.MessageMetadata;

namespace Excalibur.Dispatch.ErrorHandling;

/// <summary>
/// Default implementation of the poison message handler.
/// </summary>
public sealed partial class PoisonMessageHandler : IPoisonMessageHandler, IDisposable
{
	private volatile bool _disposed;

	private readonly IDeadLetterStore _deadLetterStore;
	private readonly IJsonSerializer _serializer;
	private readonly IServiceProvider _serviceProvider;
	private readonly PoisonMessageOptions _options;
	private readonly ILogger<PoisonMessageHandler> _logger;
	private readonly ActivitySource _activitySource;

	/// <summary>
	/// Initializes a new instance of the <see cref="PoisonMessageHandler" /> class.
	/// </summary>
	public PoisonMessageHandler(
		IDeadLetterStore deadLetterStore,
		IJsonSerializer serializer,
		IServiceProvider serviceProvider,
		IOptions<PoisonMessageOptions> options,
		ILogger<PoisonMessageHandler> logger)
	{
		ArgumentNullException.ThrowIfNull(deadLetterStore);
		ArgumentNullException.ThrowIfNull(serializer);
		ArgumentNullException.ThrowIfNull(serviceProvider);
		ArgumentNullException.ThrowIfNull(options);
		ArgumentNullException.ThrowIfNull(logger);

		_deadLetterStore = deadLetterStore;
		_serializer = serializer;
		_serviceProvider = serviceProvider;
		_options = options.Value;
		_logger = logger;
		_activitySource = new ActivitySource(DispatchTelemetryConstants.ActivitySources.PoisonMessage);
	}

	/// <inheritdoc />
	[RequiresUnreferencedCode(
			"Poison message handling serializes message payloads and metadata which may require preserved members.")]
	[RequiresDynamicCode("Uses dynamic code generation which requires JIT compilation")]
	public async Task HandlePoisonMessageAsync(
	IDispatchMessage message,
	IMessageContext context,
	string reason,
	CancellationToken cancellationToken,
	Exception? exception = null)
	{
		ArgumentNullException.ThrowIfNull(message);
		ArgumentNullException.ThrowIfNull(context);
		ArgumentException.ThrowIfNullOrWhiteSpace(reason);

		using var activity = _activitySource.StartActivity("HandlePoisonMessage");
		_ = (activity?.SetTag("message.id", context.MessageId));
		_ = (activity?.SetTag("message.type", message.GetType().FullName));
		_ = (activity?.SetTag("poison.reason", reason));

		try
		{
			// Serialize the message and metadata
			var messageBody = await _serializer.SerializeAsync(message).ConfigureAwait(false);
			var metadata = new DispatchMessageMetadata(
				MessageId: context.MessageId ?? Guid.NewGuid().ToString("N"),
				CorrelationId: context.CorrelationId ?? Guid.NewGuid().ToString("N"),
				CausationId: context.CausationId,
				TraceParent: context.TraceParent,
				TenantId: context.TenantId,
				UserId: context.UserId,
				ContentType: "application/json",
				SerializerVersion: "1.0",
				MessageVersion: "1.0",
				ContractVersion: "1.0.0");
			var messageMetadata = await _serializer.SerializeAsync(metadata).ConfigureAwait(false);

			// Extract processing info from context
			var processingInfo = ExtractProcessingInfo(context);

			var deadLetterMessage = new DeadLetterMessage
			{
				MessageId = context.MessageId ?? Guid.NewGuid().ToString("N"),
				MessageType = message.GetType().FullName ?? message.GetType().Name,
				MessageBody = messageBody,
				MessageMetadata = messageMetadata,
				Reason = reason,
				ExceptionDetails = _options.CaptureExceptionDetails && exception != null
					? SerializeException(exception)
					: null,
				ProcessingAttempts = processingInfo.AttemptCount,
				FirstAttemptAt = processingInfo.FirstAttemptTime,
				LastAttemptAt = processingInfo.CurrentAttemptTime,
				SourceSystem = context.Items.TryGetValue("SourceSystem", out var sourceSystem)
					? sourceSystem?.ToString()
					: null,
				CorrelationId = context.CorrelationId,
				Properties = ExtractCustomProperties(context),
			};

			await _deadLetterStore.StoreAsync(deadLetterMessage, cancellationToken).ConfigureAwait(false);

			LogMessageMovedToDeadLetterQueueWithReason(deadLetterMessage.MessageId, deadLetterMessage.MessageType, reason);

			// Track metrics
			if (_options.EnableMetrics)
			{
				_ = (activity?.SetTag("poison.stored", value: true));
				_ = (activity?.SetTag("poison.attempts", processingInfo.AttemptCount));
			}
		}
		catch (Exception ex)
		{
			LogFailedToHandlePoisonMessageOriginalReason(context.MessageId ?? "Unknown", reason, ex);

			_ = (activity?.SetStatus(ActivityStatusCode.Error, ex.Message));
			throw;
		}
	}

	/// <inheritdoc />
	[RequiresUnreferencedCode(
		"Dead letter message replay uses reflection to resolve message types from AssemblyQualifiedName strings for dynamic deserialization.")]
	[RequiresDynamicCode("Uses dynamic code generation which requires JIT compilation")]
	public async Task<bool> ReplayMessageAsync(string messageId, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(messageId);

		using var activity = _activitySource.StartActivity("ReplayPoisonMessage");
		_ = (activity?.SetTag("message.id", messageId));

		try
		{
			var deadLetterMessage = await _deadLetterStore.GetByIdAsync(messageId, cancellationToken)
				.ConfigureAwait(false);

			if (deadLetterMessage == null)
			{
				LogDeadLetterMessageNotFoundForReplay(messageId);
				return false;
			}

			// Deserialize the original message - uses TypeResolver for AOT compatibility
			var messageType = TypeResolution.TypeResolver.ResolveType(deadLetterMessage.MessageType);
			if (messageType == null)
			{
				LogCannotReplayMessageTypeNotFound(messageId, deadLetterMessage.MessageType);
				return false;
			}

			var message = await _serializer.DeserializeAsync(deadLetterMessage.MessageBody, messageType)
				.ConfigureAwait(false);

			if (message is not IDispatchMessage dispatchMessage)
			{
				LogCannotReplayMessageNotDispatchMessage(messageId);
				return false;
			}

			// Deserialize metadata and create context
			var metadata = await _serializer.DeserializeAsync(
				deadLetterMessage.MessageMetadata,
				typeof(DispatchMessageMetadata)).ConfigureAwait(false) as DispatchMessageMetadata;

			var metaDict = new Dictionary<string, string?>
(StringComparer.Ordinal)
			{
				["CorrelationId"] = metadata?.CorrelationId,
				["CausationId"] = metadata?.CausationId,
				["TraceParent"] = metadata?.TraceParent,
				["TenantId"] = metadata?.TenantId,
				["UserId"] = metadata?.UserId,
			};

			using var scope = _serviceProvider.CreateScope();
			var context = DispatchContextInitializer.CreateFromMetadata(metaDict);
			context.MessageId = deadLetterMessage.MessageId;
			context.Items["IsReplay"] = true;
			context.Items["OriginalDeadLetterId"] = deadLetterMessage.Id;

			// Dispatch the message
			var dispatcher = scope.ServiceProvider.GetRequiredService<IDispatcher>();
			var result = await dispatcher.DispatchAsync(dispatchMessage, context, cancellationToken)
				.ConfigureAwait(false);

			if (result is { Succeeded: true })
			{
				await _deadLetterStore.MarkAsReplayedAsync(messageId, cancellationToken).ConfigureAwait(false);

				LogSuccessfullyReplayedDeadLetterMessage(messageId);

				_ = (activity?.SetTag("replay.success", value: true));
				return true;
			}

			LogFailedToReplayDeadLetterMessage(messageId, result?.ErrorMessage ?? "Unknown error");

			_ = (activity?.SetTag("replay.success", value: false));
			return false;
		}
		catch (Exception ex)
		{
			LogErrorReplayingDeadLetterMessage(messageId, ex);

			_ = (activity?.SetStatus(ActivityStatusCode.Error, ex.Message));
			return false;
		}
	}

	/// <inheritdoc />
	public async Task<PoisonMessageStatistics> GetStatisticsAsync(CancellationToken cancellationToken)
	{
		using var activity = _activitySource.StartActivity("GetPoisonStatistics");

		var totalCount = await _deadLetterStore.GetCountAsync(cancellationToken).ConfigureAwait(false);

		// Get recent messages for time window calculation
		var recentFilter = new DeadLetterFilter { FromDate = DateTimeOffset.UtcNow.Subtract(_options.AlertTimeWindow), MaxResults = 1000 };

		var recentMessages = await _deadLetterStore.GetMessagesAsync(recentFilter, cancellationToken)
			.ConfigureAwait(false);

		var recentMessagesList = recentMessages.ToList();
		var messagesByType = recentMessagesList
			.GroupBy(static m => m.MessageType, StringComparer.Ordinal)
			.ToDictionary(static g => g.Key, static g => g.Count(), StringComparer.Ordinal);

		var messagesByReason = recentMessagesList
			.GroupBy(static m => m.Reason, StringComparer.Ordinal)
			.ToDictionary(static g => g.Key, static g => g.Count(), StringComparer.Ordinal);

		var statistics = new PoisonMessageStatistics
		{
			TotalCount = totalCount,
			RecentCount = recentMessagesList.Count,
			TimeWindow = _options.AlertTimeWindow,
			MessagesByType = messagesByType,
			MessagesByReason = messagesByReason,
			OldestMessageDate = recentMessagesList.Count > 0
				? recentMessagesList.Min(static m => m.MovedToDeadLetterAt)
				: null,
			NewestMessageDate = recentMessagesList.Count > 0
				? recentMessagesList.Max(static m => m.MovedToDeadLetterAt)
				: null,
		};

		_ = (activity?.SetTag("statistics.total", totalCount));
		_ = (activity?.SetTag("statistics.recent", recentMessagesList.Count));

		return statistics;
	}

	/// <summary>
	/// Extracts processing information from the message context.
	/// </summary>
	private static MessageProcessingInfo ExtractProcessingInfo(IMessageContext context)
	{
		var attemptCount = 1;
		var firstAttemptTime = DateTimeOffset.UtcNow;
		var currentAttemptTime = DateTimeOffset.UtcNow;

		if (context.Items.TryGetValue("ProcessingAttempts", out var attempts) && attempts is int count)
		{
			attemptCount = count;
		}

		if (context.Items.TryGetValue("FirstAttemptTime", out var firstTime) && firstTime is DateTimeOffset first)
		{
			firstAttemptTime = first;
		}

		if (context.Items.TryGetValue("CurrentAttemptTime", out var currentTime) && currentTime is DateTimeOffset current)
		{
			currentAttemptTime = current;
		}

		return new MessageProcessingInfo
		{
			AttemptCount = attemptCount,
			FirstAttemptTime = firstAttemptTime,
			CurrentAttemptTime = currentAttemptTime,
			TotalProcessingTime = currentAttemptTime - firstAttemptTime,
		};
	}

	/// <summary>
	/// Extracts custom properties from the message context.
	/// </summary>
	private static Dictionary<string, string> ExtractCustomProperties(IMessageContext context)
	{
		var properties = new Dictionary<string, string>(StringComparer.Ordinal);

		// Add selected context items as properties
		var allowedKeys = new[] { "Environment", "Version", "Component", "Feature" };

		foreach (var key in allowedKeys)
		{
			if (context.Items.TryGetValue(key, out var value) && value != null)
			{
				properties[key] = value.ToString() ?? string.Empty;
			}
		}

		return properties;
	}

	/// <summary>
	/// Serializes exception details for storage.
	/// </summary>
	[RequiresDynamicCode("Calls System.Text.Json.JsonSerializer.Serialize<TValue>(TValue, JsonSerializerOptions)")]
	[RequiresUnreferencedCode("Calls System.Text.Json.JsonSerializer.Serialize<TValue>(TValue, JsonSerializerOptions)")]
	private static string SerializeException(Exception exception)
	{
		var exceptionData = new
		{
			Type = exception.GetType().FullName,
			exception.Message,
			exception.StackTrace,
			InnerException =
				exception.InnerException != null
					? new
					{
						Type = exception.InnerException.GetType().FullName,
						exception.InnerException.Message,
						exception.InnerException.StackTrace,
					}
					: null,
			Data = exception.Data.Count > 0 ? exception.Data : null,
		};

		return JsonSerializer.Serialize(exceptionData, new JsonSerializerOptions { WriteIndented = true });
	}

	/// <summary>
	/// Disposes the handler and releases the activity source.
	/// </summary>
	public void Dispose()
	{
		if (_disposed)
		{
			return;
		}

		_activitySource?.Dispose();
		_disposed = true;
	}

	// Source-generated logging methods
	[LoggerMessage(DeliveryEventId.PoisonHandlerStored, LogLevel.Warning,
		"Message '{MessageId}' of type '{MessageType}' moved to dead letter queue: {Reason}")]
	private partial void LogMessageMovedToDeadLetterQueueWithReason(string messageId, string messageType, string reason);

	[LoggerMessage(DeliveryEventId.PoisonHandlerProcessingError, LogLevel.Error,
		"Failed to handle poison message '{MessageId}'. Original reason: {Reason}")]
	private partial void LogFailedToHandlePoisonMessageOriginalReason(string messageId, string reason, Exception ex);

	[LoggerMessage(DeliveryEventId.PoisonReplayNotFound, LogLevel.Warning,
		"Dead letter message '{MessageId}' not found for replay")]
	private partial void LogDeadLetterMessageNotFoundForReplay(string messageId);

	[LoggerMessage(DeliveryEventId.PoisonReplayTypeNotFound, LogLevel.Error,
		"Cannot replay message '{MessageId}': type '{MessageType}' not found")]
	private partial void LogCannotReplayMessageTypeNotFound(string messageId, string messageType);

	[LoggerMessage(DeliveryEventId.PoisonReplayNotDispatchMessage, LogLevel.Error,
		"Cannot replay message '{MessageId}': deserialized object is not IDispatchMessage")]
	private partial void LogCannotReplayMessageNotDispatchMessage(string messageId);

	[LoggerMessage(DeliveryEventId.PoisonReplaySuccess, LogLevel.Information,
		"Successfully replayed dead letter message '{MessageId}'")]
	private partial void LogSuccessfullyReplayedDeadLetterMessage(string messageId);

	[LoggerMessage(DeliveryEventId.PoisonReplayFailed, LogLevel.Warning,
		"Failed to replay dead letter message '{MessageId}': {ErrorMessage}")]
	private partial void LogFailedToReplayDeadLetterMessage(string messageId, string errorMessage);

	[LoggerMessage(DeliveryEventId.PoisonReplayError, LogLevel.Error,
		"Error replaying dead letter message '{MessageId}'")]
	private partial void LogErrorReplayingDeadLetterMessage(string messageId, Exception ex);
}
