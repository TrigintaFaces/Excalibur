// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0


namespace Excalibur.Data.ElasticSearch;

/// <summary>
/// Configures retry policy behavior for transient failure handling.
/// </summary>
public sealed class ElasticSearchRetryPolicyOptions
{
	/// <summary>
	/// Gets a value indicating whether retry policies are enabled.
	/// </summary>
	/// <value> A <see cref="bool" /> indicating whether retries are active. Defaults to <c> true </c>. </value>
	public bool Enabled { get; init; } = true;

	/// <summary>
	/// Gets the maximum number of retry attempts.
	/// </summary>
	/// <value> An <see cref="int" /> representing the maximum retry count. Defaults to 3. </value>
	public int MaxAttempts { get; init; } = 3;

	/// <summary>
	/// Gets the base delay for exponential backoff.
	/// </summary>
	/// <value> A <see cref="TimeSpan" /> representing the initial retry delay. Defaults to 1 second. </value>
	public TimeSpan BaseDelay { get; init; } = TimeSpan.FromSeconds(1);

	/// <summary>
	/// Gets the maximum delay between retry attempts.
	/// </summary>
	/// <value> A <see cref="TimeSpan" /> representing the maximum retry delay. Defaults to 30 seconds. </value>
	public TimeSpan MaxDelay { get; init; } = TimeSpan.FromSeconds(30);

	/// <summary>
	/// Gets a value indicating whether retry delays are randomized to decorrelate concurrent clients.
	/// </summary>
	/// <value>
	/// <see langword="true" /> to randomize each delay; otherwise <see langword="false" />. Defaults to
	/// <see langword="true" />.
	/// </value>
	/// <remarks>
	/// Replaces a <c>JitterFactor</c> double that scaled a symmetric band. The retry strategy this feeds
	/// never applied that band: it passed <c>JitterFactor &gt; 0</c> as a boolean to the resilience
	/// pipeline's own jitter switch and discarded the magnitude entirely, so every value from 0.01 to 1.0
	/// produced exactly the same behaviour and only zero differed. The knob advertised a precision it did
	/// not have; this names the one bit that was ever read.
	/// </remarks>
	public bool UseJitter { get; init; } = true;

	/// <summary>
	/// Gets a value indicating whether to use exponential backoff for retry delays.
	/// </summary>
	/// <value> A <see cref="bool" /> indicating whether to use exponential backoff. Defaults to <c> true </c>. </value>
	public bool UseExponentialBackoff { get; init; } = true;
}
