using System.Data;

using Excalibur.Core.Extensions;
using Excalibur.DataAccess.DataProcessing;
using Excalibur.Tests.Fixtures;
using Excalibur.Tests.Infrastructure.TestBaseClasses.Host;
using Excalibur.Tests.Mothers;
using Excalibur.Tests.Shared;

using Microsoft.AspNetCore.Builder;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using Shouldly;

using Xunit.Abstractions;

namespace Excalibur.Tests.Integration.DataAccess.DataProcessing;

public class SqlServerDataOrchestrationManagerShould(SqlServerContainerFixture fixture, ITestOutputHelper output)
	: SqlServerHostTestBase(fixture, output)
{
	private readonly TaskCompletionSource _handledTcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
	private readonly TaskCompletionSource _beforeDeleteTcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

	[Fact]
	public async Task AddDataTaskForRecordTypeShouldInsertIntoDatabase()
	{
		// Arrange
		var testDb = GetService<TestDb>();
		var manager = GetRequiredService<IDataOrchestrationManager>();
		var processor = GetRequiredService<IDataProcessor>() as TestWatchableDataProcessor;
		processor.PauseBeforeDelete = _beforeDeleteTcs;

		await DatabaseCleaner.CleanupDataTasksAsync(testDb.Connection).ConfigureAwait(true);

		// Act
		var taskId = await manager.AddDataTaskForRecordType("User").ConfigureAwait(true);

		// Assert
		taskId.ShouldNotBe(Guid.Empty);

		var remainingCount = await DataProcessingMother.GetTaskCount(testDb.Connection).ConfigureAwait(true);
		remainingCount.ShouldBe(1);

		testDb.Dispose();
	}

	[Fact]
	public async Task ShouldUpdateCompletedCount()
	{
		// Arrange
		var manager = GetRequiredService<IDataOrchestrationManager>();
		var taskId = await manager.AddDataTaskForRecordType("Watchable").ConfigureAwait(true);
		var processor = GetRequiredService<IDataProcessor>() as TestWatchableDataProcessor;
		processor.PauseBeforeDelete = _beforeDeleteTcs;

		// Act
		var processingTask = manager.ProcessDataTasks();

		// Assert
		await _handledTcs.Task.TimeoutAfter(TimeSpan.FromSeconds(5)).ConfigureAwait(true);

		using var connection = Fixture.CreateDbConnection();
		connection.Open();

		var completedCount = await WaitUntilUpdatedCount(taskId, connection, expectedMinimum: 1).ConfigureAwait(true);

		completedCount.ShouldBeGreaterThan(0);

		_beforeDeleteTcs.SetResult();
		await processingTask.ConfigureAwait(true);
	}

	[Fact]
	public async Task ProcessDataTasksShouldProcessAllValidTasks()
	{
		// Arrange
		using var connection = fixture.CreateDbConnection() as SqlConnection;
		await DatabaseCleaner.CleanupDataTasksAsync(connection).ConfigureAwait(true);
		var manager = GetRequiredService<IDataOrchestrationManager>();
		var processor = GetRequiredService<IDataProcessor>() as TestWatchableDataProcessor;
		processor.PauseBeforeDelete = _beforeDeleteTcs;

		// Insert multiple data tasks
		await DataProcessingMother.InsertDataTasks(manager, recordType: "User", count: 3).ConfigureAwait(true);

		var initialCount = await DataProcessingMother.GetTaskCount(connection).ConfigureAwait(true);
		initialCount.ShouldBe(3);

		// Act
		await manager.ProcessDataTasks().ConfigureAwait(true);

		// Assert
		var remainingCount = await DataProcessingMother.GetTaskCount(connection).ConfigureAwait(true);
		remainingCount.ShouldBe(0);
	}

	[Fact]
	public async Task ShouldIncrementAttemptsOnMissingHandler()
	{
		// Arrange
		var manager = GetRequiredService<IDataOrchestrationManager>();
		var dataTaskId = await manager.AddDataTaskForRecordType("MissingType").ConfigureAwait(true);
		var processor = GetRequiredService<IDataProcessor>() as TestWatchableDataProcessor;
		processor.PauseBeforeDelete = _beforeDeleteTcs;

		// Act
		await manager.ProcessDataTasks().ConfigureAwait(true);

		// Assert
		var connection = Fixture.CreateDbConnection();
		connection.Open();

		var attempts = await DataProcessingMother.GetAttempts(dataTaskId, connection).ConfigureAwait(true);
		attempts.ShouldBe(1);
	}

	[Fact]
	public async Task ShouldSkipTaskAfterMaxAttempts()
	{
		var connection = Fixture.CreateDbConnection();
		var processor = GetRequiredService<IDataProcessor>() as TestWatchableDataProcessor;
		processor.PauseBeforeDelete = _beforeDeleteTcs;

		await DataProcessingMother.SeedDataTaskAsync(connection, Fixture.Engine, "MissingType")
			.ConfigureAwait(true); // Already hits max attempts

		var manager = GetRequiredService<IDataOrchestrationManager>();

		await manager.ProcessDataTasks().ConfigureAwait(true);

		var remainingCount = await DataProcessingMother.GetTaskCount(connection).ConfigureAwait(true);
		remainingCount.ShouldBe(1);
	}

	[Fact]
	public async Task ShouldProcessBatchesRespectingConsumerBatchSize()
	{
		var manager = GetRequiredService<IDataOrchestrationManager>();
		var processor = GetRequiredService<IDataProcessor>() as TestWatchableDataProcessor;
		processor.PauseBeforeDelete = _beforeDeleteTcs;

		await DataProcessingMother.InsertDataTasks(manager, "User", 1).ConfigureAwait(true);

		await manager.ProcessDataTasks().ConfigureAwait(true);

		var connection = Fixture.CreateDbConnection();
		var remainingCount = await DataProcessingMother.GetTaskCount(connection).ConfigureAwait(true);
		remainingCount.ShouldBe(0);
	}

	[Fact]
	public async Task ShouldStopProcessingWhenAppIsStopping()
	{
		var appLifetime = GetRequiredService<IHostApplicationLifetime>();
		var manager = GetRequiredService<IDataOrchestrationManager>();
		var processor = GetRequiredService<IDataProcessor>() as TestWatchableDataProcessor;
		processor.PauseBeforeDelete = _beforeDeleteTcs;

		_ = await manager.AddDataTaskForRecordType("User").ConfigureAwait(true);

		var runTask = Task.Run(async () =>
		{
			await manager.ProcessDataTasks().ConfigureAwait(true);
		});

		await Task.Delay(100).ConfigureAwait(true);
		appLifetime.StopApplication();

		await runTask.ConfigureAwait(true);
	}

	protected override void ConfigureHostServices(WebApplicationBuilder builder, IDatabaseContainerFixture fixture)
	{
		ArgumentNullException.ThrowIfNull(builder);

		_ = builder.Services.AddDataProcessing<TestDb, TestDb>(builder.Configuration, "DataProcessing", typeof(AssemblyMarker).Assembly);
		_ = builder.Services.AddTransient<IRecordHandler<Watchable>>(sp =>
		{
			var loggerFactory = sp.GetRequiredService<ILoggerFactory>();
			var logger = loggerFactory.CreateLogger<TestWatchableRecordHandler>();
			var handler = new TestWatchableRecordHandler(logger);

			handler.SignalWhenHandled = _handledTcs;

			return handler;
		});
	}

	protected override async Task OnDatabaseInitialized(IDbConnection connection) =>
		await DataProcessingMother.EnsureDatabaseInitializedAsync(connection, DatabaseEngine.SqlServer).ConfigureAwait(false);

	private static async Task<long> WaitUntilUpdatedCount(Guid taskId, IDbConnection connection, int expectedMinimum, int maxAttempts = 10)
	{
		const int delayMs = 100;

		for (var i = 0; i < maxAttempts; i++)
		{
			var count = await DataProcessingMother.GetCompletedCount(taskId, connection).ConfigureAwait(true);
			if (count >= expectedMinimum)
			{
				return count;
			}

			await Task.Delay(delayMs).ConfigureAwait(false);
		}

		return await DataProcessingMother.GetCompletedCount(taskId, connection).ConfigureAwait(true);
	}
}
