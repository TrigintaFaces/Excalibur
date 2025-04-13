namespace Excalibur.Data.Outbox;

/// <summary>
///     Manages the dispatch process for outbox records.
/// </summary>
public interface IOutboxManager : IAsyncDisposable, IDisposable
{
	/// <summary>
	///     Executes the outbox dispatch logic for a specific dispatcher.
	/// </summary>
	/// <param name="dispatcherId"> The identifier of the dispatcher. </param>
	/// <param name="cancellationToken"> The cancellation token to observe. </param>
	/// <returns> A task that returns the number of records successfully dispatched. </returns>
	public Task<int> RunOutboxDispatchAsync(string dispatcherId, CancellationToken cancellationToken = default);
}
