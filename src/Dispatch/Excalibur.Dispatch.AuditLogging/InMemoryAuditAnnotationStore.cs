// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Concurrent;

using Excalibur.Dispatch.Compliance;

namespace Excalibur.Dispatch.AuditLogging;

/// <summary>
/// In-memory implementation of <see cref="IAuditAnnotationStore"/> for testing and development.
/// </summary>
/// <remarks>
/// <para>
/// This implementation is NOT suitable for production use:
/// - Annotations are not persisted across application restarts
/// - Memory grows unbounded
/// - No multi-instance support
/// </para>
/// <para>For production, use a persistent store implementation (SQL Server, etc.).</para>
/// </remarks>
internal sealed class InMemoryAuditAnnotationStore : IAuditAnnotationStore
{
	private readonly ConcurrentDictionary<string, List<AuditAnnotation>> _annotationsByEvent = new(StringComparer.Ordinal);
	private readonly IAuditActorProvider _actorProvider;
	private readonly TimeProvider _timeProvider;

	/// <summary>
	/// Initializes a new instance of the <see cref="InMemoryAuditAnnotationStore"/> class.
	/// </summary>
	/// <param name="actorProvider">Provider for the current actor identity.</param>
	/// <param name="timeProvider">Provider for timestamps.</param>
	public InMemoryAuditAnnotationStore(IAuditActorProvider actorProvider, TimeProvider timeProvider)
	{
		_actorProvider = actorProvider ?? throw new ArgumentNullException(nameof(actorProvider));
		_timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
	}

	/// <inheritdoc />
	public async Task TagAsync(string eventId, IReadOnlyList<string> tags, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(eventId);
		ArgumentNullException.ThrowIfNull(tags);
		cancellationToken.ThrowIfCancellationRequested();

		var actorId = await _actorProvider.GetCurrentActorIdAsync(cancellationToken).ConfigureAwait(false);
		var now = _timeProvider.GetUtcNow();
		var annotations = _annotationsByEvent.GetOrAdd(eventId, _ => []);

		lock (annotations)
		{
			var existingTags = annotations
				.Where(a => a.Type == AuditAnnotationType.Tag)
				.Select(a => a.Content)
				.ToHashSet(StringComparer.Ordinal);

			foreach (var tag in tags)
			{
				if (string.IsNullOrWhiteSpace(tag) || existingTags.Contains(tag))
				{
					continue;
				}

				annotations.Add(new AuditAnnotation
				{
					Id = Guid.NewGuid().ToString("N"),
					EventId = eventId,
					Type = AuditAnnotationType.Tag,
					Content = tag,
					ActorId = actorId,
					CreatedAt = now,
					Visibility = AuditAnnotationVisibility.Shared
				});

				existingTags.Add(tag);
			}
		}
	}

	/// <inheritdoc />
	public async Task BookmarkAsync(string eventId, string? label, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(eventId);
		cancellationToken.ThrowIfCancellationRequested();

		var actorId = await _actorProvider.GetCurrentActorIdAsync(cancellationToken).ConfigureAwait(false);
		var now = _timeProvider.GetUtcNow();
		var annotations = _annotationsByEvent.GetOrAdd(eventId, _ => []);

		lock (annotations)
		{
			// Remove existing bookmark for this actor (replace semantics)
			annotations.RemoveAll(a =>
				a.Type == AuditAnnotationType.Bookmark &&
				string.Equals(a.ActorId, actorId, StringComparison.Ordinal));

			annotations.Add(new AuditAnnotation
			{
				Id = Guid.NewGuid().ToString("N"),
				EventId = eventId,
				Type = AuditAnnotationType.Bookmark,
				Content = label ?? string.Empty,
				ActorId = actorId,
				CreatedAt = now,
				Visibility = AuditAnnotationVisibility.Personal
			});
		}
	}

	/// <inheritdoc />
	public async Task RemoveBookmarkAsync(string eventId, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(eventId);
		cancellationToken.ThrowIfCancellationRequested();

		var actorId = await _actorProvider.GetCurrentActorIdAsync(cancellationToken).ConfigureAwait(false);

		if (!_annotationsByEvent.TryGetValue(eventId, out var annotations))
		{
			return;
		}

		lock (annotations)
		{
			annotations.RemoveAll(a =>
				a.Type == AuditAnnotationType.Bookmark &&
				string.Equals(a.ActorId, actorId, StringComparison.Ordinal));
		}
	}

	/// <inheritdoc />
	public async Task<AuditAnnotationId> AnnotateAsync(string eventId, string note, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(eventId);
		ArgumentException.ThrowIfNullOrWhiteSpace(note);
		cancellationToken.ThrowIfCancellationRequested();

		var actorId = await _actorProvider.GetCurrentActorIdAsync(cancellationToken).ConfigureAwait(false);
		var now = _timeProvider.GetUtcNow();
		var id = Guid.NewGuid().ToString("N");
		var annotations = _annotationsByEvent.GetOrAdd(eventId, _ => []);

		lock (annotations)
		{
			annotations.Add(new AuditAnnotation
			{
				Id = id,
				EventId = eventId,
				Type = AuditAnnotationType.Note,
				Content = note,
				ActorId = actorId,
				CreatedAt = now,
				Visibility = AuditAnnotationVisibility.Shared
			});
		}

		return new AuditAnnotationId(id);
	}

	/// <inheritdoc />
	public Task<AuditAnnotations> GetAnnotationsAsync(string eventId, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(eventId);
		cancellationToken.ThrowIfCancellationRequested();

		if (!_annotationsByEvent.TryGetValue(eventId, out var annotations))
		{
			return Task.FromResult(new AuditAnnotations(eventId, [], [], []));
		}

		List<string> tags;
		List<AuditAnnotation> bookmarks;
		List<AuditAnnotation> notes;

		lock (annotations)
		{
			tags = annotations
				.Where(a => a.Type == AuditAnnotationType.Tag)
				.Select(a => a.Content)
				.Distinct(StringComparer.Ordinal)
				.ToList();

			bookmarks = annotations
				.Where(a => a.Type == AuditAnnotationType.Bookmark)
				.ToList();

			notes = annotations
				.Where(a => a.Type == AuditAnnotationType.Note)
				.ToList();
		}

		return Task.FromResult(new AuditAnnotations(eventId, tags, bookmarks, notes));
	}

	/// <inheritdoc />
	public Task<IReadOnlyList<string>> QueryByAnnotationAsync(AuditAnnotationQuery query, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(query);
		cancellationToken.ThrowIfCancellationRequested();

		var matchingEventIds = new List<string>();

		foreach (var (eventId, annotations) in _annotationsByEvent)
		{
			List<AuditAnnotation> snapshot;
			lock (annotations)
			{
				snapshot = [.. annotations];
			}

			if (!MatchesQuery(snapshot, query))
			{
				continue;
			}

			matchingEventIds.Add(eventId);
		}

		IReadOnlyList<string> result = matchingEventIds
			.Skip(query.Skip)
			.Take(query.MaxResults)
			.ToList();

		return Task.FromResult(result);
	}

	/// <summary>
	/// Clears all annotations from the store. For testing purposes only.
	/// </summary>
	internal void Clear()
	{
		_annotationsByEvent.Clear();
	}

	/// <summary>
	/// Gets the total count of annotated events in the store.
	/// </summary>
	internal int AnnotatedEventCount => _annotationsByEvent.Count;

	private static bool MatchesQuery(List<AuditAnnotation> annotations, AuditAnnotationQuery query)
	{
		if (query.Tags is { Count: > 0 })
		{
			var eventTags = annotations
				.Where(a => a.Type == AuditAnnotationType.Tag)
				.Select(a => a.Content)
				.ToHashSet(StringComparer.Ordinal);

			if (!query.Tags.Any(t => eventTags.Contains(t)))
			{
				return false;
			}
		}

		if (query.IsBookmarked == true)
		{
			if (!annotations.Any(a => a.Type == AuditAnnotationType.Bookmark))
			{
				return false;
			}
		}
		else if (query.IsBookmarked == false)
		{
			if (annotations.Any(a => a.Type == AuditAnnotationType.Bookmark))
			{
				return false;
			}
		}

		if (query.HasNotes == true)
		{
			if (!annotations.Any(a => a.Type == AuditAnnotationType.Note))
			{
				return false;
			}
		}
		else if (query.HasNotes == false)
		{
			if (annotations.Any(a => a.Type == AuditAnnotationType.Note))
			{
				return false;
			}
		}

		if (!string.IsNullOrEmpty(query.ActorId))
		{
			if (!annotations.Any(a => string.Equals(a.ActorId, query.ActorId, StringComparison.Ordinal)))
			{
				return false;
			}
		}

		if (query.Since.HasValue)
		{
			if (!annotations.Any(a => a.CreatedAt >= query.Since.Value))
			{
				return false;
			}
		}

		return true;
	}
}
