namespace Excalibur.DataAccess;

/// <summary>
///     Provides an event processor that ensures events are processed sequentially in the order they are received.
/// </summary>
public sealed class OrderedEventProcessor : IDisposable
{
	private readonly TaskFactory _taskFactory;

	private readonly OrderedTaskScheduler _scheduler;

	private bool _disposed;

	/// <summary>
	///     Initializes a new instance of the <see cref="OrderedEventProcessor" /> class.
	/// </summary>
	public OrderedEventProcessor()
	{
		_scheduler = new OrderedTaskScheduler();
		_taskFactory = new TaskFactory(_scheduler);
	}

	/// <summary>
	///     Finalizer for <see cref="OrderedEventProcessor" />.
	/// </summary>
	~OrderedEventProcessor() => Dispose(false);

	/// <summary>
	///     Processes a collection of events asynchronously in the order they are provided.
	/// </summary>
	/// <param name="events"> The collection of events to process. </param>
	/// <param name="processEvent"> A delegate to process individual events. </param>
	/// <returns> A task that represents the asynchronous processing operation. </returns>
	public ValueTask ProcessEventsAsync<TEvent>(IEnumerable<TEvent> events, Func<TEvent, Task> processEvent)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);

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
		ObjectDisposedException.ThrowIf(_disposed, this);

		return _taskFactory.StartNew(processEvent).Unwrap();
	}

	/// <summary>
	///     Releases all resources used by the handler, including the underlying scheduler.
	/// </summary>
	public void Dispose()
	{
		Dispose(true);
		GC.SuppressFinalize(this);
	}

	private async Task ProcessEventBatchesAsync<TEvent>(IList<TEvent> events, Func<TEvent, Task> processEvent)
	{
		foreach (var evt in events)
		{
			await _taskFactory.StartNew(() => processEvent(evt)).Unwrap().ConfigureAwait(false);
		}
	}

	/// <summary>
	///     Releases unmanaged and optionally managed resources.
	/// </summary>
	/// <param name="disposing"> True to release both managed and unmanaged resources; false to release only unmanaged resources. </param>
	private void Dispose(bool disposing)
	{
		if (_disposed)
		{
			return;
		}

		if (disposing)
		{
			_scheduler.Dispose();
		}

		_disposed = true;
	}
}
