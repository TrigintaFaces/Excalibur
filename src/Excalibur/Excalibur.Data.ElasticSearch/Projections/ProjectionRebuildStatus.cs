// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Data.ElasticSearch.Projections;

/// <summary>
/// Represents the current status of a rebuild operation.
/// </summary>
public sealed class ProjectionRebuildStatus
{
	/// <summary>
	/// Gets the operation identifier.
	/// </summary>
	/// <value>
	/// The operation identifier.
	/// </value>
	public required string OperationId { get; init; }

	/// <summary>
	/// Gets the current state of the rebuild.
	/// </summary>
	/// <value>
	/// The current state of the rebuild.
	/// </value>
	public required RebuildState State { get; init; }

	/// <summary>
	/// Gets the projection type being rebuilt.
	/// </summary>
	/// <value>
	/// The projection type being rebuilt.
	/// </value>
	public required string ProjectionType { get; init; }

	/// <summary>
	/// Gets the total number of documents to process.
	/// </summary>
	/// <value>
	/// The total number of documents to process.
	/// </value>
	public long TotalDocuments { get; init; }

	/// <summary>
	/// Gets the number of documents processed so far.
	/// </summary>
	/// <value>
	/// The number of documents processed so far.
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
	/// Gets the percentage of completion (0-100).
	/// </summary>
	/// <value>
	/// The percentage of completion (0-100).
	/// </value>
	public double PercentComplete { get; init; }

	/// <summary>
	/// Gets the timestamp when the rebuild started.
	/// </summary>
	/// <value>
	/// The timestamp when the rebuild started.
	/// </value>
	public required DateTimeOffset StartedAt { get; init; }

	/// <summary>
	/// Gets the timestamp when the rebuild completed, if applicable.
	/// </summary>
	/// <value>
	/// The timestamp when the rebuild completed, if applicable.
	/// </value>
	public DateTimeOffset? CompletedAt { get; init; }

	/// <summary>
	/// Gets the current processing rate (documents per second).
	/// </summary>
	/// <value>
	/// The current processing rate (documents per second).
	/// </value>
	public double DocumentsPerSecond { get; init; }

	/// <summary>
	/// Gets the estimated time remaining.
	/// </summary>
	/// <value>
	/// The estimated time remaining.
	/// </value>
	public TimeSpan? EstimatedTimeRemaining { get; init; }

	/// <summary>
	/// Gets the last error message, if any.
	/// </summary>
	/// <value>
	/// The last error message, if any.
	/// </value>
	public string? LastError { get; init; }

	/// <summary>
	/// Gets the current checkpoint or progress marker.
	/// </summary>
	/// <value>
	/// The current checkpoint or progress marker.
	/// </value>
	public string? Checkpoint { get; init; }
}
