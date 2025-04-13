using Excalibur.Core.Extensions;
using Excalibur.DataAccess.DataProcessing;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.Tests.Shared;

[DataTaskRecordType("Watchable")]
public sealed class TestWatchableDataProcessor(
	IHostApplicationLifetime appLifetime,
	IOptions<DataProcessingConfiguration> configuration,
	IServiceProvider serviceProvider,
	ILogger<TestWatchableDataProcessor> logger)
	: DataProcessor<Watchable>(appLifetime, configuration, serviceProvider, logger)
{
	public TaskCompletionSource? PauseBeforeDelete { get; set; }

	public override async Task<IEnumerable<Watchable>> FetchBatchAsync(long skip, int batchSize, CancellationToken cancellationToken)
	{
		await Task.Delay(10, cancellationToken).ConfigureAwait(false);

		if (skip > 0)
		{
			return [];
		}

		var watchables = Enumerable.Range(0, batchSize).Select(x => new Watchable { Id = x + 1, Name = $"TestWatchable_{x}" });
		return watchables;
	}

	protected override async ValueTask DisposeAsyncCore()
	{
		if (PauseBeforeDelete != null)
		{
			await PauseBeforeDelete.Task.TimeoutAfter(TimeSpan.FromSeconds(2)).ConfigureAwait(true);
		}

		await base.DisposeAsyncCore().ConfigureAwait(true);
	}
}

public sealed class TestWatchableRecordHandler(ILogger<TestWatchableRecordHandler> logger)
	: IRecordHandler<Watchable>
{
	public TaskCompletionSource? SignalWhenHandled { get; set; }

	public async Task HandleAsync(Watchable record, CancellationToken cancellationToken = default)
	{
		logger.LogInformation("Handling watchable: {Watchable}", record);

		await Task.Delay(10, cancellationToken).ConfigureAwait(false);

		_ = SignalWhenHandled.TrySetResult();
	}
}

public class Watchable
{
	public int Id { get; set; }
	public string Name { get; set; }
}
