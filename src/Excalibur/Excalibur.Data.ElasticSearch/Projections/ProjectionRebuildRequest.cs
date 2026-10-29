// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.EventSourcing.Projections;

namespace Excalibur.Data.ElasticSearch.Projections;

/// <summary>
/// Represents a request to rebuild a projection.
/// </summary>
/// <remarks>
/// <para>
/// Build one through <see cref="ForIndexMove" /> or <see cref="ForEventRefold{TProjection}" /> rather
/// than by hand. Each factory states the CHANGE being made, and the <see cref="Mechanism" /> follows
/// from it; there is no knob that selects a mechanism independently of the data, so the two cannot
/// disagree.
/// </para>
/// <para>
/// That is deliberate, and it is the point of the pair. A shape change -- a new or renamed field --
/// cannot be served by copying documents that never carried it, so
/// <see cref="ForEventRefold{TProjection}" /> takes no source index: there is no argument in which to
/// name one, and the re-fold binding is not reachable from outside this package. The combination that
/// cannot work therefore has no expression.
/// </para>
/// </remarks>
public sealed class ProjectionRebuildRequest
{
	/// <summary>
	/// Gets the type of projection to rebuild.
	/// </summary>
	/// <value>
	/// The type of projection to rebuild.
	/// </value>
	/// <remarks>
	/// The TYPE rather than its name. A projection's store settings live in an options bucket keyed on the
	/// FULL type name, and its generation is declared on the type; a simple name carries neither, so a
	/// request holding one would resolve an unconfigured bucket and address a different index than the
	/// store writes to. No care inside the rebuild can recover a namespace a name has already dropped.
	/// </remarks>
	public required Type ProjectionType { get; init; }

	/// <summary>
	/// Gets the target index name for the rebuilt projection.
	/// </summary>
	/// <value>
	/// The target index name for the rebuilt projection.
	/// </value>
	public required string TargetIndexName { get; init; }

	/// <summary>
	/// Gets the source index an index-to-index rebuild copies from, or <c> null </c> for a re-fold.
	/// </summary>
	/// <value>
	/// The source index an index-to-index rebuild copies from, or <c> null </c> for a re-fold.
	/// </value>
	public string? SourceIndexName { get; init; }

	/// <summary>
	/// Gets the starting point for the rebuild (e.g., event sequence number or timestamp).
	/// </summary>
	/// <value>
	/// The starting point for the rebuild (e.g., event sequence number or timestamp).
	/// </value>
	public DateTime? FromTimestamp { get; init; }

	/// <summary>
	/// Gets the ending point for the rebuild.
	/// </summary>
	/// <value>
	/// The ending point for the rebuild.
	/// </value>
	public DateTime? ToTimestamp { get; init; }

	/// <summary>
	/// Gets the batch size for processing events during rebuild.
	/// </summary>
	/// <value>
	/// The batch size for processing events during rebuild.
	/// </value>
	public int BatchSize { get; init; } = 1000;

	/// <summary>
	/// Gets a value indicating whether to create a new index or use an existing one.
	/// </summary>
	/// <value>
	/// A value indicating whether to create a new index or use an existing one.
	/// </value>
	public bool CreateNewIndex { get; init; } = true;

	/// <summary>
	/// Gets a value indicating whether to use index aliasing for zero-downtime rebuilds.
	/// </summary>
	/// <value>
	/// A value indicating whether to use index aliasing for zero-downtime rebuilds.
	/// </value>
	public bool UseAliasing { get; init; } = true;

	/// <summary>
	/// Gets the maximum degree of parallelism for the rebuild operation.
	/// </summary>
	/// <value>
	/// The maximum degree of parallelism for the rebuild operation.
	/// </value>
	public int MaxDegreeOfParallelism { get; init; } = 4;

	/// <summary>
	/// Gets additional metadata for the rebuild operation.
	/// </summary>
	/// <value>
	/// Additional metadata for the rebuild operation.
	/// </value>
	public IDictionary<string, object>? Metadata { get; init; }

	/// <summary>
	/// Gets where this rebuild's destination documents come from.
	/// </summary>
	/// <value>
	/// <see cref="ProjectionRebuildMechanism.EventRefold" /> when the request was built by
	/// <see cref="ForEventRefold{TProjection}" />; otherwise
	/// <see cref="ProjectionRebuildMechanism.IndexCopy" />.
	/// </value>
	/// <remarks>
	/// Derived, not stored. There is no setter, so a request cannot claim one mechanism while carrying
	/// the other's data.
	/// </remarks>
	public ProjectionRebuildMechanism Mechanism => Refold is null
		? ProjectionRebuildMechanism.IndexCopy
		: ProjectionRebuildMechanism.EventRefold;

	/// <summary>
	/// Gets the bound re-fold, which replays the stream through the projection's current handlers and
	/// reports the state the replay finished in.
	/// </summary>
	/// <value>
	/// The bound re-fold, or <c> null </c> for an index copy.
	/// </value>
	/// <remarks>
	/// Internal on purpose: it is the ONLY way to reach the re-fold mechanism, and
	/// <see cref="ForEventRefold{TProjection}" /> -- which has no source-index parameter -- is its only
	/// producer. A consumer therefore has no syntax for "copy an index, but for a shape change". The
	/// generic is bound by the compiler at the consumer's call site, so nothing here needs reflection.
	/// </remarks>
	internal Func<IProjectionRebuildService, CancellationToken, Task<ProjectionRebuildState>>? Refold
	{
		get;
		init;
	}

	/// <summary>
	/// Describes a LIKE-FOR-LIKE move: the destination documents are the source documents, copied.
	/// </summary>
	/// <param name="projectionType"> The projection type being rebuilt. </param>
	/// <param name="sourceIndexName"> The index to copy documents from. </param>
	/// <param name="targetIndexName"> The destination index, or the alias to swap onto a new one. </param>
	/// <returns>
	/// A request whose <see cref="Mechanism" /> is <see cref="ProjectionRebuildMechanism.IndexCopy" />.
	/// </returns>
	/// <remarks>
	/// Use this when the documents do not change shape -- a reshard, a settings change, a move to a new
	/// index name. It is far cheaper than a re-fold and remains the right answer for those. It cannot
	/// populate a field the source documents lack; use <see cref="ForEventRefold{TProjection}" /> for that.
	/// </remarks>
	public static ProjectionRebuildRequest ForIndexMove(
		Type projectionType,
		string sourceIndexName,
		string targetIndexName)
	{
		ArgumentNullException.ThrowIfNull(projectionType);
		ArgumentException.ThrowIfNullOrWhiteSpace(sourceIndexName);
		ArgumentException.ThrowIfNullOrWhiteSpace(targetIndexName);

		return new ProjectionRebuildRequest
		{
			ProjectionType = projectionType,
			SourceIndexName = sourceIndexName,
			TargetIndexName = targetIndexName,
		};
	}

	/// <summary>
	/// Describes a SHAPE CHANGE: the destination documents are re-folded from the event stream, so a new
	/// generation may contain fields no stored document has carried.
	/// </summary>
	/// <typeparam name="TProjection"> The projection whose current handlers produce the new shape. </typeparam>
	/// <returns>
	/// A request whose <see cref="Mechanism" /> is <see cref="ProjectionRebuildMechanism.EventRefold" />.
	/// </returns>
	/// <remarks>
	/// <para>
	/// Takes nothing but the projection. There is no source index because a re-fold reads no documents,
	/// and no destination index because the re-fold writes through the projection's own store -- so the
	/// request cannot name an index that disagrees with where the documents actually land. Index creation
	/// and alias swapping are off for the same reason: an empty new index swapped in front of the store's
	/// own would hide the documents the re-fold just wrote.
	/// </para>
	/// <para>
	/// A re-fold replays the whole stream and is slow -- a production replay can run for hours. The
	/// operation reports its state as in-progress for that whole duration and reaches completed only
	/// once the replay has finished, so a caller can always tell "still folding" from "done".
	/// </para>
	/// </remarks>
	public static ProjectionRebuildRequest ForEventRefold<TProjection>()
		where TProjection : class, new()
	{
		return new ProjectionRebuildRequest
		{
			// The same key the rebuild service folds under, so the two name one projection. The TYPE, so
			// the manager can reach the options bucket the store registered -- keyed on the full name --
			// and the generation the type declares.
			ProjectionType = typeof(TProjection),

			// A placeholder the manager replaces with the index the projection's store really writes to,
			// resolved through the store's own convention. Required, and not a consumer's to get wrong.
			TargetIndexName = typeof(TProjection).Name,
			CreateNewIndex = false,
			UseAliasing = false,
			Refold = static async (service, cancellationToken) =>
			{
				// The rebuild releases this projection's subscription member BEFORE replaying, so the
				// rebuilt documents and the cursor describe one point in history. It also refuses
				// silently -- on no global-stream query and on no registered projection it records a
				// failure and returns rather than throwing -- so the state is read back and a rebuild
				// that did not complete is never reported as one that did.
				await service.RebuildAsync<TProjection>(cancellationToken).ConfigureAwait(false);
				var status = await service.GetStatusAsync<TProjection>(cancellationToken)
					.ConfigureAwait(false);
				return status.State;
			},
		};
	}
}
