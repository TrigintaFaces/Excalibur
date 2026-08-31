// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.ComponentModel.DataAnnotations;

namespace Excalibur.Data.SqlServer;

/// <summary>
/// Configuration options for SQL Server provider.
/// </summary>
public sealed class SqlServerProviderOptions
{
	/// <summary>
	/// Gets or sets the provider name.
	/// </summary>
	/// <value> The provider name. </value>
	public string? Name { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether to enable Multiple Active Result Sets.
	/// </summary>
	/// <value> <c> true </c> if Multiple Active Result Sets (MARS) is enabled; otherwise, <c> false </c>. </value>
	public bool EnableMars { get; set; } = true;

	/// <summary>
	/// Gets or sets the command timeout in seconds.
	/// </summary>
	/// <value> The command timeout in seconds. </value>
	[Range(1, int.MaxValue)]
	public int CommandTimeout { get; set; } = 30;

	/// <summary>
	/// Gets or sets the retry count for transient failures.
	/// </summary>
	/// <remarks>
	/// Bounded, because the attempt budget is what bounds the total time a single data request can spend
	/// retrying: the backoff delay is capped at thirty seconds, so the worst case is this many attempts of
	/// thirty seconds. An unbounded budget left that worst case unbounded too, and a caller observes an
	/// unbounded retry as a hung request rather than a failure.
	/// </remarks>
	/// <value> The retry count for transient failures. </value>
	[Range(0, 10)]
	public int RetryCount { get; set; } = 3;

	/// <summary>
	/// Gets or sets a value indicating whether to open connections immediately.
	/// </summary>
	/// <value> <c> true </c> if connections are opened immediately; otherwise, <c> false </c>. </value>
	public bool OpenConnectionImmediately { get; set; }
}
