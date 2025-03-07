namespace Excalibur.DataAccess;

/// <summary>
///     Provides an event processor that ensures events are processed sequentially in the order they are received.
/// </summary>
public sealed class OrderedEventProcessor : IAsyncDisposable
{
	private readonly TaskFactory _taskFactory;

	private readonly OrderedTaskScheduler _scheduler;

	private int _disposedFlag;

	/// <summary>
	///     Initializes a new instance of the <see cref="OrderedEventProcessor" /> class.
	/// </summary>
	public OrderedEventProcessor()
	{
		_scheduler = new OrderedTaskScheduler();
		_taskFactory = new TaskFactory(_scheduler);
	}

	/// <summary>
	///     Processes a collection of events asynchronously in the order they are provided.
	/// </summary>
	/// <param name="events"> The collection of events to process. </param>
	/// <param name="processEvent"> A delegate to process individual events. </param>
	/// <returns> A task that represents the asynchronous processing operation. </returns>
	public ValueTask ProcessEventsAsync<TEvent>(IEnumerable<TEvent> events, Func<TEvent, Task> processEvent)
	{
		ObjectDisposedException.ThrowIf(_disposedFlag == 1, this);

		var eventList = events as IList<TEvent> ?? events.ToList();

		return eventList.Count == 0 ? ValueTask.CompletedTask : new ValueTask(ProcessEventBatchesAsync(eventList, processEvent));
	}

	/// <summary>
	///     Processes a single event asynchronously while maintaining strict ordering.
	/// </summary>
	/// <param name="processEvent"> A delegate to process the event. </param>
	/// <returns> A task that represents the asynchronous processing operation. </returns>
	public Task ProcessAsync(Func<Task> processEvent)
	{
		ObjectDisposedException.ThrowIf(_disposedFlag == 1, this);

		return _taskFactory.StartNew(processEvent).Unwrap();
	}

	public async ValueTask DisposeAsync()
	{
		if (Interlocked.CompareExchange(ref _disposedFlag, 1, 0) == 1)
		{
			return;
		}

		await _scheduler.DisposeAsync().ConfigureAwait(false);
		GC.SuppressFinalize(this);
	}

	private async Task ProcessEventBatchesAsync<TEvent>(IList<TEvent> events, Func<TEvent, Task> processEvent)
	{
		foreach (var evt in events)
		{
			await _taskFactory.StartNew(() => processEvent(evt)).Unwrap().ConfigureAwait(false);
		}
	}
}
