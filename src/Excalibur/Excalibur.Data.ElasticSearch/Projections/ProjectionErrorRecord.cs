// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Data.ElasticSearch.Projections;

/// <summary>
/// Represents a recorded projection error for tracking and analysis.
/// </summary>
public sealed class ProjectionErrorRecord
{
	/// <summary>
	/// Gets the unique identifier for the error record.
	/// </summary>
	/// <value>
	/// The unique identifier for the error record.
	/// </value>
	public required string Id { get; init; }

	/// <summary>
	/// Gets the timestamp when the error occurred.
	/// </summary>
	/// <value>
	/// The timestamp when the error occurred.
	/// </value>
	public required DateTimeOffset Timestamp { get; init; }

	/// <summary>
	/// Gets the projection type that failed.
	/// </summary>
	/// <value>
	/// The projection type that failed.
	/// </value>
	public required string ProjectionType { get; init; }

	/// <summary>
	/// Gets the operation type that failed.
	/// </summary>
	/// <value>
	/// The operation type that failed.
	/// </value>
	public required string OperationType { get; init; }

	/// <summary>
	/// Gets the document identifier, if available.
	/// </summary>
	/// <value>
	/// The document identifier, if available.
	/// </value>
	public string? DocumentId { get; init; }

	/// <summary>
	/// Gets the index name where the projection was being stored.
	/// </summary>
	/// <value>
	/// The index name where the projection was being stored.
	/// </value>
	public required string IndexName { get; init; }

	/// <summary>
	/// Gets the error message.
	/// </summary>
	/// <value>
	/// The error message.
	/// </value>
	public required string ErrorMessage { get; init; }

	/// <summary>
	/// Gets the full exception details.
	/// </summary>
	/// <value>
	/// The full exception details.
	/// </value>
	public string? ExceptionDetails { get; init; }

	/// <summary>
	/// Gets the number of retry attempts made.
	/// </summary>
	/// <value>
	/// The number of retry attempts made.
	/// </value>
	public int AttemptCount { get; init; }

	/// <summary>
	/// Gets a value indicating whether the error has been resolved.
	/// </summary>
	/// <value>
	/// A value indicating whether the error has been resolved.
	/// </value>
	public bool IsResolved { get; init; }

	/// <summary>
	/// Gets the timestamp when the error was resolved, if applicable.
	/// </summary>
	/// <value>
	/// The timestamp when the error was resolved, if applicable.
	/// </value>
	public DateTime? ResolvedAt { get; init; }

	/// <summary>
	/// Gets additional context metadata.
	/// </summary>
	/// <value>
	/// Additional context metadata.
	/// </value>
	public IDictionary<string, object>? Metadata { get; init; }
}
