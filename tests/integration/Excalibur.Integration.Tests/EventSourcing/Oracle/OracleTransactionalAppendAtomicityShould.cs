// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Data;

using Excalibur.Dispatch;
using Excalibur.EventSourcing;
using Excalibur.EventSourcing.Oracle;
using Excalibur.Integration.Tests.Data.EventStore;

using Microsoft.Extensions.Logging.Abstractions;

using Oracle.ManagedDataAccess.Client;

using Tests.Shared.Fixtures;

#pragma warning disable CA2100 // SQL strings are safe - identifiers are constants in test fixture

namespace Excalibur.Integration.Tests.EventSourcing.Oracle;

/// <summary>
/// k44na4 — real-infra lock (Oracle, gvenzl/oracle-free) for the same TRUE-concurrent-race gap fixed on
/// <see cref="ITransactionalEventStore.AppendWithOutboxStagingAsync"/>: before this bead,
/// <c>OracleEventStore.ExecuteAppendWithOutboxTransactionAsync</c> bare-rethrew on every exception, so a
/// genuine race lost past the deterministic pre-check surfaced as a raw <see cref="OracleException"/>
/// instead of <see cref="AppendResult.CreateConcurrencyConflict(long, long)"/> — unlike the plain
/// <c>AppendAsync</c> path, which already classified via <c>IsLostRace</c>.
/// </summary>
/// <remarks>
/// No delay hook is needed: every writer presents the SAME (correct at the time it reads) expectedVersion,
/// so all of them pass the pre-check inside their own transaction and race at INSERT/COMMIT time. Oracle's
/// own unique-constraint enforcement on <c>(AggregateId, AggregateType, Version, TenantId)</c> — the
/// property 4wjpvg (already landed) retired the SERIALIZABLE retry loop in favour of — decides the race;
/// exactly one writer's transaction commits and every other loses on the unique constraint once it unblocks.
/// </remarks>
[Trait("Category", "Integration")]
[Trait("Database", "Oracle")]
[Trait("Component", "EventStore")]
[Collection(OracleEventStoreTestCollection.CollectionName)]
public sealed class OracleTransactionalAppendAtomicityShould : IAsyncLifetime
{
	private readonly OracleEventStoreContainerFixture _fixture;

	public OracleTransactionalAppendAtomicityShould(OracleEventStoreContainerFixture fixture)
	{
		_fixture = fixture;
	}

	public async ValueTask InitializeAsync()
	{
		_fixture.DockerAvailable.ShouldBeTrue(
			"Oracle container must be available - real-infra conformance is never skipped.");

		await _fixture.EnsureInitializedAsync().ConfigureAwait(false);
		await EnsureOutboxTableAsync().ConfigureAwait(false);
	}

	public async ValueTask DisposeAsync() => await _fixture.CleanupTableAsync().ConfigureAwait(false);

	// -------------------------------------------------------------------------------------------------
	// k44na4 — TRUE concurrent race. Mirrors
	// SqlServerTransactionalAppendAtomicityShould.ClassifyATrueConcurrentRace_AsConcurrencyConflict_NotARawException.
	// -------------------------------------------------------------------------------------------------
	[Fact]
	public async Task ClassifyATrueConcurrentRace_AsConcurrencyConflict_NotARawException()
	{
		var store = (ITransactionalEventStore)CreateEventStore();
		var aggId = "agg-" + Guid.NewGuid().ToString("N");
		const string type = "TestAggregate";
		const int concurrency = 8;

		var tasks = Enumerable.Range(0, concurrency).Select(async i =>
		{
			var outboxId = $"ob-{i}-" + Guid.NewGuid().ToString("N");
			try
			{
				var result = await store.AppendWithOutboxStagingAsync(
					aggId, type, [new TestDomainEvent(aggId, 0)], expectedVersion: -1,
					async (txn, ct) => await InsertOutboxRowAsync(txn, outboxId, ct).ConfigureAwait(false),
					CancellationToken.None).ConfigureAwait(false);
				return (Result: (AppendResult?)result, Exception: (Exception?)null);
			}
			catch (Exception ex)
			{
				return (Result: (AppendResult?)null, Exception: ex);
			}
		}).ToArray();

		var outcomes = await Task.WhenAll(tasks).ConfigureAwait(false);

		var successes = outcomes.Where(o => o.Result is { IsConcurrencyConflict: false }).ToList();
		var conflicts = outcomes.Where(o => o.Result is { IsConcurrencyConflict: true }).ToList();
		var rawExceptions = outcomes.Where(o => o.Exception is not null).ToList();

		successes.Count.ShouldBe(1,
			"exactly one of the concurrent writers must win the race and commit version 0");
		(conflicts.Count + rawExceptions.Count).ShouldBe(concurrency - 1,
			"every losing writer must be accounted for as either a classified conflict or a raw exception");

		// THE ASSERTION k44na4 EXISTS FOR.
		rawExceptions.ShouldBeEmpty(
			"a true concurrent race must classify as AppendResult.CreateConcurrencyConflict, matching the " +
			"plain-append contract — not surface as a raw exception. Raw exception types seen: " +
			string.Join(", ", rawExceptions.Select(o => o.Exception!.GetType().Name)));

		(await CountEventsAsync(aggId).ConfigureAwait(false)).ShouldBe(1,
			"only the single winning writer's event must persist at version 0");
	}

	private IEventStore CreateEventStore() =>
		new OracleEventStore(
			_fixture.CreateConnection, NullLogger<OracleEventStore>.Instance, SingleTenantTestContext.Instance,
			schema: _fixture.Schema, table: _fixture.TableName);

	private async Task<int> CountEventsAsync(string aggregateId)
	{
		await using var connection = _fixture.CreateConnection();
		await connection.OpenAsync().ConfigureAwait(false);
		await using var command = connection.CreateCommand();
		command.CommandText = $"SELECT COUNT(*) FROM {_fixture.TableName} WHERE AggregateId = :id";
		_ = command.Parameters.Add(new OracleParameter("id", aggregateId));
		return Convert.ToInt32(await command.ExecuteScalarAsync().ConfigureAwait(false));
	}

	private static async ValueTask InsertOutboxRowAsync(IDbTransaction transaction, string outboxId, CancellationToken ct)
	{
		var oracleTransaction = (OracleTransaction)transaction;
		await using var command = ((OracleConnection)oracleTransaction.Connection!).CreateCommand();
		command.Transaction = oracleTransaction;
		command.CommandText = "INSERT INTO TestOutbox (Id, Payload) VALUES (:id, :p)";
		_ = command.Parameters.Add(new OracleParameter("id", outboxId));
		_ = command.Parameters.Add(new OracleParameter("p", "payload"));
		_ = await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
	}

	private async Task EnsureOutboxTableAsync()
	{
		await using var connection = _fixture.CreateConnection();
		await connection.OpenAsync().ConfigureAwait(false);

		await using var command = connection.CreateCommand();
		command.CommandText = """
			CREATE TABLE TestOutbox (
				Id VARCHAR2(255) NOT NULL PRIMARY KEY,
				Payload VARCHAR2(4000) NOT NULL
			)
			""";

		try
		{
			_ = await command.ExecuteNonQueryAsync().ConfigureAwait(false);
		}
		catch (OracleException ex) when (ex.Number == 955)
		{
			// ORA-00955: name is already used by an existing object -- table already created.
		}
	}

	[MessageName("Test.OracleTransactionalAppendAtomicity.TestDomainEvent")]
	private sealed record TestDomainEvent : IDomainEvent
	{
		public TestDomainEvent(string aggregateId, long version)
		{
			EventId = Guid.NewGuid().ToString();
			AggregateId = aggregateId;
			Version = version;
			OccurredAt = DateTimeOffset.UtcNow;
		}

		public string EventId { get; init; }
		public string AggregateId { get; init; }
		public long Version { get; init; }
		public DateTimeOffset OccurredAt { get; init; }
		public IDictionary<string, object>? Metadata => null;
	}
}
