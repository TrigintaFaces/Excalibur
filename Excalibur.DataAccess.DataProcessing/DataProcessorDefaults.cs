namespace Excalibur.DataAccess.DataProcessing;

/// <summary>
///     Provides default configuration values for the <see cref="DataProcessor{TRecord}" /> class.
/// </summary>
/// <remarks>
///     These constants are intended to be used as default parameters in data processing classes, ensuring consistent behavior across the
///     application when custom values are not provided.
/// </remarks>
public static class DataProcessorDefaults
{
	public const string DataProcessorDefaultTableName = "DataProcessor.DataTaskRequests";
	public const int DataProcessorDefaultDispatcherTimeout = 60000;
	public const int DataProcessorDefaultMaxAttempts = 3;
	public const int DataProcessorDefaultQueueSize = 500;
	public const int DataProcessorDefaultProducerBatchSize = 50;
	public const int DataProcessorDefaultConsumerBatchSize = 100;
}
