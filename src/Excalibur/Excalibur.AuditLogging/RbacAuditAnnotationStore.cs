// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.AuditLogging.Diagnostics;
using Excalibur.Compliance;

using Microsoft.Extensions.Logging;

namespace Excalibur.AuditLogging;

/// <summary>
/// Decorator that enforces role-based access control on audit annotation store operations.
/// </summary>
/// <remarks>
/// <para>
/// RBAC policy for annotations:
/// <list type="bullet">
/// <item>None/Developer: No access to annotations.</item>
/// <item>SecurityAnalyst: Can tag, bookmark, annotate. Can view shared annotations only.</item>
/// <item>ComplianceOfficer: Full annotation access, can view all annotations.</item>
/// <item>Administrator: Full annotation access, can view all annotations.</item>
/// </list>
/// </para>
/// <para>
/// Annotation creation is also logged as a meta-audit event when a meta-audit logger is available.
/// </para>
/// </remarks>
internal sealed partial class RbacAuditAnnotationStore : IAuditAnnotationStore
{
	private readonly IAuditAnnotationStore _innerStore;
	private readonly IAuditRoleProvider _roleProvider;
	private readonly IAuditActorProvider? _actorProvider;
	private readonly IAuditLogger? _metaAuditLogger;
	private readonly ILogger<RbacAuditAnnotationStore> _logger;

	/// <summary>
	/// Initializes a new instance of the <see cref="RbacAuditAnnotationStore"/> class.
	/// </summary>
	/// <param name="innerStore">The underlying annotation store to wrap.</param>
	/// <param name="roleProvider">The provider for the current user's role.</param>
	/// <param name="actorProvider">Optional provider for the current actor identity.</param>
	/// <param name="metaAuditLogger">Optional logger for meta-audit events.</param>
	/// <param name="logger">The logger for diagnostic output.</param>
	public RbacAuditAnnotationStore(
		IAuditAnnotationStore innerStore,
		IAuditRoleProvider roleProvider,
		IAuditActorProvider? actorProvider,
		IAuditLogger? metaAuditLogger,
		ILogger<RbacAuditAnnotationStore> logger)
	{
		_innerStore = innerStore ?? throw new ArgumentNullException(nameof(innerStore));
		_roleProvider = roleProvider ?? throw new ArgumentNullException(nameof(roleProvider));
		_actorProvider = actorProvider;
		_metaAuditLogger = metaAuditLogger;
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
	}

	/// <inheritdoc />
	public async Task TagAsync(string eventId, IReadOnlyList<string> tags, CancellationToken cancellationToken)
	{
		var role = await _roleProvider.GetCurrentRoleAsync(cancellationToken).ConfigureAwait(false);
		EnsureAnnotateAccess(role, "Tag");

		await _innerStore.TagAsync(eventId, tags, cancellationToken).ConfigureAwait(false);
		await LogMetaAuditAsync("Tag", role, $"EventId={eventId}, Tags={tags.Count}", cancellationToken).ConfigureAwait(false);
	}

	/// <inheritdoc />
	public async Task BookmarkAsync(string eventId, string? label, CancellationToken cancellationToken)
	{
		var role = await _roleProvider.GetCurrentRoleAsync(cancellationToken).ConfigureAwait(false);
		EnsureAnnotateAccess(role, "Bookmark");

		await _innerStore.BookmarkAsync(eventId, label, cancellationToken).ConfigureAwait(false);
		await LogMetaAuditAsync("Bookmark", role, $"EventId={eventId}", cancellationToken).ConfigureAwait(false);
	}

	/// <inheritdoc />
	public async Task RemoveBookmarkAsync(string eventId, CancellationToken cancellationToken)
	{
		var role = await _roleProvider.GetCurrentRoleAsync(cancellationToken).ConfigureAwait(false);
		EnsureAnnotateAccess(role, "RemoveBookmark");

		await _innerStore.RemoveBookmarkAsync(eventId, cancellationToken).ConfigureAwait(false);
		await LogMetaAuditAsync("RemoveBookmark", role, $"EventId={eventId}", cancellationToken).ConfigureAwait(false);
	}

	/// <inheritdoc />
	public async Task<AuditAnnotationId> AnnotateAsync(string eventId, string note, CancellationToken cancellationToken)
	{
		var role = await _roleProvider.GetCurrentRoleAsync(cancellationToken).ConfigureAwait(false);
		EnsureAnnotateAccess(role, "Annotate");

		var result = await _innerStore.AnnotateAsync(eventId, note, cancellationToken).ConfigureAwait(false);
		await LogMetaAuditAsync("Annotate", role, $"EventId={eventId}, AnnotationId={result.Value}", cancellationToken).ConfigureAwait(false);

		return result;
	}

	/// <inheritdoc />
	public async Task<AuditAnnotations> GetAnnotationsAsync(string eventId, CancellationToken cancellationToken)
	{
		var role = await _roleProvider.GetCurrentRoleAsync(cancellationToken).ConfigureAwait(false);
		EnsureReadAccess(role);

		var annotations = await _innerStore.GetAnnotationsAsync(eventId, cancellationToken).ConfigureAwait(false);

		// SecurityAnalyst can only see shared annotations
		if (role == AuditLogRole.SecurityAnalyst)
		{
			annotations = FilterToSharedAnnotations(annotations);
		}

		return annotations;
	}

	/// <inheritdoc />
	public async Task<IReadOnlyList<string>> QueryByAnnotationAsync(AuditAnnotationQuery query, CancellationToken cancellationToken)
	{
		var role = await _roleProvider.GetCurrentRoleAsync(cancellationToken).ConfigureAwait(false);
		EnsureReadAccess(role);

		return await _innerStore.QueryByAnnotationAsync(query, cancellationToken).ConfigureAwait(false);
	}

	private void EnsureAnnotateAccess(AuditLogRole role, string operation)
	{
		if (role < AuditLogRole.SecurityAnalyst)
		{
			LogAnnotationAccessDenied(role, operation);
			throw new UnauthorizedAccessException(
				Resources.RbacAuditAnnotationStore_AnnotatePermissionsRequired);
		}
	}

	private void EnsureReadAccess(AuditLogRole role)
	{
		if (role < AuditLogRole.SecurityAnalyst)
		{
			LogAnnotationReadAccessDenied(role);
			throw new UnauthorizedAccessException(
				Resources.RbacAuditAnnotationStore_ReadPermissionsRequired);
		}
	}

	private static AuditAnnotations FilterToSharedAnnotations(AuditAnnotations annotations)
	{
		return annotations with
		{
			Bookmarks = annotations.Bookmarks
				.Where(b => b.Visibility == AuditAnnotationVisibility.Shared)
				.ToList(),
			Notes = annotations.Notes
				.Where(n => n.Visibility == AuditAnnotationVisibility.Shared)
				.ToList()
		};
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
				EventType = AuditEventType.Administrative,
				Action = $"AuditAnnotation.{action}",
				Outcome = AuditOutcome.Success,
				Timestamp = DateTimeOffset.UtcNow,
				ActorId = actorId,
				ActorType = "AuditAnnotationAccess",
				ResourceType = "AuditAnnotation",
				Reason = $"Role={role}, {details}"
			};

			_ = await _metaAuditLogger.LogAsync(metaEvent, cancellationToken).ConfigureAwait(false);
		}
		catch (Exception ex)
		{
			LogMetaAuditFailed(ex, action);
		}
	}

	[LoggerMessage(AuditLoggingEventId.AnnotationAccessDenied, LogLevel.Warning,
		"User with role {Role} attempted annotation operation {Operation}")]
	private partial void LogAnnotationAccessDenied(AuditLogRole role, string operation);

	[LoggerMessage(AuditLoggingEventId.AnnotationReadAccessDenied, LogLevel.Warning,
		"User with role {Role} attempted to read annotations")]
	private partial void LogAnnotationReadAccessDenied(AuditLogRole role);

	[LoggerMessage(AuditLoggingEventId.AnnotationMetaAuditFailed, LogLevel.Warning,
		"Failed to log meta-audit event for annotation action {Action}")]
	private partial void LogMetaAuditFailed(Exception exception, string action);
}
