// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.EventSourcing.Abstractions;

/// <summary>
/// Options for projection queries including pagination and sorting.
/// </summary>
/// <param name="Skip">Number of records to skip (for pagination).</param>
/// <param name="Take">Maximum number of records to return (for pagination).</param>
/// <param name="OrderBy">Property name to order by.</param>
/// <param name="Descending">Whether to sort in descending order.</param>
/// <remarks>
/// <para>
/// Query options control how results are paginated and sorted. All parameters
/// are optional - pass <c>null</c> values to use provider defaults.
/// </para>
/// <para>
/// Providers translate these options to their native syntax (e.g., SQL OFFSET/FETCH,
/// MongoDB skip/limit, CosmosDb TOP/OFFSET).
/// </para>
/// </remarks>
public sealed record QueryOptions(
	int? Skip = null,
	int? Take = null,
	string? OrderBy = null,
	bool Descending = false)
{
	/// <summary>
	/// Default query options with no pagination or sorting.
	/// </summary>
	public static QueryOptions Default { get; } = new();
}
