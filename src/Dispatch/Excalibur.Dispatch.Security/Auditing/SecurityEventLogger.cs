// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics.CodeAnalysis;
using System.Threading.Channels;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Security.Diagnostics;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Excalibur.Dispatch.Security;

/// <summary>
/// Provides centralized security event logging for audit trails and compliance. All security-related events are logged through this service
/// for consistent tracking.
/// </summary>
public sealed partial class SecurityEventLogger : ISecurityEventLogger, IHostedService, IDisposable
{
	private readonly ILogger<SecurityEventLogger> _logger;
	private readonly ISecurityEventStore _eventStore;
	private readonly Channel<SecurityEvent> _eventChannel;
	private readonly CancellationTokenSource _shutdownTokenSource;
	private Task? _processingTask;

	/// <summary>
	/// Initializes a new instance of the <see cref="SecurityEventLogger" /> class.
	/// </summary>
	/// <param name="logger">The logger used to emit diagnostics.</param>
	/// <param name="eventStore">The store that persists security events.</param>
	public SecurityEventLogger(
		ILogger<SecurityEventLogger> logger,
		ISecurityEventStore eventStore)
	{
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
		_eventStore = eventStore ?? throw new ArgumentNullException(nameof(eventStore));

		// Create unbounded channel for high-throughput event logging
		_eventChannel = Channel.CreateUnbounded<SecurityEvent>(new UnboundedChannelOptions { SingleReader = true, SingleWriter = false });

		_shutdownTokenSource = new CancellationTokenSource();
	}

	/// <summary>
	/// Logs a security event asynchronously.
	/// </summary>
	/// <param name="eventType">The type of security event being logged.</param>
	/// <param name="description">The description associated with the security event.</param>
	/// <param name="severity">The severity level for the security event.</param>
	/// <param name="context">The contextual message metadata for the security event.</param>
	/// <param name="cancellationToken">A token that is observed for cancellation.</param>
	/// <returns>A task that completes when the event is queued.</returns>
	public Task LogSecurityEventAsync(
		SecurityEventType eventType,
		string description,
		SecuritySeverity severity,
		CancellationToken cancellationToken,
		IMessageContext? context = null)
	{
		var securityEvent = new SecurityEvent
		{
			Id = Guid.NewGuid(),
			Timestamp = DateTimeOffset.UtcNow,
			EventType = eventType,
			Description = description,
			Severity = severity,
			CorrelationId =
				context?.CorrelationId != null && Guid.TryParse(context.CorrelationId, out var correlationGuid)
					? correlationGuid
					: null,
			UserId = context?.Items?.TryGetValue("User:MessageId", out var userId) == true ? userId?.ToString() : null,
			SourceIp = context?.Items?.TryGetValue("Client:IP", out var ip) == true ? ip?.ToString() : null,
			UserAgent = context?.Items?.TryGetValue("Client:UserAgent", out var ua) == true ? ua?.ToString() : null,
			MessageType = context?.Items?.TryGetValue("Message:Type", out var msgType) == true ? msgType?.ToString() : null,
			AdditionalData = ExtractAdditionalData(context),
		};

		// Log to standard logger based on severity
		LogToStandardLogger(securityEvent);

		// Queue for persistent storage
		if (!_eventChannel.Writer.TryWrite(securityEvent))
		{
			// Channel is closed, log error
			LogQueueFailed(securityEvent.Id);
		}

		return Task.CompletedTask;
	}

	/// <summary>
	/// Starts the background processing of security events.
	/// </summary>
	/// <param name="cancellationToken">A token that is observed for cancellation.</param>
	/// <returns>A task that completes when startup is finished.</returns>
	public Task StartAsync(CancellationToken cancellationToken)
	{
		_processingTask = ProcessEventsAsync(_shutdownTokenSource.Token);
		LogStarted();
		return Task.CompletedTask;
	}

	/// <summary>
	/// Stops the background processing of security events.
	/// </summary>
	/// <param name="cancellationToken">A token that is observed for cancellation.</param>
	/// <returns>A task that completes when shutdown work is complete.</returns>
	public async Task StopAsync(CancellationToken cancellationToken)
	{
		LogStopping();

		// Signal no more events will be written — this allows ProcessEventsAsync
		// to drain remaining items and exit when the channel is fully consumed
		_ = _eventChannel.Writer.TryComplete();

		// Wait for processing to drain remaining events before cancelling
		if (_processingTask != null)
		{
			try
			{
				await _processingTask.WaitAsync(TimeSpan.FromSeconds(30), cancellationToken).ConfigureAwait(false);
			}
			catch (TimeoutException)
			{
				LogProcessingTimeout();
			}
			catch (OperationCanceledException)
			{
				// External cancellation requested
			}
		}

		// Cancel background processing as final cleanup
		await _shutdownTokenSource.CancelAsync().ConfigureAwait(false);

		LogStopped();
	}

	/// <summary>
	/// Disposes resources used by the security event logger.
	/// </summary>
	public void Dispose() => _shutdownTokenSource?.Dispose();

	private static Dictionary<string, object?> ExtractAdditionalData(IMessageContext? context)
	{
		var data = new Dictionary<string, object?>(StringComparer.Ordinal);

		if (context?.Items != null)
		{
			// Extract relevant security context
			foreach (var item in context.Items)
			{
				if (item.Key.StartsWith("Security:", StringComparison.OrdinalIgnoreCase) ||
					item.Key.StartsWith("Auth:", StringComparison.OrdinalIgnoreCase) ||
					item.Key.StartsWith("Validation:", StringComparison.OrdinalIgnoreCase))
				{
					data[item.Key] = item.Value;
				}
			}
		}

		return data;
	}

	// Source-generated logging methods
	[LoggerMessage(SecurityEventId.SecurityEventQueueFailed, LogLevel.Error,
		"Failed to queue security event {SecurityEventId} - channel closed")]
	private partial void LogQueueFailed(Guid securityEventId);

	[LoggerMessage(SecurityEventId.SecurityEventLoggerStarted, LogLevel.Information,
		"Security event logger started")]
	private partial void LogStarted();

	[LoggerMessage(SecurityEventId.SecurityEventLoggerStopping, LogLevel.Information,
		"Security event logger stopping")]
	private partial void LogStopping();

	[LoggerMessage(SecurityEventId.SecurityEventLoggerStopped, LogLevel.Information,
		"Security event logger stopped")]
	private partial void LogStopped();

	[LoggerMessage(SecurityEventId.SecurityEventProcessingTimeout, LogLevel.Warning,
		"Security event processing did not complete within timeout")]
	private partial void LogProcessingTimeout();

	[LoggerMessage(SecurityEventId.SecurityEventsStored, LogLevel.Debug,
		"Stored {Count} security events")]
	private partial void LogEventsStored(int count);

	[LoggerMessage(SecurityEventId.SecurityEventsStoreFailed, LogLevel.Error,
		"Failed to store {Count} security events")]
	private partial void LogStoreFailed(int count, Exception ex);

	[LoggerMessage(SecurityEventId.SecurityEventIndividualStoreFailed, LogLevel.Error,
		"Failed to store individual security event {SecurityEventId}")]
	private partial void LogIndividualStoreFailed(Guid securityEventId, Exception ex);

	[LoggerMessage(SecurityEventId.SecurityEventProcessingLoopFailed, LogLevel.Error,
		"Security event processing loop failed")]
	private partial void LogProcessingLoopFailed(Exception ex);

	[LoggerMessage(SecurityEventId.SecurityEventLoggedWithDetails, LogLevel.Information,
		"Security Event: {EventType} - {Description} [Severity: {Severity}, User: {UserId}, IP: {SourceIp}]")]
	private partial void LogSecurityEvent(SecurityEventType eventType, string description, SecuritySeverity severity,
		string userId, string sourceIp);

	private async Task ProcessEventsAsync(CancellationToken cancellationToken)
	{
		const int batchSize = 100;
		var batchTimeout = TimeSpan.FromSeconds(5);
		var events = new List<SecurityEvent>(batchSize);

		try
		{
			while (!cancellationToken.IsCancellationRequested)
			{
				events.Clear();

				using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
				timeoutCts.CancelAfter(batchTimeout);

				await CollectEventBatchAsync(events, batchSize, timeoutCts, cancellationToken).ConfigureAwait(false);

				if (events.Count > 0)
				{
					await StoreEventBatchAsync(events, timeoutCts).ConfigureAwait(false);
				}

				// Exit when channel writer has completed and all items have been consumed
				if (_eventChannel.Reader.Completion.IsCompleted)
				{
					break;
				}
			}
		}
		catch (Exception ex)
		{
			LogProcessingLoopFailed(ex);
		}
	}

	private async Task CollectEventBatchAsync(List<SecurityEvent> events, int batchSize, CancellationTokenSource timeoutCts, CancellationToken cancellationToken)
	{
		try
		{
			while (events.Count < batchSize &&
				   await _eventChannel.Reader.WaitToReadAsync(timeoutCts.Token).ConfigureAwait(false))
			{
				if (_eventChannel.Reader.TryRead(out var evt))
				{
					events.Add(evt);
				}
			}
		}
		catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
		{
			// Timeout reached, process what we have
		}
	}

	[UnconditionalSuppressMessage("Trimming", "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access", Justification = "ISecurityEventStore implementation controls serialization")]
	[UnconditionalSuppressMessage("AOT", "IL3050:Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality", Justification = "ISecurityEventStore implementation controls serialization")]
	private async Task StoreEventBatchAsync(List<SecurityEvent> events, CancellationTokenSource timeoutCts)
	{
		try
		{
			await _eventStore.StoreEventsAsync(events, timeoutCts.Token).ConfigureAwait(false);
			LogEventsStored(events.Count);
		}
		catch (Exception ex)
		{
			LogStoreFailed(events.Count, ex);
			await StoreEventsIndividuallyAsync(events, timeoutCts).ConfigureAwait(false);
		}
	}

	[UnconditionalSuppressMessage("Trimming", "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access", Justification = "ISecurityEventStore implementation controls serialization")]
	[UnconditionalSuppressMessage("AOT", "IL3050:Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality", Justification = "ISecurityEventStore implementation controls serialization")]
	private async Task StoreEventsIndividuallyAsync(List<SecurityEvent> events, CancellationTokenSource timeoutCts)
	{
		foreach (var evt in events)
		{
			try
			{
				await _eventStore.StoreEventsAsync(new[] { evt }, timeoutCts.Token).ConfigureAwait(false);
			}
			catch (Exception individualEx)
			{
				LogIndividualStoreFailed(evt.Id, individualEx);
			}
		}
	}

	private void LogToStandardLogger(SecurityEvent securityEvent)
	{
		// Use pre-compiled logging for security events with dynamic log level support
		_ = securityEvent.Severity switch
		{
			SecuritySeverity.Critical => LogLevel.Critical,
			SecuritySeverity.High => LogLevel.Error,
			SecuritySeverity.Medium => LogLevel.Warning,
			SecuritySeverity.Low => LogLevel.Information,
			_ => LogLevel.Information,
		};

		LogSecurityEvent(
			securityEvent.EventType,
			securityEvent.Description,
			securityEvent.Severity,
			securityEvent.UserId ?? "Unknown",
			securityEvent.SourceIp ?? "Unknown");
	}
}
