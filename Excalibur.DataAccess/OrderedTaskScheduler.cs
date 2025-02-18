using System.Collections.Concurrent;

namespace Excalibur.DataAccess;

/// <summary>
///     A task scheduler that ensures tasks are executed in the exact order they are queued. Tasks are processed sequentially on a dedicated
///     background thread.
/// </summary>
public sealed class OrderedTaskScheduler : TaskScheduler, IDisposable
{
	private readonly BlockingCollection<Task> _tasks = new();

	private readonly Thread _thread;

	private bool _disposed;

	/// <summary>
	///     Initializes a new instance of the <see cref="OrderedTaskScheduler" /> class.
	/// </summary>
	public OrderedTaskScheduler()
	{
		_thread = new Thread(Run)
		{
			IsBackground = true // Ensures the thread does not prevent the application from exiting
		};
		_thread.Start();
	}

	/// <summary>
	///     Releases all resources used by the scheduler and stops the execution thread.
	/// </summary>
	public void Dispose()
	{
		Dispose(true);
		GC.SuppressFinalize(this);
	}

	/// <summary>
	///     Schedules a task for execution asynchronously while maintaining strict order.
	/// </summary>
	/// <param name="action"> The action to execute asynchronously. </param>
	/// <returns> A <see cref="Task" /> representing the scheduled operation. </returns>
	public Task ScheduleAsync(Func<Task> action)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);

		var task = new Task<Task>(action);
		QueueTask(task);

		// Unwrap the nested task to get a single Task.
		return task.Unwrap();
	}

	/// <inheritdoc />
	protected override void QueueTask(Task task)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);

		_tasks.Add(task);
	}

	/// <inheritdoc />
	protected override bool TryExecuteTaskInline(Task task, bool taskWasPreviouslyQueued) =>

		// Inline execution is not allowed to ensure strict order
		false;

	/// <inheritdoc />
	protected override IEnumerable<Task>? GetScheduledTasks() => _tasks.ToArray();

	/// <summary>
	///     Runs the task execution loop to process tasks sequentially.
	/// </summary>
	private void Run()
	{
		foreach (var task in _tasks.GetConsumingEnumerable())
		{
			_ = TryExecuteTask(task);
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
			try
			{
				_tasks.CompleteAdding();
				_thread.Join();
			}
			finally
			{
				_tasks.Dispose();
			}
		}

		_disposed = true;
	}
}
