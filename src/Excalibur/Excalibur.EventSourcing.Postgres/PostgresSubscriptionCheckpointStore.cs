// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Dapper;

using Excalibur.EventSourcing.Postgres.Requests;
using Excalibur.EventSourcing.Subscriptions;

using Npgsql;

namespace Excalibur.EventSourcing.Postgres;

/// <summary>
/// A durable <see cref="ISubscriptionCheckpointStore"/> backed by PostgreSQL.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why a durable store is not optional.</b> The gapless global position exists so a catch-up
/// subscriber can hold one number and never look back. An in-memory checkpoint forfeits that on every
/// process start: the subscription replays the whole stream, which on an append-only store grows without
/// bound, and every projection is rebuilt from zero after a restart or a deployment.
/// </para>
/// <para>
/// <b>The advance is a compare-and-set, and every statement here is a single atomic one.</b> Two
/// instances of the same subscription share one checkpoint row and race for it. A read-then-write would
/// let the slower instance overwrite the faster one's progress with a lower mark, and the events between
/// the two marks would then be delivered a second time — so the caller states the value it believes is
/// current, and the store reports whether that belief held.
/// </para>
/// <para>
/// <b>The two prior states are distinct.</b> A caller passing <see langword="null"/> claims there is no
/// checkpoint at all, and it must LOSE to a writer that has since created one — otherwise the instance
/// that started second would reset a live subscription to its own position. <c>ON CONFLICT DO NOTHING</c>
/// expresses that directly: it is an insert that declines when the row exists, not an upsert. An upsert
/// would make the two prior states interchangeable and quietly reintroduce the overwrite this contract
/// exists to prevent.
/// </para>
/// <para>
/// No tenant term appears anywhere in this store, deliberately. A subscription reads the global stream of
/// the store it is attached to; under tenant sharding each shard is its own database with its own
/// position sequence, so it carries its own copy of this table and its own checkpoints.
/// </para>
/// </remarks>
internal sealed class PostgresSubscriptionCheckpointStore : ISubscriptionCheckpointStore
{
	private readonly Func<NpgsqlConnection> _connectionFactory;
	private readonly string _table;

	/// <summary>
	/// Initializes a new instance of the <see cref="PostgresSubscriptionCheckpointStore"/> class.
	/// </summary>
	/// <param name="connectionFactory">Creates a connection to the checkpoint database.</param>
	/// <param name="schema">The schema holding the checkpoint table. Default: "public".</param>
	/// <param name="table">The checkpoint table name. Default: "subscription_checkpoints".</param>
	internal PostgresSubscriptionCheckpointStore(
		Func<NpgsqlConnection> connectionFactory,
		string schema = "public",
		string table = "subscription_checkpoints")
	{
		ArgumentNullException.ThrowIfNull(connectionFactory);

		_connectionFactory = connectionFactory;
		_table = PgTableName.Format(schema, table);
	}

	/// <inheritdoc />
	public async Task<long?> GetCheckpointAsync(string subscriptionName, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrEmpty(subscriptionName);

		await using var connection = _connectionFactory();
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

#pragma warning disable CA2100 // Table name validated by SqlIdentifierValidator in PgTableName.Format
		return await connection.ExecuteScalarAsync<long?>(
			new CommandDefinition(
				$"SELECT position FROM {_table} WHERE subscription_name = @Name;",
				new { Name = subscriptionName },
				cancellationToken: cancellationToken)).ConfigureAwait(false);
#pragma warning restore CA2100
	}

	/// <inheritdoc />
	public async Task<CheckpointAdvanceOutcome> AdvanceCheckpointAsync(
		string subscriptionName,
		long? expectedPosition,
		long newPosition,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrEmpty(subscriptionName);

		await using var connection = _connectionFactory();
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

		// One statement either way, and in both cases the predicate carries the caller's expectation so
		// the row is matched and written atomically. Nothing affected means the belief did not hold.
		//
		// The UPDATE's WHERE pins the expected position; the INSERT's ON CONFLICT declines an existing
		// row rather than overwriting it. Neither is an upsert, and neither may become one.
		var sql = expectedPosition is null
			? $"""
				INSERT INTO {_table} (subscription_name, position, updated_at)
				VALUES (@Name, @NewPosition, now())
				ON CONFLICT (subscription_name) DO NOTHING;
				"""
			: $"""
				UPDATE {_table}
				SET position = @NewPosition, updated_at = now()
				WHERE subscription_name = @Name AND position = @Expected;
				""";

#pragma warning disable CA2100 // Table name validated by SqlIdentifierValidator in PgTableName.Format
		var affected = await connection.ExecuteAsync(
			new CommandDefinition(
				sql,
				new { Name = subscriptionName, NewPosition = newPosition, Expected = expectedPosition },
				cancellationToken: cancellationToken)).ConfigureAwait(false);
#pragma warning restore CA2100

		return affected == 1 ? CheckpointAdvanceOutcome.Advanced : CheckpointAdvanceOutcome.Superseded;
	}

	/// <inheritdoc />
	public async Task<IReadOnlyList<SubscriptionCheckpoint>> EnumerateCheckpointsAsync(
		CancellationToken cancellationToken)
	{
		await using var connection = _connectionFactory();
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

#pragma warning disable CA2100 // Table name validated by SqlIdentifierValidator in PgTableName.Format
		var rows = await connection.QueryAsync<CheckpointRow>(
			new CommandDefinition(
				$"SELECT subscription_name AS SubscriptionName, position AS Position FROM {_table};",
				cancellationToken: cancellationToken)).ConfigureAwait(false);
#pragma warning restore CA2100

		return [.. rows.Select(static r => new SubscriptionCheckpoint(r.SubscriptionName, r.Position))];
	}

	/// <summary>Row shape for <see cref="EnumerateCheckpointsAsync"/>.</summary>
	/// <remarks>
	/// Named explicitly rather than materialized into <see cref="SubscriptionCheckpoint"/> directly: that
	/// type is a positional record struct, and mapping a SELECT onto one depends on column order rather
	/// than column name, which breaks silently the moment the projection changes.
	/// </remarks>
	private sealed record CheckpointRow(string SubscriptionName, long Position);
}
