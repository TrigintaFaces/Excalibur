// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Data.ElasticSearch.Projections;

/// <summary>
/// Represents the result of initiating a projection rebuild.
/// </summary>
public sealed class ProjectionRebuildResult
{
	/// <summary>
	/// Gets the unique identifier for the rebuild operation.
	/// </summary>
	/// <value>
	/// The unique identifier for the rebuild operation.
	/// </value>
	public required string OperationId { get; init; }

	/// <summary>
	/// Gets a value indicating whether the rebuild was successfully started.
	/// </summary>
	/// <value>
	/// A value indicating whether the rebuild was successfully started.
	/// </value>
	public required bool Started { get; init; }

	/// <summary>
	/// Gets the message describing the result.
	/// </summary>
	/// <value>
	/// The message describing the result.
	/// </value>
	public string? Message { get; init; }

	/// <summary>
	/// Gets the timestamp when the rebuild was initiated.
	/// </summary>
	/// <value>
	/// The timestamp when the rebuild was initiated.
	/// </value>
	public required DateTimeOffset StartedAt { get; init; }

	/// <summary>
	/// Gets the estimated completion time, if available.
	/// </summary>
	/// <value>
	/// The estimated completion time, if available.
	/// </value>
	public DateTimeOffset? EstimatedCompletionTime { get; init; }
}
