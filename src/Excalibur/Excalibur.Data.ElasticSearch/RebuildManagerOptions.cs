// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Data.ElasticSearch;

/// <summary>
/// Configures projection rebuild operations.
/// </summary>
public sealed class RebuildManagerOptions
{
	/// <summary>
	/// Gets a value indicating whether rebuild operations are enabled.
	/// </summary>
	/// <value>
	/// A value indicating whether rebuild operations are enabled.
	/// </value>
	public bool Enabled { get; init; } = true;

	/// <summary>
	/// Gets the default batch size for rebuild operations.
	/// </summary>
	/// <value>
	/// The default batch size for rebuild operations.
	/// </value>
	public int DefaultBatchSize { get; init; } = 1000;

	/// <summary>
	/// Gets the maximum degree of parallelism.
	/// </summary>
	/// <value>
	/// The maximum degree of parallelism.
	/// </value>
	public int MaxDegreeOfParallelism { get; init; } = 4;

	/// <summary>
	/// Gets a value indicating whether to use index aliasing for zero-downtime rebuilds.
	/// </summary>
	/// <value>
	/// A value indicating whether to use index aliasing for zero-downtime rebuilds.
	/// </value>
	public bool UseAliasing { get; init; } = true;

	/// <summary>
	/// Gets the rebuild operation timeout.
	/// </summary>
	/// <value>
	/// The rebuild operation timeout.
	/// </value>
	public TimeSpan OperationTimeout { get; init; } = TimeSpan.FromHours(24);
}
