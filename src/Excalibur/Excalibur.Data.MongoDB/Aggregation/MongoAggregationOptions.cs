// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Data.MongoDB.Aggregation;

/// <summary>
/// Configuration options for MongoDB aggregation pipeline execution.
/// </summary>
public sealed class MongoAggregationOptions
{
	/// <summary>
	/// Gets or sets a value indicating whether the aggregation is allowed to use disk storage for large datasets.
	/// </summary>
	/// <value><see langword="true"/> to allow disk use; otherwise, <see langword="false"/>. Defaults to <see langword="false"/>.</value>
	public bool AllowDiskUse { get; set; }

	/// <summary>
	/// Gets or sets the maximum time the aggregation is allowed to run.
	/// </summary>
	/// <value>The maximum execution time. Defaults to 30 seconds.</value>
	public TimeSpan MaxTime { get; set; } = TimeSpan.FromSeconds(30);

	/// <summary>
	/// Gets or sets the collation to use for string comparisons.
	/// </summary>
	/// <value>The collation locale string (e.g., "en", "de"), or <see langword="null"/> for the default collation.</value>
	public string? Collation { get; set; }

	/// <summary>
	/// Gets or sets the maximum batch size for the cursor.
	/// </summary>
	/// <value>The batch size. Defaults to 1000.</value>
	public int BatchSize { get; set; } = 1000;
}
