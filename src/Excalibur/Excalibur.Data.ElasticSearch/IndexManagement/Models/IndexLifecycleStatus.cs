// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Data.ElasticSearch.IndexManagement;

/// <summary>
/// Represents the lifecycle status of an index.
/// </summary>
public sealed class IndexLifecycleStatus
{
	/// <summary>
	/// Gets the name of the index.
	/// </summary>
	/// <value> The index name. </value>
	public required string IndexName { get; init; }

	/// <summary>
	/// Gets the current lifecycle phase.
	/// </summary>
	/// <value> The current phase (hot, warm, cold, delete). </value>
	public required string Phase { get; init; }

	/// <summary>
	/// Gets the name of the lifecycle policy.
	/// </summary>
	/// <value> The policy name applied to this index. </value>
	public string? PolicyName { get; init; }

	/// <summary>
	/// Gets the age of the index.
	/// </summary>
	/// <value> The time since index creation. </value>
	public TimeSpan? Age { get; init; }
}
