namespace Excalibur.DataAccess.DataProcessing;

/// <summary>
///     Defines a handler for processing individual records of type <typeparamref name="TRecord" />.
/// </summary>
/// <typeparam name="TRecord"> The type of the record to be handled. </typeparam>
/// <remarks>
///     This interface is designed to be implemented by classes that perform specific operations on individual records, such as validation,
///     transformation, or storage.
/// </remarks>
public interface IRecordHandler<in TRecord>
{
	/// <summary>
	///     Processes a single record asynchronously.
	/// </summary>
	/// <param name="record"> The record to process. </param>
	/// <param name="cancellationToken"> A token to monitor for cancellation requests. Defaults to <see cref="CancellationToken.None" />. </param>
	/// <returns> A <see cref="Task" /> that represents the asynchronous processing operation. </returns>
	/// <exception cref="ArgumentNullException"> Thrown if <paramref name="record" /> is <c> null </c>. </exception>
	public Task HandleAsync(TRecord record, CancellationToken cancellationToken = default);
}
