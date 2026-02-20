// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Transport.Aws;

/// <summary>
/// Configuration options for standard AWS SQS queue settings.
/// </summary>
/// <remarks>
/// <para>
/// These settings control how messages are handled in the SQS queue, including
/// visibility timeout, message retention, and optional dead-letter queue configuration.
/// </para>
/// <para>
/// All values are validated against AWS SQS constraints when set. Invalid values
/// will throw <see cref="ArgumentOutOfRangeException"/> immediately (fail fast).
/// </para>
/// </remarks>
public sealed class AwsSqsQueueOptions
{
	#region SQS Constraints

	/// <summary>
	/// The minimum visibility timeout allowed by SQS (0 seconds).
	/// </summary>
	public static readonly TimeSpan MinVisibilityTimeout = TimeSpan.Zero;

	/// <summary>
	/// The maximum visibility timeout allowed by SQS (12 hours).
	/// </summary>
	public static readonly TimeSpan MaxVisibilityTimeout = TimeSpan.FromHours(12);

	/// <summary>
	/// The default visibility timeout (30 seconds).
	/// </summary>
	public static readonly TimeSpan DefaultVisibilityTimeout = TimeSpan.FromSeconds(30);

	/// <summary>
	/// The minimum message retention period allowed by SQS (1 minute).
	/// </summary>
	public static readonly TimeSpan MinMessageRetentionPeriod = TimeSpan.FromMinutes(1);

	/// <summary>
	/// The maximum message retention period allowed by SQS (14 days).
	/// </summary>
	public static readonly TimeSpan MaxMessageRetentionPeriod = TimeSpan.FromDays(14);

	/// <summary>
	/// The default message retention period (4 days).
	/// </summary>
	public static readonly TimeSpan DefaultMessageRetentionPeriod = TimeSpan.FromDays(4);

	/// <summary>
	/// The minimum receive wait time (0 seconds - short polling).
	/// </summary>
	public const int MinReceiveWaitTimeSeconds = 0;

	/// <summary>
	/// The maximum receive wait time (20 seconds - long polling).
	/// </summary>
	public const int MaxReceiveWaitTimeSeconds = 20;

	/// <summary>
	/// The default receive wait time (0 seconds - short polling).
	/// </summary>
	public const int DefaultReceiveWaitTimeSeconds = 0;

	/// <summary>
	/// The minimum delay seconds (0 - no delay).
	/// </summary>
	public const int MinDelaySeconds = 0;

	/// <summary>
	/// The maximum delay seconds (900 - 15 minutes).
	/// </summary>
	public const int MaxDelaySeconds = 900;

	/// <summary>
	/// The default delay seconds (0 - no delay).
	/// </summary>
	public const int DefaultDelaySeconds = 0;

	#endregion

	private TimeSpan _visibilityTimeout = DefaultVisibilityTimeout;
	private TimeSpan _messageRetentionPeriod = DefaultMessageRetentionPeriod;
	private int _receiveWaitTimeSeconds = DefaultReceiveWaitTimeSeconds;
	private int _delaySeconds = DefaultDelaySeconds;

	/// <summary>
	/// Gets or sets the visibility timeout for messages in the queue.
	/// </summary>
	/// <value>
	/// A value between 0 seconds and 12 hours. Default is 30 seconds.
	/// </value>
	/// <exception cref="ArgumentOutOfRangeException">
	/// Thrown when the value is less than 0 or greater than 12 hours.
	/// </exception>
	/// <remarks>
	/// <para>
	/// The visibility timeout is the length of time that a message received from a queue
	/// will be invisible to other receiving components. During this time, the original
	/// consumer can process and delete the message.
	/// </para>
	/// <para>
	/// If the message is not deleted within the visibility timeout, it becomes visible
	/// again and may be received by another consumer.
	/// </para>
	/// </remarks>
	public TimeSpan VisibilityTimeout
	{
		get => _visibilityTimeout;
		set
		{
			if (value < MinVisibilityTimeout)
			{
				throw new ArgumentOutOfRangeException(
					nameof(value),
					value,
					$"Visibility timeout must be at least {MinVisibilityTimeout.TotalSeconds} seconds.");
			}

			if (value > MaxVisibilityTimeout)
			{
				throw new ArgumentOutOfRangeException(
					nameof(value),
					value,
					$"Visibility timeout cannot exceed {MaxVisibilityTimeout.TotalHours} hours.");
			}

			_visibilityTimeout = value;
		}
	}

	/// <summary>
	/// Gets or sets the message retention period for the queue.
	/// </summary>
	/// <value>
	/// A value between 1 minute and 14 days. Default is 4 days.
	/// </value>
	/// <exception cref="ArgumentOutOfRangeException">
	/// Thrown when the value is less than 1 minute or greater than 14 days.
	/// </exception>
	/// <remarks>
	/// <para>
	/// The message retention period is the length of time that SQS retains a message
	/// in the queue. After this period, the message is automatically deleted.
	/// </para>
	/// <para>
	/// This setting affects all messages in the queue, not just individual messages.
	/// </para>
	/// </remarks>
	public TimeSpan MessageRetentionPeriod
	{
		get => _messageRetentionPeriod;
		set
		{
			if (value < MinMessageRetentionPeriod)
			{
				throw new ArgumentOutOfRangeException(
					nameof(value),
					value,
					$"Message retention period must be at least {MinMessageRetentionPeriod.TotalMinutes} minute(s).");
			}

			if (value > MaxMessageRetentionPeriod)
			{
				throw new ArgumentOutOfRangeException(
					nameof(value),
					value,
					$"Message retention period cannot exceed {MaxMessageRetentionPeriod.TotalDays} days.");
			}

			_messageRetentionPeriod = value;
		}
	}

	/// <summary>
	/// Gets or sets the receive wait time in seconds for long polling.
	/// </summary>
	/// <value>
	/// A value between 0 and 20 seconds. Default is 0 (short polling).
	/// </value>
	/// <exception cref="ArgumentOutOfRangeException">
	/// Thrown when the value is less than 0 or greater than 20.
	/// </exception>
	/// <remarks>
	/// <para>
	/// When set to a value greater than 0, enables long polling which reduces the cost
	/// of using SQS by eliminating the number of empty responses and false empty responses.
	/// </para>
	/// <para>
	/// Long polling (up to 20 seconds) is recommended for most use cases as it reduces
	/// costs and allows consumers to receive messages as soon as they arrive.
	/// </para>
	/// </remarks>
	public int ReceiveWaitTimeSeconds
	{
		get => _receiveWaitTimeSeconds;
		set
		{
			ArgumentOutOfRangeException.ThrowIfLessThan(value, MinReceiveWaitTimeSeconds);
			ArgumentOutOfRangeException.ThrowIfGreaterThan(value, MaxReceiveWaitTimeSeconds);
			_receiveWaitTimeSeconds = value;
		}
	}

	/// <summary>
	/// Gets or sets the delay in seconds before messages become available for consumption.
	/// </summary>
	/// <value>
	/// A value between 0 and 900 seconds (15 minutes). Default is 0 (no delay).
	/// </value>
	/// <exception cref="ArgumentOutOfRangeException">
	/// Thrown when the value is less than 0 or greater than 900.
	/// </exception>
	/// <remarks>
	/// <para>
	/// When set to a value greater than 0, messages sent to the queue will be invisible
	/// for the specified number of seconds before becoming available for consumption.
	/// </para>
	/// <para>
	/// This is useful for implementing delayed message processing scenarios.
	/// </para>
	/// </remarks>
	public int DelaySeconds
	{
		get => _delaySeconds;
		set
		{
			ArgumentOutOfRangeException.ThrowIfLessThan(value, MinDelaySeconds);
			ArgumentOutOfRangeException.ThrowIfGreaterThan(value, MaxDelaySeconds);
			_delaySeconds = value;
		}
	}

	/// <summary>
	/// Gets or sets the dead-letter queue configuration.
	/// </summary>
	/// <value>
	/// The dead-letter queue options, or <see langword="null"/> if no DLQ is configured.
	/// </value>
	/// <remarks>
	/// <para>
	/// When configured, messages that fail processing after the specified maximum receive count
	/// are automatically moved to the dead-letter queue for later analysis or reprocessing.
	/// </para>
	/// </remarks>
	public AwsSqsDeadLetterOptions? DeadLetterQueue { get; set; }

	/// <summary>
	/// Gets a value indicating whether a dead-letter queue is configured.
	/// </summary>
	/// <value>
	/// <see langword="true"/> if a dead-letter queue is configured; otherwise, <see langword="false"/>.
	/// </value>
	public bool HasDeadLetterQueue => DeadLetterQueue is not null;
}
