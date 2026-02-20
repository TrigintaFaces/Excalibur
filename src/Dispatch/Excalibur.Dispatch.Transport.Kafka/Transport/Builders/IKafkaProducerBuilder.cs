// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Transport.Kafka;

/// <summary>
/// Fluent builder interface for configuring Kafka producer settings.
/// </summary>
/// <remarks>
/// <para>
/// This interface follows the Microsoft-style fluent builder pattern.
/// All methods return <c>this</c> for method chaining.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// kafka.ConfigureProducer(producer =>
/// {
///     producer.ClientId("my-app-producer")
///             .Acks(KafkaAckLevel.All)
///             .EnableIdempotence(true)
///             .CompressionType(KafkaCompressionType.Snappy)
///             .LingerMs(TimeSpan.FromMilliseconds(10));
/// });
/// </code>
/// </example>
public interface IKafkaProducerBuilder
{
	/// <summary>
	/// Sets the producer client ID.
	/// </summary>
	/// <param name="clientId">The client ID used to identify the producer to the broker.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <exception cref="ArgumentException">
	/// Thrown when <paramref name="clientId"/> is null, empty, or whitespace.
	/// </exception>
	IKafkaProducerBuilder ClientId(string clientId);

	/// <summary>
	/// Sets the acknowledgment level for message delivery.
	/// </summary>
	/// <param name="acks">The acks setting for the producer.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <remarks>
	/// <para>
	/// Acknowledgment levels:
	/// </para>
	/// <list type="bullet">
	///   <item><description>None: Fire and forget (highest performance, lowest durability)</description></item>
	///   <item><description>Leader: Wait for leader replica only (balanced)</description></item>
	///   <item><description>All: Wait for all in-sync replicas (highest durability)</description></item>
	/// </list>
	/// </remarks>
	IKafkaProducerBuilder Acks(KafkaAckLevel acks);

	/// <summary>
	/// Enables or disables idempotent producer for exactly-once semantics.
	/// </summary>
	/// <param name="enable">True to enable idempotence; otherwise, false.</param>
	/// <returns>The builder for fluent chaining.</returns>
	IKafkaProducerBuilder EnableIdempotence(bool enable = true);

	/// <summary>
	/// Sets the compression type for messages.
	/// </summary>
	/// <param name="compressionType">The compression algorithm to use.</param>
	/// <returns>The builder for fluent chaining.</returns>
	IKafkaProducerBuilder CompressionType(KafkaCompressionType compressionType);

	/// <summary>
	/// Enables transactional messaging with the specified transactional ID.
	/// </summary>
	/// <param name="transactionalId">The transactional ID for exactly-once processing.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <exception cref="ArgumentException">
	/// Thrown when <paramref name="transactionalId"/> is null, empty, or whitespace.
	/// </exception>
	IKafkaProducerBuilder EnableTransactions(string transactionalId);

	/// <summary>
	/// Sets the maximum time to wait for a batch to fill before sending.
	/// </summary>
	/// <param name="linger">The linger time duration.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <exception cref="ArgumentOutOfRangeException">
	/// Thrown when <paramref name="linger"/> is negative.
	/// </exception>
	IKafkaProducerBuilder LingerMs(TimeSpan linger);

	/// <summary>
	/// Sets the maximum batch size in bytes.
	/// </summary>
	/// <param name="batchSize">The maximum size of a message batch.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <exception cref="ArgumentOutOfRangeException">
	/// Thrown when <paramref name="batchSize"/> is less than or equal to zero.
	/// </exception>
	IKafkaProducerBuilder BatchSize(int batchSize);

	/// <summary>
	/// Adds an additional producer configuration property.
	/// </summary>
	/// <param name="key">The configuration property key.</param>
	/// <param name="value">The configuration property value.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <exception cref="ArgumentException">
	/// Thrown when <paramref name="key"/> is null, empty, or whitespace.
	/// </exception>
	IKafkaProducerBuilder WithConfig(string key, string value);
}

/// <summary>
/// Internal implementation of the Kafka producer builder.
/// </summary>
internal sealed class KafkaProducerBuilder : IKafkaProducerBuilder
{
	private readonly KafkaProducerOptions _options;

	/// <summary>
	/// Initializes a new instance of the <see cref="KafkaProducerBuilder"/> class.
	/// </summary>
	/// <param name="options">The producer options to configure.</param>
	public KafkaProducerBuilder(KafkaProducerOptions options)
	{
		_options = options ?? throw new ArgumentNullException(nameof(options));
	}

	/// <inheritdoc/>
	public IKafkaProducerBuilder ClientId(string clientId)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(clientId);
		_options.ClientId = clientId;
		return this;
	}

	/// <inheritdoc/>
	public IKafkaProducerBuilder Acks(KafkaAckLevel acks)
	{
		_options.Acks = acks;
		return this;
	}

	/// <inheritdoc/>
	public IKafkaProducerBuilder EnableIdempotence(bool enable = true)
	{
		_options.EnableIdempotence = enable;
		return this;
	}

	/// <inheritdoc/>
	public IKafkaProducerBuilder CompressionType(KafkaCompressionType compressionType)
	{
		_options.CompressionType = compressionType;
		return this;
	}

	/// <inheritdoc/>
	public IKafkaProducerBuilder EnableTransactions(string transactionalId)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(transactionalId);
		_options.EnableTransactions = true;
		_options.TransactionalId = transactionalId;
		return this;
	}

	/// <inheritdoc/>
	public IKafkaProducerBuilder LingerMs(TimeSpan linger)
	{
		if (linger < TimeSpan.Zero)
		{
			throw new ArgumentOutOfRangeException(nameof(linger), "Linger time cannot be negative.");
		}

		_options.LingerMs = linger;
		return this;
	}

	/// <inheritdoc/>
	public IKafkaProducerBuilder BatchSize(int batchSize)
	{
		if (batchSize <= 0)
		{
			throw new ArgumentOutOfRangeException(nameof(batchSize), "Batch size must be greater than zero.");
		}

		_options.BatchSize = batchSize;
		return this;
	}

	/// <inheritdoc/>
	public IKafkaProducerBuilder WithConfig(string key, string value)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(key);
		_options.AdditionalConfig[key] = value;
		return this;
	}
}
