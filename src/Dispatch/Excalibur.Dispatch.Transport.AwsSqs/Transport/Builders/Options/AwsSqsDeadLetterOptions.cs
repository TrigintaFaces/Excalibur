// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Transport.Aws;

/// <summary>
/// Configuration options for AWS SQS dead-letter queue (DLQ) settings.
/// </summary>
/// <remarks>
/// <para>
/// When a message fails processing after the maximum number of receive attempts,
/// it is automatically moved to the configured dead-letter queue for later analysis
/// or reprocessing.
/// </para>
/// <para>
/// The <see cref="QueueArn"/> must reference an existing SQS queue that has been
/// configured as a dead-letter queue for the source queue.
/// </para>
/// </remarks>
public sealed class AwsSqsDeadLetterOptions
{
	/// <summary>
	/// The minimum allowed value for <see cref="MaxReceiveCount"/>.
	/// </summary>
	public const int MinMaxReceiveCount = 1;

	/// <summary>
	/// The maximum allowed value for <see cref="MaxReceiveCount"/>.
	/// </summary>
	public const int MaxMaxReceiveCount = 1000;

	/// <summary>
	/// The default value for <see cref="MaxReceiveCount"/>.
	/// </summary>
	public const int DefaultMaxReceiveCount = 5;

	private string? _queueArn;
	private int _maxReceiveCount = DefaultMaxReceiveCount;

	/// <summary>
	/// Gets or sets the ARN of the dead-letter queue.
	/// </summary>
	/// <value>
	/// The Amazon Resource Name (ARN) of the dead-letter queue.
	/// Must be in the format: arn:aws:sqs:{region}:{account-id}:{queue-name}
	/// </value>
	/// <exception cref="ArgumentException">
	/// Thrown when the value is null, empty, or whitespace.
	/// </exception>
	/// <remarks>
	/// The dead-letter queue must exist in the same AWS region as the source queue.
	/// </remarks>
	public string? QueueArn
	{
		get => _queueArn;
		set
		{
			if (value is not null)
			{
				ArgumentException.ThrowIfNullOrWhiteSpace(value);
			}

			_queueArn = value;
		}
	}

	/// <summary>
	/// Gets or sets the maximum number of times a message is received before being sent to the dead-letter queue.
	/// </summary>
	/// <value>
	/// A value between 1 and 1000. Default is 5.
	/// </value>
	/// <exception cref="ArgumentOutOfRangeException">
	/// Thrown when the value is less than 1 or greater than 1000.
	/// </exception>
	/// <remarks>
	/// <para>
	/// When a consumer receives a message but does not delete it within the visibility timeout,
	/// the message becomes visible again and the receive count is incremented.
	/// </para>
	/// <para>
	/// Once the receive count exceeds <see cref="MaxReceiveCount"/>, the message is moved
	/// to the dead-letter queue.
	/// </para>
	/// </remarks>
	public int MaxReceiveCount
	{
		get => _maxReceiveCount;
		set
		{
			ArgumentOutOfRangeException.ThrowIfLessThan(value, MinMaxReceiveCount);
			ArgumentOutOfRangeException.ThrowIfGreaterThan(value, MaxMaxReceiveCount);
			_maxReceiveCount = value;
		}
	}
}
