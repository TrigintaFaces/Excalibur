// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.ComponentModel.DataAnnotations;

namespace Excalibur.Data.DataProcessing;

/// <summary>
/// Represents the configuration settings for data processing tasks.
/// </summary>
public sealed class DataProcessingOptions
{
	private readonly int _dispatcherTimeoutMilliseconds = DataProcessorDefaultDispatcherTimeout;
	private readonly int _maxAttempts = DataProcessorDefaultMaxAttempts;
	private readonly string _schemaName = DataProcessorDefaultSchemaName;
	private readonly string _tableName = DataProcessorDefaultTableName;
	private readonly int _queueSize = DataProcessorDefaultQueueSize;
	private readonly int _producerBatchSize = DataProcessorDefaultProducerBatchSize;
	private readonly int _consumerBatchSize = DataProcessorDefaultConsumerBatchSize;

	/// <summary>
	/// Gets the schema name for the data task requests table.
	/// </summary>
	/// <value>The default schema name is "DataProcessor".</value>
	[Required]
	public string SchemaName
	{
		get => _schemaName;
		init
		{
			ArgumentException.ThrowIfNullOrWhiteSpace(value, nameof(SchemaName));

			_schemaName = value;
		}
	}

	/// <summary>
	/// Gets the name of the table used to store data task requests.
	/// </summary>
	/// <value>The default table name is "DataTaskRequests".</value>
	[Required]
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
	/// Gets the fully qualified table name (schema + table) with bracket-escaping.
	/// </summary>
	public string QualifiedTableName => $"[{SchemaName}].[{TableName}]";

	/// <summary>
	/// Gets the timeout, in milliseconds, for a dispatcher to process data tasks.
	/// </summary>
	/// <value> The timeout must be greater than 0. The default value is 60000 milliseconds (60 seconds). </value>
	/// <exception cref="ArgumentOutOfRangeException"> Thrown when the value is less than or equal to 0. </exception>
	[Range(1, int.MaxValue)]
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
	/// Gets the maximum number of attempts allowed for processing a single data task.
	/// </summary>
	/// <value> The maximum attempts must be greater than 0. The default value is 3 attempts. </value>
	/// <exception cref="ArgumentOutOfRangeException"> Thrown when the value is less than or equal to 0. </exception>
	[Range(1, int.MaxValue)]
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
	/// Gets the maximum queue size for the in-memory queue. Defaults to 500 if not provided.
	/// </summary>
	/// <value>
	/// The maximum queue size for the in-memory queue. Defaults to 5000 if not provided.
	/// </value>
	[Range(1, int.MaxValue)]
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
	/// Gets the batch size for reserving records in the producer loop. Defaults to 100 if not provided.
	/// </summary>
	/// <value>
	/// The batch size for reserving records in the producer loop. Defaults to 100 if not provided.
	/// </value>
	[Range(1, int.MaxValue)]
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
	/// Gets the batch size for dequeuing records in the consumer loop. Defaults to 10 if not provided.
	/// </summary>
	/// <value>
	/// The batch size for dequeuing records in the consumer loop. Defaults to 10 if not provided.
	/// </value>
	[Range(1, int.MaxValue)]
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
