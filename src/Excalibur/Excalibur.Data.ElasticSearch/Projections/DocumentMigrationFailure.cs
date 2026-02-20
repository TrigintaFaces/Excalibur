// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Data.ElasticSearch.Projections;

/// <summary>
/// Represents a document migration failure.
/// </summary>
public sealed class DocumentMigrationFailure
{
	/// <summary>
	/// Gets the document identifier.
	/// </summary>
	/// <value>
	/// The document identifier.
	/// </value>
	public required string DocumentId { get; init; }

	/// <summary>
	/// Gets the failure reason.
	/// </summary>
	/// <value>
	/// The failure reason.
	/// </value>
	public required string Reason { get; init; }

	/// <summary>
	/// Gets the field that caused the failure.
	/// </summary>
	/// <value>
	/// The field that caused the failure.
	/// </value>
	public string? FailedField { get; init; }

	/// <summary>
	/// Gets the original value that failed.
	/// </summary>
	/// <value>
	/// The original value that failed.
	/// </value>
	public object? OriginalValue { get; init; }
}
