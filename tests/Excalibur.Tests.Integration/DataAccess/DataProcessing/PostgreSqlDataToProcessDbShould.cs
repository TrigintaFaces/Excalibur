using System.Data;

using Dapper;

using Excalibur.DataAccess.DataProcessing;
using Excalibur.Tests.Fixtures;
using Excalibur.Tests.Infrastructure.TestBaseClasses.Host;
using Excalibur.Tests.Mothers;
using Excalibur.Tests.Shared;

using Shouldly;

using Xunit.Abstractions;

namespace Excalibur.Tests.Integration.DataAccess.DataProcessing;

[Collection("PostgreSqlIntegrationContainerCollection")]
public class PostgreSqlDataToProcessDbShould(PostgreSqlContainerFixture fixture, ITestOutputHelper output)
	: PostgreSqlHostTestBase(fixture, output)
{
	[Fact]
	public async Task ShouldConnectToDatabaseAndExecuteQuery()
	{
		// Arrange
		var testDb = GetService<TestDb>();
		var db = new DataToProcessDb(testDb);

		// Act
		var result = await db.Connection.ExecuteScalarAsync<int>("SELECT 1").ConfigureAwait(true);

		// Assert
		result.ShouldBe(1);

		testDb.Dispose();
	}

	[Fact]
	public async Task ShouldPerformInsertAndReadOperations()
	{
		// Arrange
		var testDb = GetService<TestDb>();
		var db = new DataToProcessDb(testDb);

		// Insert data
		var insertSql =
			"INSERT INTO DataProcessor.DataTaskRequests (DataTaskId, CreatedAt, RecordType, Attempts, MaxAttempts, CompletedCount) VALUES (@DataTaskId, @CreatedAt, @RecordType, @Attempts, @MaxAttempts, @CompletedCount)";
		var taskId = Guid.NewGuid();
		var now = DateTime.UtcNow;

		_ = await db.Connection.ExecuteAsync(insertSql, new
		{
			DataTaskId = taskId,
			CreatedAt = now,
			RecordType = "User",
			Attempts = 0,
			MaxAttempts = 3,
			CompletedCount = 0
		}).ConfigureAwait(true);

		// Act
		var count = await db.Connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM DataProcessor.DataTaskRequests").ConfigureAwait(true);

		// Assert
		count.ShouldBe(1);

		testDb.Dispose();
	}

	protected override async Task OnDatabaseInitialized(IDbConnection connection) =>
		await DataProcessingMother.EnsureDatabaseInitializedAsync(connection, DatabaseEngine.PostgreSql).ConfigureAwait(false);
}
