// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Data;

namespace Excalibur.Data.Persistence;

/// <summary>
/// Represents global persistence options.
/// </summary>
public sealed class PersistenceOptions
{
	/// <summary>
	/// Gets or sets a value indicating whether to enable distributed tracing.
	/// </summary>
	/// <value>
	/// A value indicating whether to enable distributed tracing.
	/// </value>
	public bool EnableTracing { get; set; } = true;

	/// <summary>
	/// Gets or sets a value indicating whether to enable performance metrics.
	/// </summary>
	/// <value>
	/// A value indicating whether to enable performance metrics.
	/// </value>
	public bool EnableMetrics { get; set; } = true;

	/// <summary>
	/// Gets or sets a value indicating whether to enable sensitive data logging.
	/// </summary>
	/// <value>
	/// A value indicating whether to enable sensitive data logging.
	/// </value>
	public bool EnableSensitiveDataLogging { get; set; }

	/// <summary>
	/// Gets or sets the default command timeout in seconds.
	/// </summary>
	/// <value>
	/// The default command timeout in seconds.
	/// </value>
	public int DefaultCommandTimeout { get; set; } = 30;

	/// <summary>
	/// Gets or sets the default transaction isolation level.
	/// </summary>
	/// <value>
	/// The default transaction isolation level.
	/// </value>
	public IsolationLevel DefaultIsolationLevel { get; set; } = IsolationLevel.ReadCommitted;

	/// <summary>
	/// Gets or sets a value indicating whether to automatically retry on transient failures.
	/// </summary>
	/// <value>
	/// A value indicating whether to automatically retry on transient failures.
	/// </value>
	public bool EnableAutoRetry { get; set; } = true;

	/// <summary>
	/// Gets or sets the maximum number of retry attempts.
	/// </summary>
	/// <value>
	/// The maximum number of retry attempts.
	/// </value>
	public int MaxRetryAttempts { get; set; } = 3;

	/// <summary>
	/// Gets or sets the base retry delay in milliseconds.
	/// </summary>
	/// <value>
	/// The base retry delay in milliseconds.
	/// </value>
	public int RetryDelayMilliseconds { get; set; } = 100;
}
