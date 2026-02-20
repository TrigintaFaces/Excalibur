// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Transport.Kafka;

/// <summary>
/// Processes individual messages from a Kafka stream.
/// </summary>
/// <typeparam name="TKey">The type of the message key.</typeparam>
/// <typeparam name="TValue">The type of the message value.</typeparam>
/// <remarks>
/// <para>
/// Implementations of this interface define the transformation logic for each message
/// in a Kafka stream processing topology. The processor receives deserialized key-value
/// pairs from the input topic and performs application-specific processing.
/// </para>
/// <para>
/// This follows the Microsoft pattern of single-method processing interfaces
/// (similar to <c>IHostedService.ExecuteAsync</c>), keeping the contract focused
/// on the core operation.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// public class OrderProcessor : IKafkaStreamProcessor&lt;string, OrderEvent&gt;
/// {
///     public Task ProcessAsync(
///         ConsumeResult&lt;string, OrderEvent&gt; result,
///         CancellationToken cancellationToken)
///     {
///         // Process the order event
///         return Task.CompletedTask;
///     }
/// }
/// </code>
/// </example>
public interface IKafkaStreamProcessor<TKey, TValue>
{
	/// <summary>
	/// Processes a single message from the Kafka stream.
	/// </summary>
	/// <param name="result">The consume result containing the key, value, and metadata.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>A <see cref="Task"/> representing the asynchronous processing operation.</returns>
	/// <exception cref="ArgumentNullException">
	/// Thrown when <paramref name="result"/> is null.
	/// </exception>
	Task ProcessAsync(ConsumeResult<TKey, TValue> result, CancellationToken cancellationToken);
}
