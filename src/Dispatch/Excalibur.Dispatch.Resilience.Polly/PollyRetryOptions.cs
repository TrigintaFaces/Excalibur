// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0


using System.ComponentModel.DataAnnotations;

namespace Excalibur.Dispatch.Resilience.Polly;

/// <summary>
/// Configuration options for retry policy with jitter support.
/// </summary>
public sealed class PollyRetryOptions
{
	/// <summary>
	/// Gets or sets maximum number of retry attempts.
	/// </summary>
	/// <value>The current <see cref="MaxRetryAttempts"/> value.</value>
	[Range(0, 100)]
	public int MaxRetryAttempts { get; set; } = 3;

	/// <summary>
	/// Gets or sets base delay between retry attempts.
	/// </summary>
	/// <value>The baseline delay applied before backoff strategies.</value>
	public TimeSpan BaseDelay { get; set; } = TimeSpan.FromSeconds(1);

	/// <summary>
	/// Gets or sets backoff strategy for retry delays.
	/// </summary>
	/// <value>The algorithm used to compute successive retry delays. Defaults to <see cref="BackoffStrategy.Exponential"/>.</value>
	public BackoffStrategy BackoffStrategy { get; set; } = BackoffStrategy.Exponential;

	/// <summary>
	/// Gets or sets a value indicating whether to add jitter to retry delays.
	/// </summary>
	/// <value>The current <see cref="UseJitter"/> value.</value>
	public bool UseJitter { get; set; } = true;

	/// <summary>
	/// Gets or sets a predicate that selects which exceptions are retried.
	/// </summary>
	/// <remarks>
	/// <para>
	/// When this is <see langword="null"/>, exceptions other than cancellation and the framework exclusions
	/// below are retried, matching Polly's default cancellation behavior.
	/// </para>
	/// <para>
	/// An explicit predicate may select attempt-local cancellation when the caller remains active.
	/// Caller cancellation still terminates the pipeline. The predicate is combined with the framework's
	/// exclusions using AND, so it cannot re-enable a tenant-isolation violation.
	/// </para>
	/// <para>
	/// Exceptions that are never retried regardless of this predicate:
	/// <see cref="Excalibur.Dispatch.Messaging.TenantIsolationViolationException"/>,
	/// because a request that escaped its tenant boundary will escape it identically on every attempt, and
	/// retrying multiplies the exposure instead of recovering from it.
	/// </para>
	/// </remarks>
	/// <value>
	/// A predicate returning <see langword="true"/> for exceptions that may be retried, or
	/// <see langword="null"/> to use the default exclusion of cancellation and tenant-isolation failures.
	/// </value>
	public Func<Exception, bool>? ShouldRetry { get; set; }

	// JitterStrategy, JitterFactor and OperationTimeout were removed here. They were read only by the
	// concrete RetryPolicy, which nothing resolved; once that duplicate was deleted they became public
	// options no code reads, and a knob a consumer can set but nothing honours is worse than no knob.
	//
	// Their replacements on the wired adapter are Polly's own: UseJitter (with FullJitter forcing it on)
	// supersedes the hand-rolled JitterStrategy/JitterFactor pair, which is the direction
	// microsoft-first requires -- build ON the primitive rather than parameterising a private copy of it.
	// OperationTimeout has no replacement here by design: an end-to-end timeout is a SEPARATE Polly
	// strategy, not a field on the retry strategy, so declaring it beside these was the wrong seam.

	/// <summary>
	/// Gets or sets maximum delay cap for any single retry.
	/// </summary>
	/// <value>The upper bound applied to any individual retry delay. Defaults to one minute.</value>
	public TimeSpan? MaxDelay { get; set; } = TimeSpan.FromMinutes(1);

	/// <summary>
	/// Gets or sets a value indicating whether to log detailed retry information.
	/// </summary>
	/// <value><see langword="true"/> to emit diagnostic log entries for each retry; otherwise, <see langword="false"/>. Defaults to <see langword="true"/>.</value>
	public bool EnableDetailedLogging { get; set; } = true;
}
