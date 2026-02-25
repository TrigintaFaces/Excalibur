// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


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
/// <see cref="Dispatch.Resilience.Polly.RetryOptions"/> instead.
/// </para>
/// </remarks>
public sealed class RetryOptions
{
	/// <summary>
	/// Gets or sets the maximum number of retry attempts.
	/// </summary>
	/// <value>Default is 3.</value>
	public int MaxAttempts { get; set; } = 3;

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
	/// Gets or sets the jitter factor for randomizing retry delays.
	/// </summary>
	/// <value>Default is 0.1 (10% jitter).</value>
	public double JitterFactor { get; set; } = 0.1;

	/// <summary>
	/// Gets or sets a value indicating whether to add jitter to retry delays.
	/// </summary>
	/// <remarks>
	/// Jitter helps prevent thundering herd scenarios where many clients
	/// retry simultaneously after a failure.
	/// </remarks>
	/// <value><see langword="true"/> to randomize retry delays; otherwise, <see langword="false"/>. Default is <see langword="true"/>.</value>
	public bool UseJitter { get; set; } = true;

	/// <summary>
	/// Gets the exception types that should trigger retries.
	/// </summary>
	/// <remarks>
	/// If empty, all exceptions except those in <see cref="NonRetryableExceptions"/> will be retried.
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
