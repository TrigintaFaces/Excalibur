// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Data.ElasticSearch.Projections;

/// <summary>
/// Configuration settings for projection error handling.
/// </summary>
public sealed class ProjectionErrorHandlingOptions
{
	/// <summary>
	/// Gets a value indicating whether to store error records for analysis.
	/// </summary>
	/// <value>
	/// A value indicating whether to store error records for analysis.
	/// </value>
	public bool StoreErrors { get; init; } = true;

	/// <summary>
	/// Gets a value indicating whether to log detailed error information.
	/// </summary>
	/// <value>
	/// A value indicating whether to log detailed error information.
	/// </value>
	public bool LogDetailedErrors { get; init; } = true;

	/// <summary>
	/// Gets the index name for storing error records.
	/// </summary>
	/// <value>
	/// The index name for storing error records.
	/// </value>
	public string ErrorIndexName { get; init; } = "projection-errors";

	/// <summary>
	/// Gets the retention period for error records.
	/// </summary>
	/// <value>
	/// The retention period for error records.
	/// </value>
	public TimeSpan RetentionPeriod { get; init; } = TimeSpan.FromDays(30);
}
