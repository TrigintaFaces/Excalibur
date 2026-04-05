// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Outbox;

/// <summary>
/// Core builder methods for configuring fundamental inbox processing parameters.
/// </summary>
/// <remarks>
/// <para>
/// Contains the essential settings for inbox message processing: queue capacity,
/// batch sizes, per-run limits, and retry attempts.
/// </para>
/// </remarks>
public interface IInboxOptionsCoreBuilder
{
    /// <summary>
    /// Sets the capacity of the internal message processing queue.
    /// </summary>
    /// <param name="capacity">The maximum number of messages that can be held in the queue.</param>
    /// <returns>This builder for method chaining.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="capacity"/> is less than 1.</exception>
    IInboxOptionsBuilder WithQueueCapacity(int capacity);

    /// <summary>
    /// Sets the batch size for loading messages from storage.
    /// </summary>
    /// <param name="batchSize">The number of messages to load per batch.</param>
    /// <returns>This builder for method chaining.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="batchSize"/> is less than 1.</exception>
    IInboxOptionsBuilder WithProducerBatchSize(int batchSize);

    /// <summary>
    /// Sets the batch size for processing messages.
    /// </summary>
    /// <param name="batchSize">The number of messages to process together.</param>
    /// <returns>This builder for method chaining.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="batchSize"/> is less than 1.</exception>
    IInboxOptionsBuilder WithConsumerBatchSize(int batchSize);

    /// <summary>
    /// Sets the maximum number of messages to process per run.
    /// </summary>
    /// <param name="total">The maximum messages per run.</param>
    /// <returns>This builder for method chaining.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="total"/> is less than 1.</exception>
    IInboxOptionsBuilder WithPerRunTotal(int total);

    /// <summary>
    /// Sets the maximum number of processing attempts for failed messages.
    /// </summary>
    /// <param name="maxAttempts">The maximum retry attempts.</param>
    /// <returns>This builder for method chaining.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="maxAttempts"/> is less than 1.</exception>
    IInboxOptionsBuilder WithMaxAttempts(int maxAttempts);
}
