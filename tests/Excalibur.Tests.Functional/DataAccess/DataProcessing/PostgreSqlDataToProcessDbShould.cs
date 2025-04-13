using System.Data;

using Excalibur.DataAccess.DataProcessing;
using Excalibur.Tests.Fixtures;
using Excalibur.Tests.Infrastructure.TestBaseClasses.PersistenceOnly;
using Excalibur.Tests.Shared;

using Shouldly;

using Xunit.Abstractions;

namespace Excalibur.Tests.Functional.DataAccess.DataProcessing;

public class PostgreSqlDataToProcessDbShould(PostgreSqlContainerFixture fixture, ITestOutputHelper output)
	: PostgreSqlPersistenceOnlyTestBase(fixture, output)
{
	[Fact]
	public Task OpenAndCloseShouldNotThrow()
	{
		// Arrange
		var testDb = GetRequiredService<TestDb>();
		var db = new DataToProcessDb(testDb);

		// Act & Assert
		Should.NotThrow(db.Open);
		Should.NotThrow(db.Close);

		testDb.Dispose();
		return Task.CompletedTask;
	}

	[Fact]
	public Task ConnectionShouldBeValid()
	{
		// Arrange
		var testDb = GetRequiredService<TestDb>();
		var db = new DataToProcessDb(testDb);

		// Act
		var npgsqlConnection = db.Connection;

		// Assert
		_ = npgsqlConnection.ShouldNotBeNull();
		npgsqlConnection.State.ShouldBe(ConnectionState.Open);

		testDb.Dispose();
		return Task.CompletedTask;
	}
}
