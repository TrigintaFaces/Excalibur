// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.AuditLogging.Diagnostics;
using Excalibur.Dispatch.Compliance;

using Microsoft.Extensions.Logging;

namespace Excalibur.Dispatch.AuditLogging;

/// <summary>
/// Decorator that enforces role-based access control on audit store operations.
/// </summary>
/// <remarks>
/// <para>
/// This decorator implements segregation of duties for audit log access:
/// - None/Developer: No access
/// - SecurityAnalyst: Security events only
/// - ComplianceOfficer: All events, read-only
/// - Administrator: Full access including export
/// </para>
/// <para>
/// This decorator also implements meta-auditing - logging who accessed the audit logs.
/// </para>
/// </remarks>
public sealed partial class RbacAuditStore : IAuditStore
{
	/// <summary>
	/// Event types accessible to SecurityAnalyst role.
	/// </summary>
	private static readonly AuditEventType[] SecurityEventTypes =
	[
		AuditEventType.Authentication,
		AuditEventType.Authorization,
		AuditEventType.Security
	];

	private readonly IAuditStore _innerStore;
	private readonly IAuditRoleProvider _roleProvider;
	private readonly IAuditActorProvider? _actorProvider;
	private readonly IAuditLogger? _metaAuditLogger;
	private readonly ILogger<RbacAuditStore> _logger;

	/// <summary>
	/// Initializes a new instance of the <see cref="RbacAuditStore"/> class.
	/// </summary>
	/// <param name="innerStore">The underlying audit store to wrap.</param>
	/// <param name="roleProvider">The provider for the current user's role.</param>
	/// <param name="logger">The logger for diagnostic output.</param>
	/// <param name="actorProvider">Optional provider for the current actor identity. When null, the role name is used.</param>
	/// <param name="metaAuditLogger">Optional logger for meta-audit events (audit access logging).</param>
	public RbacAuditStore(
		IAuditStore innerStore,
		IAuditRoleProvider roleProvider,
		ILogger<RbacAuditStore> logger,
		IAuditActorProvider? actorProvider = null,
		IAuditLogger? metaAuditLogger = null)
	{
		_innerStore = innerStore ?? throw new ArgumentNullException(nameof(innerStore));
		_roleProvider = roleProvider ?? throw new ArgumentNullException(nameof(roleProvider));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
		_actorProvider = actorProvider;
		_metaAuditLogger = metaAuditLogger;
	}

	/// <inheritdoc />
	/// <remarks>
	/// Store operations are always allowed - writing audit events should not be blocked by RBAC.
	/// </remarks>
	public Task<AuditEventId> StoreAsync(AuditEvent auditEvent, CancellationToken cancellationToken)
	{
		// Storing audit events is always allowed - RBAC only applies to read operations
		return _innerStore.StoreAsync(auditEvent, cancellationToken);
	}

	/// <inheritdoc />
	/// <exception cref="UnauthorizedAccessException">Thrown when the current user lacks permission.</exception>
	public async Task<AuditEvent?> GetByIdAsync(string eventId, CancellationToken cancellationToken)
	{
		var role = await _roleProvider.GetCurrentRoleAsync(cancellationToken).ConfigureAwait(false);
		EnsureReadAccess(role);

		var auditEvent = await _innerStore.GetByIdAsync(eventId, cancellationToken).ConfigureAwait(false);

		// Filter based on role if event exists
		if (auditEvent is not null && !CanAccessEvent(auditEvent, role))
		{
			LogAuditLogAccessDenied(role, eventId, auditEvent.EventType);
			return null;
		}

		await LogMetaAuditAsync("GetById", role, eventId, cancellationToken).ConfigureAwait(false);
		return auditEvent;
	}

	/// <inheritdoc />
	/// <exception cref="UnauthorizedAccessException">Thrown when the current user lacks permission.</exception>
	public async Task<IReadOnlyList<AuditEvent>> QueryAsync(AuditQuery query, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(query);

		var role = await _roleProvider.GetCurrentRoleAsync(cancellationToken).ConfigureAwait(false);
		EnsureReadAccess(role);

		var filteredQuery = ApplyRoleFilters(query, role);
		var results = await _innerStore.QueryAsync(filteredQuery, cancellationToken).ConfigureAwait(false);

		await LogMetaAuditAsync("Query", role, $"ResultCount={results.Count}", cancellationToken).ConfigureAwait(false);

		return results;
	}

	/// <inheritdoc />
	/// <exception cref="UnauthorizedAccessException">Thrown when the current user lacks permission.</exception>
	public async Task<long> CountAsync(AuditQuery query, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(query);

		var role = await _roleProvider.GetCurrentRoleAsync(cancellationToken).ConfigureAwait(false);
		EnsureReadAccess(role);

		var filteredQuery = ApplyRoleFilters(query, role);
		return await _innerStore.CountAsync(filteredQuery, cancellationToken).ConfigureAwait(false);
	}

	/// <inheritdoc />
	/// <exception cref="UnauthorizedAccessException">Thrown when the current user lacks permission.</exception>
	public async Task<AuditIntegrityResult> VerifyChainIntegrityAsync(
		DateTimeOffset startDate,
		DateTimeOffset endDate,
		CancellationToken cancellationToken)
	{
		var role = await _roleProvider.GetCurrentRoleAsync(cancellationToken).ConfigureAwait(false);

		// Only ComplianceOfficer and Administrator can verify integrity
		if (role < AuditLogRole.ComplianceOfficer)
		{
			LogIntegrityVerificationAccessDenied(role);
			throw new UnauthorizedAccessException(
				Resources.RbacAuditStore_IntegrityPermissionsRequired);
		}

		await LogMetaAuditAsync("VerifyIntegrity", role, $"{startDate:O} to {endDate:O}", cancellationToken)
			.ConfigureAwait(false);

		return await _innerStore.VerifyChainIntegrityAsync(startDate, endDate, cancellationToken)
			.ConfigureAwait(false);
	}

	/// <inheritdoc />
	/// <exception cref="UnauthorizedAccessException">Thrown when the current user lacks permission.</exception>
	public async Task<AuditEvent?> GetLastEventAsync(string? tenantId, CancellationToken cancellationToken)
	{
		var role = await _roleProvider.GetCurrentRoleAsync(cancellationToken).ConfigureAwait(false);
		EnsureReadAccess(role);

		return await _innerStore.GetLastEventAsync(tenantId, cancellationToken).ConfigureAwait(false);
	}

	private static void EnsureReadAccess(AuditLogRole role)
	{
		if (role is AuditLogRole.None or AuditLogRole.Developer)
		{
			throw new UnauthorizedAccessException(
				Resources.RbacAuditStore_ReadPermissionsRequired);
		}
	}

	private static bool CanAccessEvent(AuditEvent auditEvent, AuditLogRole role)
	{
		// ComplianceOfficer and Administrator can access all events
		if (role >= AuditLogRole.ComplianceOfficer)
		{
			return true;
		}

		// SecurityAnalyst can only access security-related events
		if (role == AuditLogRole.SecurityAnalyst)
		{
			return SecurityEventTypes.Contains(auditEvent.EventType);
		}

		return false;
	}

	private static AuditQuery ApplyRoleFilters(AuditQuery query, AuditLogRole role)
	{
		// ComplianceOfficer and Administrator see all events
		if (role >= AuditLogRole.ComplianceOfficer)
		{
			return query;
		}

		// SecurityAnalyst only sees security events
		if (role == AuditLogRole.SecurityAnalyst)
		{
			// If query already specifies event types, intersect with allowed types
			if (query.EventTypes is { Count: > 0 })
			{
				var intersection = query.EventTypes
					.Where(t => SecurityEventTypes.Contains(t))
					.ToList();

				return query with { EventTypes = intersection };
			}

			// Otherwise, restrict to security event types
			return query with { EventTypes = SecurityEventTypes };
		}

		return query;
	}

	private async Task LogMetaAuditAsync(
		string action,
		AuditLogRole role,
		string details,
		CancellationToken cancellationToken)
	{
		if (_metaAuditLogger is null)
		{
			return;
		}

		try
		{
			var actorId = _actorProvider is not null
				? await _actorProvider.GetCurrentActorIdAsync(cancellationToken).ConfigureAwait(false)
				: $"role:{role}";

			var metaEvent = new AuditEvent
			{
				EventId = $"meta-{Guid.NewGuid():N}",
				EventType = AuditEventType.DataAccess,
				Action = $"AuditLog.{action}",
				Outcome = AuditOutcome.Success,
				Timestamp = DateTimeOffset.UtcNow,
				ActorId = actorId,
				ActorType = "AuditLogAccess",
				ResourceType = "AuditLog",
				Reason = $"Role={role}, {details}"
			};

			_ = await _metaAuditLogger.LogAsync(metaEvent, cancellationToken).ConfigureAwait(false);
		}
		catch (Exception ex)
		{
			// Meta-audit failures should not block the main operation
			LogMetaAuditLogFailed(ex, action);
		}
	}

	[LoggerMessage(AuditLoggingEventId.AuditLogAccessDenied, LogLevel.Warning,
		"User with role {Role} attempted to access event {EventId} of type {EventType}")]
	private partial void LogAuditLogAccessDenied(AuditLogRole role, string eventId, AuditEventType eventType);

	[LoggerMessage(AuditLoggingEventId.AuditIntegrityVerificationAccessDenied, LogLevel.Warning,
		"User with role {Role} attempted to verify audit integrity")]
	private partial void LogIntegrityVerificationAccessDenied(AuditLogRole role);

	[LoggerMessage(AuditLoggingEventId.MetaAuditLoggingFailed, LogLevel.Warning,
		"Failed to log meta-audit event for action {Action}")]
	private partial void LogMetaAuditLogFailed(Exception exception, string action);
}
