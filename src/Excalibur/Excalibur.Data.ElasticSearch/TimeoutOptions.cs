// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Data.ElasticSearch;

/// <summary>
/// Configures timeout settings for different operation types.
/// </summary>
public sealed class TimeoutOptions
{
	/// <summary>
	/// Gets the timeout for search operations.
	/// </summary>
	/// <value> A <see cref="TimeSpan" /> representing the search timeout. Defaults to 30 seconds. </value>
	public TimeSpan SearchTimeout { get; init; } = TimeSpan.FromSeconds(30);

	/// <summary>
	/// Gets the timeout for index operations.
	/// </summary>
	/// <value> A <see cref="TimeSpan" /> representing the index timeout. Defaults to 60 seconds. </value>
	public TimeSpan IndexTimeout { get; init; } = TimeSpan.FromSeconds(60);

	/// <summary>
	/// Gets the timeout for bulk operations.
	/// </summary>
	/// <value> A <see cref="TimeSpan" /> representing the bulk timeout. Defaults to 120 seconds. </value>
	public TimeSpan BulkTimeout { get; init; } = TimeSpan.FromSeconds(120);

	/// <summary>
	/// Gets the timeout for delete operations.
	/// </summary>
	/// <value> A <see cref="TimeSpan" /> representing the delete timeout. Defaults to 30 seconds. </value>
	public TimeSpan DeleteTimeout { get; init; } = TimeSpan.FromSeconds(30);
}
