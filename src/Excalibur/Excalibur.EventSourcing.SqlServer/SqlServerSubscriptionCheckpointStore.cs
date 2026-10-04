// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Dapper;

using Excalibur.EventSourcing.Subscriptions;
using Excalibur.EventSourcing.SqlServer.Requests;

using Microsoft.Data.SqlClient;

namespace Excalibur.EventSourcing.SqlServer;

/// <summary>
/// A durable <see cref="ISubscriptionCheckpointStore"/> backed by SQL Server.
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
/// that started second would reset a live subscription to its own position. That is why the null case is
/// an insert guarded by the primary key rather than an upsert: an upsert would silently make the two
/// prior states interchangeable and quietly reintroduce the overwrite this contract exists to prevent.
/// </para>
/// <para>
/// No tenant term appears anywhere in this store, deliberately. A subscription reads the global stream of
/// the store it is attached to; under tenant sharding each shard is its own database with its own
/// position sequence, so it carries its own copy of this table and its own checkpoints.
/// </para>
/// </remarks>
internal sealed class SqlServerSubscriptionCheckpointStore : ISubscriptionCheckpointStore
{
	/// <summary>SQL Server error numbers for a unique-constraint and a unique-index violation.</summary>
	/// <remarks>
	/// The same logical collision is reported under either number depending on how the uniqueness was
	/// declared, so both mean "a row already existed" here.
	/// </remarks>
	private const int UniqueConstraintViolation = 2627;
	private const int UniqueIndexViolation = 2601;

	private readonly Func<SqlConnection> _connectionFactory;
	private readonly string _table;

	/// <summary>
	/// Initializes a new instance of the <see cref="SqlServerSubscriptionCheckpointStore"/> class.
	/// </summary>
	/// <param name="connectionString">The connection string for the checkpoint database.</param>
	/// <param name="schema">The schema holding the checkpoint table. Default: "dbo".</param>
	/// <param name="table">The checkpoint table name. Default: "SubscriptionCheckpoints".</param>
	internal SqlServerSubscriptionCheckpointStore(
		string connectionString,
		string schema = "dbo",
		string table = "SubscriptionCheckpoints")
		: this(CreateConnectionFactory(connectionString), schema, table)
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="SqlServerSubscriptionCheckpointStore"/> class.
	/// </summary>
	/// <param name="connectionFactory">Creates a connection to the checkpoint database.</param>
	/// <param name="schema">The schema holding the checkpoint table. Default: "dbo".</param>
	/// <param name="table">The checkpoint table name. Default: "SubscriptionCheckpoints".</param>
	internal SqlServerSubscriptionCheckpointStore(
		Func<SqlConnection> connectionFactory,
		string schema = "dbo",
		string table = "SubscriptionCheckpoints")
	{
		ArgumentNullException.ThrowIfNull(connectionFactory);

		_connectionFactory = connectionFactory;
		_table = SqlTableName.Format(schema, table);
	}

	/// <inheritdoc />
	public async Task<long?> GetCheckpointAsync(string subscriptionName, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrEmpty(subscriptionName);

		await using var connection = _connectionFactory();
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

#pragma warning disable CA2100 // Table name validated by SqlIdentifierValidator in SqlTableName.Format
		return await connection.ExecuteScalarAsync<long?>(
			new CommandDefinition(
				$"SELECT Position FROM {_table} WHERE SubscriptionName = @Name;",
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
		ArgumentOutOfRangeException.ThrowIfNegative(newPosition);
		if (expectedPosition is { } prior)
		{
			ArgumentOutOfRangeException.ThrowIfNegative(prior, nameof(expectedPosition));
			ArgumentOutOfRangeException.ThrowIfLessThan(newPosition, prior);
		}


		await using var connection = _connectionFactory();
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

		return expectedPosition is { } expected
			? await UpdateAsync(connection, subscriptionName, expected, newPosition, cancellationToken)
				.ConfigureAwait(false)
			: await InsertAsync(connection, subscriptionName, newPosition, cancellationToken)
				.ConfigureAwait(false);
	}

	/// <inheritdoc />
	public async Task<IReadOnlyList<SubscriptionCheckpoint>> EnumerateCheckpointsAsync(
		CancellationToken cancellationToken)
	{
		await using var connection = _connectionFactory();
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

#pragma warning disable CA2100 // Table name validated by SqlIdentifierValidator in SqlTableName.Format
		var rows = await connection.QueryAsync<CheckpointRow>(
			new CommandDefinition(
				$"SELECT SubscriptionName, Position FROM {_table};",
				cancellationToken: cancellationToken)).ConfigureAwait(false);
#pragma warning restore CA2100

		return [.. rows.Select(static r => new SubscriptionCheckpoint(r.SubscriptionName, r.Position))];
	}

	/// <summary>
	/// Advances a checkpoint the caller believes is at <paramref name="expected"/>.
	/// </summary>
	/// <remarks>
	/// One statement. The predicate carries the expectation, so the row is matched and written in the same
	/// atomic operation and no interleaving can slip between the two. Nothing updated means the row is not
	/// where the caller believed -- another instance moved it, or it does not exist at all -- and both are
	/// the caller having lost the race.
	/// </remarks>
	private async Task<CheckpointAdvanceOutcome> UpdateAsync(
		SqlConnection connection,
		string subscriptionName,
		long expected,
		long newPosition,
		CancellationToken cancellationToken)
	{
#pragma warning disable CA2100 // Table name validated by SqlIdentifierValidator in SqlTableName.Format
		var affected = await connection.ExecuteAsync(
			new CommandDefinition(
				$"""
				UPDATE {_table}
				SET Position = @NewPosition, UpdatedAt = SYSDATETIMEOFFSET()
				WHERE SubscriptionName = @Name AND Position = @Expected;
				""",
				new { Name = subscriptionName, NewPosition = newPosition, Expected = expected },
				cancellationToken: cancellationToken)).ConfigureAwait(false);
#pragma warning restore CA2100

		return affected == 1 ? CheckpointAdvanceOutcome.Advanced : CheckpointAdvanceOutcome.Superseded;
	}

	/// <summary>
	/// Creates a checkpoint the caller believes does not yet exist.
	/// </summary>
	/// <remarks>
	/// A plain INSERT, with the primary key doing the work. Two instances starting together both believe
	/// there is no checkpoint; exactly one insert wins and the other is refused by the key, which is the
	/// outcome the contract wants. An upsert here would let the loser overwrite the winner and rewind a
	/// subscription that had already moved on.
	/// </remarks>
	private async Task<CheckpointAdvanceOutcome> InsertAsync(
		SqlConnection connection,
		string subscriptionName,
		long newPosition,
		CancellationToken cancellationToken)
	{
		try
		{
#pragma warning disable CA2100 // Table name validated by SqlIdentifierValidator in SqlTableName.Format
			_ = await connection.ExecuteAsync(
				new CommandDefinition(
					$"""
					INSERT INTO {_table} (SubscriptionName, Position, UpdatedAt)
					VALUES (@Name, @NewPosition, SYSDATETIMEOFFSET());
					""",
					new { Name = subscriptionName, NewPosition = newPosition },
					cancellationToken: cancellationToken)).ConfigureAwait(false);
#pragma warning restore CA2100

			return CheckpointAdvanceOutcome.Advanced;
		}
		catch (SqlException ex) when (ex.Number is UniqueConstraintViolation or UniqueIndexViolation)
		{
			// A row already exists, so the caller's "there is no checkpoint" belief was wrong. This is an
			// ordinary outcome of the race, not a fault, and it is reported rather than thrown.
			return CheckpointAdvanceOutcome.Superseded;
		}
	}

	private static Func<SqlConnection> CreateConnectionFactory(string connectionString)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

		return () => new SqlConnection(connectionString);
	}

	/// <summary>Row shape for <see cref="EnumerateCheckpointsAsync"/>.</summary>
	/// <remarks>
	/// Named explicitly rather than materialized into <see cref="SubscriptionCheckpoint"/> directly: that
	/// type is a positional record struct, and mapping a SELECT onto one depends on column order rather
	/// than column name, which breaks silently the moment the projection changes.
	/// </remarks>
	private sealed record CheckpointRow(string SubscriptionName, long Position);
}
