// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Outbox;

/// <summary>
/// Internal implementation of <see cref="IInboxOptionsBuilder"/>.
/// </summary>
internal sealed class InboxOptionsBuilder : IInboxOptionsBuilder
{
	private readonly InboxPreset _preset;
	private int _queueCapacity;
	private int _producerBatchSize;
	private int _consumerBatchSize;
	private int _perRunTotal;
	private int _maxAttempts;
	private int _parallelProcessingDegree;
	private TimeSpan _batchProcessingTimeout;
	private TimeSpan? _defaultMessageTtl;
	private bool _enableDynamicBatchSizing;
	private int _minBatchSize;
	private int _maxBatchSize;
	private bool _enableBatchDatabaseOperations;

	private InboxOptionsBuilder(InboxPreset preset)
	{
		_preset = preset;
		ApplyPreset(preset);
	}

	/// <summary>
	/// Creates a builder from the specified preset.
	/// </summary>
	/// <param name="preset">The preset to apply.</param>
	/// <returns>A new builder instance.</returns>
	public static InboxOptionsBuilder FromPreset(InboxPreset preset) => new(preset);

	/// <inheritdoc/>
	public IInboxOptionsBuilder WithQueueCapacity(int capacity)
	{
		ArgumentOutOfRangeException.ThrowIfLessThan(capacity, 1);
		ArgumentOutOfRangeException.ThrowIfGreaterThan(capacity, 100000);
		_queueCapacity = capacity;
		return this;
	}

	/// <inheritdoc/>
	public IInboxOptionsBuilder WithProducerBatchSize(int batchSize)
	{
		ArgumentOutOfRangeException.ThrowIfLessThan(batchSize, 1);
		ArgumentOutOfRangeException.ThrowIfGreaterThan(batchSize, 10000);
		_producerBatchSize = batchSize;
		return this;
	}

	/// <inheritdoc/>
	public IInboxOptionsBuilder WithConsumerBatchSize(int batchSize)
	{
		ArgumentOutOfRangeException.ThrowIfLessThan(batchSize, 1);
		ArgumentOutOfRangeException.ThrowIfGreaterThan(batchSize, 10000);
		_consumerBatchSize = batchSize;
		return this;
	}

	/// <inheritdoc/>
	public IInboxOptionsBuilder WithPerRunTotal(int total)
	{
		ArgumentOutOfRangeException.ThrowIfLessThan(total, 1);
		_perRunTotal = total;
		return this;
	}

	/// <inheritdoc/>
	public IInboxOptionsBuilder WithMaxAttempts(int maxAttempts)
	{
		ArgumentOutOfRangeException.ThrowIfLessThan(maxAttempts, 1);
		_maxAttempts = maxAttempts;
		return this;
	}

	/// <inheritdoc/>
	public IInboxOptionsBuilder WithParallelism(int maxDegree)
	{
		ArgumentOutOfRangeException.ThrowIfLessThan(maxDegree, 1);
		_parallelProcessingDegree = maxDegree;
		return this;
	}

	/// <inheritdoc/>
	public IInboxOptionsBuilder WithBatchProcessingTimeout(TimeSpan timeout)
	{
		if (timeout <= TimeSpan.Zero)
		{
			throw new ArgumentOutOfRangeException(nameof(timeout), timeout,
				"BatchProcessingTimeout must be positive.");
		}

		_batchProcessingTimeout = timeout;
		return this;
	}

	/// <inheritdoc/>
	public IInboxOptionsBuilder WithDefaultMessageTtl(TimeSpan? ttl)
	{
		if (ttl.HasValue && ttl.Value <= TimeSpan.Zero)
		{
			throw new ArgumentOutOfRangeException(nameof(ttl), ttl,
				"DefaultMessageTtl must be positive when specified.");
		}

		_defaultMessageTtl = ttl;
		return this;
	}

	/// <inheritdoc/>
	public IInboxOptionsBuilder EnableDynamicBatchSizing(int minBatchSize = 10, int maxBatchSize = 1000)
	{
		ArgumentOutOfRangeException.ThrowIfLessThan(minBatchSize, 1);
		ArgumentOutOfRangeException.ThrowIfLessThan(maxBatchSize, minBatchSize);

		_enableDynamicBatchSizing = true;
		_minBatchSize = minBatchSize;
		_maxBatchSize = maxBatchSize;
		return this;
	}

	/// <inheritdoc/>
	public IInboxOptionsBuilder DisableBatchDatabaseOperations()
	{
		_enableBatchDatabaseOperations = false;
		return this;
	}

	/// <inheritdoc/>
	public InboxOptions Build()
	{
		Validate();

		return new InboxOptions(
			_preset,
			_queueCapacity,
			_producerBatchSize,
			_consumerBatchSize,
			_perRunTotal,
			_maxAttempts,
			_parallelProcessingDegree,
			_batchProcessingTimeout,
			_defaultMessageTtl,
			_enableDynamicBatchSizing,
			_minBatchSize,
			_maxBatchSize,
			_enableBatchDatabaseOperations);
	}

	private void ApplyPreset(InboxPreset preset)
	{
		// Common defaults
		_enableBatchDatabaseOperations = true;
		_enableDynamicBatchSizing = false;
		_minBatchSize = 10;
		_maxBatchSize = 1000;
		_defaultMessageTtl = null;

		switch (preset)
		{
			case InboxPreset.HighThroughput:
				_queueCapacity = 2000;
				_producerBatchSize = 500;
				_consumerBatchSize = 200;
				_perRunTotal = 5000;
				_maxAttempts = 3;
				_parallelProcessingDegree = 8;
				_batchProcessingTimeout = TimeSpan.FromMinutes(2);
				break;

			case InboxPreset.Balanced:
				_queueCapacity = 500;
				_producerBatchSize = 100;
				_consumerBatchSize = 50;
				_perRunTotal = 1000;
				_maxAttempts = 5;
				_parallelProcessingDegree = 4;
				_batchProcessingTimeout = TimeSpan.FromMinutes(5);
				break;

			case InboxPreset.HighReliability:
				_queueCapacity = 100;
				_producerBatchSize = 20;
				_consumerBatchSize = 10;
				_perRunTotal = 200;
				_maxAttempts = 10;
				_parallelProcessingDegree = 1;
				_batchProcessingTimeout = TimeSpan.FromMinutes(10);
				break;

			case InboxPreset.Custom:
			default:
				// Use Balanced defaults as the base
				_queueCapacity = 500;
				_producerBatchSize = 100;
				_consumerBatchSize = 50;
				_perRunTotal = 1000;
				_maxAttempts = 5;
				_parallelProcessingDegree = 4;
				_batchProcessingTimeout = TimeSpan.FromMinutes(5);
				break;
		}
	}

	private void Validate()
	{
		if (_queueCapacity < 1)
		{
			throw new InvalidOperationException("QueueCapacity must be at least 1.");
		}

		if (_producerBatchSize < 1)
		{
			throw new InvalidOperationException("ProducerBatchSize must be at least 1.");
		}

		if (_consumerBatchSize < 1)
		{
			throw new InvalidOperationException("ConsumerBatchSize must be at least 1.");
		}

		if (_perRunTotal < 1)
		{
			throw new InvalidOperationException("PerRunTotal must be at least 1.");
		}

		if (_maxAttempts < 1)
		{
			throw new InvalidOperationException("MaxAttempts must be at least 1.");
		}

		if (_parallelProcessingDegree < 1)
		{
			throw new InvalidOperationException("ParallelProcessingDegree must be at least 1.");
		}

		if (_queueCapacity < _producerBatchSize)
		{
			throw new InvalidOperationException(
				"QueueCapacity must be greater than or equal to ProducerBatchSize.");
		}

		if (_batchProcessingTimeout <= TimeSpan.Zero)
		{
			throw new InvalidOperationException("BatchProcessingTimeout must be positive.");
		}

		if (_enableDynamicBatchSizing && _minBatchSize > _maxBatchSize)
		{
			throw new InvalidOperationException(
				"MinBatchSize cannot be greater than MaxBatchSize when dynamic batch sizing is enabled.");
		}
	}
}
