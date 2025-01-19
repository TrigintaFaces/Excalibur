namespace Excalibur.DataAccess;

/// <summary>
///     Defines a contract for fetching records asynchronously.
/// </summary>
/// <typeparam name="TRecord"> The type of the records being fetched. </typeparam>
public interface IRecordFetcher<out TRecord>
{
	/// <summary>
	///     Fetches all records asynchronously, starting from the specified position.
	/// </summary>
	/// <param name="skip">
	///     The number of records to skip before starting the fetch. Useful for resuming operations or implementing paging.
	/// </param>
	/// <param name="cancellationToken"> A token that can be used to cancel the operation. </param>
	/// <returns> An asynchronous enumerable of records of type <typeparamref name="TRecord" />. </returns>
	IAsyncEnumerable<TRecord> FetchAllAsync(long skip, CancellationToken cancellationToken = default);
}
