// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;
using Excalibur.Inbox.Postgres;
using Excalibur.Integration.Tests.Data.EventStore;

using Microsoft.Extensions.Logging.Abstractions;

using Npgsql;

#pragma warning disable CA2100 // SQL strings use a compile-time const table name in a test fixture.

namespace Excalibur.Integration.Tests.Inbox.Postgres;

/// <summary>
/// Docker-Postgres engage-test (bd-pux4gk AC-6, S842 ADR-336 Wave 2) for the real-provider atomicity of
/// <see cref="PostgresInboxStore.TryClaimAsync"/>. A unit lock with a fake store proves the middleware admits one;
/// only a real database proves the per-provider claim primitive (<c>INSERT … ON CONFLICT (message_id, handler_type)
/// DO NOTHING</c>) is itself atomic under genuine concurrency. N callers race the SAME (messageId, handlerType) at the
/// SQL layer; exactly one INSERT must win (the others conflict and claim <see langword="false"/>).
/// </summary>
/// <remarks>
/// Determinism: the exactly-one outcome is guaranteed by the database's row-uniqueness on the primary key, not by any
/// timing — no <c>sleep</c>, no barrier. A non-atomic check-then-insert would instead admit more than one (or throw a
/// unique-violation), so the <c>== 1</c> assertion is non-vacuous.
/// </remarks>
[Collection(PostgresEventStoreTestCollection.CollectionName)]
[Trait("Category", "Integration")]
[Trait("Database", "Postgres")]
[Trait("Component", "Inbox")]
public sealed class PostgresInboxStoreClaimAtomicityShould : IClassFixture<PostgresEventStoreContainerFixture>
{
	private const string TableName = "inbox_claim_atomicity_test";
	private const int Concurrency = 16;
	private readonly PostgresEventStoreContainerFixture _fixture;

	public PostgresInboxStoreClaimAtomicityShould(PostgresEventStoreContainerFixture fixture)
	{
		_fixture = fixture;
	}

	[Fact]
	public async Task Admit_exactly_one_claim_when_concurrent_callers_race_the_same_message()
	{
		await EnsureTableAsync();

		var connectionString = _fixture.ConnectionString;
		var options = new PostgresInboxOptions
		{
			ConnectionString = connectionString,
			SchemaName = "public",
			TableName = TableName,
		};
		var store = new PostgresInboxStore(
			() => new NpgsqlConnection(connectionString),
			options,
			NullLogger<PostgresInboxStore>.Instance);

		const string messageId = "msg-ac6";
		const string handlerType = "TestHandler";

		// Race N concurrent claims of the SAME (messageId, handlerType) on separate connections/threads.
		var tasks = Enumerable.Range(0, Concurrency)
			.Select(_ => Task.Run(() => store.TryClaimAsync(messageId, handlerType, CancellationToken.None).AsTask()))
			.ToArray();

		var results = await Task.WhenAll(tasks).ConfigureAwait(false);

		results.Count(claimed => claimed).ShouldBe(
			1,
			$"INSERT … ON CONFLICT must admit exactly one of {Concurrency} concurrent claims; got [{string.Join(",", results)}]");

		// A subsequent claim on the now-claimed key is denied (the claim row is non-terminal Processing).
		(await store.TryClaimAsync(messageId, handlerType, CancellationToken.None)).ShouldBeFalse(
			"a claim already held must be denied to a later caller");

		// Releasing the claim re-admits a redelivery (AC-4 semantics, proven against the real provider).
		await store.ReleaseAsync(messageId, handlerType, CancellationToken.None);
		(await store.TryClaimAsync(messageId, handlerType, CancellationToken.None)).ShouldBeTrue(
			"after release the message must be re-admitted on the real provider");
	}

	private async Task EnsureTableAsync()
	{
		await using var connection = _fixture.CreateConnection();
		await connection.OpenAsync().ConfigureAwait(false);

		const string sql = $"""
			DROP TABLE IF EXISTS public.{TableName};
			CREATE TABLE public.{TableName} (
				message_id VARCHAR(255) NOT NULL,
				handler_type VARCHAR(255) NOT NULL,
				message_type VARCHAR(255) NOT NULL,
				payload BYTEA NOT NULL,
				metadata JSONB,
				received_at TIMESTAMPTZ NOT NULL,
				processed_at TIMESTAMPTZ,
				status INT NOT NULL,
				retry_count INT NOT NULL,
				correlation_id VARCHAR(255),
				tenant_id VARCHAR(255),
				source VARCHAR(255),
				last_error TEXT,
				last_attempt_at TIMESTAMPTZ,
				PRIMARY KEY (message_id, handler_type)
			);
			""";

		await using var command = new NpgsqlCommand(sql, connection);
		_ = await command.ExecuteNonQueryAsync().ConfigureAwait(false);
	}
}
