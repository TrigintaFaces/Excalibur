// Copyright (c) 2025 The Excalibur Project Authors
//
// Licensed under multiple licenses:
// - Excalibur License 1.0 (see LICENSE-EXCALIBUR.txt)
// - GNU Affero General Public License v3.0 or later (AGPL-3.0) (see LICENSE-AGPL-3.0.txt)
// - Server Side Public License v1.0 (SSPL-1.0) (see LICENSE-SSPL-1.0.txt)
// - Apache License 2.0 (see LICENSE-APACHE-2.0.txt)
//
// You may not use this file except in compliance with the License terms above. You may obtain copies of the licenses in
// the project root or online.
//
// Unless required by applicable law or agreed to in writing, software distributed under the License is distributed on
// an "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.

namespace Excalibur.Data.Outbox;

/// <summary>
///   Represents the configuration settings for the outbox system.
/// </summary>
public class OutboxConfiguration
{
	private readonly string _tableName = OutboxDefaultTableName;
	private readonly string _deadLetterTableName = OutboxDefaultDeadLetterTableName;
	private readonly int _dispatcherTimeoutMilliseconds = OutboxDefaultDispatcherTimeout;
	private readonly int _maxAttempts = OutboxDefaultMaxAttempts;
	private readonly int _queueSize = OutboxDefaultQueueSize;
	private readonly int _producerBatchSize = OutboxDefaultProducerBatchSize;
	private readonly int _consumerBatchSize = OutboxDefaultConsumerBatchSize;

	/// <summary>
	///   Gets the name of the table used for storing outbox messages.
	/// </summary>
	/// <value> A <see cref="string" /> representing the name of the outbox table. </value>
	public string TableName
	{
		get => _tableName;
		init
		{
			ArgumentException.ThrowIfNullOrWhiteSpace(value, nameof(TableName));
			_tableName = value;
		}
	}

	/// <summary>
	///   Gets the name of the dead-letter table for storing messages that failed to dispatch.
	/// </summary>
	/// <value> A <see cref="string" /> representing the name of the dead-letter table. </value>
	public string DeadLetterTableName
	{
		get => _deadLetterTableName;
		init
		{
			ArgumentException.ThrowIfNullOrWhiteSpace(value, nameof(DeadLetterTableName));
			_deadLetterTableName = value;
		}
	}

	/// <summary>
	///   Gets the timeout, in milliseconds, for the dispatcher to reserve messages.
	/// </summary>
	/// <value> An <see cref="int" /> representing the timeout in milliseconds. </value>
	public int DispatcherTimeoutMilliseconds
	{
		get => _dispatcherTimeoutMilliseconds;
		init
		{
			ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(value, 0, nameof(DispatcherTimeoutMilliseconds));

			_dispatcherTimeoutMilliseconds = value;
		}
	}

	/// <summary>
	///   Gets the maximum number of attempts allowed for processing a message before it is moved to the dead-letter table.
	/// </summary>
	/// <value> An <see cref="int" /> representing the maximum number of attempts. </value>
	public int MaxAttempts
	{
		get => _maxAttempts;
		init
		{
			ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(value, 0, nameof(MaxAttempts));

			_maxAttempts = value;
		}
	}

	/// <summary>
	///   Gets the maximum queue size for the in-memory queue. Defaults to 20,000 if not provided. Recommended range is
	///   5,000–20,000 depending on throughput requirements.
	/// </summary>
	public int QueueSize
	{
		get => _queueSize;
		init
		{
			ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(value, 0, nameof(QueueSize));

			_queueSize = value;
		}
	}

	/// <summary>
	///   Gets the batch size for reserving records in the producer loop. Defaults to 500 if not provided. Recommended
	///   range is 100–1,000.
	/// </summary>
	public int ProducerBatchSize
	{
		get => _producerBatchSize;
		init
		{
			ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(value, 0, nameof(ProducerBatchSize));

			_producerBatchSize = value;
		}
	}

	/// <summary>
	///   Gets the batch size for dequeuing records in the consumer loop. Defaults to 250 if not provided. Recommended
	///   range is 50–500.
	/// </summary>
	public int ConsumerBatchSize
	{
		get => _consumerBatchSize;
		init
		{
			ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(value, 0, nameof(ConsumerBatchSize));

			_consumerBatchSize = value;
		}
	}
}
