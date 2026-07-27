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
/// RBAC policy for annotations. Role decides whether you may read and write annotations at all;
/// <em>authorship</em> decides which of them you receive:
/// <list type="bullet">
/// <item>None/Developer: No access to annotations.</item>
/// <item>SecurityAnalyst: Can tag, bookmark, annotate.</item>
/// <item>ComplianceOfficer: Full annotation administration.</item>
/// <item>Administrator: Full annotation administration.</item>
/// </list>
/// </para>
/// <para>
/// Reads are scoped by authorship for <strong>every</strong> role: an actor receives shared annotations plus
/// their own, and no role bypasses that. Administration is the ability to manage the annotation log, not a
/// licence to read another actor's private notes — a higher role does not widen what is returned.
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

		// Applied for EVERY reader, not just one role. A higher role administers the audit log; it does not
		// acquire read access to other actors' private annotations by virtue of sorting higher in an enum.
		var currentActorId = await ResolveCurrentActorIdAsync(cancellationToken).ConfigureAwait(false);

		return FilterToReadableAnnotations(annotations, currentActorId);
	}

	/// <inheritdoc />
	public async Task<IReadOnlyList<string>> QueryByAnnotationAsync(AuditAnnotationQuery query, CancellationToken cancellationToken)
	{
		var role = await _roleProvider.GetCurrentRoleAsync(cancellationToken).ConfigureAwait(false);
		EnsureReadAccess(role);

		var candidates = await _innerStore.QueryByAnnotationAsync(query, cancellationToken).ConfigureAwait(false);

		// A query result is a read, and it discloses through membership rather than content: returning an
		// event id because it carries an annotation the caller may not read tells them that annotation exists.
		// GetAnnotationsAsync denies that same content one method away, so leaving this path unfiltered undoes
		// the denial. Each candidate is therefore re-checked through the SAME predicate the direct read uses —
		// one definition, so the two paths cannot drift apart.
		var currentActorId = await ResolveCurrentActorIdAsync(cancellationToken).ConfigureAwait(false);
		var visible = new List<string>(candidates.Count);

		foreach (var candidateEventId in candidates)
		{
			var annotations = await _innerStore.GetAnnotationsAsync(candidateEventId, cancellationToken)
				.ConfigureAwait(false);
			var readable = FilterToReadableAnnotations(annotations, currentActorId);

			if (readable.Tags.Count > 0 || readable.Bookmarks.Count > 0 || readable.Notes.Count > 0)
			{
				visible.Add(candidateEventId);
			}
		}

		return visible;
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

	/// <summary>
	/// Decides whether one annotation is readable by the current actor.
	/// </summary>
	/// <param name="visibility">The annotation's visibility.</param>
	/// <param name="annotationActorId">The actor who authored the annotation.</param>
	/// <param name="currentActorId">The actor performing the read, or <see langword="null"/> if unknown.</param>
	/// <returns><see langword="true"/> when the annotation may be returned.</returns>
	/// <remarks>
	/// <para>
	/// The axis is <strong>authorship</strong>, not rank: an annotation is readable when it is shared, or when
	/// the actor reading it is the actor who wrote it. Filtering on visibility alone gets both directions
	/// wrong at once — it hides an actor's own private notes from that actor, while handing every actor's
	/// private notes to anyone whose role happens to sort higher.
	/// </para>
	/// <para>
	/// Rank grants administration, not read of private content, so no role bypasses this. If a break-glass
	/// capability is ever required it belongs as its own named, audited operation rather than as a side
	/// effect of an enum comparison.
	/// </para>
	/// <para>
	/// When the current actor cannot be determined this returns shared annotations only: authorship cannot be
	/// proven, so it is not assumed.
	/// </para>
	/// </remarks>
	private static bool IsReadableBy(
		AuditAnnotationVisibility visibility,
		string? annotationActorId,
		string? currentActorId) =>
		visibility == AuditAnnotationVisibility.Shared
		|| (currentActorId is not null
			&& annotationActorId is not null
			&& string.Equals(annotationActorId, currentActorId, StringComparison.Ordinal));

	private static AuditAnnotations FilterToReadableAnnotations(AuditAnnotations annotations, string? currentActorId)
	{
		return annotations with
		{
			Bookmarks = annotations.Bookmarks
				.Where(b => IsReadableBy(b.Visibility, b.ActorId, currentActorId))
				.ToList(),
			Notes = annotations.Notes
				.Where(n => IsReadableBy(n.Visibility, n.ActorId, currentActorId))
				.ToList()
		};
	}

	/// <summary>
	/// Resolves the actor performing the current read, or <see langword="null"/> when no provider is
	/// registered. A null result narrows what is returned; it never widens it.
	/// </summary>
	private async Task<string?> ResolveCurrentActorIdAsync(CancellationToken cancellationToken)
	{
		if (_actorProvider is null)
		{
			return null;
		}

		return await _actorProvider.GetCurrentActorIdAsync(cancellationToken).ConfigureAwait(false);
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
