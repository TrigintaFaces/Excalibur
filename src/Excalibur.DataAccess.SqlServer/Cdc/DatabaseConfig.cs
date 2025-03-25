using System.ComponentModel.DataAnnotations;

namespace Excalibur.DataAccess.SqlServer.Cdc;

/// <summary>
///     Represents the configuration settings required for CDC (Change Data Capture) processing.
/// </summary>
public class DatabaseConfig : IDatabaseConfig
{
	private readonly bool _stopOnMissingTableHandler = CdcDefaultStopOnMissingTableHandler;
	private readonly string[] _captureInstances = CdcDefaultCaptureInstances;
	private readonly int _batchTimeInterval = CdcDefaultBatchTimeInterval;
	private readonly int _queueSize = CdcDefaultQueueSize;
	private readonly int _producerBatchSize = CdcDefaultProducerBatchSize;
	private readonly int _consumerBatchSize = CdcDefaultConsumerBatchSize;

	/// <inheritdoc />
	[Required]
	public required string DatabaseName { get; init; }

	/// <inheritdoc />
	[Required]
	public required string DatabaseConnectionIdentifier { get; init; }

	/// <inheritdoc />
	[Required]
	public required string StateConnectionIdentifier { get; init; }

	/// <inheritdoc />
	[Required]
	public bool StopOnMissingTableHandler
	{
		get => _stopOnMissingTableHandler;
		init
		{
			ArgumentNullException.ThrowIfNull(value, nameof(StopOnMissingTableHandler));
			_stopOnMissingTableHandler = value;
		}
	}

	/// <inheritdoc />
	[Required]
	public string[] CaptureInstances
	{
		get => _captureInstances;
		init
		{
			ArgumentNullException.ThrowIfNull(value, nameof(CaptureInstances));
			_captureInstances = value!;
		}
	}

	/// <inheritdoc />
	[Required]
	public int BatchTimeInterval
	{
		get => _batchTimeInterval;
		init
		{
			ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(value, 0, nameof(BatchTimeInterval));
			_batchTimeInterval = value;
		}
	}

	/// <inheritdoc />
	[Required]
	public int QueueSize
	{
		get => _queueSize;
		init
		{
			ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(value, 0, nameof(QueueSize));
			_queueSize = value;
		}
	}

	/// <inheritdoc />
	[Required]
	public int ProducerBatchSize
	{
		get => _producerBatchSize;
		init
		{
			ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(value, 0, nameof(ProducerBatchSize));
			_producerBatchSize = value;
		}
	}

	/// <inheritdoc />
	[Required]
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
