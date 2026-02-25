// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;

namespace Excalibur.Saga;

/// <summary>
/// Configuration options for advanced saga orchestration.
/// </summary>
[UnconditionalSuppressMessage(
		"Trimming",
		"IL2026:Using member 'System.ComponentModel.DataAnnotations.RangeAttribute.RangeAttribute(Type, String, String)' can break functionality when trimming application code.",
		Justification = "Range attributes are used for configuration validation and do not affect runtime logic.")]
public sealed class AdvancedSagaOptions
{
	/// <summary>
	/// Gets or sets the default timeout for saga execution.
	/// </summary>
	/// <value>
	/// The default timeout for saga execution. Defaults to 30 minutes.
	/// </value>
	[Range(typeof(TimeSpan), "00:00:01", "24:00:00")]
	public TimeSpan DefaultTimeout { get; set; } = TimeSpan.FromMinutes(30);

	/// <summary>
	/// Gets or sets the default timeout for individual saga steps.
	/// </summary>
	/// <value>
	/// The default timeout for individual saga steps. Defaults to 5 minutes.
	/// </value>
	[Range(typeof(TimeSpan), "00:00:01", "01:00:00")]
	public TimeSpan DefaultStepTimeout { get; set; } = TimeSpan.FromMinutes(5);

	/// <summary>
	/// Gets or sets the maximum retry attempts for failed steps.
	/// </summary>
	/// <value>
	/// The maximum retry attempts for failed steps. Defaults to 3.
	/// </value>
	[Range(0, 100)]
	public int MaxRetryAttempts { get; set; } = 3;

	/// <summary>
	/// Gets or sets the base delay for retry backoff.
	/// </summary>
	/// <value>
	/// The base delay for retry backoff. Defaults to 1 second.
	/// </value>
	[Range(typeof(TimeSpan), "00:00:00.100", "00:10:00")]
	public TimeSpan RetryBaseDelay { get; set; } = TimeSpan.FromSeconds(1);

	/// <summary>
	/// Gets or sets a value indicating whether to enable automatic compensation on failure.
	/// </summary>
	/// <value>
	/// <see langword="true"/> if automatic compensation is enabled; otherwise, <see langword="false"/>. Defaults to true.
	/// </value>
	public bool EnableAutoCompensation { get; set; } = true;

	/// <summary>
	/// Gets or sets a value indicating whether to persist saga state.
	/// </summary>
	/// <value>
	/// <see langword="true"/> if saga state should be persisted; otherwise, <see langword="false"/>. Defaults to true.
	/// </value>
	public bool EnableStatePersistence { get; set; } = true;

	/// <summary>
	/// Gets or sets the maximum degree of parallelism for parallel saga steps.
	/// </summary>
	/// <value>
	/// The maximum degree of parallelism. Defaults to 10.
	/// </value>
	[Range(1, 1000)]
	public int MaxDegreeOfParallelism { get; set; } = 10;

	/// <summary>
	/// Gets or sets a value indicating whether to enable saga metrics collection.
	/// </summary>
	/// <value>
	/// <see langword="true"/> if metrics collection is enabled; otherwise, <see langword="false"/>. Defaults to true.
	/// </value>
	public bool EnableMetrics { get; set; } = true;

	/// <summary>
	/// Gets or sets the saga state cleanup interval.
	/// </summary>
	/// <value>
	/// The cleanup interval for expired saga states. Defaults to 1 hour.
	/// </value>
	[Range(typeof(TimeSpan), "00:01:00", "24:00:00")]
	public TimeSpan CleanupInterval { get; set; } = TimeSpan.FromHours(1);

	/// <summary>
	/// Gets or sets the retention period for completed saga states.
	/// </summary>
	/// <value>
	/// The retention period for completed saga states. Defaults to 7 days.
	/// </value>
	[Range(typeof(TimeSpan), "00:01:00", "365.00:00:00")]
	public TimeSpan CompletedSagaRetention { get; set; } = TimeSpan.FromDays(7);
}

