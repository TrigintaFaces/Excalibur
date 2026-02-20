// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Resilience;

namespace Excalibur.Dispatch.Options.Resilience;

/// <summary>
/// Specifies retry configuration for a specific message type.
/// </summary>
/// <remarks>
/// <para>
/// Apply this attribute to message types to override the global retry configuration
/// with message-specific settings.
/// </para>
/// <para>
/// Example:
/// <code>
/// [Retry(MaxAttempts = 5, BaseDelayMs = 500)]
/// public record ImportDataAction(...) : IDispatchAction;
/// </code>
/// </para>
/// </remarks>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
public sealed class RetryAttribute : Attribute
{
	/// <summary>
	/// Gets or sets the maximum number of retry attempts.
	/// </summary>
	/// <value>Default is 3.</value>
	public int MaxAttempts { get; set; } = 3;

	/// <summary>
	/// Gets or sets the base delay between retry attempts in milliseconds.
	/// </summary>
	/// <value>Default is 1000 (1 second).</value>
	public int BaseDelayMs { get; set; } = 1000;

	/// <summary>
	/// Gets or sets the maximum delay between retry attempts in milliseconds.
	/// </summary>
	/// <value>Default is 30000 (30 seconds).</value>
	public int MaxDelayMs { get; set; } = 30000;

	/// <summary>
	/// Gets or sets the backoff strategy to use.
	/// </summary>
	/// <value>Default is <see cref="BackoffStrategy.Exponential"/>.</value>
	public BackoffStrategy BackoffStrategy { get; set; } = BackoffStrategy.Exponential;

	/// <summary>
	/// Gets or sets the jitter factor for randomizing retry delays.
	/// </summary>
	/// <value>Default is 0.1 (10% jitter).</value>
	public double JitterFactor { get; set; } = 0.1;

	/// <summary>
	/// Gets or sets a value indicating whether to use jitter.
	/// </summary>
	/// <value>Default is <see langword="true"/>.</value>
	public bool UseJitter { get; set; } = true;
}
