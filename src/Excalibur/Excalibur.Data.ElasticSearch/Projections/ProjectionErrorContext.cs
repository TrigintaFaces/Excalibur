// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Data.ElasticSearch.Projections;

/// <summary>
/// Provides context information about a projection operation error.
/// </summary>
public sealed class ProjectionErrorContext
{
	/// <summary>
	/// Gets the type of projection that failed.
	/// </summary>
	/// <value>
	/// The type of projection that failed.
	/// </value>
	public required string ProjectionType { get; init; }

	/// <summary>
	/// Gets the operation type that failed (e.g., "Index", "Update", "Delete").
	/// </summary>
	/// <value>
	/// The operation type that failed (e.g., "Index", "Update", "Delete").
	/// </value>
	public required string OperationType { get; init; }

	/// <summary>
	/// Gets the exception that caused the failure.
	/// </summary>
	/// <value>
	/// The exception that caused the failure.
	/// </value>
	public required Exception Exception { get; init; }

	/// <summary>
	/// Gets the document that failed to be projected.
	/// </summary>
	/// <value>
	/// The document that failed to be projected.
	/// </value>
	public object? Document { get; init; }

	/// <summary>
	/// Gets the identifier of the document, if available.
	/// </summary>
	/// <value>
	/// The identifier of the document, if available.
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
	/// Gets the number of retry attempts made.
	/// </summary>
	/// <value>
	/// The number of retry attempts made.
	/// </value>
	public int AttemptCount { get; init; }

	/// <summary>
	/// Gets additional context metadata.
	/// </summary>
	/// <value>
	/// Additional context metadata.
	/// </value>
	public IDictionary<string, object>? Metadata { get; init; }
}
