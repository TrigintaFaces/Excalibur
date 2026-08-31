// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Transport.Google;

/// <summary>
/// Configuration options for ordering key message processing.
/// </summary>
public sealed class OrderingKeyOptions
{
	/// <summary>
	/// Gets or sets the maximum number of ordering keys that can be processed concurrently. Default is the number of processor cores.
	/// </summary>
	/// <value>
	/// The maximum number of ordering keys that can be processed concurrently. Default is the number of processor cores.
	/// </value>
	public int MaxConcurrentOrderingKeys { get; set; } = Environment.ProcessorCount;

	/// <summary>
	/// Gets or sets the maximum number of messages that can be queued per ordering key. Default is 1000.
	/// </summary>
	/// <value>
	/// The maximum number of messages that can be queued per ordering key. Default is 1000.
	/// </value>
	public int MaxMessagesPerOrderingKey { get; set; } = 1000;

	/// <summary>
	/// Gets or sets a value indicating whether to automatically remove empty ordering key queues. Default is true.
	/// </summary>
	/// <value>
	/// A value indicating whether gets or sets whether to automatically remove empty ordering key queues. Default is true.
	/// </value>
	public bool RemoveEmptyQueues { get; set; } = true;

	/// <summary>
	/// Gets or sets the maximum number of retries for failed messages. Only applies when EnforceStrictOrdering is false. Default is 3.
	/// </summary>
	/// <value>
	/// The maximum number of retries for failed messages. Only applies when EnforceStrictOrdering is false. Default is 3.
	/// </value>
	public int MaxRetryAttempts { get; set; } = 3;

	/// <summary>
	/// Gets or sets the delay between retries. Default is 1 second.
	/// </summary>
	/// <value>
	/// The delay between retries. Default is 1 second.
	/// </value>
	public TimeSpan RetryDelay { get; set; } = TimeSpan.FromSeconds(1);

	/// <summary>
	/// Validates the options.
	/// </summary>
	/// <exception cref="ArgumentException"></exception>
	public void Validate()
	{
		if (MaxConcurrentOrderingKeys <= 0)
		{
			throw new ArgumentException(
				"MaxConcurrentOrderingKeys must be greater than 0.",
				nameof(MaxConcurrentOrderingKeys));
		}

		if (MaxMessagesPerOrderingKey <= 0)
		{
			throw new ArgumentException(
				"MaxMessagesPerOrderingKey must be greater than 0.",
				nameof(MaxMessagesPerOrderingKey));
		}

		if (MaxRetryAttempts < 0)
		{
			throw new ArgumentException(
				"MaxRetryAttempts must be non-negative.",
				nameof(MaxRetryAttempts));
		}

		if (RetryDelay < TimeSpan.Zero)
		{
			throw new ArgumentException(
				"RetryDelay must be non-negative.",
				nameof(RetryDelay));
		}
	}
}
