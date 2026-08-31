// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Data;

using Dapper;

using Excalibur.Data;
using Excalibur.Data.SqlServer.Persistence;

using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging.Abstractions;


namespace Excalibur.Integration.Tests.Data;

/// <summary>
/// Integration tests bound to the SHIPPED SQL Server persistence provider - the type
/// AddSqlServerPersistence registers - covering database statistics materialization and
/// batch execution atomicity against real SQL Server.
/// </summary>
[Collection(SqlServerTestCollection.CollectionName)]
[Trait("Category", "Integration")]
[Trait("Database", "SqlServer")]
public sealed class SqlServerShippedPersistenceProviderShould : IAsyncLifetime
{
	private readonly SqlServerContainerFixture _fixture;
	private string _tableName = null!;

	public SqlServerShippedPersistenceProviderShould(SqlServerContainerFixture fixture) => _fixture = fixture;

	public async ValueTask InitializeAsync()
	{
		_tableName = $"BatchTest_{Guid.NewGuid():N}"[..30];
		await using var conn = new SqlConnection(_fixture.ConnectionString);
		await conn.OpenAsync();
		await conn.ExecuteAsync($"CREATE TABLE [{_tableName}] (Id INT PRIMARY KEY, Name NVARCHAR(200) NOT NULL)");
	}

	public async ValueTask DisposeAsync()
	{
		await using var conn = new SqlConnection(_fixture.ConnectionString);
		await conn.OpenAsync();
		await conn.ExecuteAsync($"DROP TABLE IF EXISTS [{_tableName}]");
	}

	private SqlServerPersistenceProvider BuildProvider()
	{
		var opts = new SqlServerPersistenceOptions
		{
			ConnectionString = _fixture.ConnectionString,
			Security = { TrustServerCertificate = true },
		};
		var wrapped = Microsoft.Extensions.Options.Options.Create(opts);
		return new SqlServerPersistenceProvider(
			wrapped,
			NullLogger<SqlServerPersistenceProvider>.Instance,
			NullLoggerFactory.Instance,
			new SqlServerPersistenceMetrics(wrapped),
			new StubRetryPolicy());
	}

	private sealed class StubRetryPolicy : Excalibur.Data.Resilience.IDataRequestRetryPolicy
	{
		public int MaxRetryAttempts => 0;
		public TimeSpan BaseRetryDelay => TimeSpan.Zero;
		public bool ShouldRetry(Exception exception) => false;
	}

	[Fact]
	public async Task ReturnDatabaseStatistics()
	{
		using var provider = BuildProvider();
		var stats = await provider.GetDatabaseStatisticsAsync(CancellationToken.None);
		stats.ShouldNotBeNull();
		stats["SqlServerEdition"].ShouldBeOfType<string>();
		stats["ProductVersion"].ShouldBeOfType<string>();
		stats["ProductLevel"].ShouldBeOfType<string>();
	}

	[Fact]
	public async Task ExecuteBatchAndPersistEveryRow()
	{
		using var provider = BuildProvider();

		var requests = new List<IDataRequest<IDbConnection, object>>();
		for (var i = 1; i <= 3; i++)
		{
			requests.Add(new InsertRequest(_tableName, i, $"Item-{i}", CancellationToken.None));
		}

		var results = await provider.ExecuteBatchAsync(requests, CancellationToken.None);
		results.Count().ShouldBe(3);

		await using var conn = new SqlConnection(_fixture.ConnectionString);
		await conn.OpenAsync();
		var count = await conn.ExecuteScalarAsync<int>($"SELECT COUNT(*) FROM [{_tableName}]");
		count.ShouldBe(3);
	}

	[Fact]
	public async Task ExecuteBatchInTransactionScopeAndPersist()
	{
		using var provider = BuildProvider();

		var requests = new List<IDataRequest<IDbConnection, object>>();
		for (var i = 1; i <= 3; i++)
		{
			requests.Add(new InsertRequest(_tableName, i, $"Item-{i}", CancellationToken.None));
		}

		int count;
		using (var scope = provider.CreateTransactionScope())
		{
			var results = await provider.ExecuteBatchInTransactionAsync(requests, scope, CancellationToken.None);
			await scope.CommitAsync(CancellationToken.None);
			count = results.Count();
		}

		count.ShouldBe(3);

		await using var verify = new SqlConnection(_fixture.ConnectionString);
		await verify.OpenAsync();
		var persisted = await verify.ExecuteScalarAsync<int>($"SELECT COUNT(*) FROM [{_tableName}]");
		persisted.ShouldBe(3, "scope-path batch must actually commit its rows");
	}

	[Fact]
	public async Task RollBackTheWholeBatchWhenOneRequestFails()
	{
		using var provider = BuildProvider();

		// Third request duplicates the first Id, violating the primary key.
		var requests = new List<IDataRequest<IDbConnection, object>>
		{
			new InsertRequest(_tableName, 1, "Item-1", CancellationToken.None),
			new InsertRequest(_tableName, 2, "Item-2", CancellationToken.None),
			new InsertRequest(_tableName, 1, "Item-dup", CancellationToken.None),
		};

		_ = await Should.ThrowAsync<Exception>(
			async () => await provider.ExecuteBatchAsync(requests, CancellationToken.None));

		await using var verify = new SqlConnection(_fixture.ConnectionString);
		await verify.OpenAsync();
		var persisted = await verify.ExecuteScalarAsync<int>($"SELECT COUNT(*) FROM [{_tableName}]");
		persisted.ShouldBe(0, "a failed batch must roll back every row it inserted");
	}

	private sealed class InsertRequest : IDataRequest<IDbConnection, object>
	{
		public InsertRequest(string tableName, int id, string name, CancellationToken cancellationToken)
		{
			var sql = $"INSERT INTO [{tableName}] (Id, Name) VALUES (@Id, @Name); SELECT @Id";
			Parameters = new DynamicParameters();
			Parameters.Add("Id", id);
			Parameters.Add("Name", name);
			Command = new CommandDefinition(sql, Parameters, cancellationToken: cancellationToken);
			ResolveAsync = async conn => (object)await conn.QuerySingleAsync<int>(Command);
		}

		public CommandDefinition Command { get; }
		public DynamicParameters Parameters { get; }
		public Func<IDbConnection, Task<object>> ResolveAsync { get; }
		public string RequestId { get; } = Guid.NewGuid().ToString();
		public string RequestType => "ReproInsert";
		public DateTimeOffset CreatedAt { get; } = DateTimeOffset.UtcNow;
		public string? CorrelationId => null;
		public IDictionary<string, object>? Metadata => null;
	}
}
