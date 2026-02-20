// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Transport.Kafka;

/// <summary>
/// Fluent builder interface for configuring Kafka consumer settings.
/// </summary>
/// <remarks>
/// <para>
/// This interface follows the Microsoft-style fluent builder pattern.
/// All methods return <c>this</c> for method chaining.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// kafka.ConfigureConsumer(consumer =>
/// {
///     consumer.GroupId("my-consumer-group")
///             .AutoOffsetReset(KafkaOffsetReset.Earliest)
///             .SessionTimeout(TimeSpan.FromSeconds(45))
///             .MaxBatchSize(100);
/// });
/// </code>
/// </example>
public interface IKafkaConsumerBuilder
{
	/// <summary>
	/// Sets the consumer group ID.
	/// </summary>
	/// <param name="groupId">The consumer group identifier.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <exception cref="ArgumentException">
	/// Thrown when <paramref name="groupId"/> is null, empty, or whitespace.
	/// </exception>
	IKafkaConsumerBuilder GroupId(string groupId);

	/// <summary>
	/// Sets the auto offset reset policy.
	/// </summary>
	/// <param name="offsetReset">Where to start consuming when no committed offset exists.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <remarks>
	/// <para>
	/// Offset reset policies:
	/// </para>
	/// <list type="bullet">
	///   <item><description>Earliest: Start from the beginning of the topic</description></item>
	///   <item><description>Latest: Start from the end (new messages only)</description></item>
	///   <item><description>None: Throw exception if no offset found</description></item>
	/// </list>
	/// </remarks>
	IKafkaConsumerBuilder AutoOffsetReset(KafkaOffsetReset offsetReset);

	/// <summary>
	/// Enables or disables automatic offset commits.
	/// </summary>
	/// <param name="enable">True to auto-commit offsets; otherwise, false for manual commits.</param>
	/// <returns>The builder for fluent chaining.</returns>
	IKafkaConsumerBuilder EnableAutoCommit(bool enable = true);

	/// <summary>
	/// Sets the auto-commit interval when auto-commit is enabled.
	/// </summary>
	/// <param name="interval">The interval between auto-commits.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <exception cref="ArgumentOutOfRangeException">
	/// Thrown when <paramref name="interval"/> is negative or zero.
	/// </exception>
	IKafkaConsumerBuilder AutoCommitInterval(TimeSpan interval);

	/// <summary>
	/// Sets the session timeout for detecting consumer failures.
	/// </summary>
	/// <param name="timeout">The session timeout duration.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <exception cref="ArgumentOutOfRangeException">
	/// Thrown when <paramref name="timeout"/> is negative or zero.
	/// </exception>
	IKafkaConsumerBuilder SessionTimeout(TimeSpan timeout);

	/// <summary>
	/// Sets the maximum poll interval before the consumer is considered failed.
	/// </summary>
	/// <param name="interval">The maximum poll interval.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <exception cref="ArgumentOutOfRangeException">
	/// Thrown when <paramref name="interval"/> is negative or zero.
	/// </exception>
	IKafkaConsumerBuilder MaxPollInterval(TimeSpan interval);

	/// <summary>
	/// Sets the maximum number of messages to fetch in a single poll.
	/// </summary>
	/// <param name="maxMessages">The maximum batch size for consuming.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <exception cref="ArgumentOutOfRangeException">
	/// Thrown when <paramref name="maxMessages"/> is less than or equal to zero.
	/// </exception>
	IKafkaConsumerBuilder MaxBatchSize(int maxMessages);

	/// <summary>
	/// Adds an additional consumer configuration property.
	/// </summary>
	/// <param name="key">The configuration property key.</param>
	/// <param name="value">The configuration property value.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <exception cref="ArgumentException">
	/// Thrown when <paramref name="key"/> is null, empty, or whitespace.
	/// </exception>
	IKafkaConsumerBuilder WithConfig(string key, string value);
}

/// <summary>
/// Internal implementation of the Kafka consumer builder.
/// </summary>
internal sealed class KafkaConsumerBuilder : IKafkaConsumerBuilder
{
	private readonly KafkaConsumerOptions _options;

	/// <summary>
	/// Initializes a new instance of the <see cref="KafkaConsumerBuilder"/> class.
	/// </summary>
	/// <param name="options">The consumer options to configure.</param>
	public KafkaConsumerBuilder(KafkaConsumerOptions options)
	{
		_options = options ?? throw new ArgumentNullException(nameof(options));
	}

	/// <inheritdoc/>
	public IKafkaConsumerBuilder GroupId(string groupId)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(groupId);
		_options.GroupId = groupId;
		return this;
	}

	/// <inheritdoc/>
	public IKafkaConsumerBuilder AutoOffsetReset(KafkaOffsetReset offsetReset)
	{
		_options.AutoOffsetReset = offsetReset;
		return this;
	}

	/// <inheritdoc/>
	public IKafkaConsumerBuilder EnableAutoCommit(bool enable = true)
	{
		_options.EnableAutoCommit = enable;
		return this;
	}

	/// <inheritdoc/>
	public IKafkaConsumerBuilder AutoCommitInterval(TimeSpan interval)
	{
		if (interval <= TimeSpan.Zero)
		{
			throw new ArgumentOutOfRangeException(nameof(interval), "Auto-commit interval must be positive.");
		}

		_options.AutoCommitInterval = interval;
		return this;
	}

	/// <inheritdoc/>
	public IKafkaConsumerBuilder SessionTimeout(TimeSpan timeout)
	{
		if (timeout <= TimeSpan.Zero)
		{
			throw new ArgumentOutOfRangeException(nameof(timeout), "Session timeout must be positive.");
		}

		_options.SessionTimeout = timeout;
		return this;
	}

	/// <inheritdoc/>
	public IKafkaConsumerBuilder MaxPollInterval(TimeSpan interval)
	{
		if (interval <= TimeSpan.Zero)
		{
			throw new ArgumentOutOfRangeException(nameof(interval), "Max poll interval must be positive.");
		}

		_options.MaxPollInterval = interval;
		return this;
	}

	/// <inheritdoc/>
	public IKafkaConsumerBuilder MaxBatchSize(int maxMessages)
	{
		if (maxMessages <= 0)
		{
			throw new ArgumentOutOfRangeException(nameof(maxMessages), "Max batch size must be greater than zero.");
		}

		_options.MaxBatchSize = maxMessages;
		return this;
	}

	/// <inheritdoc/>
	public IKafkaConsumerBuilder WithConfig(string key, string value)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(key);
		_options.AdditionalConfig[key] = value;
		return this;
	}
}
