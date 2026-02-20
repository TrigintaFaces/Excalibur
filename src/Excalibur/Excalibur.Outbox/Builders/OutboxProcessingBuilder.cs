// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Outbox;

/// <summary>
/// Internal implementation of the outbox processing builder.
/// </summary>
internal sealed class OutboxProcessingBuilder : IOutboxProcessingBuilder
{
	private readonly OutboxConfiguration _config;

	/// <summary>
	/// Initializes a new instance of the <see cref="OutboxProcessingBuilder"/> class.
	/// </summary>
	/// <param name="config">The outbox configuration to modify.</param>
	public OutboxProcessingBuilder(OutboxConfiguration config)
	{
		_config = config ?? throw new ArgumentNullException(nameof(config));
	}

	/// <inheritdoc/>
	public IOutboxProcessingBuilder BatchSize(int size)
	{
		ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(size, 0);
		_config.BatchSize = size;
		return this;
	}

	/// <inheritdoc/>
	public IOutboxProcessingBuilder PollingInterval(TimeSpan interval)
	{
		if (interval <= TimeSpan.Zero)
		{
			throw new ArgumentOutOfRangeException(nameof(interval), interval, "Polling interval must be positive.");
		}

		_config.PollingInterval = interval;
		return this;
	}

	/// <inheritdoc/>
	public IOutboxProcessingBuilder MaxRetryCount(int count)
	{
		ArgumentOutOfRangeException.ThrowIfNegative(count);
		_config.MaxRetryCount = count;
		return this;
	}

	/// <inheritdoc/>
	public IOutboxProcessingBuilder RetryDelay(TimeSpan delay)
	{
		if (delay <= TimeSpan.Zero)
		{
			throw new ArgumentOutOfRangeException(nameof(delay), delay, "Retry delay must be positive.");
		}

		_config.RetryDelay = delay;
		return this;
	}

	/// <inheritdoc/>
	public IOutboxProcessingBuilder ProcessorId(string processorId)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(processorId);
		_config.ProcessorId = processorId;
		return this;
	}

	/// <inheritdoc/>
	public IOutboxProcessingBuilder EnableParallelProcessing(int maxDegreeOfParallelism = 4)
	{
		ArgumentOutOfRangeException.ThrowIfLessThan(maxDegreeOfParallelism, 1);
		_config.EnableParallelProcessing = true;
		_config.MaxDegreeOfParallelism = maxDegreeOfParallelism;
		return this;
	}
}
