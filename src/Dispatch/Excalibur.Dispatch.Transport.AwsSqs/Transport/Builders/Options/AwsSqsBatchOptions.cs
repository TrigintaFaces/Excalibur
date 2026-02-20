// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Transport.Aws;

/// <summary>
/// Configuration options for AWS SQS batch operations.
/// </summary>
/// <remarks>
/// <para>
/// Batch operations improve throughput and reduce costs by sending or receiving
/// multiple messages in a single API call. AWS SQS limits batch sizes to 10 messages.
/// </para>
/// <para>
/// All values are validated against AWS SQS constraints when set. Invalid values
/// throw <see cref="ArgumentOutOfRangeException"/> immediately (fail fast).
/// </para>
/// </remarks>
public sealed class AwsSqsBatchOptions
{
	#region SQS Constraints

	/// <summary>
	/// The minimum batch size (1 message).
	/// </summary>
	public const int MinBatchSize = 1;

	/// <summary>
	/// The maximum batch size allowed by SQS (10 messages).
	/// </summary>
	public const int MaxBatchSize = 10;

	/// <summary>
	/// The default send batch size (10 messages).
	/// </summary>
	public const int DefaultSendBatchSize = 10;

	/// <summary>
	/// The default receive max messages (10 messages).
	/// </summary>
	public const int DefaultReceiveMaxMessages = 10;

	/// <summary>
	/// The minimum batch window (no batching).
	/// </summary>
	public static readonly TimeSpan MinSendBatchWindow = TimeSpan.Zero;

	/// <summary>
	/// The maximum recommended batch window (1 second).
	/// </summary>
	/// <remarks>
	/// While there's no hard limit, batching beyond 1 second typically increases
	/// latency without significant cost benefits.
	/// </remarks>
	public static readonly TimeSpan MaxRecommendedSendBatchWindow = TimeSpan.FromSeconds(1);

	/// <summary>
	/// The default send batch window (100 milliseconds).
	/// </summary>
	public static readonly TimeSpan DefaultSendBatchWindow = TimeSpan.FromMilliseconds(100);

	#endregion

	private int _sendBatchSize = DefaultSendBatchSize;
	private TimeSpan _sendBatchWindow = DefaultSendBatchWindow;
	private int _receiveMaxMessages = DefaultReceiveMaxMessages;

	/// <summary>
	/// Gets or sets the maximum number of messages to send in a single batch.
	/// </summary>
	/// <value>
	/// A value between 1 and 10. Default is 10.
	/// </value>
	/// <exception cref="ArgumentOutOfRangeException">
	/// Thrown when the value is less than 1 or greater than 10.
	/// </exception>
	/// <remarks>
	/// <para>
	/// Batch sending reduces API calls and costs. Messages are accumulated until
	/// either the batch size is reached or the <see cref="SendBatchWindow"/> expires.
	/// </para>
	/// </remarks>
	public int SendBatchSize
	{
		get => _sendBatchSize;
		set
		{
			ArgumentOutOfRangeException.ThrowIfLessThan(value, MinBatchSize);
			ArgumentOutOfRangeException.ThrowIfGreaterThan(value, MaxBatchSize);
			_sendBatchSize = value;
		}
	}

	/// <summary>
	/// Gets or sets the time window for accumulating messages before sending a batch.
	/// </summary>
	/// <value>
	/// A non-negative time span. Default is 100 milliseconds.
	/// </value>
	/// <exception cref="ArgumentOutOfRangeException">
	/// Thrown when the value is negative.
	/// </exception>
	/// <remarks>
	/// <para>
	/// When set to <see cref="TimeSpan.Zero"/>, messages are sent immediately without batching.
	/// Increasing this value allows more messages to be accumulated in a single batch,
	/// reducing costs but increasing latency.
	/// </para>
	/// </remarks>
	public TimeSpan SendBatchWindow
	{
		get => _sendBatchWindow;
		set
		{
			if (value < MinSendBatchWindow)
			{
				throw new ArgumentOutOfRangeException(
					nameof(value),
					value,
					"Send batch window cannot be negative.");
			}

			_sendBatchWindow = value;
		}
	}

	/// <summary>
	/// Gets or sets the maximum number of messages to receive in a single poll.
	/// </summary>
	/// <value>
	/// A value between 1 and 10. Default is 10.
	/// </value>
	/// <exception cref="ArgumentOutOfRangeException">
	/// Thrown when the value is less than 1 or greater than 10.
	/// </exception>
	/// <remarks>
	/// <para>
	/// Higher values improve throughput when processing multiple messages, but may
	/// increase memory usage and processing latency for individual messages.
	/// </para>
	/// </remarks>
	public int ReceiveMaxMessages
	{
		get => _receiveMaxMessages;
		set
		{
			ArgumentOutOfRangeException.ThrowIfLessThan(value, MinBatchSize);
			ArgumentOutOfRangeException.ThrowIfGreaterThan(value, MaxBatchSize);
			_receiveMaxMessages = value;
		}
	}
}
