// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Data.ElasticSearch.IndexManagement;

/// <summary>
/// Represents the result of an index rollover operation.
/// </summary>
public sealed class IndexRolloverResult
{
	/// <summary>
	/// Gets a value indicating whether the rollover operation was successful.
	/// </summary>
	/// <value> True if rollover was successful, false otherwise. </value>
	public required bool IsSuccessful { get; init; }

	/// <summary>
	/// Gets a value indicating whether a rollover was actually performed.
	/// </summary>
	/// <value> True if rollover was performed, false if conditions were not met. </value>
	public required bool RolledOver { get; init; }

	/// <summary>
	/// Gets the old index name.
	/// </summary>
	/// <value> The name of the old index. </value>
	public string? OldIndex { get; init; }

	/// <summary>
	/// Gets the new index name created by rollover.
	/// </summary>
	/// <value> The name of the new index. </value>
	public string? NewIndex { get; init; }

	/// <summary>
	/// Gets the collection of rollover errors, if any.
	/// </summary>
	/// <value> A collection of error messages. </value>
	public IEnumerable<string> Errors { get; init; } = [];
}
