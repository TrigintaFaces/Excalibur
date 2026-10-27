// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0


using Excalibur.Dispatch.Resilience;

namespace Excalibur.Dispatch.Options.Resilience;

/// <summary>
/// Configuration options for retry behavior across the Excalibur framework.
/// </summary>
/// <remarks>
/// <para>
/// This is the canonical retry options class for core retry logic. It consolidates
/// retry configuration from <c>Options.Core</c>, <c>Options.Middleware</c>, and
/// <c>Transport.Abstractions.ServiceMesh</c>.
/// </para>
/// <para>
/// For Polly-specific retry options with advanced features like <c>JitterStrategy</c>,
/// <c>OperationTimeout</c>, and custom <c>ShouldRetry</c> predicates, use
/// <c>Excalibur.Dispatch.Resilience.Polly.PollyRetryOptions</c> instead.
/// </para>
/// </remarks>
public sealed class RetryOptions
{
	/// <summary>
	/// Gets or sets the maximum number of retry attempts after the initial attempt fails.
	/// </summary>
	/// <value>Default is 3 (1 initial attempt + 3 retries = 4 total attempts).</value>
	public int MaxRetryAttempts { get; set; } = 3;

	/// <summary>
	/// Gets or sets the base delay between retry attempts.
	/// </summary>
	/// <value>Default is 1 second.</value>
	public TimeSpan BaseDelay { get; set; } = TimeSpan.FromSeconds(1);

	/// <summary>
	/// Gets or sets the maximum delay between retry attempts.
	/// </summary>
	/// <value>Default is 30 seconds.</value>
	public TimeSpan MaxDelay { get; set; } = TimeSpan.FromSeconds(30);

	/// <summary>
	/// Gets or sets the backoff strategy for calculating retry delays.
	/// </summary>
	/// <value>Default is <see cref="BackoffStrategy.Exponential"/>.</value>
	public BackoffStrategy BackoffStrategy { get; set; } = BackoffStrategy.Exponential;

	/// <summary>
	/// Gets or sets the backoff multiplier for exponential backoff.
	/// </summary>
	/// <value>Default is 2.0 (doubles delay each retry).</value>
	public double BackoffMultiplier { get; set; } = 2.0;

	/// <summary>
	/// Gets or sets a value indicating whether to add jitter to retry delays.
	/// </summary>
	/// <remarks>
	/// <para>
	/// Jitter prevents the thundering herd: clients that failed together would otherwise retry together.
	/// When enabled, the delay is sampled uniformly from zero up to the value the configured
	/// <see cref="BackoffStrategy" /> prescribes, so it never exceeds that value and may be well below it.
	/// Sampling the low end is the property that decorrelates clients; a band around the nominal delay
	/// adds noise without spreading them.
	/// </para>
	/// <para>
	/// <b>Applies to every strategy.</b> It used to be read by only one of them while the others decided
	/// jitter for themselves, so a configuration could request jitter and silently receive none. It is now
	/// the single switch, and <see cref="BackoffStrategy" /> names only the growth law.
	/// </para>
	/// <para>
	/// Defaults to <see langword="false" />, which is the default Polly's own
	/// <c>RetryStrategyOptions.UseJitter</c> uses. Jitter is worth enabling for any workload where several
	/// instances can fail at the same moment.
	/// </para>
	/// </remarks>
	/// <value><see langword="true"/> to randomize retry delays; otherwise, <see langword="false"/>. Default is <see langword="false"/>.</value>
	public bool UseJitter { get; set; }

	/// <summary>
	/// Gets the exception types that should trigger retries.
	/// </summary>
	/// <remarks>
	/// If empty, no allowlist is applied and the shared failure classifier decides: only exceptions it
	/// classifies as transient are retried. Permanent and poison faults — deserialization, validation,
	/// argument and authorization failures — are abandoned after the first attempt, not retried.
	/// <see cref="NonRetryableExceptions"/> is checked first either way and always wins.
	/// </remarks>
	/// <value>The set of exceptions eligible for retries.</value>
	public HashSet<Type> RetryableExceptions { get; } = [];

	/// <summary>
	/// Gets the exception types that should not trigger retries.
	/// </summary>
	/// <remarks>
	/// These exceptions are considered non-transient and will not be retried,
	/// regardless of the <see cref="RetryableExceptions"/> configuration.
	/// </remarks>
	/// <value>The set of exceptions that bypass retry logic.</value>
	public HashSet<Type> NonRetryableExceptions { get; } =
	[
		typeof(ArgumentException),
		typeof(ArgumentNullException),
		typeof(InvalidOperationException),
	];
}
