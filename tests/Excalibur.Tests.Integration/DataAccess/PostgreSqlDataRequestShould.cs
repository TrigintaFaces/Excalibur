using System.Data;

using Excalibur.DataAccess;
using Excalibur.Tests.Fixtures;
using Excalibur.Tests.Infrastructure.TestBaseClasses.Host;
using Excalibur.Tests.Mothers;
using Excalibur.Tests.Shared;

using Npgsql;

using Xunit.Abstractions;

namespace Excalibur.Tests.Integration.DataAccess;

public class PostgreSqlDataRequestShould(PostgreSqlContainerFixture fixture, ITestOutputHelper output)
	: PostgreSqlHostTestBase(fixture, output)
{
	[Fact]
	public async Task GetUserByIdShouldReturnUserWhenUserExists()
	{
		using var connection = fixture.CreateDbConnection() as NpgsqlConnection;
		await connection.OpenAsync().ConfigureAwait(true);

		var request = new GetUserByIdRequest(1);
		var user = await connection.ResolveAsync(request).ConfigureAwait(true);

		Assert.NotNull(user);
		Assert.Equal(1, user.Id);
		Assert.Equal("Jane Doe", user.Name);
	}

	protected override async Task OnDatabaseInitialized(IDbConnection connection) =>
		await UserMother.EnsureDatabaseInitializedAsync(connection, DatabaseEngine.PostgreSql).ConfigureAwait(false);
}
