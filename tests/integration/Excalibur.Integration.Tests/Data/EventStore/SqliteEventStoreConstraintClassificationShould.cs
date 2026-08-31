// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;
using Excalibur.EventSourcing;
using Excalibur.EventSourcing.Sqlite;
using Excalibur.Integration.Tests.Infrastructure;

using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;

using Shouldly;

using Xunit;

namespace Excalibur.Integration.Tests.Data.EventStore;

/// <summary>
/// Real-SQLite lock on the direction the conflict classification can be wrong in: reporting a breach
/// that is NOT a lost race as though it were one.
/// </summary>
/// <remarks>
/// <para>
/// SQLite reports every constraint under one PRIMARY result code and names the individual constraint
/// only in the EXTENDED code. The events table declares eight NOT NULL columns alongside its UNIQUE
/// stream key, so reading the primary code alone classifies a NOT NULL breach as a lost race. A caller
/// whose retry policy keys on the conflict flag then reloads the aggregate and re-attempts a write that
/// cannot ever succeed, once per attempt its policy allows, and reports a concurrency conflict that no
/// concurrent writer was ever party to.
/// </para>
/// <para>
/// The first arm proves the store now separates the two. The second arm establishes, against the real
/// engine rather than from a table of constants, the fact the first arm depends on: two entirely
/// different breaches carry the SAME primary code and different extended codes, so the primary code
/// cannot discriminate between them and the extended one can.
/// </para>
/// <para>
/// SQLite is an embedded engine, so this is real infrastructure with no container and is never skipped.
/// No mock is involved anywhere: the codes asserted are the ones the engine itself produced.
/// </para>
/// </remarks>
[Collection(SqliteEventStoreTestCollection.CollectionName)]
[Trait("Category", "Integration")]
[Trait("Component", "Core")]
[Trait("Database", "Sqlite")]
public sealed class SqliteEventStoreConstraintClassificationShould : IClassFixture<SqliteEventStoreFixture>
{
	private const string AggregateType = "ConstraintClassificationAggregate";

	private const int SqliteConstraint = 19;
	private const int SqliteConstraintNotNull = 1299;
	private const int SqliteConstraintUnique = 2067;

	private readonly SqliteEventStoreFixture _fixture;

	/// <summary>
	/// Initializes a new instance of the <see cref="SqliteEventStoreConstraintClassificationShould"/> class.
	/// </summary>
	/// <param name="fixture">The SQLite fixture.</param>
	public SqliteEventStoreConstraintClassificationShould(SqliteEventStoreFixture fixture) => _fixture = fixture;

	[Fact]
	public async Task RefuseAnAppendThatBreachesItsOwnConstraint_RatherThanCallingItALostRace()
	{
		await _fixture.CleanupAsync().ConfigureAwait(false);

		var store = StoreFor("tenant-" + Guid.NewGuid().ToString("N"));
		var aggregateId = "agg-" + Guid.NewGuid().ToString("N");

		var seed = await store.AppendAsync(
			aggregateId,
			AggregateType,
			new IDomainEvent[] { new Placed(aggregateId) },
			expectedVersion: -1,
			CancellationToken.None).ConfigureAwait(false);
		seed.Success.ShouldBeTrue("the seed append fixes the stream at a known version");

		// An event whose identifier is absent breaches a NOT NULL column. Nothing else touches this
		// stream, so it stays exactly where the seed left it: whatever the append is, it is not a race
		// it lost -- there is no other writer.
		var breach = await Should.ThrowAsync<SqliteException>(async () => await store.AppendAsync(
			aggregateId,
			AggregateType,
			new IDomainEvent[] { new Placed(aggregateId) { EventId = null! } },
			expectedVersion: 0,
			CancellationToken.None).ConfigureAwait(false)).ConfigureAwait(false);

		breach.SqliteErrorCode.ShouldBe(
			SqliteConstraint,
			"the breach must reach the classifier as a constraint violation, or this arm proves nothing");
		breach.SqliteExtendedErrorCode.ShouldBe(
			SqliteConstraintNotNull,
			"a missing required value, which no concurrent writer can cause and no reload can repair");

		// The stream is where it was, which is the whole point: a caller told this was a conflict would
		// reload this same version and re-attempt the identical unwritable event.
		var current = await store.LoadAsync(aggregateId, AggregateType, CancellationToken.None).ConfigureAwait(false);
		current.Count.ShouldBe(1, "nothing was written and nothing else claimed the version");

		await _fixture.CleanupAsync().ConfigureAwait(false);
	}

	[Fact]
	public async Task ReportADifferentExtendedCodeForEachConstraint_ThoughThePrimaryCodeIsShared()
	{
		await using var connection = new SqliteConnection(_fixture.ConnectionString);
		await connection.OpenAsync(CancellationToken.None).ConfigureAwait(false);

		await ExecuteAsync(connection, "DROP TABLE IF EXISTS [ConstraintCodeProbe]").ConfigureAwait(false);
		await ExecuteAsync(
			connection,
			"CREATE TABLE [ConstraintCodeProbe] (Id INTEGER NOT NULL, Name TEXT NOT NULL, UNIQUE(Id))")
			.ConfigureAwait(false);
		await ExecuteAsync(connection, "INSERT INTO [ConstraintCodeProbe] (Id, Name) VALUES (1, 'first')")
			.ConfigureAwait(false);

		var notNull = await Should.ThrowAsync<SqliteException>(async () =>
			await ExecuteAsync(connection, "INSERT INTO [ConstraintCodeProbe] (Id, Name) VALUES (2, NULL)")
				.ConfigureAwait(false)).ConfigureAwait(false);

		var duplicate = await Should.ThrowAsync<SqliteException>(async () =>
			await ExecuteAsync(connection, "INSERT INTO [ConstraintCodeProbe] (Id, Name) VALUES (1, 'second')")
				.ConfigureAwait(false)).ConfigureAwait(false);

		// The two breaches are unrelated -- one is a value the caller failed to supply, the other is a key
		// another row already holds -- and the primary code cannot tell them apart.
		notNull.SqliteErrorCode.ShouldBe(SqliteConstraint);
		duplicate.SqliteErrorCode.ShouldBe(SqliteConstraint);

		// The extended code can, which is why the classification reads it.
		notNull.SqliteExtendedErrorCode.ShouldBe(SqliteConstraintNotNull);
		duplicate.SqliteExtendedErrorCode.ShouldBe(SqliteConstraintUnique);

		await ExecuteAsync(connection, "DROP TABLE [ConstraintCodeProbe]").ConfigureAwait(false);
	}

	// CA2100: every caller of this helper passes a string literal written in this file - a probe table
	// created and dropped in-process to read SQLite's own extended result codes. No value here is
	// caller-supplied or derived from test data, so there is nothing for an injection to travel through.
	// The analyzer cannot see that through the parameter, which is what it is reporting.
#pragma warning disable CA2100
	private static async Task ExecuteAsync(SqliteConnection connection, string sql)
	{
		await using var command = connection.CreateCommand();
		command.CommandText = sql;
		_ = await command.ExecuteNonQueryAsync(CancellationToken.None).ConfigureAwait(false);
	}
#pragma warning restore CA2100

	private SqliteEventStore StoreFor(string tenantId) =>
		new(
			_fixture.ConnectionString,
			NullLogger<SqliteEventStore>.Instance,
			new FixedTestTenantContext(tenantId),
			Microsoft.Extensions.Options.Options.Create(new TenantContextOptions { RequireTenant = true }));

	private sealed record Placed(string AggregateId) : IDomainEvent
	{
		public string EventId { get; init; } = Guid.NewGuid().ToString();

		public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;

		public string EventType { get; init; } = nameof(Placed);

		public IDictionary<string, object>? Metadata { get; init; }
	}
}
