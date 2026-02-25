// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Transport.Kafka;

/// <summary>
/// Builder for composing Kafka stream processing topologies.
/// </summary>
/// <typeparam name="TKey">The type of the message key.</typeparam>
/// <typeparam name="TValue">The type of the message value.</typeparam>
/// <remarks>
/// <para>
/// Provides a fluent API for defining stream processing pipelines:
/// <c>From(topic).Transform(func).To(topic).Build()</c>.
/// This follows the Microsoft builder pattern (similar to <c>HostBuilder</c>
/// and <c>ResiliencePipelineBuilder</c>).
/// </para>
/// </remarks>
/// <example>
/// <code>
/// var options = new KafkaStreamBuilder&lt;string, OrderEvent&gt;()
///     .From("raw-orders")
///     .Transform(result => { /* transform */ return result; })
///     .To("processed-orders")
///     .Build();
/// </code>
/// </example>
public sealed class KafkaStreamBuilder<TKey, TValue>
{
	private string _inputTopic = string.Empty;
	private string _outputTopic = string.Empty;
	private Func<ConsumeResult<TKey, TValue>, ConsumeResult<TKey, TValue>>? _transform;

	/// <summary>
	/// Specifies the input topic to consume messages from.
	/// </summary>
	/// <param name="topic">The input topic name.</param>
	/// <returns>This builder instance for chaining.</returns>
	/// <exception cref="ArgumentNullException">
	/// Thrown when <paramref name="topic"/> is null.
	/// </exception>
	/// <exception cref="ArgumentException">
	/// Thrown when <paramref name="topic"/> is empty or whitespace.
	/// </exception>
	public KafkaStreamBuilder<TKey, TValue> From(string topic)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(topic);
		_inputTopic = topic;
		return this;
	}

	/// <summary>
	/// Specifies a synchronous transformation to apply to each consumed message.
	/// </summary>
	/// <param name="transform">The transformation function.</param>
	/// <returns>This builder instance for chaining.</returns>
	/// <exception cref="ArgumentNullException">
	/// Thrown when <paramref name="transform"/> is null.
	/// </exception>
	public KafkaStreamBuilder<TKey, TValue> Transform(
		Func<ConsumeResult<TKey, TValue>, ConsumeResult<TKey, TValue>> transform)
	{
		ArgumentNullException.ThrowIfNull(transform);
		_transform = transform;
		return this;
	}

	/// <summary>
	/// Specifies the output topic to produce processed messages to.
	/// </summary>
	/// <param name="topic">The output topic name.</param>
	/// <returns>This builder instance for chaining.</returns>
	/// <exception cref="ArgumentNullException">
	/// Thrown when <paramref name="topic"/> is null.
	/// </exception>
	/// <exception cref="ArgumentException">
	/// Thrown when <paramref name="topic"/> is empty or whitespace.
	/// </exception>
	public KafkaStreamBuilder<TKey, TValue> To(string topic)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(topic);
		_outputTopic = topic;
		return this;
	}

	/// <summary>
	/// Builds the <see cref="KafkaStreamOptions"/> from the configured builder state.
	/// </summary>
	/// <returns>A configured <see cref="KafkaStreamOptions"/> instance.</returns>
	/// <exception cref="InvalidOperationException">
	/// Thrown when the input topic has not been specified via <see cref="From"/>.
	/// </exception>
	public KafkaStreamOptions Build()
	{
		if (string.IsNullOrWhiteSpace(_inputTopic))
		{
			throw new InvalidOperationException("Input topic must be specified via From().");
		}

		return new KafkaStreamOptions
		{
			InputTopic = _inputTopic,
			OutputTopic = _outputTopic,
		};
	}

	/// <summary>
	/// Gets the configured transformation function, if any.
	/// </summary>
	/// <returns>The transform function, or <c>null</c> if none was configured.</returns>
	public Func<ConsumeResult<TKey, TValue>, ConsumeResult<TKey, TValue>>? GetTransform() => _transform;
}
