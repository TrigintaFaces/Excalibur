using System.Data;

using Dapper;

using Excalibur.DataAccess.DataProcessing;
using Excalibur.Tests.Fixtures;
using Excalibur.Tests.Infrastructure.TestBaseClasses.Host;
using Excalibur.Tests.Mothers;
using Excalibur.Tests.Shared;

using Microsoft.AspNetCore.Builder;

using Shouldly;

using Xunit.Abstractions;

namespace Excalibur.Tests.Functional.DataAccess.DataProcessing;

public class SqlServerDataOrchestrationManagerShould(SqlServerContainerFixture fixture, ITestOutputHelper output)
	: HostTestBase<SqlServerContainerFixture>(fixture, output)
{
	[Fact]
	public async Task ShouldAddDataTaskToDatabase()
	{
		// Arrange
		var manager = GetRequiredService<IDataOrchestrationManager>();
		var connection = Fixture.CreateDbConnection();
		connection.Open();

		// Act
		var taskId = await manager.AddDataTaskForRecordType("User").ConfigureAwait(true);

		// Assert
		var command = new CommandDefinition("SELECT COUNT(*) FROM DataProcessor.DataTaskRequests WHERE DataTaskId = @Id",
			new { Id = taskId });
		var count = await connection.ExecuteScalarAsync<long>(command).ConfigureAwait(true);
		count.ShouldBe(1);
	}

	[Fact]
	public async Task ShouldProcessValidDataTask()
	{
		// Arrange
		var manager = GetRequiredService<IDataOrchestrationManager>();

		var dataTaskId = await manager.AddDataTaskForRecordType("User").ConfigureAwait(true);

		// Act
		await manager.ProcessDataTasks().ConfigureAwait(true);

		// Assert
		var connection = Fixture.CreateDbConnection();
		connection.Open();
		var verifyCmd = new CommandDefinition("SELECT COUNT(*) FROM DataProcessor.DataTaskRequests WHERE DataTaskId = @Id",
			new { Id = dataTaskId });
		var remaining = await connection.ExecuteScalarAsync<long>(verifyCmd).ConfigureAwait(true);
		remaining.ShouldBe(0);
	}

	[Fact]
	public async Task ShouldSkipUnknownProcessorType()
	{
		// Arrange
		var manager = GetRequiredService<IDataOrchestrationManager>();
		var connection = Fixture.CreateDbConnection();
		connection.Open();

		var dataTaskId = await manager.AddDataTaskForRecordType("UnknownType").ConfigureAwait(true);

		// Act
		await manager.ProcessDataTasks().ConfigureAwait(true);

		// Assert
		var cmd = new CommandDefinition("SELECT Attempts FROM DataProcessor.DataTaskRequests WHERE DataTaskId = @Id",
			new { Id = dataTaskId });
		var attempts = await connection.ExecuteScalarAsync<int>(cmd).ConfigureAwait(true);
		attempts.ShouldBe(1);
	}

	protected override void ConfigureHostServices(WebApplicationBuilder builder, IDatabaseContainerFixture fixture)
	{
		ArgumentNullException.ThrowIfNull(builder);

		_ = builder.Services.AddDataProcessing<TestDb, TestDb>(builder.Configuration, "DataProcessing", typeof(AssemblyMarker).Assembly);
	}

	protected override async Task OnDatabaseInitialized(IDbConnection connection) =>
		await DataProcessingMother.EnsureDatabaseInitializedAsync(connection, DatabaseEngine.SqlServer).ConfigureAwait(false);
}
