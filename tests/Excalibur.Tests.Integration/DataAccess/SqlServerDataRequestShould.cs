using System.Data;

using Excalibur.DataAccess;
using Excalibur.Tests.Fixtures;
using Excalibur.Tests.Infrastructure.TestBaseClasses.Host;
using Excalibur.Tests.Mothers;
using Excalibur.Tests.Shared;

using Microsoft.Data.SqlClient;

using Xunit.Abstractions;

namespace Excalibur.Tests.Integration.DataAccess;

public class SqlServerDataRequestShould(SqlServerContainerFixture fixture, ITestOutputHelper output)
	: SqlServerHostTestBase(fixture, output)
{
	[Fact]
	public async Task GetUserByIdShouldReturnUserWhenUserExists()
	{
		using var connection = GetRequiredService<IDbConnection>() as SqlConnection;
		await connection.OpenAsync().ConfigureAwait(true);

		var request = new GetUserByIdRequest(1);
		var user = await connection.ResolveAsync(request).ConfigureAwait(true);

		Assert.NotNull(user);
		Assert.Equal(1, user.Id);
		Assert.Equal("John Doe", user.Name);
	}

	protected override async Task OnDatabaseInitialized(IDbConnection connection) =>
		await UserMother.EnsureDatabaseInitializedAsync(connection, DatabaseEngine.SqlServer).ConfigureAwait(false);
}
