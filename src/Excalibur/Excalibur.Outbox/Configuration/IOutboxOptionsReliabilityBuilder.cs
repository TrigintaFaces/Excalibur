// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Outbox;

/// <summary>
/// Reliability builder methods for configuring outbox retry, retention, and cleanup behavior.
/// </summary>
/// <remarks>
/// <para>
/// Contains settings that control how the outbox handles failures, retains processed
/// messages, and performs automatic cleanup of old entries.
/// </para>
/// </remarks>
internal interface IOutboxOptionsReliabilityBuilder
{
	/// <summary>
	/// Sets the maximum number of retry attempts for failed messages.
	/// </summary>
	/// <param name="maxRetries">The maximum retry count. Must be non-negative.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <exception cref="ArgumentOutOfRangeException">
	/// Thrown when <paramref name="maxRetries"/> is negative.
	/// </exception>
	IOutboxOptionsBuilder WithMaxRetries(int maxRetries);

	/// <summary>
	/// Sets the delay between retry attempts.
	/// </summary>
	/// <param name="delay">The retry delay. Must be positive.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <exception cref="ArgumentOutOfRangeException">
	/// Thrown when <paramref name="delay"/> is not positive.
	/// </exception>
	IOutboxOptionsBuilder WithRetryDelay(TimeSpan delay);



}
