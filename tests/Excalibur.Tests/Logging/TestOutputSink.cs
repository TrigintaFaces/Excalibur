using System.Globalization;

using Excalibur.DataAccess;

using Serilog.Core;
using Serilog.Events;
using Serilog.Formatting.Display;

using Xunit.Abstractions;

namespace Excalibur.Tests.Logging;

public class TestOutputSink : ILogEventSink, IAsyncDisposable
{
	private readonly ITestOutputHelper _outputHelper;
	private readonly InMemoryDataQueue<string> _logQueue;
	private readonly CancellationTokenSource _cts = new();
	private readonly Task _logProcessingTask;

	private readonly MessageTemplateTextFormatter _formatter =
		new(
			"{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level}] {Message:j}{NewLine}{Exception}",
			CultureInfo.InvariantCulture
		);

	private int _disposedFlag;

	public TestOutputSink(ITestOutputHelper outputHelper, int queueCapacity = 1000)
	{
		ArgumentNullException.ThrowIfNull(outputHelper);

		_outputHelper = outputHelper;
		_logQueue = new InMemoryDataQueue<string>(queueCapacity);

		// Start background log processing
		_logProcessingTask = Task.Run(ProcessLogsAsync);
	}

	void ILogEventSink.Emit(LogEvent? logEvent)
	{
		if (logEvent == null)
		{
			return;
		}

		using var writer = new StringWriter();
		_formatter.Format(logEvent, writer);
		var formattedMessage = writer.ToString();

		_logQueue.EnqueueAsync(formattedMessage, _cts.Token).AsTask().GetAwaiter().GetResult();

		if (logEvent.Exception != null)
		{
			_logQueue.EnqueueAsync(logEvent.Exception.ToString(), _cts.Token).AsTask().GetAwaiter().GetResult();
		}
	}

	public async ValueTask DisposeAsync()
	{
		if (Interlocked.CompareExchange(ref _disposedFlag, 1, 0) == 1)
		{
			return;
		}

		await _cts.CancelAsync().ConfigureAwait(false);
		_logQueue.CompleteWriter();

		try
		{
			await _logProcessingTask.WaitAsync(TimeSpan.FromMinutes(5)).ConfigureAwait(false);
		}
		catch (OperationCanceledException)
		{
			// Expected when cancellation is requested
		}
		finally
		{
			await FlushRemainingLogsAsync().ConfigureAwait(false);
			await _logQueue.DisposeAsync().ConfigureAwait(false);
			_cts.Dispose();
		}

		GC.SuppressFinalize(this);
	}

	public void Unregister() => _ = DisposeAsync().AsTask();

	private async Task ProcessLogsAsync()
	{
		try
		{
			await foreach (var message in _logQueue.DequeueAllAsync(_cts.Token).ConfigureAwait(false))
			{
				_outputHelper.WriteLine(message);
			}
		}
		catch (OperationCanceledException)
		{
			// Graceful cancellation
		}
	}

	/// <summary>
	///     Ensures all remaining log messages are written before exiting.
	/// </summary>
	private async Task FlushRemainingLogsAsync()
	{
		while (!_logQueue.IsEmpty())
		{
			var batch = await _logQueue.DequeueBatchAsync(10, CancellationToken.None).ConfigureAwait(false);
			foreach (var message in batch)
			{
				_outputHelper.WriteLine(message);
			}
		}
	}
}
