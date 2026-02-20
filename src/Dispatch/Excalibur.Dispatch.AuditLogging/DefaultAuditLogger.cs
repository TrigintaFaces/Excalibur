// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Dispatch.AuditLogging.Diagnostics;
using Excalibur.Dispatch.Compliance;

using Microsoft.Extensions.Logging;

namespace Excalibur.Dispatch.AuditLogging;

/// <summary>
/// Default implementation of <see cref="IAuditLogger"/> that delegates to an <see cref="IAuditStore"/>.
/// </summary>
/// <remarks>
/// <para>
/// The audit logger provides:
/// - Fire-and-forget safe logging (catches and logs errors)
/// - Hash chain integrity verification
/// - Event validation before storage
/// </para>
/// </remarks>
public sealed partial class DefaultAuditLogger : IAuditLogger
{
	private readonly IAuditStore _auditStore;
	private readonly ILogger<DefaultAuditLogger> _logger;

	/// <summary>
	/// Initializes a new instance of the <see cref="DefaultAuditLogger"/> class.
	/// </summary>
	/// <param name="auditStore">The audit store for persistent storage.</param>
	/// <param name="logger">The logger for diagnostic output.</param>
	public DefaultAuditLogger(IAuditStore auditStore, ILogger<DefaultAuditLogger> logger)
	{
		_auditStore = auditStore ?? throw new ArgumentNullException(nameof(auditStore));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
	}

	/// <inheritdoc />
	public async Task<AuditEventId> LogAsync(AuditEvent auditEvent, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(auditEvent);

		ValidateAuditEvent(auditEvent);

		try
		{
			var result = await _auditStore.StoreAsync(auditEvent, cancellationToken).ConfigureAwait(false);

			LogAuditEventStored(result.EventId, auditEvent.EventType, auditEvent.ActorId);

			return result;
		}
		catch (OperationCanceledException)
		{
			throw;
		}
		catch (Exception ex)
		{
			// Log the error but don't throw - audit logging should not break the main flow
			// In production, you might want to queue failed events for retry
			LogAuditEventStoreFailed(ex, auditEvent.EventId, auditEvent.EventType);

			// Return a failure indicator
			return new AuditEventId
			{
				EventId = auditEvent.EventId,
				EventHash = string.Empty,
				SequenceNumber = -1,
				RecordedAt = DateTimeOffset.UtcNow
			};
		}
	}

	/// <inheritdoc />
	public async Task<AuditIntegrityResult> VerifyIntegrityAsync(
		DateTimeOffset startDate,
		DateTimeOffset endDate,
		CancellationToken cancellationToken)
	{
		if (startDate > endDate)
		{
			throw new ArgumentException(
					Resources.DefaultAuditLogger_StartDateAfterEndDate,
					nameof(startDate));
		}

		try
		{
			LogIntegrityVerificationStarted(startDate, endDate);

			var result = await _auditStore.VerifyChainIntegrityAsync(startDate, endDate, cancellationToken)
				.ConfigureAwait(false);

			if (result.IsValid)
			{
				LogIntegrityVerificationCompleted(result.EventsVerified);
			}
			else
			{
				LogIntegrityVerificationFailed(
						result.FirstViolationEventId,
						result.ViolationDescription);
			}

			return result;
		}
		catch (Exception ex)
		{
			LogIntegrityVerificationError(ex, startDate, endDate);

			throw;
		}
	}

	private static void ValidateAuditEvent(AuditEvent auditEvent)
	{
		if (string.IsNullOrWhiteSpace(auditEvent.EventId))
		{
			throw new ArgumentException(Resources.DefaultAuditLogger_EventIdRequired, nameof(auditEvent));
		}

		if (string.IsNullOrWhiteSpace(auditEvent.Action))
		{
			throw new ArgumentException(Resources.DefaultAuditLogger_ActionRequired, nameof(auditEvent));
		}

		if (string.IsNullOrWhiteSpace(auditEvent.ActorId))
		{
			throw new ArgumentException(Resources.DefaultAuditLogger_ActorIdRequired, nameof(auditEvent));
		}

		if (auditEvent.Timestamp == default)
		{
			throw new ArgumentException(Resources.DefaultAuditLogger_TimestampRequired, nameof(auditEvent));
		}
	}

	[LoggerMessage(AuditLoggingEventId.AuditEventStored, LogLevel.Debug,
			"Logged audit event {EventId} of type {EventType} for actor {ActorId}")]
	private partial void LogAuditEventStored(string eventId, AuditEventType eventType, string actorId);

	[LoggerMessage(AuditLoggingEventId.AuditWriteFailed, LogLevel.Error,
			"Failed to log audit event {EventId} of type {EventType}")]
	private partial void LogAuditEventStoreFailed(Exception exception, string eventId, AuditEventType eventType);

	[LoggerMessage(AuditLoggingEventId.AuditIntegrityVerificationStarted, LogLevel.Information,
			"Starting audit integrity verification from {StartDate:O} to {EndDate:O}")]
	private partial void LogIntegrityVerificationStarted(DateTimeOffset startDate, DateTimeOffset endDate);

	[LoggerMessage(AuditLoggingEventId.AuditIntegrityVerificationCompleted, LogLevel.Information,
			"Audit integrity verification completed successfully. Verified {EventCount} events.")]
	private partial void LogIntegrityVerificationCompleted(long eventCount);

	[LoggerMessage(AuditLoggingEventId.AuditIntegrityVerificationFailed, LogLevel.Warning,
			"Audit integrity verification FAILED. First violation at event {EventId}: {Description}")]
	private partial void LogIntegrityVerificationFailed(string? eventId, string? description);

	[LoggerMessage(AuditLoggingEventId.AuditIntegrityVerificationError, LogLevel.Error,
			"Error during audit integrity verification from {StartDate:O} to {EndDate:O}")]
	private partial void LogIntegrityVerificationError(Exception exception, DateTimeOffset startDate, DateTimeOffset endDate);
}
