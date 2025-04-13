using System.Data;

using Dapper;

using Excalibur.DataAccess;
using Excalibur.Tests.Fixtures;
using Excalibur.Tests.Shared;

namespace Excalibur.Tests.Mothers;

public static class UserMother
{
	public static async Task EnsureDatabaseInitializedAsync(IDbConnection connection, DatabaseEngine engine)
	{
		var createTableSql = engine switch
		{
			DatabaseEngine.SqlServer => """
			                            IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Users')
			                            CREATE TABLE Users (
			                                Id INT IDENTITY(1,1) PRIMARY KEY,
			                                Name NVARCHAR(100) NOT NULL
			                            );
			                            """,
			DatabaseEngine.PostgreSql => """
			                             CREATE TABLE IF NOT EXISTS Users (
			                                 Id SERIAL PRIMARY KEY,
			                                 Name VARCHAR(100) NOT NULL
			                             );
			                             """,
			_ => throw new NotSupportedException($"Unsupported engine: {engine}")
		};

		var insertUserSql = engine switch
		{
			DatabaseEngine.SqlServer => """
			                            IF NOT EXISTS (SELECT * FROM Users WHERE Id = 1)
			                            INSERT INTO Users (Name) VALUES ('John Doe');
			                            """,
			DatabaseEngine.PostgreSql => """
			                             INSERT INTO Users (Name)
			                             SELECT 'Jane Doe'
			                             WHERE NOT EXISTS (SELECT 1 FROM Users WHERE Id = 1);
			                             """,
			_ => throw new NotSupportedException($"Unsupported engine: {engine}")
		};

		// Execute schema and seed data
		_ = await connection.Ready().ExecuteAsync(createTableSql).ConfigureAwait(false);
		_ = await connection.Ready().ExecuteAsync(insertUserSql).ConfigureAwait(false);
	}

	public static User Create(string name = "Jane Doe", int? id = null) =>
		new() { Id = id ?? 1, Name = name };

	public static IEnumerable<User> CreateMany(int count) =>
		Enumerable.Range(1, count).Select(i => Create($"User {i}", i));
}
