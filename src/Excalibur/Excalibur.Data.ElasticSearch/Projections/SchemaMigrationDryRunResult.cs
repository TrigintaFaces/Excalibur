// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Data.ElasticSearch.Projections;

/// <summary>
/// Represents the result of a migration dry run.
/// </summary>
public sealed class SchemaMigrationDryRunResult
{
	/// <summary>
	/// Gets a value indicating whether the dry run succeeded.
	/// </summary>
	/// <value>
	/// A value indicating whether the dry run succeeded.
	/// </value>
	public required bool Success { get; init; }

	/// <summary>
	/// Gets the number of documents tested.
	/// </summary>
	/// <value>
	/// The number of documents tested.
	/// </value>
	public required int DocumentsTested { get; init; }

	/// <summary>
	/// Gets the number of documents that would succeed.
	/// </summary>
	/// <value>
	/// The number of documents that would succeed.
	/// </value>
	public required int DocumentsSuccessful { get; init; }

	/// <summary>
	/// Gets the number of documents that would fail.
	/// </summary>
	/// <value>
	/// The number of documents that would fail.
	/// </value>
	public required int DocumentsFailed { get; init; }

	/// <summary>
	/// Gets sample failures for analysis.
	/// </summary>
	/// <value>
	/// Sample failures for analysis.
	/// </value>
	public IReadOnlyList<DocumentMigrationFailure>? SampleFailures { get; init; }

	/// <summary>
	/// Gets performance metrics from the dry run.
	/// </summary>
	/// <value>
	/// Performance metrics from the dry run.
	/// </value>
	public DryRunPerformanceMetrics? PerformanceMetrics { get; init; }
}
