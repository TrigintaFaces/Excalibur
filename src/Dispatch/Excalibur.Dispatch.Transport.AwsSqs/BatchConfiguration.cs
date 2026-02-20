// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Transport.Aws;

/// <summary>
/// Configuration for batch processing.
/// </summary>
public sealed class BatchConfiguration
{
	/// <summary>
	/// Gets or sets the maximum batch size.
	/// </summary>
	/// <value>
	/// The maximum batch size.
	/// </value>
	public int MaxBatchSize { get; set; } = 10;

	/// <summary>
	/// Gets or sets the batch timeout.
	/// </summary>
	/// <value>
	/// The batch timeout.
	/// </value>
	public TimeSpan BatchTimeout { get; set; } = TimeSpan.FromSeconds(5);

	/// <summary>
	/// Gets or sets a value indicating whether to enable parallel processing.
	/// </summary>
	/// <value>
	/// A value indicating whether to enable parallel processing.
	/// </value>
	public bool EnableParallelProcessing { get; set; } = true;

	/// <summary>
	/// Gets or sets the maximum degree of parallelism.
	/// </summary>
	/// <value>
	/// The maximum degree of parallelism.
	/// </value>
	public int MaxDegreeOfParallelism { get; set; } = Environment.ProcessorCount;

	/// <summary>
	/// Gets or sets a value indicating whether to enable automatic retries.
	/// </summary>
	/// <value>
	/// A value indicating whether to enable automatic retries.
	/// </value>
	public bool EnableAutoRetry { get; set; } = true;

	/// <summary>
	/// Gets or sets the maximum retry attempts.
	/// </summary>
	/// <value>
	/// The maximum retry attempts.
	/// </value>
	public int MaxRetryAttempts { get; set; } = 3;
}
