namespace Excalibur.DataAccess;

/// <summary>
///     Provides an event processor that ensures events are processed sequentially in the order they are received.
/// </summary>
public sealed class OrderedEventProcessor : IAsyncDisposable
{
	private readonly SemaphoreSlim _semaphore = new(1, 1);

	private bool _disposed;

	/// <summary>
	///     Processes a single event asynchronously while maintaining strict ordering.
	/// </summary>
	/// <param name="processEvent"> A delegate to process the event. </param>
	/// <returns> A task that represents the asynchronous processing operation. </returns>
	public async Task ProcessAsync(Func<Task> processEvent)
	{
		ArgumentNullException.ThrowIfNull(processEvent);
		ObjectDisposedException.ThrowIf(_disposed, this);

		await _semaphore.WaitAsync().ConfigureAwait(false);
		try
		{
			await processEvent().ConfigureAwait(false);
		}
		finally
		{
			_ = _semaphore.Release();
		}
	}

	public ValueTask DisposeAsync()
	{
		_disposed = true;

		_semaphore.Dispose();
		GC.SuppressFinalize(this);

		return ValueTask.CompletedTask;
	}
}
