// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0


using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

using Excalibur.Dispatch;
using Excalibur.Dispatch.Diagnostics;
using Excalibur.Dispatch.Features;
using Excalibur.Dispatch.Messaging;
using Excalibur.Dispatch.Options.ErrorHandling;
using Excalibur.Dispatch.Serialization;

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
	private static readonly string[] AllowedContextKeys = ["Environment", "Version", "Component", "Feature"];

	/// <summary>
	/// How long the release of a replay claim may take before it is abandoned and logged.
	/// </summary>
	/// <remarks>
	/// Bounded because the release runs on the failure path with its own token: a store that has stopped
	/// answering must not hold the caller there indefinitely. Abandoning it leaves one message marked
	/// replayed that was not delivered, which is logged as an error rather than left silent.
	/// </remarks>
	private static readonly TimeSpan ClaimReleaseBudget = TimeSpan.FromSeconds(5);

	private volatile bool _disposed;

	private readonly IDeadLetterStore _deadLetterStore;
	private readonly DispatchJsonSerializer _serializer;
	private readonly IServiceProvider _serviceProvider;
	private readonly PoisonMessageOptions _options;
	private readonly ILogger<PoisonMessageHandler> _logger;
	private readonly ActivitySource _activitySource;

	/// <summary>
	/// Initializes a new instance of the <see cref="PoisonMessageHandler" /> class.
	/// </summary>
	public PoisonMessageHandler(
		IDeadLetterStore deadLetterStore,
		DispatchJsonSerializer serializer,
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
			var messageBody = await _serializer.SerializeAsync(message, message.GetType()).ConfigureAwait(false);
			var metadata = new DispatchMessageMetadata(
				MessageId: context.MessageId ?? Guid.NewGuid().ToString("N"),
				CorrelationId: context.CorrelationId ?? Guid.NewGuid().ToString("N"),
				CausationId: context.CausationId,
				TraceParent: context.GetTraceParent(),
				TenantId: context.GetTenantId(),
				UserId: context.GetUserId(),
				ContentType: "application/json",
				SerializerVersion: "1.0",
				MessageVersion: "1.0",
				ContractVersion: "1.0.0");
			var messageMetadata = await _serializer.SerializeAsync(metadata, typeof(DispatchMessageMetadata)).ConfigureAwait(false);

			// Extract processing info from context
			var processingInfo = ExtractProcessingInfo(context);

			// The declared name, not the CLR FullName: every other durable surface (event store, outbox,
			// CloudEvents) stores the declared name, and a FullName moves when a consumer refactors a
			// namespace, orphaning its own dead letters. GetDeclaredName is used rather than GetName
			// because GetName THROWS for a type with no declared name, and this is the error path -- it
			// must not fail a second time. Both forms resolve on replay: the type registry claims a type
			// under its declared name AND its CLR forms, so entries stored under either name replay.
			var messageClrType = message.GetType();
			var declaredMessageType = MessageNameHelper.GetDeclaredName(messageClrType)
				?? messageClrType.FullName ?? messageClrType.Name;

			var deadLetterMessage = new DeadLetterMessage
			{
				MessageId = context.MessageId ?? Guid.NewGuid().ToString("N"),
				MessageType = declaredMessageType,
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

			// REFUSE A MESSAGE THAT HAS ALREADY BEEN REPLAYED. Without this, a second call to this member
			// for the same id dispatches the message a second time: the mark happens AFTER the dispatch,
			// so nothing between the two reads consulted it. That needs no concurrency at all -- a
			// sequential second call is enough, and an operator retrying a replay that appeared to hang is
			// the ordinary way to make it happen.
			//
			// This is READ-THEN-ACT AND IT IS NOT A CLAIM, which is the honest limit of it. Two callers
			// racing can both pass this guard, because the mark it reads is written after the effect and
			// cannot refuse: IDeadLetterStore.MarkAsReplayedAsync returns plain Task, so it is
			// structurally unable to report that somebody else got there first. Closing the concurrent
			// case needs that member to become a claim and to return an outcome, which is a change to a
			// shipped contract with three implementations and is tracked separately. What this removes is
			// the reachable sequential case, which is the one a consumer meets.
			if (deadLetterMessage.IsReplayed)
			{
				LogDeadLetterMessageAlreadyReplayed(messageId);
				return false;
			}

			// Deserialize the original message - uses TypeResolver for AOT compatibility.
			// EXPLICIT allowAssemblyScan: false — a dead-letter message's stored type name is untrusted
			// input (THE gadget-chain vector), so it must never fall back to the unbounded assembly scan; only
			// registry-resolved types replay. Stated explicitly (not defaulted) to keep the secure posture visible
			// at the vector site and immune to a future default-flip (enforce-invariants-structurally).
			var messageType = TypeResolution.TypeResolver.ResolveType(deadLetterMessage.MessageType, allowAssemblyScan: false);
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

			await using var scope = _serviceProvider.CreateAsyncScope();
			var context = DispatchContextInitializer.CreateFromMetadata(metaDict);
			context.MessageId = deadLetterMessage.MessageId;
			context.Items["IsReplay"] = true;
			context.Items["OriginalDeadLetterId"] = deadLetterMessage.Id;

			// CLAIM BEFORE THE EFFECT, NEVER AFTER IT.
			//
			// The mark used to be written after a successful dispatch, which made it a RECORD of a replay
			// rather than a CLAIM on one. Two callers could both read an unmarked message, both dispatch it,
			// and the second mark would rewrite a flag that was already set -- so the message was delivered
			// twice and nothing recorded that. The early guard above closes the SEQUENTIAL case by reading
			// the flag, and no read can close the concurrent one: both reads see the same value.
			//
			// The claim is the store's own conditional write, whose outcome is the decision. Exactly one
			// caller receives Claimed; everyone else is told why not and stands down.
			//
			// It is taken HERE rather than at the top of the method on purpose: a message that cannot be
			// resolved, decoded or dispatched at all fails above without consuming its claim, so an
			// undecodable entry stays exactly as replayable as it was.
			var claim = await _deadLetterStore.MarkAsReplayedAsync(messageId, cancellationToken).ConfigureAwait(false);
			if (claim != DeadLetterReplayOutcome.Claimed)
			{
				LogDeadLetterReplayClaimRefused(messageId, claim.ToString());

				_ = (activity?.SetTag("replay.success", value: false));
				return false;
			}

			// FROM HERE THE CLAIM IS HELD, so every exit either keeps it (the replay succeeded) or releases
			// it. A claim kept after a failure would mark a message replayed that was never delivered, and
			// the row would be indistinguishable from one that was.
			try
			{
				// Dispatch the message
				var dispatcher = scope.ServiceProvider.GetRequiredService<IDispatcher>();
				var result = await dispatcher.DispatchAsync(dispatchMessage, context, cancellationToken)
					.ConfigureAwait(false);

				if (result is { Succeeded: true })
				{
					LogSuccessfullyReplayedDeadLetterMessage(messageId);

					_ = (activity?.SetTag("replay.success", value: true));
					return true;
				}

				LogFailedToReplayDeadLetterMessage(messageId, result?.ErrorMessage ?? "Unknown error");

				await ReleaseReplayClaimAsync(messageId).ConfigureAwait(false);

				_ = (activity?.SetTag("replay.success", value: false));
				return false;
			}
			catch
			{
				// Including cancellation. A caller that gave up is the case where releasing matters most:
				// the message was not delivered and must stay replayable. The outer handler logs.
				await ReleaseReplayClaimAsync(messageId).ConfigureAwait(false);
				throw;
			}
		}
		catch (Exception ex)
		{
			LogErrorReplayingDeadLetterMessage(messageId, ex);

			_ = (activity?.SetStatus(ActivityStatusCode.Error, ex.Message));
			return false;
		}
	}

	/// <summary>
	/// Returns a held replay claim so a replay that did not succeed can be attempted again.
	/// </summary>
	/// <param name="messageId">The message whose claim is being released.</param>
	/// <remarks>
	/// <para>
	/// <b>Deliberately does NOT take the caller's token.</b> A cancelled replay is exactly when the release
	/// is required, so passing a token that is already cancelled would skip the one write that keeps the
	/// message replayable. It gets its own small budget instead, so a wedged store cannot hang the caller.
	/// </para>
	/// <para>
	/// <b>And it never throws.</b> It runs on the failure path, so an exception raised here would replace
	/// the real reason the replay failed with a secondary one. A release that could not be written is
	/// logged, because the consequence -- one message marked replayed that was never delivered -- is a
	/// state an operator may need to correct by hand.
	/// </para>
	/// </remarks>
	private async Task ReleaseReplayClaimAsync(string messageId)
	{
		using var budget = new CancellationTokenSource(ClaimReleaseBudget);

		try
		{
			await _deadLetterStore.ReleaseReplayClaimAsync(messageId, budget.Token).ConfigureAwait(false);
		}
		catch (Exception ex)
		{
			LogDeadLetterReplayClaimNotReleased(messageId, ex);
		}
	}

	/// <inheritdoc />
	public async Task<PoisonMessageStatistics> GetStatisticsAsync(CancellationToken cancellationToken)
	{
		using var activity = _activitySource.StartActivity("GetPoisonStatistics");

		long totalCount = 0;
		if (_deadLetterStore is IDeadLetterStoreAdmin admin)
		{
			totalCount = await admin.GetCountAsync(cancellationToken).ConfigureAwait(false);
		}

		// Get recent messages for time window calculation
		var recentFilter = new DeadLetterFilter { FromDate = DateTimeOffset.UtcNow.Subtract(_options.Alerting.AlertTimeWindow), MaxResults = 1000 };

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
			TimeWindow = _options.Alerting.AlertTimeWindow,
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

		if (context.GetProcessingFeature()?.FirstAttemptTime is { } first)
		{
			firstAttemptTime = first;
		}
		else if (context.Items.TryGetValue("FirstAttemptTime", out var firstTime) && firstTime is DateTimeOffset legacyFirst)
		{
			firstAttemptTime = legacyFirst;
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
		foreach (var key in AllowedContextKeys)
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
	private static string SerializeException(Exception exception)
	{
		var exceptionData = new PoisonExceptionInfo
		{
			Type = exception.GetType().FullName,
			Message = exception.Message,
			StackTrace = exception.StackTrace,
			InnerException =
				exception.InnerException != null
					? new PoisonExceptionInfo
					{
						Type = exception.InnerException.GetType().FullName,
						Message = exception.InnerException.Message,
						StackTrace = exception.InnerException.StackTrace,
					}
					: null,
			Data = exception.Data.Count > 0
				? exception.Data.Cast<System.Collections.DictionaryEntry>()
					.ToDictionary(
						e => e.Key.ToString() ?? string.Empty,
						e => e.Value?.ToString(),
						StringComparer.Ordinal)
				: null,
		};

		return JsonSerializer.Serialize(exceptionData, Serialization.DispatchJsonContext.Default.PoisonExceptionInfo);
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

	[LoggerMessage(DeliveryEventId.PoisonReplayClaimRefused, LogLevel.Warning,
		"Dead letter message '{MessageId}' was not claimed for replay ({Outcome}); it was not dispatched. "
		+ "Another caller holds or completed this replay")]
	private partial void LogDeadLetterReplayClaimRefused(string messageId, string outcome);

	[LoggerMessage(DeliveryEventId.PoisonReplayClaimNotReleased, LogLevel.Error,
		"The replay claim on dead letter message '{MessageId}' could not be released after a replay that "
		+ "did not succeed; it remains marked replayed although it was not delivered")]
	private partial void LogDeadLetterReplayClaimNotReleased(string messageId, Exception exception);

	[LoggerMessage(DeliveryEventId.PoisonReplayAlreadyReplayed, LogLevel.Warning,
		"Dead letter message '{MessageId}' was already replayed; this replay was refused rather than "
		+ "dispatching it a second time")]
	private partial void LogDeadLetterMessageAlreadyReplayed(string messageId);

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
