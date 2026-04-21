// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Data.ElasticSearch.Internal;

/// <summary>
/// Document shape for a tracked write-model event (projected into the
/// <c>-consistency-writes</c> index). Internal data-shape crossing the
/// projection-tracking seams.
/// </summary>
internal sealed class WriteEventDocument
{
	public string EventId { get; init; } = string.Empty;

	public string AggregateId { get; init; } = string.Empty;

	public string EventType { get; init; } = string.Empty;

	public DateTimeOffset WriteTimestamp { get; init; }
}

/// <summary>
/// Document shape for a tracked read-model projection (projected into the
/// <c>-consistency-reads</c> index).
/// </summary>
internal sealed class ReadEventDocument
{
	public string EventId { get; init; } = string.Empty;

	public string ProjectionType { get; init; } = string.Empty;

	public DateTimeOffset ReadTimestamp { get; init; }
}

/// <summary>
/// Document shape for a projection's latest-checkpoint marker (projected
/// into the <c>-consistency-checkpoints</c> index).
/// </summary>
internal sealed class ProjectionCheckpointDocument
{
	public string ProjectionType { get; init; } = string.Empty;

	public string? LastEventId { get; init; }

	public DateTimeOffset? LastProcessedAt { get; init; }

	public DateTimeOffset UpdatedAt { get; init; }
}

/// <summary>
/// Domain filter record for <see cref="IProjectionEventScan.SearchReadsAsync"/>.
/// All fields optional; the adapter translates non-null fields into SDK
/// query clauses. Consolidates the 4+ distinct read-search lambdas in
/// <see cref="Projections.EventualConsistencyTracker"/>.
/// </summary>
/// <param name="EventId">Exact term filter on the <c>eventId</c> field.</param>
/// <param name="EventIds">Terms filter on the <c>eventId</c> field (OR of values).</param>
/// <param name="ProjectionType">Exact term filter on the <c>projectionType</c> field.</param>
/// <param name="FromTimestamp">Inclusive lower bound on <c>readTimestamp</c>.</param>
/// <param name="ToTimestamp">Inclusive upper bound on <c>readTimestamp</c>.</param>
/// <param name="MaxResults">Result cap; adapter applies a sensible default if null.</param>
/// <param name="SortByReadTimestampDesc">If true, sort by <c>readTimestamp</c> descending.</param>
internal readonly record struct ReadEventSearch(
	string? EventId = null,
	IReadOnlyList<string>? EventIds = null,
	string? ProjectionType = null,
	DateTime? FromTimestamp = null,
	DateTime? ToTimestamp = null,
	int? MaxResults = null,
	bool SortByReadTimestampDesc = false);

/// <summary>
/// Count-filter discriminator for <see cref="IProjectionEventScan.GetDocumentCountAsync"/>.
/// Only one axis used by the consumer today; kept as an enum so the adapter
/// owns the query-descriptor lambda.
/// </summary>
internal enum ProjectionCountFilter
{
	/// <summary>Count all documents in the index (no filter).</summary>
	All,

	/// <summary>Count reads matching a specific <c>projectionType</c> (value supplied separately).</summary>
	ReadsByProjectionType,
}

/// <summary>
/// Discriminator for <see cref="IProjectionIndexProvisioning.CreateIndexAsync"/>.
/// The adapter selects the correct <c>TypeMapping</c> for each consistency
/// index kind so the consumer never constructs SDK mapping descriptors.
/// </summary>
internal enum ConsistencyIndexKind
{
	WriteEvents,
	ReadEvents,
	Checkpoints,
}
