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

			// The index arm is load-bearing, not decoration: index materialization threw on every call
			// while the general catch recorded the message under an "error" key and returned the partial
			// dictionary, so this key was ABSENT from every result and no arm noticed. Asserting it is
			// what keeps the read whole rather than merely non-throwing. The probe table has a primary
			// key, so at least one index must come back.
			schema.ContainsKey("indexes").ShouldBeTrue(
				"an existing table must report its indexes; an absent key means the index read failed and "
				+ "was swallowed");
			((System.Collections.ICollection)schema["indexes"]).Count.ShouldBeGreaterThan(
				0,
				"the probe table declares a primary key, so its index list cannot be empty");
		}
		finally
		{
			ExecuteNonQuery(provider, $"DROP TABLE IF EXISTS {tableName}");
		}
	}

	[Fact]
	public async Task PropagateTheFailure_WhenTheServerCannotBeReached()
	{
		// Arrange — initialise against the live server (initialisation requires a working connection), then
		// point the SAME options instance at a host that does not exist. The provider builds its connection
		// string per call, so the next read fails at connect time. A distinct host means a distinct
		// connection pool, so nothing this arm does can disturb another test's pooled connections.
		using var provider = CreateInitializedProvider(out var options);
		await provider.InitializeAsync(options, CancellationToken.None);

		options.ConnectionString =
			"Host=127.0.0.1;Port=1;Database=nope;Username=nope;Password=nope;Timeout=1;Command Timeout=1";

		// Act + Assert — a failure that is NOT a missing table must propagate too. The pre-fix impl caught
		// EVERY exception, recorded its message under an "error" key and RETURNED NORMALLY, so a caller
		// that did not know to probe for that key read an unreachable server as a schema it could act on.
		// RED on the pre-fix impl, which returned a dictionary here rather than throwing.
		//
		// This is not hypothetical: the pre-fix swallow was concealing a live defect on the SUCCESS path
		// too. Index materialization threw on every real call, so every consumer received a schema with
		// columns, no indexes, and no error they would ever see. Making the failure loud is what surfaced
		// it; the liveness arm below now asserts the indexes it was silently missing.
		_ = await Should.ThrowAsync<Exception>(
			async () => await provider.GetSchemaInfoAsync(
				"rkyos3_liveness_probe", schemaName: null, CancellationToken.None));
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
