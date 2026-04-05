// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Transport.Kafka;

/// <summary>
/// Extension methods for <see cref="IKafkaProducerBuilder"/>.
/// </summary>
public static class KafkaProducerBuilderExtensions
{
	/// <summary>
	/// Enables transactional messaging with the specified transactional ID.
	/// </summary>
	/// <param name="builder">The builder.</param>
	/// <param name="transactionalId">The transactional ID for exactly-once processing.</param>
	/// <returns>The builder for fluent chaining.</returns>
	public static IKafkaProducerBuilder EnableTransactions(this IKafkaProducerBuilder builder, string transactionalId)
	{
		ArgumentNullException.ThrowIfNull(builder);
		return ((KafkaProducerBuilder)builder).EnableTransactions(transactionalId);
	}

	/// <summary>
	/// Sets the maximum time to wait for a batch to fill before sending.
	/// </summary>
	/// <param name="builder">The builder.</param>
	/// <param name="linger">The linger time duration.</param>
	/// <returns>The builder for fluent chaining.</returns>
	public static IKafkaProducerBuilder LingerMs(this IKafkaProducerBuilder builder, TimeSpan linger)
	{
		ArgumentNullException.ThrowIfNull(builder);
		return ((KafkaProducerBuilder)builder).LingerMs(linger);
	}

	/// <summary>
	/// Sets the maximum batch size in bytes.
	/// </summary>
	/// <param name="builder">The builder.</param>
	/// <param name="batchSize">The maximum size of a message batch.</param>
	/// <returns>The builder for fluent chaining.</returns>
	public static IKafkaProducerBuilder BatchSize(this IKafkaProducerBuilder builder, int batchSize)
	{
		ArgumentNullException.ThrowIfNull(builder);
		return ((KafkaProducerBuilder)builder).BatchSize(batchSize);
	}
}
