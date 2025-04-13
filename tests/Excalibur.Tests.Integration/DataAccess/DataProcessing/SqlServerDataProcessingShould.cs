using System.Data;

using Dapper;

using Excalibur.DataAccess.DataProcessing;
using Excalibur.Tests.Fixtures;
using Excalibur.Tests.Infrastructure.TestBaseClasses.Host;
using Excalibur.Tests.Mothers;
using Excalibur.Tests.Shared;

using Microsoft.AspNetCore.Builder;
using Microsoft.Data.SqlClient;

using Shouldly;

using Xunit.Abstractions;

namespace Excalibur.Tests.Integration.DataAccess.DataProcessing;

public class SqlServerDataProcessingShould(SqlServerContainerFixture fixture, ITestOutputHelper output)
	: SqlServerHostTestBase(fixture, output)
{
	[Fact]
	public async Task AddDataTaskForRecordTypeShouldInsertIntoDatabase()
	{
		using var connection = fixture.CreateDbConnection() as SqlConnection;
		await connection.OpenAsync().ConfigureAwait(true);

		var manager = GetRequiredService<IDataOrchestrationManager>();

		var taskId = await manager.AddDataTaskForRecordType("User").ConfigureAwait(true);

		var result = await connection.QueryFirstOrDefaultAsync<Guid>(
			"SELECT DataTaskId FROM DataProcessor.DataTaskRequests WHERE DataTaskId = @taskId",
			new { taskId }).ConfigureAwait(true);

		result.ShouldBe(taskId);
	}

	[Fact]
	public async Task ProcessDataTasksShouldCompleteSuccessfully()
	{
		using var connection = fixture.CreateDbConnection() as SqlConnection;
		await connection.OpenAsync().ConfigureAwait(true);
		await DataProcessingMother.SeedDataTaskAsync(connection, DatabaseEngine.SqlServer, "User").ConfigureAwait(true);

		var manager = GetRequiredService<IDataOrchestrationManager>();

		await manager.ProcessDataTasks().ConfigureAwait(true);

		var count = await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM DataProcessor.DataTaskRequests;").ConfigureAwait(true);
		count.ShouldBe(0);
	}

	protected override void ConfigureHostServices(WebApplicationBuilder builder, IDatabaseContainerFixture fixture)
	{
		ArgumentNullException.ThrowIfNull(builder);

		_ = builder.Services.AddDataProcessing<TestDb, TestDb>(builder.Configuration, "DataProcessing", typeof(AssemblyMarker).Assembly);
	}

	protected override async Task OnDatabaseInitialized(IDbConnection connection) =>
		await DataProcessingMother.EnsureDatabaseInitializedAsync(connection, DatabaseEngine.SqlServer).ConfigureAwait(false);
}
