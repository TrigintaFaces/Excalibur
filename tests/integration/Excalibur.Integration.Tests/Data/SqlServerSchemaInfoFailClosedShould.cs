// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Dapper;

using Excalibur.Data;
using Excalibur.Data.SqlServer.Persistence;

using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Integration.Tests.Data;

/// <summary>
/// Real-SQL-Server regression lock for the fail-closed behaviour of
/// <see cref="SqlServerPersistenceProvider.GetSchemaInfoAsync"/>.
/// </summary>
/// <remarks>
/// <para>
/// The method previously returned a dictionary with an EMPTY column list for a table that does not
/// exist, which is indistinguishable to a reading caller from a table that is present. A table with zero
/// columns is not a state SQL Server can be in, so the empty result carried no information the caller
/// could act on and every caller read "absent" as "present with nothing in it". The Postgres provider
/// already stated the opposite contract for the same call, so a consumer swapping providers silently
/// swapped a throw for a value. Both now raise <see cref="ResourceNotFoundException"/>.
/// </para>
/// <para>
/// The arms are paired: the SAFETY arm proves the missing table is refused (RED on the pre-fix
/// implementation, which returned a dictionary); the LIVENESS arm proves an existing table still returns
/// its schema — so the fix is enforcement of the missing-table contract, not a blanket refusal that
/// would satisfy the safety arm perfectly and be useless. Both run against real SQL Server: a mocked
/// connection cannot reproduce the server's actual empty result for an absent table.
/// </para>
/// </remarks>
[Collection(SqlServerTestCollection.CollectionName)]
[Trait("Category", "Integration")]
[Trait("Component", "Core")]
[Trait("Database", "SqlServer")]
public sealed class SqlServerSchemaInfoFailClosedShould
{
	private readonly SqlServerContainerFixture _fixture;

	public SqlServerSchemaInfoFailClosedShould(SqlServerContainerFixture fixture) => _fixture = fixture;

	[Fact]
	public async Task ThrowResourceNotFound_WhenTableDoesNotExist()
	{
		using var provider = BuildProvider();

		// RED on the pre-fix impl, which returned a dictionary whose "Columns" list was empty.
		_ = await Should.ThrowAsync<ResourceNotFoundException>(
			async () => await provider.GetSchemaInfoAsync(
				"table_that_does_not_exist_ff41", schemaName: null, CancellationToken.None));
	}

	[Fact]
	public async Task ReturnSchemaDictionary_WhenTableExists()
	{
		var tableName = $"SchemaInfoLiveness_{Guid.NewGuid():N}"[..30];
		using var provider = BuildProvider();

		await using var connection = new SqlConnection(_fixture.ConnectionString);
		await connection.OpenAsync(TestContext.Current.CancellationToken);
		_ = await connection.ExecuteAsync(
			$"CREATE TABLE [{tableName}] (Id INT PRIMARY KEY, Name NVARCHAR(200) NOT NULL)");

		try
		{
			var schema = await provider.GetSchemaInfoAsync(tableName, schemaName: null, CancellationToken.None);

			// Liveness: an existing table still yields its schema, no throw.
			_ = schema.ShouldNotBeNull();
			schema["TableName"].ShouldBe(tableName);
			schema.ContainsKey("Columns").ShouldBeTrue("an existing table must report its columns");
		}
		finally
		{
			_ = await connection.ExecuteAsync($"DROP TABLE IF EXISTS [{tableName}]");
		}
	}

	private SqlServerPersistenceProvider BuildProvider()
	{
		var options = new SqlServerPersistenceOptions
		{
			ConnectionString = _fixture.ConnectionString,
			Security = { TrustServerCertificate = true },
		};
		var wrapped = Microsoft.Extensions.Options.Options.Create(options);

		return new SqlServerPersistenceProvider(
			wrapped,
			NullLogger<SqlServerPersistenceProvider>.Instance,
			NullLoggerFactory.Instance,
			new SqlServerPersistenceMetrics(wrapped),
			new NoRetryPolicy());
	}

	private sealed class NoRetryPolicy : Excalibur.Data.Resilience.IDataRequestRetryPolicy
	{
		public int MaxRetryAttempts => 0;

		public TimeSpan BaseRetryDelay => TimeSpan.Zero;

		public bool ShouldRetry(Exception exception) => false;
	}
}
