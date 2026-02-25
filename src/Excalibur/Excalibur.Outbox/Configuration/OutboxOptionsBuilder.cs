// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Outbox;

/// <summary>
/// Internal implementation of <see cref="IOutboxOptionsBuilder"/>.
/// </summary>
internal sealed class OutboxOptionsBuilder : IOutboxOptionsBuilder
{
	private readonly OutboxPreset _preset;
	private int _batchSize;
	private TimeSpan _pollingInterval;
	private int _maxRetryCount;
	private TimeSpan _retryDelay;
	private TimeSpan _messageRetentionPeriod;
	private bool _enableAutomaticCleanup;
	private TimeSpan _cleanupInterval;
	private bool _enableBackgroundProcessing;
	private string? _processorId;
	private bool _enableParallelProcessing;
	private int _maxDegreeOfParallelism;

	private OutboxOptionsBuilder(OutboxPreset preset)
	{
		_preset = preset;
		ApplyPreset(preset);
	}

	/// <summary>
	/// Creates a builder from the specified preset.
	/// </summary>
	/// <param name="preset">The preset to apply.</param>
	/// <returns>A new builder instance.</returns>
	public static OutboxOptionsBuilder FromPreset(OutboxPreset preset) => new(preset);

	/// <inheritdoc/>
	public IOutboxOptionsBuilder WithProcessorId(string processorId)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(processorId);
		_processorId = processorId;
		return this;
	}

	/// <inheritdoc/>
	public IOutboxOptionsBuilder EnableBackgroundProcessing(bool enable = true)
	{
		_enableBackgroundProcessing = enable;
		return this;
	}

	/// <inheritdoc/>
	public IOutboxOptionsBuilder WithBatchSize(int batchSize)
	{
		ArgumentOutOfRangeException.ThrowIfLessThan(batchSize, 1);
		ArgumentOutOfRangeException.ThrowIfGreaterThan(batchSize, 10000);
		_batchSize = batchSize;
		return this;
	}

	/// <inheritdoc/>
	public IOutboxOptionsBuilder WithPollingInterval(TimeSpan interval)
	{
		if (interval < TimeSpan.FromMilliseconds(10))
		{
			throw new ArgumentOutOfRangeException(nameof(interval), interval,
				"PollingInterval must be at least 10ms.");
		}

		_pollingInterval = interval;
		return this;
	}

	/// <inheritdoc/>
	public IOutboxOptionsBuilder WithParallelism(int maxDegree)
	{
		ArgumentOutOfRangeException.ThrowIfLessThan(maxDegree, 1);
		_enableParallelProcessing = maxDegree > 1;
		_maxDegreeOfParallelism = maxDegree;
		return this;
	}

	/// <inheritdoc/>
	public IOutboxOptionsBuilder WithMaxRetries(int maxRetries)
	{
		ArgumentOutOfRangeException.ThrowIfNegative(maxRetries);
		_maxRetryCount = maxRetries;
		return this;
	}

	/// <inheritdoc/>
	public IOutboxOptionsBuilder WithRetryDelay(TimeSpan delay)
	{
		if (delay <= TimeSpan.Zero)
		{
			throw new ArgumentOutOfRangeException(nameof(delay), delay,
				"RetryDelay must be positive.");
		}

		_retryDelay = delay;
		return this;
	}

	/// <inheritdoc/>
	public IOutboxOptionsBuilder WithRetentionPeriod(TimeSpan period)
	{
		if (period <= TimeSpan.Zero)
		{
			throw new ArgumentOutOfRangeException(nameof(period), period,
				"RetentionPeriod must be positive.");
		}

		_messageRetentionPeriod = period;
		return this;
	}

	/// <inheritdoc/>
	public IOutboxOptionsBuilder WithCleanupInterval(TimeSpan interval)
	{
		if (interval <= TimeSpan.Zero)
		{
			throw new ArgumentOutOfRangeException(nameof(interval), interval,
				"CleanupInterval must be positive.");
		}

		_cleanupInterval = interval;
		return this;
	}

	/// <inheritdoc/>
	public IOutboxOptionsBuilder DisableAutomaticCleanup()
	{
		_enableAutomaticCleanup = false;
		return this;
	}

	/// <inheritdoc/>
	public OutboxOptions Build()
	{
		Validate();

		return new OutboxOptions(
			_preset,
			_batchSize,
			_pollingInterval,
			_maxRetryCount,
			_retryDelay,
			_messageRetentionPeriod,
			_enableAutomaticCleanup,
			_cleanupInterval,
			_enableBackgroundProcessing,
			_processorId,
			_enableParallelProcessing,
			_maxDegreeOfParallelism);
	}

	private void ApplyPreset(OutboxPreset preset)
	{
		// Set common defaults
		_enableAutomaticCleanup = true;
		_enableBackgroundProcessing = true;

		switch (preset)
		{
			case OutboxPreset.HighThroughput:
				_batchSize = 1000;
				_pollingInterval = TimeSpan.FromMilliseconds(100);
				_maxRetryCount = 3;
				_retryDelay = TimeSpan.FromMinutes(1);
				_enableParallelProcessing = true;
				_maxDegreeOfParallelism = 8;
				_messageRetentionPeriod = TimeSpan.FromDays(1);
				_cleanupInterval = TimeSpan.FromMinutes(15);
				break;

			case OutboxPreset.Balanced:
				_batchSize = 100;
				_pollingInterval = TimeSpan.FromSeconds(1);
				_maxRetryCount = 5;
				_retryDelay = TimeSpan.FromMinutes(5);
				_enableParallelProcessing = true;
				_maxDegreeOfParallelism = 4;
				_messageRetentionPeriod = TimeSpan.FromDays(7);
				_cleanupInterval = TimeSpan.FromHours(1);
				break;

			case OutboxPreset.HighReliability:
				_batchSize = 10;
				_pollingInterval = TimeSpan.FromSeconds(5);
				_maxRetryCount = 10;
				_retryDelay = TimeSpan.FromMinutes(15);
				_enableParallelProcessing = false;
				_maxDegreeOfParallelism = 1;
				_messageRetentionPeriod = TimeSpan.FromDays(30);
				_cleanupInterval = TimeSpan.FromHours(6);
				break;

			case OutboxPreset.Custom:
			default:
				// Use Balanced defaults as the base for Custom
				_batchSize = 100;
				_pollingInterval = TimeSpan.FromSeconds(5);
				_maxRetryCount = 3;
				_retryDelay = TimeSpan.FromMinutes(5);
				_enableParallelProcessing = false;
				_maxDegreeOfParallelism = 4;
				_messageRetentionPeriod = TimeSpan.FromDays(7);
				_cleanupInterval = TimeSpan.FromHours(1);
				break;
		}
	}

	private void Validate()
	{
		if (_batchSize < 1)
		{
			throw new InvalidOperationException("BatchSize must be at least 1.");
		}

		if (_batchSize > 10000)
		{
			throw new InvalidOperationException("BatchSize cannot exceed 10000.");
		}

		if (_pollingInterval < TimeSpan.FromMilliseconds(10))
		{
			throw new InvalidOperationException("PollingInterval must be at least 10ms.");
		}

		if (_maxRetryCount < 0)
		{
			throw new InvalidOperationException("MaxRetryCount cannot be negative.");
		}

		if (_maxDegreeOfParallelism < 1)
		{
			throw new InvalidOperationException("MaxDegreeOfParallelism must be at least 1.");
		}

		if (_enableAutomaticCleanup && _messageRetentionPeriod < _cleanupInterval)
		{
			throw new InvalidOperationException(
				"RetentionPeriod must be greater than or equal to CleanupInterval when automatic cleanup is enabled.");
		}

		if (_retryDelay <= TimeSpan.Zero)
		{
			throw new InvalidOperationException("RetryDelay must be positive.");
		}

		if (_messageRetentionPeriod <= TimeSpan.Zero)
		{
			throw new InvalidOperationException("RetentionPeriod must be positive.");
		}

		if (_enableAutomaticCleanup && _cleanupInterval <= TimeSpan.Zero)
		{
			throw new InvalidOperationException("CleanupInterval must be positive when automatic cleanup is enabled.");
		}
	}
}
