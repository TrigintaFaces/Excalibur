// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Data.ElasticSearch.Projections;

/// <summary>
/// Represents a summary of a rebuild operation.
/// </summary>
public sealed class ProjectionRebuildSummary
{
	/// <summary>
	/// Gets the operation identifier.
	/// </summary>
	/// <value>
	/// The operation identifier.
	/// </value>
	public required string OperationId { get; init; }

	/// <summary>
	/// Gets the projection type.
	/// </summary>
	/// <value>
	/// The projection type.
	/// </value>
	public required string ProjectionType { get; init; }

	/// <summary>
	/// Gets the current state.
	/// </summary>
	/// <value>
	/// The current state.
	/// </value>
	public required RebuildState State { get; init; }

	/// <summary>
	/// Gets when the operation started.
	/// </summary>
	/// <value>
	/// When the operation started.
	/// </value>
	public required DateTimeOffset StartedAt { get; init; }

	/// <summary>
	/// Gets when the operation completed, if applicable.
	/// </summary>
	/// <value>
	/// When the operation completed, if applicable.
	/// </value>
	public DateTimeOffset? CompletedAt { get; init; }

	/// <summary>
	/// Gets the total documents processed.
	/// </summary>
	/// <value>
	/// The total documents processed.
	/// </value>
	public long ProcessedDocuments { get; init; }

	/// <summary>
	/// Gets the number of failed documents.
	/// </summary>
	/// <value>
	/// The number of failed documents.
	/// </value>
	public long FailedDocuments { get; init; }

	/// <summary>
	/// Gets the duration of the operation.
	/// </summary>
	/// <value>
	/// The duration of the operation.
	/// </value>
	public TimeSpan? Duration { get; init; }
}
