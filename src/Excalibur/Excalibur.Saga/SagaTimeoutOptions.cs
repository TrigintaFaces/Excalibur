// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.ComponentModel.DataAnnotations;

namespace Excalibur.Saga;

/// <summary>
/// Configuration options for saga timeout delivery service.
/// </summary>
public sealed class SagaTimeoutOptions
{
	/// <summary>
	/// Gets or sets the interval between polls for due timeouts.
	/// Default: 1 second.
	/// </summary>
	/// <value>The poll interval. Minimum supported is 100ms.</value>
	public TimeSpan PollInterval { get; set; } = TimeSpan.FromSeconds(1);

	/// <summary>
	/// Gets or sets the maximum number of timeouts to process per poll cycle.
	/// Default: 100.
	/// </summary>
	/// <value>The batch size for processing due timeouts.</value>
	[Range(1, int.MaxValue)]
	public int BatchSize { get; set; } = 100;

	/// <summary>
	/// Gets or sets the timeout for graceful shutdown drain.
	/// Default: 30 seconds.
	/// </summary>
	/// <value>The maximum time to wait for pending timeout deliveries during shutdown.</value>
	public TimeSpan ShutdownTimeout { get; set; } = TimeSpan.FromSeconds(30);

	/// <summary>
	/// Gets or sets a value indicating whether to log each timeout delivery at Debug level.
	/// Default: true.
	/// </summary>
	/// <value>True to enable verbose logging; otherwise false.</value>
	public bool EnableVerboseLogging { get; set; } = true;
}
