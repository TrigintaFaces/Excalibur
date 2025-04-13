using Excalibur.DataAccess.DataProcessing;
using Excalibur.Tests.Fixtures;
using Excalibur.Tests.Infrastructure.TestBaseClasses.PersistenceOnly;
using Excalibur.Tests.Shared;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using Shouldly;

using Xunit.Abstractions;

namespace Excalibur.Tests.Functional.DataAccess.DataProcessing;

public class SqlServerDataToProcessDbShould(SqlServerContainerFixture fixture, ITestOutputHelper output)
	: SqlServerPersistenceOnlyTestBase(fixture, output)
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

		return Task.CompletedTask;
	}

	[Fact]
	public Task ConnectionShouldBeValid()
	{
		// Arrange
		var testDb = GetRequiredService<TestDb>();
		var db = new DataToProcessDb(testDb);

		// Act
		var sqlConnection = db.Connection;

		// Assert
		_ = sqlConnection.ShouldNotBeNull();
		sqlConnection.State.ShouldBe(System.Data.ConnectionState.Open);

		return Task.CompletedTask;
	}

	protected override void AddServices(IServiceCollection services, IConfiguration configuration)
	{
	}
}
