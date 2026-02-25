// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Data.ElasticSearch.Projections;

/// <summary>
/// Provides context information about bulk operation errors.
/// </summary>
public sealed class BulkOperationErrorContext
{
	/// <summary>
	/// Gets the type of projection being processed in bulk.
	/// </summary>
	/// <value>
	/// The type of projection being processed in bulk.
	/// </value>
	public required string ProjectionType { get; init; }

	/// <summary>
	/// Gets the operation type (e.g., "BulkIndex", "BulkUpdate").
	/// </summary>
	/// <value>
	/// The operation type (e.g., "BulkIndex", "BulkUpdate").
	/// </value>
	public required string OperationType { get; init; }

	/// <summary>
	/// Gets the index name for the bulk operation.
	/// </summary>
	/// <value>
	/// The index name for the bulk operation.
	/// </value>
	public required string IndexName { get; init; }

	/// <summary>
	/// Gets the total number of documents in the bulk operation.
	/// </summary>
	/// <value>
	/// The total number of documents in the bulk operation.
	/// </value>
	public required int TotalDocuments { get; init; }

	/// <summary>
	/// Gets the number of successfully processed documents.
	/// </summary>
	/// <value>
	/// The number of successfully processed documents.
	/// </value>
	public required int SuccessfulDocuments { get; init; }

	/// <summary>
	/// Gets the collection of failed document operations.
	/// </summary>
	/// <value>
	/// The collection of failed document operations.
	/// </value>
	public required IReadOnlyList<BulkOperationFailure> Failures { get; init; }

	/// <summary>
	/// Gets additional context metadata.
	/// </summary>
	/// <value>
	/// Additional context metadata.
	/// </value>
	public IDictionary<string, object>? Metadata { get; init; }
}
