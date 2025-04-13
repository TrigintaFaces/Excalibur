using System.Data;

using Excalibur.DataAccess;
using Excalibur.Tests.Fixtures;
using Excalibur.Tests.Infrastructure.TestBaseClasses.Host;
using Excalibur.Tests.Mothers;
using Excalibur.Tests.Shared;

using Shouldly;

using Xunit.Abstractions;

namespace Excalibur.Tests.Integration.DataAccess;

public class PostgreSqlDbShould(PostgreSqlContainerFixture fixture, ITestOutputHelper output)
	: PostgreSqlHostTestBase(fixture, output)
{
	[Fact]
	public async Task OpenShouldWorkWithRealDatabase()
	{
		// Arrange
		using var db = GetService<TestDb>();

		// Act & Assert
		Should.NotThrow(() => db.Open());
	}

	[Fact]
	public async Task CloseShouldWorkWithRealDatabase()
	{
		// Arrange
		using var db = GetService<TestDb>();

		// Act & Assert
		Should.NotThrow(() => db.Close());
	}

	[Fact]
	public async Task DisposeShouldWorkWithRealDatabase()
	{
		// Arrange
		using var db = GetService<TestDb>();

		// Act & Assert
		Should.NotThrow(() => db.Dispose());
	}

	[Fact]
	public async Task GetUserByIdShouldReturnUserWhenUserExists()
	{
		using var db = GetService<TestDb>();
		db.Open();

		var request = new GetUserByIdRequest(1);
		var user = await db.Connection.ResolveAsync(request).ConfigureAwait(true);

		// Assertions
		Assert.NotNull(user);
		Assert.Equal(1, user.Id);
		Assert.Equal("Jane Doe", user.Name);

		db.Close();
	}

	protected override async Task OnDatabaseInitialized(IDbConnection connection) =>
		await UserMother.EnsureDatabaseInitializedAsync(connection, DatabaseEngine.PostgreSql).ConfigureAwait(false);
}
