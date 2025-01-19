using Excalibur.DataAccess.DataProcessing.Exceptions;

namespace Excalibur.DataAccess.DataProcessing;

/// <summary>
///     A registry for managing and retrieving <see cref="IDataProcessor" /> implementations based on their associated record types.
/// </summary>
public class DataProcessorRegistry : IDataProcessorRegistry
{
	private readonly Dictionary<string, IDataProcessor> _processors;

	/// <summary>
	///     Initializes a new instance of the <see cref="DataProcessorRegistry" /> class with a collection of data processors.
	/// </summary>
	/// <param name="processors"> The collection of data processors to register. </param>
	/// <exception cref="ArgumentNullException"> Thrown if <paramref name="processors" /> is <c> null </c>. </exception>
	/// <exception cref="InvalidDataProcessorException">
	///     Thrown if a processor does not have a valid record type or its configuration is invalid.
	/// </exception>
	/// <exception cref="MultipleDataProcessorException"> Thrown if multiple processors are registered for the same record type. </exception>
	public DataProcessorRegistry(IEnumerable<IDataProcessor> processors)
	{
		ArgumentNullException.ThrowIfNull(processors);

		_processors = new Dictionary<string, IDataProcessor>(StringComparer.OrdinalIgnoreCase);

		foreach (var processor in processors)
		{
			var type = processor.GetType();

			if (!DataProcessorDiscovery.TryGetRecordType(type, out var recordType))
			{
				throw new InvalidDataProcessorException(type);
			}

			if (!_processors.TryAdd(recordType, processor))
			{
				throw new MultipleDataProcessorException(recordType);
			}
		}
	}

	/// <inheritdoc />
	public bool TryGetProcessor(string recordType, out IDataProcessor processor)
	{
		ArgumentException.ThrowIfNullOrEmpty(recordType);

		return _processors.TryGetValue(recordType, out processor);
	}

	/// <inheritdoc />
	public IDataProcessor GetProcessor(string recordType)
	{
		if (_processors.TryGetValue(recordType, out var processor))
		{
			return processor;
		}

		throw new MissingDataProcessorException(recordType);
	}
}
