namespace Excalibur.DataAccess.DataProcessing;

/// <summary>
///     Represents the configuration settings for data processing tasks.
/// </summary>
public class DataProcessingConfiguration
{
	private readonly int _dispatcherTimeoutMilliseconds = 5000;
	private readonly int _maxAttempts = 3;

	/// <summary>
	///     Gets or sets the timeout, in milliseconds, for a dispatcher to process data tasks.
	/// </summary>
	/// <value> The timeout must be greater than 0. The default value is 5000 milliseconds (5 seconds). </value>
	/// <exception cref="ArgumentOutOfRangeException"> Thrown when the value is less than or equal to 0. </exception>
	public int DispatcherTimeoutMilliseconds
	{
		get => _dispatcherTimeoutMilliseconds;
		init
		{
			if (value <= 0)
			{
				throw new ArgumentOutOfRangeException(
					nameof(DispatcherTimeoutMilliseconds),
					"Dispatcher timeout must be greater than zero."
				);
			}

			_dispatcherTimeoutMilliseconds = value;
		}
	}

	/// <summary>
	///     Gets or sets the maximum number of attempts allowed for processing a single data task.
	/// </summary>
	/// <value> The maximum attempts must be greater than 0. The default value is 3 attempts. </value>
	/// <exception cref="ArgumentOutOfRangeException"> Thrown when the value is less than or equal to 0. </exception>
	public int MaxAttempts
	{
		get => _maxAttempts;
		init
		{
			if (value <= 0)
			{
				throw new ArgumentOutOfRangeException(
					nameof(MaxAttempts),
					"Max attempts must be greater than zero."
				);
			}

			_maxAttempts = value;
		}
	}

	/// <summary>
	///     Gets or sets the name of the table used to store data task requests.
	/// </summary>
	/// <value> The default table name is "DataProcessor.DataTaskRequests". </value>
	public string TableName { get; init; } = "DataProcessor.DataTaskRequests";
}
