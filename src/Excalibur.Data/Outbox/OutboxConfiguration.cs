namespace Excalibur.Data.Outbox;

/// <summary>
///     Represents the configuration settings for the outbox system.
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
	///     Gets the name of the table used for storing outbox messages.
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
	///     Gets the name of the dead-letter table for storing messages that failed to dispatch.
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
	///     Gets the timeout, in milliseconds, for the dispatcher to reserve messages.
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
	///     Gets the maximum number of attempts allowed for processing a message before it is moved to the dead-letter table.
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
	///     Gets the maximum queue size for the in-memory queue. Defaults to 500 if not provided.
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
	///     Gets the batch size for reserving records in the producer loop. Defaults to 50 if not provided.
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
	///     Gets the batch size for dequeuing records in the consumer loop. Defaults to 100 if not provided.
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
