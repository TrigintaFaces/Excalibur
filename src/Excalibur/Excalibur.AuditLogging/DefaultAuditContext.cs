// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.Metrics;

using Excalibur.AuditLogging.Diagnostics;
using Excalibur.Compliance;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.AuditLogging;

/// <summary>
/// Default scoped implementation of <see cref="IAuditContext"/> that delegates
/// audit event creation to <see cref="IAuditLogger"/>.
/// </summary>
/// <remarks>
/// <para>
/// This class is registered as <b>scoped</b> in DI and initialized by
/// <c>AuditContextMiddleware</c> before handler execution. The middleware sets
/// correlation ID, actor ID, tenant ID, and message type name from the current
/// pipeline context.
/// </para>
/// <para>
/// When <see cref="AuditContextOptions.MaxAssertionsPerScope"/> is exceeded,
/// additional assertions are logged as a warning and silently dropped to avoid
/// breaking handler flow.
/// </para>
/// </remarks>
internal sealed partial class DefaultAuditContext : IAuditContext
{
	private static readonly Counter<long> AssertionsDroppedCounter =
		AuditLoggingTelemetryConstants.Meter.CreateCounter<long>(
			AuditLoggingTelemetryConstants.MetricNames.AssertionsDropped,
			unit: "{assertion}",
			description: "Total audit assertions dropped due to MaxAssertionsPerScope being exceeded.");

	private readonly IAuditLogger _auditLogger;
	private readonly TimeProvider _timeProvider;
	private readonly AuditContextOptions _options;
	private readonly ILogger<DefaultAuditContext> _logger;

	private readonly Dictionary<string, string> _metadata = new(StringComparer.Ordinal);
	private string? _correlationId;
	private string? _actorId;
	private string? _tenantId;
	private string? _messageTypeName;
	private string? _resourceId;
	private string? _resourceType;
	private int _assertionCount;

	/// <summary>
	/// Initializes a new instance of the <see cref="DefaultAuditContext"/> class.
	/// </summary>
	/// <param name="auditLogger">The audit logger to delegate event creation to.</param>
	/// <param name="timeProvider">The time provider for timestamps.</param>
	/// <param name="options">The audit context configuration options.</param>
	/// <param name="logger">The logger for diagnostic output.</param>
	public DefaultAuditContext(
		IAuditLogger auditLogger,
		TimeProvider timeProvider,
		IOptions<AuditContextOptions> options,
		ILogger<DefaultAuditContext> logger)
	{
		_auditLogger = auditLogger ?? throw new ArgumentNullException(nameof(auditLogger));
		_timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
		_options = options?.Value ?? throw new ArgumentNullException(nameof(options));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
	}

	/// <summary>
	/// Initializes the scope context from the pipeline. Called by AuditContextMiddleware.
	/// </summary>
	/// <param name="correlationId">The correlation ID from the message context.</param>
	/// <param name="actorId">The actor ID from the audit actor provider.</param>
	/// <param name="tenantId">The tenant ID, if multi-tenant.</param>
	/// <param name="messageTypeName">The message type name being processed.</param>
	internal void Initialize(string? correlationId, string? actorId, string? tenantId, string? messageTypeName)
	{
		_correlationId = correlationId;
		_actorId = actorId;
		_tenantId = tenantId;
		_messageTypeName = messageTypeName;
		_assertionCount = 0;
		_metadata.Clear();
		_resourceId = null;
		_resourceType = null;

		LogScopeInitialized(correlationId, messageTypeName);
	}

	/// <inheritdoc />
	public async Task<AuditEventId?> AssertAsync(
		bool condition,
		string message,
		AuditEventType eventType,
		CancellationToken cancellationToken)
	{
		if (!condition)
		{
			return null;
		}

		if (_assertionCount >= _options.MaxAssertionsPerScope)
		{
			AssertionsDroppedCounter.Add(1);
			LogMaxAssertionsExceeded(_options.MaxAssertionsPerScope, message);
			return null;
		}

		_assertionCount++;

		var auditEvent = BuildAuditEvent(message, eventType, AuditOutcome.Success, "Assert");

		try
		{
			var result = await _auditLogger.LogAsync(auditEvent, cancellationToken).ConfigureAwait(false);
			LogAssertionRecorded(result.EventId, message);
			return result;
		}
		catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
		{
			throw;
		}
		catch (Exception ex)
		{
			LogAuditContextLoggingFailed(ex, "AssertAsync");
			return null;
		}
	}

	/// <inheritdoc />
	public async Task<AuditEventId> ObserveAsync(
		string message,
		AuditEventType eventType,
		AuditOutcome outcome,
		CancellationToken cancellationToken)
	{
		if (_assertionCount >= _options.MaxAssertionsPerScope)
		{
			AssertionsDroppedCounter.Add(1);
			LogMaxAssertionsExceeded(_options.MaxAssertionsPerScope, message);

			// Return a sentinel value -- we don't throw per spec decision (log+drop)
			return new AuditEventId
			{
				EventId = string.Empty,
				EventHash = string.Empty,
				SequenceNumber = -1,
				RecordedAt = _timeProvider.GetUtcNow()
			};
		}

		_assertionCount++;

		var auditEvent = BuildAuditEvent(message, eventType, outcome, "Observe");

		try
		{
			var result = await _auditLogger.LogAsync(auditEvent, cancellationToken).ConfigureAwait(false);
			LogObservationRecorded(result.EventId, message);
			return result;
		}
		catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
		{
			throw;
		}
		catch (Exception ex)
		{
			LogAuditContextLoggingFailed(ex, "ObserveAsync");

			return new AuditEventId
			{
				EventId = string.Empty,
				EventHash = string.Empty,
				SequenceNumber = -1,
				RecordedAt = _timeProvider.GetUtcNow()
			};
		}
	}

	/// <inheritdoc />
	public IAuditContext WithMetadata(string key, string value)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(key);
		ArgumentNullException.ThrowIfNull(value);

		_metadata[key] = value;
		return this;
	}

	/// <inheritdoc />
	public IAuditContext ForResource(string resourceId, string resourceType)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(resourceId);
		ArgumentException.ThrowIfNullOrWhiteSpace(resourceType);

		_resourceId = resourceId;
		_resourceType = resourceType;
		return this;
	}

	private AuditEvent BuildAuditEvent(string message, AuditEventType eventType, AuditOutcome outcome, string action)
	{
		var metadata = new Dictionary<string, string>(_metadata, StringComparer.Ordinal);

		if (_options.IncludeMessageTypeName && !string.IsNullOrEmpty(_messageTypeName))
		{
			metadata["MessageType"] = _messageTypeName;
		}

		return new AuditEvent
		{
			EventId = $"ctx-{Guid.NewGuid():N}",
			EventType = eventType,
			Action = $"AuditContext.{action}",
			Outcome = outcome,
			Timestamp = _timeProvider.GetUtcNow(),
			ActorId = _actorId ?? "unknown",
			ActorType = "Handler",
			ResourceId = _resourceId,
			ResourceType = _resourceType,
			TenantId = _tenantId,
			CorrelationId = _correlationId,
			Reason = message,
			Metadata = metadata.Count > 0 ? metadata : null
		};
	}

	[LoggerMessage(AuditLoggingEventId.AuditContextScopeInitialized, LogLevel.Debug,
		"Audit context scope initialized. CorrelationId={CorrelationId}, MessageType={MessageTypeName}")]
	private partial void LogScopeInitialized(string? correlationId, string? messageTypeName);

	[LoggerMessage(AuditLoggingEventId.AuditAssertionRecorded, LogLevel.Debug,
		"Audit assertion recorded: EventId={EventId}, Message={Message}")]
	private partial void LogAssertionRecorded(string eventId, string message);

	[LoggerMessage(AuditLoggingEventId.AuditObservationRecorded, LogLevel.Debug,
		"Audit observation recorded: EventId={EventId}, Message={Message}")]
	private partial void LogObservationRecorded(string eventId, string message);

	[LoggerMessage(AuditLoggingEventId.MaxAssertionsExceeded, LogLevel.Warning,
		"Maximum assertions per scope ({MaxAssertions}) exceeded. Dropping assertion: {Message}")]
	private partial void LogMaxAssertionsExceeded(int maxAssertions, string message);

	[LoggerMessage(AuditLoggingEventId.AuditContextLoggingFailed, LogLevel.Warning,
		"Audit context logging failed during {Operation}")]
	private partial void LogAuditContextLoggingFailed(Exception exception, string operation);
}
