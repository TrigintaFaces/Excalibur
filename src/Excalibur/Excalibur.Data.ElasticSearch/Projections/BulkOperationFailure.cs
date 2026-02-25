// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Data.ElasticSearch.Projections;

/// <summary>
/// Represents a single document failure in a bulk operation.
/// </summary>
public sealed class BulkOperationFailure
{
	/// <summary>
	/// Gets the document identifier.
	/// </summary>
	/// <value>
	/// The document identifier.
	/// </value>
	public required string DocumentId { get; init; }

	/// <summary>
	/// Gets the document that failed.
	/// </summary>
	/// <value>
	/// The document that failed.
	/// </value>
	public object? Document { get; init; }

	/// <summary>
	/// Gets the error message for this specific document.
	/// </summary>
	/// <value>
	/// The error message for this specific document.
	/// </value>
	public required string ErrorMessage { get; init; }

	/// <summary>
	/// Gets the error type or code.
	/// </summary>
	/// <value>
	/// The error type or code.
	/// </value>
	public string? ErrorType { get; init; }
}
