using System.Data;

using Excalibur.DataAccess;
using Excalibur.DataAccess.SqlServer;
using Excalibur.Tests.Fixtures;
using Excalibur.Tests.Infrastructure.TestBaseClasses.PersistenceOnly;

using Microsoft.Data.SqlClient;

using Shouldly;

using Xunit.Abstractions;

namespace Excalibur.Tests.Integration.DataAccess.SqlServer;

public class SqlDbShould(SqlServerContainerFixture fixture, ITestOutputHelper output) : SqlServerPersistenceOnlyTestBase(fixture, output)
{
	[Fact]
	public void ProperlyImplementIDbInterface()
	{
		// Arrange & Act
		using var connection = Fixture.CreateDbConnection();
		using var sqlDb = new SqlDb(connection);

		// Assert
		_ = sqlDb.ShouldBeAssignableTo<IDb>();
		_ = sqlDb.Connection.ShouldNotBeNull();
	}

	[Fact]
	public void HandleConnectionStateCorrectly()
	{
		// Arrange
		using var connection = Fixture.CreateDbConnection();
		using var sqlDb = new SqlDb(connection);

		// Act & Assert - Open should put the connection in a ready state
		sqlDb.Open();
		sqlDb.Connection.State.ShouldBe(ConnectionState.Open);

		// Act & Assert - Close should close the connection
		sqlDb.Close();
		connection.State.ShouldBe(ConnectionState.Closed);
	}

	[Fact]
	public async Task ExecuteQueryAgainstRealDatabase()
	{
		// Arrange
		using var connection = Fixture.CreateDbConnection();
		connection.Open();

		// Act Create a simple test query to verify connection works
		using var command = connection.CreateCommand();
		command.CommandText = "SELECT 1 AS TestValue";

		// Use regular synchronous API since ExecuteReaderAsync isn't available on IDbCommand
		using var reader = command.ExecuteReader();
		var hasRows = reader.Read();
		var value = hasRows ? reader.GetInt32(0) : -1;

		// Assert
		hasRows.ShouldBeTrue();
		value.ShouldBe(1);

		await Task.CompletedTask.ConfigureAwait(true);
	}

	[Fact]
	public void DisposeConnectionWhenDisposed()
	{
		SqlConnection connection = null;
		try
		{
			connection = new SqlConnection("Data Source=(local);Initial Catalog=TestDb;Integrated Security=True");

			using (var sqlDb = new SqlDb(connection))
			{
				// trigger Dispose
			}

			// Act & Assert
			var ex = Should.Throw<InvalidOperationException>(() => connection.Open());
			ex.Message.ShouldContain("ConnectionString");
		}
		finally
		{
			connection?.Dispose();
		}
	}

	[Fact]
	public void ProperlyHandleConnectionErrors()
	{
		SqlConnection connection = null;
		try
		{
			// Create an invalid connection
			connection = new SqlConnection("Data Source=nonexistent;Initial Catalog=NonExistentDb;Connection Timeout=1");

			// Use a using statement to ensure proper disposal
			using (var sqlDb = new SqlDb(connection))
			{
				// Act & Assert - Attempting to open should throw
				var exception = Should.Throw<SqlException>(() => sqlDb.Open());

				// Optional: verify common part of error message
				exception.Message.ShouldContain("network-related", Case.Insensitive);
			}
		}
		finally
		{
			// Cleanup - Though connection should already be disposed at this point
			connection?.Dispose();
		}
	}
}
