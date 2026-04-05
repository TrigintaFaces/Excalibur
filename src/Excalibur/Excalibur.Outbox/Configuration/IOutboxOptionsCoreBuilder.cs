// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Outbox;

/// <summary>
/// Core builder methods for configuring fundamental outbox processing parameters.
/// </summary>
/// <remarks>
/// <para>
/// Contains the essential settings for outbox message processing: processor identity,
/// background processing toggle, batch size, polling interval, and parallelism.
/// </para>
/// </remarks>
public interface IOutboxOptionsCoreBuilder
{
    /// <summary>
    /// Sets the unique identifier for this processor instance.
    /// </summary>
    /// <param name="processorId">The processor identifier.</param>
    /// <returns>The builder for fluent chaining.</returns>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="processorId"/> is null, empty, or whitespace.
    /// </exception>
    /// <remarks>
    /// <para>
    /// The processor ID is used to identify which instance processed a message,
    /// useful for debugging in distributed deployments.
    /// </para>
    /// </remarks>
    IOutboxOptionsBuilder WithProcessorId(string processorId);

    /// <summary>
    /// Sets whether background processing is enabled.
    /// </summary>
    /// <param name="enable">True to enable background processing; otherwise, false.</param>
    /// <returns>The builder for fluent chaining.</returns>
    /// <remarks>
    /// <para>
    /// When enabled, a hosted service will be registered that periodically
    /// polls the outbox and publishes pending messages.
    /// </para>
    /// </remarks>
    IOutboxOptionsBuilder EnableBackgroundProcessing(bool enable = true);

    /// <summary>
    /// Sets the maximum number of messages to process in a single batch.
    /// </summary>
    /// <param name="batchSize">The batch size. Must be between 1 and 10000.</param>
    /// <returns>The builder for fluent chaining.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="batchSize"/> is less than 1 or greater than 10000.
    /// </exception>
    IOutboxOptionsBuilder WithBatchSize(int batchSize);

    /// <summary>
    /// Sets the interval between processing cycles.
    /// </summary>
    /// <param name="interval">The polling interval. Must be at least 10ms.</param>
    /// <returns>The builder for fluent chaining.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="interval"/> is less than 10 milliseconds.
    /// </exception>
    IOutboxOptionsBuilder WithPollingInterval(TimeSpan interval);

    /// <summary>
    /// Sets the maximum degree of parallelism and enables parallel processing.
    /// </summary>
    /// <param name="maxDegree">The maximum number of concurrent processing operations. Must be at least 1.</param>
    /// <returns>The builder for fluent chaining.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="maxDegree"/> is less than 1.
    /// </exception>
    /// <remarks>
    /// <para>
    /// Parallel processing can improve throughput but may affect message ordering.
    /// Set to 1 for sequential processing.
    /// </para>
    /// </remarks>
    IOutboxOptionsBuilder WithParallelism(int maxDegree);
}
