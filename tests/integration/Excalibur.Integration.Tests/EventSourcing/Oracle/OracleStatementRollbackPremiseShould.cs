// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Dapper;

using Excalibur.EventSourcing.Oracle;
using Excalibur.Integration.Tests.Data.EventStore;

using global::Oracle.ManagedDataAccess.Client;

namespace Excalibur.Integration.Tests.EventSourcing.Oracle;

/// <summary>
/// Pins the two Oracle premises the shared relational destination's write path rests on.
/// </summary>
/// <remarks>
/// <para>
/// The store reads a duplicate-key violation as "a row exists" and then issues a FURTHER statement in
/// the SAME transaction. That is only sound if Oracle rolls back the offending STATEMENT and leaves the
/// transaction usable. Documented Oracle behaviour says it does; this asserts it, because the write path
/// is correct only while it remains true and an adversarial review flagged it as reasoned rather than
/// measured.
/// </para>
/// <para>
/// It also pins the position column's DEFAULT, which the shipped script now declares so a consumer's
/// own seeding INSERT behaves the same on every relational provider.
/// </para>
/// </remarks>
[Collection(OracleEventStoreTestCollection.CollectionName)]
[Trait("Category", "Integration")]
[Trait("Component", "EventStore")]
[Trait("Database", "Oracle")]
[Trait("Pattern", "STORE")]
public sealed class OracleStatementRollbackPremiseShould(OracleEventStoreContainerFixture fixture)
	: IClassFixture<OracleEventStoreContainerFixture>, IAsyncLifetime
{
	[System.Diagnostics.CodeAnalysis.SuppressMessage(
		"Usage",
		"CA2213:Disposable fields should be disposed",
		Justification = "xUnit owns and disposes the class fixture.")]
	private readonly OracleEventStoreContainerFixture _fixture = fixture;

	private readonly string _tableName = "ROLLBK_" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();

	/// <inheritdoc />
	public async ValueTask InitializeAsync()
	{
		_fixture.DockerAvailable.ShouldBeTrue("this arm measures a real engine's transaction semantics");
		await _fixture.EnsureInitializedAsync().ConfigureAwait(false);
	}

	/// <inheritdoc />
	public ValueTask DisposeAsync() => ValueTask.CompletedTask;

	/// <summary>
	/// A duplicate key rolls back only the offending statement; the transaction still commits later work.
	/// </summary>
	/// <returns>A task representing the arm.</returns>
	[Fact]
	public async Task KeepTheTransactionUsableAfterADuplicateKeyRollsBackItsOwnStatement()
	{
		await using var connection = new OracleConnection(_fixture.ConnectionString);
		await connection.OpenAsync().ConfigureAwait(false);
		_ = await connection.ExecuteAsync(
			OracleProjectionSchema.CreateTransactionalTableScript(_tableName, _fixture.Schema)).ConfigureAwait(false);

		var table = $"\"{_fixture.Schema.ToUpperInvariant()}\".\"{_tableName}\"";
		await using var transaction = await connection.BeginTransactionAsync().ConfigureAwait(false);

		async Task<int> Write(string sql, object parameters) =>
			await connection.ExecuteAsync(sql, parameters, (OracleTransaction)transaction).ConfigureAwait(false);

		var insert = $"INSERT INTO {table} (TenantId, Id, Data, LastAppliedPosition, TransactionalSource, "
			+ "TransactionalAggregateType) VALUES (:Tenant, :Id, :Data, :State, NULL, NULL)";

		(await Write(insert, new { Tenant = "t", Id = "first", Data = "{}", State = 1L }).ConfigureAwait(false))
			.ShouldBe(1);

		var duplicate = await Should.ThrowAsync<OracleException>(async () =>
			await Write(insert, new { Tenant = "t", Id = "first", Data = "{}", State = 2L }).ConfigureAwait(false))
			.ConfigureAwait(false);
		duplicate.Number.ShouldBe(1);

		// THE PREMISE: the transaction is still usable, and the statement that failed left nothing behind.
		(await Write(insert, new { Tenant = "t", Id = "second", Data = "{}", State = 3L }).ConfigureAwait(false))
			.ShouldBe(1);
		(await Write($"UPDATE {table} SET LastAppliedPosition = 9 WHERE TenantId = :Tenant AND Id = :Id",
			new { Tenant = "t", Id = "first" }).ConfigureAwait(false)).ShouldBe(1);

		await transaction.CommitAsync().ConfigureAwait(false);

		// Both rows survive the commit, and the duplicate never overwrote the first row's position.
		var rows = (await connection.QueryAsync<(string Id, long Position)>(
			$"SELECT Id, LastAppliedPosition FROM {table} ORDER BY Id").ConfigureAwait(false)).ToList();
		rows.Count.ShouldBe(2);
		rows[0].ShouldBe(("first", 9L));
		rows[1].ShouldBe(("second", 3L));
	}

	/// <summary>The shipped script defaults the position column to the unplaceable sentinel.</summary>
	/// <returns>A task representing the arm.</returns>
	/// <remarks>
	/// A consumer seeding or backfilling a destination by hand omits this column. PostgreSQL and SQL
	/// Server have always defaulted it; until this change Oracle and SQLite rejected the same INSERT.
	/// </remarks>
	[Fact]
	public async Task DefaultThePositionColumnToTheUnplaceableSentinel()
	{
		await using var connection = new OracleConnection(_fixture.ConnectionString);
		await connection.OpenAsync().ConfigureAwait(false);
		_ = await connection.ExecuteAsync(
			OracleProjectionSchema.CreateTransactionalTableScript(_tableName + "D", _fixture.Schema)).ConfigureAwait(false);

		var table = $"\"{_fixture.Schema.ToUpperInvariant()}\".\"{_tableName}D\"";

		// No LastAppliedPosition column: exactly the shape of a hand-written seeding INSERT.
		_ = await connection.ExecuteAsync(
			$"INSERT INTO {table} (TenantId, Id, Data) VALUES (:Tenant, :Id, :Data)",
			new { Tenant = "t", Id = "seeded", Data = "{}" }).ConfigureAwait(false);

		var position = await connection.ExecuteScalarAsync<long>(
			$"SELECT LastAppliedPosition FROM {table} WHERE TenantId = :Tenant AND Id = :Id",
			new { Tenant = "t", Id = "seeded" }).ConfigureAwait(false);

		position.ShouldBe(-2L, "an unseeded row must read as UNPLACEABLE, never as position zero");
	}
}
