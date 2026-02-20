// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.ComponentModel.DataAnnotations;

namespace Excalibur.Dispatch.Resilience.Polly;

/// <summary>
/// Configuration options for the timeout manager.
/// </summary>
public sealed class TimeoutManagerOptions
{
	private readonly Dictionary<string, TimeSpan> _operationTimeouts = new(StringComparer.Ordinal);

	/// <summary>
	/// Gets or sets the default timeout for all operations when no override is specified.
	/// </summary>
	/// <value>The baseline timeout applied across operations. Defaults to 30 seconds.</value>
	public TimeSpan DefaultTimeout { get; set; } = TimeSpan.FromSeconds(30);

	/// <summary>
	/// Gets specific timeouts for named operations.
	/// </summary>
	/// <value>A dictionary mapping operation names to their configured timeout values.</value>
	public IDictionary<string, TimeSpan> OperationTimeouts => _operationTimeouts;

	/// <summary>
	/// Gets or sets the timeout applied to database operations.
	/// </summary>
	/// <value>The maximum duration permitted for database calls. Defaults to 15 seconds.</value>
	public TimeSpan DatabaseTimeout { get; set; } = TimeSpan.FromSeconds(15);

	/// <summary>
	/// Gets or sets the timeout applied to HTTP operations.
	/// </summary>
	/// <value>The maximum duration permitted for outbound HTTP requests. Defaults to 100 seconds.</value>
	public TimeSpan HttpTimeout { get; set; } = TimeSpan.FromSeconds(100);

	/// <summary>
	/// Gets or sets the timeout applied to message queue operations.
	/// </summary>
	/// <value>The maximum duration permitted for queue operations. Defaults to 60 seconds.</value>
	public TimeSpan MessageQueueTimeout { get; set; } = TimeSpan.FromSeconds(60);

	/// <summary>
	/// Gets or sets the timeout applied to cache operations.
	/// </summary>
	/// <value>The maximum duration permitted for cache operations. Defaults to 5 seconds.</value>
	public TimeSpan CacheTimeout { get; set; } = TimeSpan.FromSeconds(5);

	/// <summary>
	/// Gets or sets a value indicating whether to log timeout warnings.
	/// </summary>
	/// <value><see langword="true"/> to emit warnings when operations exceed their timeout; otherwise, <see langword="false"/>. Defaults to <see langword="true"/>.</value>
	public bool LogTimeoutWarnings { get; set; } = true;

	/// <summary>
	/// Gets or sets the threshold for logging slow operations as a percentage of the timeout.
	/// </summary>
	/// <value>A fractional value (0-1) that triggers slow-operation logging. Defaults to 0.8.</value>
	[Range(0.0, 1.0)]
	public double SlowOperationThreshold { get; set; } = 0.8; // 80% of timeout
}
