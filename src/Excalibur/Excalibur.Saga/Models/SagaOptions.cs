// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.ComponentModel.DataAnnotations;

namespace Excalibur.Saga;

/// <summary>
/// Configuration options for the saga pattern.
/// </summary>
public sealed class SagaOptions
{
	/// <summary>
	/// Gets or sets the maximum number of concurrent saga operations.
	/// </summary>
	/// <value>The maximum concurrency, default is 10.</value>
	[Range(1, int.MaxValue)]
	public int MaxConcurrency { get; set; } = 10;

	/// <summary>
	/// Gets or sets the default timeout for saga operations.
	/// </summary>
	/// <value>The default timeout, default is 30 minutes.</value>
	public TimeSpan DefaultTimeout { get; set; } = TimeSpan.FromMinutes(30);

	/// <summary>
	/// Gets or sets the retention period for completed sagas before cleanup.
	/// </summary>
	/// <value>The saga retention period, default is 30 days.</value>
	public TimeSpan SagaRetentionPeriod { get; set; } = TimeSpan.FromDays(30);

	/// <summary>
	/// Gets or sets whether to enable automatic cleanup of completed sagas.
	/// </summary>
	/// <value>True to enable automatic cleanup, default is true.</value>
	public bool EnableAutomaticCleanup { get; set; } = true;

	/// <summary>
	/// Gets or sets the interval between cleanup cycles.
	/// </summary>
	/// <value>The cleanup interval, default is 1 hour.</value>
	public TimeSpan CleanupInterval { get; set; } = TimeSpan.FromHours(1);

	/// <summary>
	/// Gets or sets the maximum number of retry attempts for failed saga steps.
	/// </summary>
	/// <value>The maximum retry attempts, default is 3.</value>
	[Range(0, 100)]
	public int MaxRetryAttempts { get; set; } = 3;

	/// <summary>
	/// Gets or sets the delay between retry attempts.
	/// </summary>
	/// <value>The retry delay, default is 1 minute.</value>
	public TimeSpan RetryDelay { get; set; } = TimeSpan.FromMinutes(1);

	/// <summary>
	/// Gets or sets whether to enable optimistic concurrency control.
	/// </summary>
	/// <value>True to enable optimistic concurrency, default is true.</value>
	public bool EnableOptimisticConcurrency { get; set; } = true;
}
