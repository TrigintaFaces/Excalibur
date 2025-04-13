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

namespace Excalibur.Tests.Integration.DataAccess.DataProcessing;

[Collection("SqlServerIntegrationContainerCollection")]
public class SqlServerServiceCollectionExtensionsShould(SqlServerContainerFixture fixture, ITestOutputHelper output)
	: SqlServerHostTestBase(fixture, output)
{
	[Fact]
	public async Task ShouldAddAndRetrieveDataTaskFromSqlServer()
	{
		// Arrange
		var manager = GetRequiredService<IDataOrchestrationManager>();

		// Act
		var taskId = await manager.AddDataTaskForRecordType("User").ConfigureAwait(true);

		// Assert
		taskId.ShouldNotBe(Guid.Empty);

		using var connection = fixture.CreateDbConnection();
		var count = await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM DataProcessor.DataTaskRequests").ConfigureAwait(true);
		count.ShouldBe(1);
	}

	protected override void ConfigureHostServices(WebApplicationBuilder builder, IDatabaseContainerFixture fixture)
	{
		ArgumentNullException.ThrowIfNull(builder);

		_ = builder.Services.AddDataProcessing<TestDb, TestDb>(builder.Configuration, "DataProcessing", typeof(AssemblyMarker).Assembly);
	}

	protected override async Task OnDatabaseInitialized(IDbConnection connection) =>
		await DataProcessingMother.EnsureDatabaseInitializedAsync(connection, DatabaseEngine.SqlServer).ConfigureAwait(false);
}
