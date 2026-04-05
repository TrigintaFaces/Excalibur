// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Outbox;

/// <summary>
/// Advanced builder methods for configuring inbox parallelism, timeouts, TTL,
/// dynamic batch sizing, and database operation modes.
/// </summary>
/// <remarks>
/// <para>
/// Contains advanced tuning settings that most consumers will not need to change
/// from the defaults provided by the presets.
/// </para>
/// </remarks>
public interface IInboxOptionsAdvancedBuilder
{
    /// <summary>
    /// Sets the degree of parallelism for batch processing.
    /// </summary>
    /// <param name="maxDegree">The maximum parallel processing degree.</param>
    /// <returns>This builder for method chaining.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="maxDegree"/> is less than 1.</exception>
    IInboxOptionsBuilder WithParallelism(int maxDegree);

    /// <summary>
    /// Sets the timeout for processing a batch of messages.
    /// </summary>
    /// <param name="timeout">The processing timeout.</param>
    /// <returns>This builder for method chaining.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="timeout"/> is not positive.</exception>
    IInboxOptionsBuilder WithBatchProcessingTimeout(TimeSpan timeout);

    /// <summary>
    /// Sets the default time-to-live for messages.
    /// </summary>
    /// <param name="ttl">The message TTL, or null for no expiration.</param>
    /// <returns>This builder for method chaining.</returns>
    IInboxOptionsBuilder WithDefaultMessageTtl(TimeSpan? ttl);

    /// <summary>
    /// Enables dynamic batch sizing based on throughput.
    /// </summary>
    /// <param name="minBatchSize">The minimum batch size.</param>
    /// <param name="maxBatchSize">The maximum batch size.</param>
    /// <returns>This builder for method chaining.</returns>
    IInboxOptionsBuilder EnableDynamicBatchSizing(int minBatchSize = 10, int maxBatchSize = 1000);

    /// <summary>
    /// Disables batch database operations (use single-message operations).
    /// </summary>
    /// <returns>This builder for method chaining.</returns>
    IInboxOptionsBuilder DisableBatchDatabaseOperations();
}
