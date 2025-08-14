using Excalibur.DataAccess.DataProcessing.Exceptions;

namespace Excalibur.DataAccess.DataProcessing;

/// <summary>
///     Provides a registry for managing and retrieving <see cref="IDataProcessor" /> instances by record type.
/// </summary>
/// <remarks>
///     This interface defines methods to fetch registered data processors based on their associated record type. It supports safe retrieval
///     with validation or exception handling if a processor is not found.
/// </remarks>
public interface IDataProcessorRegistry
{
	/// <summary>
	///     Attempts to retrieve a registered <see cref="IDataProcessor" /> by its associated record type.
	/// </summary>
	/// <param name="recordType"> The type of record the processor is associated with. </param>
	/// <param name="processor">
	///     When this method returns, contains the <see cref="IDataProcessor" /> associated with the specified
	///     <paramref name="recordType" />, if one is found; otherwise, <c> null </c>.
	/// </param>
	/// <returns> <c> true </c> if a processor for the specified <paramref name="recordType" /> is found; otherwise, <c> false </c>. </returns>
	public bool TryGetProcessor(string recordType, out IDataProcessor processor);

	/// <summary>
	///     Retrieves a registered <see cref="IDataProcessor" /> by its associated record type.
	/// </summary>
	/// <param name="recordType"> The type of record the processor is associated with. </param>
	/// <returns> The <see cref="IDataProcessor" /> associated with the specified <paramref name="recordType" />. </returns>
	/// <exception cref="MissingDataProcessorException">
	///     Thrown if no <see cref="IDataProcessor" /> is registered for the specified <paramref name="recordType" />.
	/// </exception>
	public IDataProcessor GetProcessor(string recordType);
}
