// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Transport.Kafka;

/// <summary>
/// Extension methods for <see cref="IKafkaConsumerBuilder"/>.
/// </summary>
public static class KafkaConsumerBuilderExtensions
{
	/// <summary>
	/// Sets the auto-commit interval when auto-commit is enabled.
	/// </summary>
	/// <param name="builder">The builder.</param>
	/// <param name="interval">The interval between auto-commits.</param>
	/// <returns>The builder for fluent chaining.</returns>
	public static IKafkaConsumerBuilder AutoCommitInterval(this IKafkaConsumerBuilder builder, TimeSpan interval)
	{
		ArgumentNullException.ThrowIfNull(builder);
		return ((KafkaConsumerBuilder)builder).AutoCommitInterval(interval);
	}

	/// <summary>
	/// Sets the maximum poll interval before the consumer is considered failed.
	/// </summary>
	/// <param name="builder">The builder.</param>
	/// <param name="interval">The maximum poll interval.</param>
	/// <returns>The builder for fluent chaining.</returns>
	public static IKafkaConsumerBuilder MaxPollInterval(this IKafkaConsumerBuilder builder, TimeSpan interval)
	{
		ArgumentNullException.ThrowIfNull(builder);
		return ((KafkaConsumerBuilder)builder).MaxPollInterval(interval);
	}

	/// <summary>
	/// Sets the maximum number of messages to fetch in a single poll.
	/// </summary>
	/// <param name="builder">The builder.</param>
	/// <param name="maxMessages">The maximum batch size for consuming.</param>
	/// <returns>The builder for fluent chaining.</returns>
	public static IKafkaConsumerBuilder MaxBatchSize(this IKafkaConsumerBuilder builder, int maxMessages)
	{
		ArgumentNullException.ThrowIfNull(builder);
		return ((KafkaConsumerBuilder)builder).MaxBatchSize(maxMessages);
	}
}
