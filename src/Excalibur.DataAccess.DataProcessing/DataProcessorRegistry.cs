using Excalibur.DataAccess.DataProcessing.Exceptions;

using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.DataAccess.DataProcessing;

/// <summary>
///     A registry for managing and retrieving <see cref="IDataProcessor" /> implementations based on their associated record types.
/// </summary>
public class DataProcessorRegistry : IDataProcessorRegistry
{
	private readonly Dictionary<string, Func<IServiceProvider, IDataProcessor>> _factories;

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

		_factories = new Dictionary<string, Func<IServiceProvider, IDataProcessor>>(StringComparer.OrdinalIgnoreCase);

		foreach (var processor in processors)
		{
			var processorType = processor.GetType();

			if (!DataProcessorDiscovery.TryGetRecordType(processorType, out var recordType))
			{
				throw new InvalidDataProcessorException(processorType);
			}

			if (!_factories.TryAdd(recordType, sp => (IDataProcessor)sp.GetRequiredService(processorType)))
			{
				throw new MultipleDataProcessorException(recordType);
			}
		}
	}

	/// <inheritdoc />
	public bool TryGetFactory(string recordType, out Func<IServiceProvider, IDataProcessor> factory)
	{
		ArgumentException.ThrowIfNullOrEmpty(recordType);
		return _factories.TryGetValue(recordType, out factory);
	}

	/// <inheritdoc />
	public Func<IServiceProvider, IDataProcessor> GetFactory(string recordType)
	{
		if (TryGetFactory(recordType, out var factory))
		{
			return factory;
		}

		throw new MissingDataProcessorException(recordType);
	}
}
