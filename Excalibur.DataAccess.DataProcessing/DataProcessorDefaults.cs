global using static Excalibur.DataAccess.DataProcessing.DataProcessorDefaults;

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
	/// <summary>
	///     The default batch size for data processing. Indicates the number of records to process in a single batch.
	/// </summary>
	public const int BatchSize = 1000;

	/// <summary>
	///     The default batch interval in seconds. Specifies the delay between processing batches.
	/// </summary>
	public const int BatchIntervalSeconds = 30;

	/// <summary>
	///     The default maximum degree of parallelism for data processing. Limits the number of parallel consumers.
	/// </summary>
	public const int MaxDegreeOfParallelism = 1;
}
