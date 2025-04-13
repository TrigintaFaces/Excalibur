using Excalibur.DataAccess.DataProcessing;
using Excalibur.Tests.Fixtures;
using Excalibur.Tests.Infrastructure.TestBaseClasses.PersistenceOnly;
using Excalibur.Tests.Shared;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Shouldly;

using Xunit.Abstractions;

namespace Excalibur.Tests.Functional.DataAccess;

public class DataProcessingShould(SqlServerContainerFixture fixture, ITestOutputHelper output)
	: SqlServerPersistenceOnlyTestBase(fixture, output)
{
	private readonly ITestOutputHelper _output = output;

	[Fact]
	public async Task ProcessRecordsCorrectly()
	{
		// Arrange
		var testRecords = new List<User>
		{
			new() { Id = 1, Name = "Record 1" }, new() { Id = 2, Name = "Record 2" }, new() { Id = 3, Name = "Record 3" }
		};

		var recordHandler = GetRequiredService<TestUserRecordHandler>();

		var services = new ServiceCollection();
		_ = services.AddSingleton<IRecordHandler<User>>(recordHandler);

		var sp = services.BuildServiceProvider();

		using var processor = new TestUserDataProcessorOverride(
			GetRequiredService<IHostApplicationLifetime>(),
			GetRequiredService<IOptions<DataProcessingConfiguration>>(),
			sp,
			GetRequiredService<ILogger<TestUserDataProcessor>>(),
			testRecords);

		// Act
		var processedCount = await processor.RunAsync(0, async (count, _) =>
		{
			_output.WriteLine($"Processed {count} records");
			await Task.CompletedTask.ConfigureAwait(true);
		}).ConfigureAwait(true);

		// Assert
		processedCount.ShouldBe(3);
		recordHandler.ProcessedRecords.ShouldBe(testRecords);
	}

	protected override void AddServices(IServiceCollection services, IConfiguration configuration)
	{
		_ = services.AddDataProcessing<TestDb, TestDb>(configuration, "DataProcessing", typeof(AssemblyMarker).Assembly);

		_ = services.AddScoped(sp =>
		{
			var appLifetime = sp.GetRequiredService<IHostApplicationLifetime>();
			var loggerFactory = sp.GetRequiredService<ILoggerFactory>();
			var config = GetRequiredService<IOptions<DataProcessingConfiguration>>();
			var logger = loggerFactory.CreateLogger<TestUserDataProcessor>();

			return new TestUserDataProcessor(appLifetime, config, sp, logger);
		});
		_ = services.AddScoped<IDataProcessor>(sp =>
		{
			var appLifetime = sp.GetRequiredService<IHostApplicationLifetime>();
			var loggerFactory = sp.GetRequiredService<ILoggerFactory>();
			var config = GetRequiredService<IOptions<DataProcessingConfiguration>>();
			var logger = loggerFactory.CreateLogger<TestUserDataProcessor>();

			return new TestUserDataProcessor(appLifetime, config, sp, logger);
		});

		_ = services.AddScoped<TestUserRecordHandler>();
		_ = services.AddScoped<IRecordHandler<User>>(sp => sp.GetRequiredService<TestUserRecordHandler>());
	}

	private sealed class TestUserDataProcessorOverride(
		IHostApplicationLifetime appLifetime,
		IOptions<DataProcessingConfiguration> config,
		IServiceProvider sp,
		ILogger<TestUserDataProcessor> logger,
		List<User> records)
		: TestUserDataProcessor(appLifetime, config, sp, logger)
	{
		public override Task<IEnumerable<User>> FetchBatchAsync(long skip, int batchSize, CancellationToken cancellationToken)
		{
			var result = records.Skip((int)skip).Take(batchSize).ToList();
			return Task.FromResult<IEnumerable<User>>(result);
		}
	}
}
