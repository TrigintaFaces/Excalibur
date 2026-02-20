// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Data.ElasticSearch.IndexManagement;

/// <summary>
/// Represents the result of index optimization operations.
/// </summary>
public sealed class IndexOptimizationResult
{
	/// <summary>
	/// Gets a value indicating whether the optimization was successful.
	/// </summary>
	/// <value> True if optimization completed successfully, false otherwise. </value>
	public required bool IsSuccessful { get; init; }

	/// <summary>
	/// Gets the optimization actions that were performed.
	/// </summary>
	/// <value> A collection of performed optimization actions. </value>
	public IEnumerable<string> PerformedActions { get; init; } = [];

	/// <summary>
	/// Gets the collection of optimization errors, if any.
	/// </summary>
	/// <value> A collection of error messages. </value>
	public IEnumerable<string> Errors { get; init; } = [];

	/// <summary>
	/// Gets performance improvements achieved.
	/// </summary>
	/// <value> A dictionary of performance metrics and their improvements. </value>
	public Dictionary<string, string>? PerformanceImprovements { get; init; }
}
