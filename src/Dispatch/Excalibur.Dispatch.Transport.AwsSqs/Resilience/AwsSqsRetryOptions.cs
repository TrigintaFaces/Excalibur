// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.ComponentModel.DataAnnotations;

using Excalibur.Dispatch.Resilience;

namespace Excalibur.Dispatch.Transport.AwsSqs;

/// <summary>
/// Configuration options for AWS SQS retry policies.
/// </summary>
/// <remarks>
/// <para>
/// Configures retry behavior for AWS SDK calls including send, receive, and delete
/// operations. The retry policy wraps the underlying AWS SDK retry mechanism with
/// configurable strategies and limits.
/// </para>
/// <para>
/// This follows the Microsoft <c>ResiliencePipelineBuilder</c> pattern of declarative
/// resilience configuration via IOptions.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// services.AddAwsSqsRetryPolicy(options =>
/// {
///     options.MaxRetryAttempts = 5;
///     options.BaseDelay = TimeSpan.FromMilliseconds(200);
///     options.MaxDelay = TimeSpan.FromSeconds(30);
///     options.RetryStrategy = SqsRetryStrategy.Exponential;
/// });
/// </code>
/// </example>
public sealed class AwsSqsRetryOptions
{
	/// <summary>
	/// Gets or sets the maximum number of retry attempts.
	/// </summary>
	/// <value>The maximum retries. Default is 3.</value>
	[Range(0, 20)]
	public int MaxRetryAttempts { get; set; } = 3;

	/// <summary>
	/// Gets or sets the base delay between retry attempts.
	/// </summary>
	/// <remarks>
	/// For <see cref="SqsRetryStrategy.Exponential"/>, this is the initial delay.
	/// For <see cref="SqsRetryStrategy.Linear"/>, this is the fixed increment.
	/// For <see cref="SqsRetryStrategy.Fixed"/>, this is the delay between all retries.
	/// </remarks>
	/// <value>The base delay. Default is 200 milliseconds.</value>
	public TimeSpan BaseDelay { get; set; } = TimeSpan.FromMilliseconds(200);

	/// <summary>
	/// Gets or sets the maximum delay between retry attempts.
	/// </summary>
	/// <remarks>
	/// Caps the delay for exponential and linear strategies to prevent
	/// excessively long waits between retries.
	/// </remarks>
	/// <value>The maximum delay. Default is 30 seconds.</value>
	public TimeSpan MaxDelay { get; set; } = TimeSpan.FromSeconds(30);

	/// <summary>
	/// Gets or sets the retry backoff strategy.
	/// </summary>
	/// <value>The retry strategy. Default is <see cref="SqsRetryStrategy.Exponential"/>.</value>
	public SqsRetryStrategy RetryStrategy { get; set; } = SqsRetryStrategy.Exponential;

	/// <summary>
	/// Computes the delay to wait before the given retry attempt according to the configured
	/// <see cref="RetryStrategy"/>.
	/// </summary>
	/// <param name="attempt">The 1-based retry attempt number. Values below <c>1</c> are treated as <c>1</c>.</param>
	/// <returns>
	/// The delay before the attempt, capped at <see cref="MaxDelay"/> and never negative.
	/// </returns>
	/// <remarks>
	/// <para>
	/// This makes the documented backoff behavior of <see cref="SqsRetryStrategy"/> concrete:
	/// </para>
	/// <list type="bullet">
	/// <item><see cref="SqsRetryStrategy.Exponential"/> uses <see cref="ExponentialBackoff"/>
	/// (<c>BaseDelay * 2^(attempt-1)</c>) with symmetric jitter to avoid thundering herd.</item>
	/// <item><see cref="SqsRetryStrategy.Linear"/> increases linearly (<c>BaseDelay * attempt</c>).</item>
	/// <item><see cref="SqsRetryStrategy.Fixed"/> waits a constant <see cref="BaseDelay"/>.</item>
	/// </list>
	/// <para>
	/// The exponential strategy uses sensible fixed defaults not exposed as options: a multiplier of
	/// <c>2.0</c> and a jitter factor of <c>0.2</c> (matching the AWS-recommended <c>2^attempt</c> with jitter).
	/// </para>
	/// </remarks>
	public TimeSpan GetRetryDelay(int attempt)
	{
		if (attempt < 1)
		{
			attempt = 1;
		}

		switch (RetryStrategy)
		{
			case SqsRetryStrategy.Fixed:
				return BaseDelay;

			case SqsRetryStrategy.Linear:
				var linearMs = Math.Min(BaseDelay.TotalMilliseconds * attempt, MaxDelay.TotalMilliseconds);
				return TimeSpan.FromMilliseconds(Math.Max(0, linearMs));

			case SqsRetryStrategy.Exponential:
			default:
				return ExponentialBackoff.Calculate(attempt, new BackoffParameters
				{
					BaseDelay = BaseDelay,
					MaxDelay = MaxDelay,
					Multiplier = 2.0,
					UseJitter = true,
					JitterFactor = 0.2,
				});
		}
	}
}
