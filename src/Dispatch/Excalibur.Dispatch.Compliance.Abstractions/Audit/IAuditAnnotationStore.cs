// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Compliance;

/// <summary>
/// Stores annotations (tags, bookmarks, notes) linked to audit events.
/// </summary>
/// <remarks>
/// <para>
/// Annotations are additive — they never modify the original <see cref="AuditEvent"/>
/// or its hash chain. Each annotation is a separate record linked by event ID.
/// </para>
/// <para>
/// Implementations must be thread-safe. Duplicate tags on the same event are idempotent.
/// </para>
/// </remarks>
public interface IAuditAnnotationStore
{
	/// <summary>
	/// Adds tags to an audit event. Duplicate tags are idempotent.
	/// </summary>
	/// <param name="eventId">The audit event to tag.</param>
	/// <param name="tags">The tag labels to apply.</param>
	/// <param name="cancellationToken">A token to cancel the operation.</param>
	Task TagAsync(string eventId, IReadOnlyList<string> tags, CancellationToken cancellationToken);

	/// <summary>
	/// Bookmarks an event for the current actor with an optional label.
	/// </summary>
	/// <param name="eventId">The audit event to bookmark.</param>
	/// <param name="label">An optional label for the bookmark.</param>
	/// <param name="cancellationToken">A token to cancel the operation.</param>
	Task BookmarkAsync(string eventId, string? label, CancellationToken cancellationToken);

	/// <summary>
	/// Removes a bookmark from an event for the current actor.
	/// </summary>
	/// <param name="eventId">The audit event to remove the bookmark from.</param>
	/// <param name="cancellationToken">A token to cancel the operation.</param>
	Task RemoveBookmarkAsync(string eventId, CancellationToken cancellationToken);

	/// <summary>
	/// Adds a free-text note to an audit event.
	/// </summary>
	/// <param name="eventId">The audit event to annotate.</param>
	/// <param name="note">The note text.</param>
	/// <param name="cancellationToken">A token to cancel the operation.</param>
	/// <returns>The identifier of the created annotation.</returns>
	Task<AuditAnnotationId> AnnotateAsync(string eventId, string note, CancellationToken cancellationToken);

	/// <summary>
	/// Retrieves all annotations for a given audit event.
	/// </summary>
	/// <param name="eventId">The audit event to retrieve annotations for.</param>
	/// <param name="cancellationToken">A token to cancel the operation.</param>
	/// <returns>The aggregated annotations for the event.</returns>
	Task<AuditAnnotations> GetAnnotationsAsync(string eventId, CancellationToken cancellationToken);

	/// <summary>
	/// Queries events by annotation criteria (tags, bookmarked, has-notes).
	/// </summary>
	/// <param name="query">The annotation query parameters.</param>
	/// <param name="cancellationToken">A token to cancel the operation.</param>
	/// <returns>A list of matching audit event IDs.</returns>
	Task<IReadOnlyList<string>> QueryByAnnotationAsync(AuditAnnotationQuery query, CancellationToken cancellationToken);
}
