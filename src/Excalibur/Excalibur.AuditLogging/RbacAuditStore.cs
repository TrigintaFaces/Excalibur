// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.AuditLogging.Diagnostics;
using Excalibur.Compliance;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Excalibur.AuditLogging;

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
	private readonly IServiceScopeFactory _scopeFactory;
	private readonly ILogger<RbacAuditStore> _logger;

	/// <summary>
	/// Initializes a new instance of the <see cref="RbacAuditStore"/> class.
	/// </summary>
	/// <param name="innerStore">The underlying audit store to wrap.</param>
	/// <param name="scopeFactory">
	/// The factory this store opens a scope from on every operation, to resolve the caller's role, identity
	/// and meta-audit logger. Those three are per-caller state and are deliberately NOT constructor
	/// parameters: this store is registered with the lifetime of the store it wraps, which is a singleton,
	/// so anything held in a field here answers for one caller for the life of the process. Taking them by
	/// constructor made every meta-audit record name the first caller and made the role check -- an access
	/// control -- decide on that caller's role, and under scope validation it refused to start at all.
	/// </param>
	/// <param name="logger">The logger for diagnostic output.</param>
	/// <remarks>
	/// A provider that reads ambient state -- claims, an <c>IHttpContextAccessor</c>, an async-local -- is
	/// resolved correctly from the scope opened here, because that state flows with the call rather than
	/// with the container scope. A provider whose identity is instead written into the caller's DI scope by
	/// host middleware cannot be reached from a singleton at all, and reports its unattributed default here
	/// rather than another caller's identity.
	/// </remarks>
	public RbacAuditStore(
		IAuditStore innerStore,
		IServiceScopeFactory scopeFactory,
		ILogger<RbacAuditStore> logger)
	{
		_innerStore = innerStore ?? throw new ArgumentNullException(nameof(innerStore));
		_scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
	}

	/// <summary>
	/// Forwards capability resolution to the wrapped store so that optional capabilities — including
	/// durability (<see cref="IDurableAuditStore"/>) — remain discoverable through this decorator.
	/// </summary>
	/// <param name="serviceType"> The capability interface to resolve. </param>
	/// <returns> The capability from the wrapped store, or <see langword="null"/> when unavailable. </returns>
	/// <remarks>
	/// A decorator that did not forward would silently disable every capability of the store it wraps —
	/// a durable store behind RBAC would report as non-durable. This forward keeps the chain transparent.
	/// </remarks>
	object? IServiceProvider.GetService(Type serviceType)
	{
		ArgumentNullException.ThrowIfNull(serviceType);

		return _innerStore.GetService(serviceType);
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
		var role = await GetCurrentRoleAsync(cancellationToken).ConfigureAwait(false);
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

		var role = await GetCurrentRoleAsync(cancellationToken).ConfigureAwait(false);
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

		var role = await GetCurrentRoleAsync(cancellationToken).ConfigureAwait(false);
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
		var role = await GetCurrentRoleAsync(cancellationToken).ConfigureAwait(false);

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
		var role = await GetCurrentRoleAsync(cancellationToken).ConfigureAwait(false);
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

	/// <summary>
	/// Resolves the current caller's role from a scope opened for this operation.
	/// </summary>
	/// <remarks>
	/// The role provider is required: this is an access-control input, and a host that registered none must
	/// be refused rather than defaulted. Resolution is per operation so the decision belongs to the caller
	/// being checked.
	/// </remarks>
	private async Task<AuditLogRole> GetCurrentRoleAsync(CancellationToken cancellationToken)
	{
		await using var scope = _scopeFactory.CreateAsyncScope();

		return await scope.ServiceProvider
			.GetRequiredService<IAuditRoleProvider>()
			.GetCurrentRoleAsync(cancellationToken)
			.ConfigureAwait(false);
	}

	private async Task LogMetaAuditAsync(
		string action,
		AuditLogRole role,
		string details,
		CancellationToken cancellationToken)
	{
		await using var scope = _scopeFactory.CreateAsyncScope();

		// Resolved OUTSIDE the try, so a host that never registered a meta-audit logger fails loudly.
		// Meta-auditing records who read the audit trail -- a segregation-of-duties control that must never
		// be silently disabled. The try below covers the WRITE failing, which must not fail the read.
		var metaAuditLogger = scope.ServiceProvider.GetRequiredService<IAuditLogger>();
		var actorProvider = scope.ServiceProvider.GetService<IAuditActorProvider>();

		try
		{
			var actorId = actorProvider is not null
				? await actorProvider.GetCurrentActorIdAsync(cancellationToken).ConfigureAwait(false)
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

			_ = await metaAuditLogger.LogAsync(metaEvent, cancellationToken).ConfigureAwait(false);
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
