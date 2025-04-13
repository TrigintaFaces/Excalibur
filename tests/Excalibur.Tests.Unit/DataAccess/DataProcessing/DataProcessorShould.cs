using Excalibur.DataAccess.DataProcessing;
using Excalibur.Tests.Shared;

using FakeItEasy;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Shouldly;

namespace Excalibur.Tests.Unit.DataAccess.DataProcessing;

public class DataProcessorShould
{
	private readonly IServiceProvider _serviceProvider;
	private readonly IHostApplicationLifetime _appLifetime;
	private readonly ILogger<DataProcessorShould> _logger;
	private readonly DataProcessingConfiguration _config;

	public DataProcessorShould()
	{
		var services = new ServiceCollection();
		_logger = A.Fake<ILogger<DataProcessorShould>>();
		_appLifetime = A.Fake<IHostApplicationLifetime>();

		_ = services.AddScoped<IRecordHandler<string>, NoOpStringHandler>();
		_serviceProvider = services.BuildServiceProvider();

		_config = new DataProcessingConfiguration { QueueSize = 10, ProducerBatchSize = 5, ConsumerBatchSize = 5 };
	}

	[Fact]
	public async Task RunAsyncShouldThrowIfDisposed()
	{
		var processor = new TestStringDataProcessor(_appLifetime, Options.Create(_config), _serviceProvider, _logger);
		await processor.DisposeAsync().ConfigureAwait(true);

		_ = await Should.ThrowAsync<ObjectDisposedException>(async () =>
			await processor.RunAsync(0, (_, _) => Task.CompletedTask).ConfigureAwait(true)).ConfigureAwait(true);
	}

	[Fact]
	public async Task ShouldTriggerShutdownHandlerAndDispose()
	{
		// Arrange
		var fetcher = new OneShotFetcher<string?>(["record"]);
		using var processor = new TestStringDataProcessor(
			_appLifetime,
			Options.Create(_config),
			_serviceProvider,
			_logger,
			fetchFn: fetcher.FetchBatchAsync);

		var runTask = processor.RunAsync(0, (_, _) => Task.CompletedTask);

		// Act
		_appLifetime.StopApplication();
		await Task.Delay(100).ConfigureAwait(true);

		// Assert
		await Should.NotThrowAsync(() => runTask).ConfigureAwait(true);
	}

	[Fact]
	public async Task ShouldSkipNullRecordsInConsumerLoop()
	{
		var fetcher = new OneShotFetcher<string?>([null, "hello", null]);

		using var processor = new TestStringDataProcessor(
			_appLifetime,
			Options.Create(_config),
			_serviceProvider,
			_logger,
			fetchFn: fetcher.FetchBatchAsync);

		var processed = new List<long>();
		_ = await processor.RunAsync(0, (complete, _) =>
		{
			processed.Add(complete);
			return Task.CompletedTask;
		}).ConfigureAwait(true);

		processed.Count.ShouldBe(1);
	}

	[Fact]
	public async Task ShouldContinueOnHandlerExceptionInConsumerLoop()
	{
		var fetcher = new OneShotFetcher<string?>(["a", "b"]);

		using var processor = new TestStringDataProcessor(
			_appLifetime,
			Options.Create(_config),
			new ThrowingHandlerProvider(),
			_logger,
			fetchFn: fetcher.FetchBatchAsync);

		await Should.NotThrowAsync(() => processor.RunAsync(0, (_, _) => Task.CompletedTask)).ConfigureAwait(true);
	}

	private sealed class NoOpStringHandler : IRecordHandler<string>
	{
		public Task HandleAsync(string record, CancellationToken cancellationToken = default) => Task.CompletedTask;
	}

	private sealed class ThrowingHandler : IRecordHandler<string>
	{
#pragma warning disable CA1303 // Do not pass literals as localized parameters

		public Task HandleAsync(string record, CancellationToken cancellationToken = default) =>
			throw new InvalidOperationException("Simulated failure");

#pragma warning restore CA1303 // Do not pass literals as localized parameters
	}

	private sealed class ThrowingHandlerProvider : IServiceProvider
	{
		public object? GetService(Type serviceType)
		{
			if (serviceType == typeof(IRecordHandler<string>))
			{
				return new ThrowingHandler();
			}

			return null;
		}
	}

	private sealed class TestStringDataProcessor(
		IHostApplicationLifetime appLifetime,
		IOptions<DataProcessingConfiguration> config,
		IServiceProvider sp,
		ILogger logger,
		Func<long, int, CancellationToken, Task<IEnumerable<string>>>? fetchFn = null)
		: DataProcessor<string>(appLifetime, config, sp, logger)
	{
		public override Task<IEnumerable<string>> FetchBatchAsync(long skip, int batchSize, CancellationToken cancellationToken)
		{
			if (fetchFn is not null)
			{
				return fetchFn(skip, batchSize, cancellationToken);
			}

			return Task.FromResult<IEnumerable<string>>(["test"]);
		}
	}
}
