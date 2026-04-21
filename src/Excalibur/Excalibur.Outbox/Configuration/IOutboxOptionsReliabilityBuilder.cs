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
public interface IOutboxOptionsReliabilityBuilder
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

    /// <summary>
    /// Sets the retention period for successfully sent messages before cleanup.
    /// </summary>
    /// <param name="period">The retention period. Must be positive.</param>
    /// <returns>The builder for fluent chaining.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="period"/> is not positive.
    /// </exception>
    IOutboxOptionsBuilder WithRetentionPeriod(TimeSpan period);

    /// <summary>
    /// Sets the interval between cleanup cycles.
    /// </summary>
    /// <param name="interval">The cleanup interval. Must be positive.</param>
    /// <returns>The builder for fluent chaining.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="interval"/> is not positive.
    /// </exception>
    IOutboxOptionsBuilder WithCleanupInterval(TimeSpan interval);

    /// <summary>
    /// Disables automatic cleanup of old messages.
    /// </summary>
    /// <returns>The builder for fluent chaining.</returns>
    /// <remarks>
    /// <para>
    /// When automatic cleanup is disabled, you are responsible for cleaning up
    /// old messages manually to prevent unbounded storage growth.
    /// </para>
    /// </remarks>
    IOutboxOptionsBuilder DisableAutomaticCleanup();
}
