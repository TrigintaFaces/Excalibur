// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.ComponentModel.DataAnnotations;

namespace Excalibur.Dispatch.Resilience.Polly;

/// <summary>
/// Configuration options for the hedging resilience policy.
/// </summary>
/// <remarks>
/// <para>
/// Hedging sends parallel requests after a configured delay, returning the first
/// successful result. This reduces tail latency at the cost of increased resource usage.
/// </para>
/// <para>
/// This follows the pattern from Polly v8's <c>HedgingStrategyOptions</c>.
/// </para>
/// </remarks>
public sealed class HedgingOptions
{
	/// <summary>
	/// Gets or sets the maximum number of hedged attempts (excluding the primary request).
	/// </summary>
	/// <value>The maximum hedged attempts. Defaults to 2.</value>
	[Range(1, 10)]
	public int MaxHedgedAttempts { get; set; } = 2;

	/// <summary>
	/// Gets or sets the delay before launching each hedged attempt.
	/// </summary>
	/// <value>The delay before launching a hedged request. Defaults to 2 seconds.</value>
	public TimeSpan Delay { get; set; } = TimeSpan.FromSeconds(2);

	/// <summary>
	/// Gets or sets a value indicating whether to enable detailed logging of hedging operations.
	/// </summary>
	/// <value><see langword="true"/> to emit diagnostic log entries for hedging; otherwise, <see langword="false"/>. Defaults to <see langword="true"/>.</value>
	public bool EnableDetailedLogging { get; set; } = true;

	/// <summary>
	/// Gets or sets the predicate to determine which exceptions should trigger hedging.
	/// </summary>
	/// <value>A predicate that returns <see langword="true"/> for exceptions that should trigger hedging.</value>
	public Func<Exception, bool>? ShouldHedge { get; set; }
}
