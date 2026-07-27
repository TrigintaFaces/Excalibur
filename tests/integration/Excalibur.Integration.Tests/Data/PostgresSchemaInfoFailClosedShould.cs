// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
using Excalibur.Data;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using PersistenceProvider = Excalibur.Data.Postgres.Persistence.PostgresPersistenceProvider;
using PostgresPersistenceOptions = Excalibur.Data.Postgres.Persistence.PostgresPersistenceOptions;

namespace Excalibur.Integration.Tests.Data;

/// <summary>
/// Real-Postgres regression lock for the fail-closed behaviour of
/// <see cref="Excalibur.Data.Postgres.Persistence.PostgresPersistenceProvider.GetSchemaInfoAsync"/>.
/// </summary>
/// <remarks>
/// The method previously returned a success-shaped dictionary carrying an "error" key for a table that
/// does not exist. A caller that did not know to probe for that key received a value, no exception, and
/// concluded the call succeeded against a table that is absent — a fail-silent success signal with no
/// effect behind it. The fix throws <see cref="ResourceNotFoundException"/> instead.
///
/// <para>
/// The two arms are paired per testing-patterns §3 (safety must be joined with liveness): the SAFETY arm
/// proves the missing table is refused (RED on the pre-fix impl, which returned normally); the LIVENESS
/// arm proves an existing table still returns its schema — so the fix is enforcement, not "throw on
/// everything" (a gate that refuses every input is safe and useless). Both run against real Postgres via
/// TestContainers per verify-against-real-infra-not-mock: a mocked connection cannot reproduce the
/// server's actual "table absent" result.
/// </para>
/// </remarks>
[Collection(PostgresTestCollection.CollectionName)]
[Trait("Category", "Integration")]
[Trait("Component", "Core")]
[Trait("Database", "Postgres")]
public sealed class PostgresSchemaInfoFailClosedShould
{
	private readonly PostgresContainerFixture _fixture;

	public PostgresSchemaInfoFailClosedShould(PostgresContainerFixture fixture)
	{
		_fixture = fixture;
	}

	[Fact]
	public async Task ThrowResourceNotFound_WhenTableDoesNotExist()
	{
		// Arrange
		using var provider = CreateInitializedProvider(out var options);
		await provider.InitializeAsync(options, CancellationToken.None);

		// Act + Assert — an absent table must fail closed, not return a success-shaped dictionary.
		// RED on the pre-fix impl (which set schemaInfo["error"] and returned normally, no throw).
		_ = await Should.ThrowAsync<ResourceNotFoundException>(
			async () => await provider.GetSchemaInfoAsync(
				"rkyos3_table_that_does_not_exist", schemaName: null, CancellationToken.None));
	}

	[Fact]
	public async Task ReturnSchemaDictionary_WhenTableExists()
	{
		// Arrange
		const string tableName = "rkyos3_liveness_probe";
		using var provider = CreateInitializedProvider(out var options);
		await provider.InitializeAsync(options, CancellationToken.None);

		ExecuteNonQuery(provider, $"CREATE TABLE IF NOT EXISTS {tableName} (id integer PRIMARY KEY, name text)");
		try
		{
			// Act
			var schema = await provider.GetSchemaInfoAsync(tableName, schemaName: null, CancellationToken.None);

			// Assert — an existing table still yields its schema, no throw (liveness: the fix is
			// enforcement of the missing-table contract, not a blanket refusal).
			_ = schema.ShouldNotBeNull();
			schema["table_name"].ShouldBe(tableName);
			schema.ContainsKey("columns").ShouldBeTrue("an existing table must report its columns");
		}
		finally
		{
			ExecuteNonQuery(provider, $"DROP TABLE IF EXISTS {tableName}");
		}
	}

	private PersistenceProvider CreateInitializedProvider(out PostgresPersistenceOptions options)
	{
		options = new PostgresPersistenceOptions { ConnectionString = _fixture.ConnectionString };
		return new PersistenceProvider(Options.Create(options), NullLogger<PersistenceProvider>.Instance);
	}

	private static void ExecuteNonQuery(PersistenceProvider provider, string sql)
	{
		using var connection = provider.CreateConnection();
		connection.Open();
		using var command = connection.CreateCommand();
		command.CommandText = sql;
		_ = command.ExecuteNonQuery();
	}
}
