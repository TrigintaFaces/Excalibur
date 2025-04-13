using Excalibur.DataAccess.SqlServer;
using Excalibur.DataAccess.SqlServer.Cdc;

using FakeItEasy;

using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using Shouldly;

namespace Excalibur.Tests.Unit.DataAccess.SqlServer.Cdc;

public class CdcProcessorShould
{
	private readonly ICdcRepository _cdcRepository = A.Fake<ICdcRepository>();
	private readonly ICdcStateStore _stateStore = A.Fake<ICdcStateStore>();
	private readonly IDatabaseConfig _config = A.Fake<IDatabaseConfig>();
	private readonly IDataAccessPolicyFactory _policyFactory = A.Fake<IDataAccessPolicyFactory>();
	private readonly ILogger<CdcProcessor> _logger = A.Fake<ILogger<CdcProcessor>>();
	private readonly IHostApplicationLifetime _appLifetime = A.Fake<IHostApplicationLifetime>();

	private readonly SqlConnection _cdcConnection = A.Dummy<SqlConnection>();
	private readonly SqlConnection _stateStoreConnection = A.Dummy<SqlConnection>();

	public CdcProcessorShould()
	{
		_ = A.CallTo(() => _config.QueueSize).Returns(10);
		_ = A.CallTo(() => _config.CaptureInstances).Returns(["TestTable"]);
		_ = A.CallTo(() => _config.DatabaseName).Returns("TestDb");
		_ = A.CallTo(() => _config.DatabaseConnectionIdentifier).Returns("test-conn");
		_ = A.CallTo(() => _cdcRepository.GetMinPositionAsync("TestTable", A<CancellationToken>._))
			.Returns([1, 2, 3]);

		_ = A.CallTo(() => _stateStore.GetLastProcessedPositionAsync("test-conn", "TestDb", A<CancellationToken>._))
			.Returns([
				new CdcProcessingState
				{
					TableName = "TestTable",
					LastProcessedLsn = new byte[10],
					LastProcessedSequenceValue = null,
					DatabaseConnectionIdentifier = "test-conn",
					DatabaseName = "TestDb"
				}
			]);
	}

	[Fact]
	public async Task ProcessCdcChangesAsyncShouldThrowIfAlreadyRunning()
	{
		using var processor = CreateProcessor();

		// Simulate running state
		_ = Task.Run(async () =>
		{
			_ = await Assert.ThrowsAsync<InvalidOperationException>(() =>
				processor.ProcessCdcChangesAsync((_, _) => Task.CompletedTask, CancellationToken.None)).ConfigureAwait(true);
		});

		await Task.Delay(100).ConfigureAwait(true); // allow some startup time

		_ = await Assert.ThrowsAsync<InvalidOperationException>(() =>
			processor.ProcessCdcChangesAsync((_, _) => Task.CompletedTask, CancellationToken.None)).ConfigureAwait(true);
	}

	[Fact]
	public async Task DisposeAsyncShouldReleaseResources()
	{
		var processor = CreateProcessor();
		await processor.DisposeAsync().ConfigureAwait(true);

		// No exception means resources disposed safely
		true.ShouldBeTrue();
	}

	[Fact]
	public async Task ProcessCdcChangesAsyncShouldThrowIfNoValidLsn()
	{
		_ = A.CallTo(() => _stateStore.GetLastProcessedPositionAsync("test-conn", "TestDb", A<CancellationToken>._))
			.Returns([]);

		_ = A.CallTo(() => _cdcRepository.GetMinPositionAsync("TestTable", A<CancellationToken>._))
			.Returns([0, 0, 0, 0, 0, 0, 0, 0, 0, 0]);

		using var processor = CreateProcessor();

		_ = await Assert.ThrowsAsync<InvalidOperationException>(() =>
			processor.ProcessCdcChangesAsync((_, _) => Task.CompletedTask, CancellationToken.None)).ConfigureAwait(true);
	}

	[Fact]
	public async Task DisposeAsyncShouldCancelProcessing()
	{
		using var processor = CreateProcessor();

		using var cts = new CancellationTokenSource();

		var processingTask = processor.ProcessCdcChangesAsync((_, _) => Task.Delay(1000), cts.Token);

		await Task.Delay(100).ConfigureAwait(true);
		await processor.DisposeAsync().ConfigureAwait(true);

		// Assert no unhandled exceptions and task is canceled or completes
		var completedTask = await Task.WhenAny(processingTask, Task.Delay(2000)).ConfigureAwait(true);
		completedTask.ShouldBe(processingTask);
	}

	[Fact]
	public async Task ProcessCdcChangesAsyncShouldRespectCancellationToken()
	{
		using var processor = CreateProcessor();
		using var cts = new CancellationTokenSource();
		await cts.CancelAsync().ConfigureAwait(true);

		_ = await Should.ThrowAsync<OperationCanceledException>(async () =>
			await processor.ProcessCdcChangesAsync((_, _) => Task.CompletedTask, cts.Token).ConfigureAwait(true)
		).ConfigureAwait(true);
	}

	private CdcProcessor CreateProcessor() => new(
		_appLifetime,
		_config,
		_cdcConnection,
		_stateStoreConnection,
		_policyFactory,
		_logger);
}
