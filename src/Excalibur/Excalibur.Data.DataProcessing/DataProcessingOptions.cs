// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.ComponentModel.DataAnnotations;

using Excalibur.Data.Validation;

namespace Excalibur.Data.DataProcessing;

/// <summary>
/// Represents the configuration settings for data processing tasks.
/// </summary>
public sealed class DataProcessingOptions
{
	private readonly TimeSpan _processingStallTimeout = DataProcessorDefaults.ProcessingStallTimeout;
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
			SqlIdentifierValidator.ThrowIfInvalid(value, nameof(SchemaName));

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
			SqlIdentifierValidator.ThrowIfInvalid(value, nameof(TableName));

			_tableName = value;
		}
	}

	/// <summary>
	/// Gets the fully qualified table name (schema + table) with bracket-escaping.
	/// </summary>
	public string QualifiedTableName => $"[{SchemaName}].[{TableName}]";

	/// <summary>
	/// Gets how long one claimed task may make NO PROGRESS before it is cancelled.
	/// </summary>
	/// <value>Must be greater than zero. The default is 60 seconds.</value>
	/// <exception cref="ArgumentOutOfRangeException">The value is zero or negative.</exception>
	/// <remarks>
	/// <para>
	/// <b>This clock measures silence, not duration, and the distinction is the whole point.</b> It is
	/// restarted every time the task durably accounts for a record, so a task that is working is never
	/// cancelled for working slowly. A total wall-clock deadline cannot tell a wedged task from a healthy
	/// one that simply has a lot to do: under one, there is always a record count above which the task
	/// fails no matter how fast each record is, and making each record faster only moves that count.
	/// </para>
	/// <para>
	/// Cancellation requests a stop; the manager joins processing before releasing task ownership. A task
	/// cancelled here keeps the progress it checkpointed and resumes from it on the next attempt.
	/// </para>
	/// <para>
	/// Set <see cref="ProcessingTotalTimeout"/> as well if you also need an absolute cap for the genuinely
	/// runaway case — a cursor that never reaches its end. The two describe different faults and are
	/// reported with different messages.
	/// </para>
	/// </remarks>
	public TimeSpan ProcessingStallTimeout
	{
		get => _processingStallTimeout;
		init
		{
			ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(value, TimeSpan.Zero, nameof(ProcessingStallTimeout));

			_processingStallTimeout = value;
		}
	}

	/// <summary>
	/// Gets an optional absolute cap on one claimed task's wall-clock duration, regardless of progress.
	/// </summary>
	/// <value>
	/// <see langword="null"/> by default, meaning a task that keeps making progress is allowed to finish.
	/// </value>
	/// <exception cref="ArgumentOutOfRangeException">The value is zero or negative.</exception>
	/// <remarks>
	/// Leave this unset unless you have a specific runaway to bound. It exists for the case
	/// <see cref="ProcessingStallTimeout"/> cannot catch — work that genuinely never ends while reporting
	/// progress throughout — and setting it re-introduces, deliberately, the failure mode that a task with
	/// more work than the cap always fails.
	/// </remarks>
	public TimeSpan? ProcessingTotalTimeout
	{
		get;
		init
		{
			if (value is { } cap)
			{
				ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(cap, TimeSpan.Zero, nameof(ProcessingTotalTimeout));
			}

			field = value;
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
	/// Gets the maximum queue size for the in-memory bounded channel. Defaults to 5000 if not provided.
	/// </summary>
	/// <value>
	/// The maximum queue size for the in-memory bounded channel. Defaults to 5000 if not provided.
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
