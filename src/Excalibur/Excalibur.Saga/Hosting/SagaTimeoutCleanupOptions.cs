// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.ComponentModel.DataAnnotations;

namespace Excalibur.Saga.Hosting;

/// <summary>
/// Configuration options for the <see cref="SagaTimeoutCleanupService"/>.
/// </summary>
public sealed class SagaTimeoutCleanupOptions
{
	/// <summary>
	/// Gets or sets the interval between cleanup cycles.
	/// Default: 1 hour.
	/// </summary>
	/// <value>The poll interval for the cleanup service.</value>
	public TimeSpan CleanupInterval { get; set; } = TimeSpan.FromHours(1);

	/// <summary>
	/// Gets or sets the age threshold for saga instances to be considered timed out.
	/// Sagas that have been running longer than this duration without completion
	/// are eligible for cleanup.
	/// Default: 24 hours.
	/// </summary>
	/// <value>The timeout age threshold.</value>
	public TimeSpan TimeoutThreshold { get; set; } = TimeSpan.FromHours(24);

	/// <summary>
	/// Gets or sets the maximum number of saga instances to clean up per cycle.
	/// Default: 100.
	/// </summary>
	/// <value>The batch size for cleanup operations. Must be at least 1.</value>
	[Range(1, int.MaxValue)]
	public int BatchSize { get; set; } = 100;

	/// <summary>
	/// Gets or sets whether to enable verbose logging of cleanup operations.
	/// Default: <see langword="false"/>.
	/// </summary>
	/// <value><see langword="true"/> to log each cleaned-up saga; otherwise, <see langword="false"/>.</value>
	public bool EnableVerboseLogging { get; set; }
}
