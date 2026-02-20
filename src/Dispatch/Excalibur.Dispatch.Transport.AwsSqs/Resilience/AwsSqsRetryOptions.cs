// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.ComponentModel.DataAnnotations;

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
///     options.MaxRetries = 5;
///     options.BaseDelay = TimeSpan.FromMilliseconds(200);
///     options.MaxDelay = TimeSpan.FromSeconds(30);
///     options.RetryStrategy = RetryStrategy.Exponential;
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
	public int MaxRetries { get; set; } = 3;

	/// <summary>
	/// Gets or sets the base delay between retry attempts.
	/// </summary>
	/// <remarks>
	/// For <see cref="Resilience.RetryStrategy.Exponential"/>, this is the initial delay.
	/// For <see cref="Resilience.RetryStrategy.Linear"/>, this is the fixed increment.
	/// For <see cref="Resilience.RetryStrategy.Fixed"/>, this is the delay between all retries.
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
	/// <value>The retry strategy. Default is <see cref="Resilience.RetryStrategy.Exponential"/>.</value>
	public RetryStrategy RetryStrategy { get; set; } = RetryStrategy.Exponential;
}
