// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Dapper;

using Excalibur.EventSourcing.Oracle.Requests;
using Excalibur.EventSourcing.Subscriptions;

using Oracle.ManagedDataAccess.Client;

namespace Excalibur.EventSourcing.Oracle;

/// <summary>
/// A durable <see cref="ISubscriptionCheckpointStore"/> backed by Oracle.
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
/// the two marks would then be delivered a second time.
/// </para>
/// <para>
/// <b>The two prior states are distinct.</b> A caller passing <see langword="null"/> claims there is no
/// checkpoint at all and must LOSE to a writer that has since created one, so that case is a plain
/// INSERT with the primary key refusing the loser — never a MERGE. A MERGE would make the two prior
/// states interchangeable and let a late-starting instance reset a live subscription.
/// </para>
/// <para>
/// <b>Binding.</b> ODP.NET binds by POSITION unless told otherwise, and this request goes through Dapper
/// rather than a hand-built command, so no placeholder is repeated: each appears exactly once and the
/// parameters are added in the order the text uses them. That is correct under both binding modes.
/// </para>
/// </remarks>
internal sealed class OracleSubscriptionCheckpointStore : ISubscriptionCheckpointStore
{
	/// <summary>ORA-00001: unique constraint violated — the row already existed.</summary>
	private const int UniqueConstraintViolated = 1;

	private readonly Func<OracleConnection> _connectionFactory;
	private readonly string _table;

	/// <summary>
	/// Initializes a new instance of the <see cref="OracleSubscriptionCheckpointStore"/> class.
	/// </summary>
	/// <param name="connectionFactory">Creates a connection to the checkpoint database.</param>
	/// <param name="schema">The schema holding the checkpoint table. Default: "EXCALIBUR".</param>
	/// <param name="table">The checkpoint table name. Default: "SUBSCRIPTIONCHECKPOINTS".</param>
	internal OracleSubscriptionCheckpointStore(
		Func<OracleConnection> connectionFactory,
		string schema = "EXCALIBUR",
		string table = "SUBSCRIPTIONCHECKPOINTS")
	{
		ArgumentNullException.ThrowIfNull(connectionFactory);

		_connectionFactory = connectionFactory;
		_table = OracleTableName.Format(schema, table);
	}

	/// <inheritdoc />
	public async Task<long?> GetCheckpointAsync(string subscriptionName, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrEmpty(subscriptionName);

		await using var connection = _connectionFactory();
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

#pragma warning disable CA2100 // Table name validated by SqlIdentifierValidator in OracleTableName.Format
		return await connection.ExecuteScalarAsync<long?>(
			new CommandDefinition(
				$"SELECT POSITION FROM {_table} WHERE SUBSCRIPTIONNAME = :Name",
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

		if (expectedPosition is { } expected)
		{
			// One statement: the predicate carries the expectation, so the row is matched and written
			// atomically. Nothing updated means the row is not where the caller believed.
#pragma warning disable CA2100 // Table name validated by SqlIdentifierValidator in OracleTableName.Format
			var affected = await connection.ExecuteAsync(
				new CommandDefinition(
					$"""
					UPDATE {_table}
					SET POSITION = :NewPosition, UPDATEDAT = SYSTIMESTAMP
					WHERE SUBSCRIPTIONNAME = :Name AND POSITION = :Expected
					""",
					new { NewPosition = newPosition, Name = subscriptionName, Expected = expected },
					cancellationToken: cancellationToken)).ConfigureAwait(false);
#pragma warning restore CA2100

			return affected == 1 ? CheckpointAdvanceOutcome.Advanced : CheckpointAdvanceOutcome.Superseded;
		}

		try
		{
#pragma warning disable CA2100 // Table name validated by SqlIdentifierValidator in OracleTableName.Format
			_ = await connection.ExecuteAsync(
				new CommandDefinition(
					$"""
					INSERT INTO {_table} (SUBSCRIPTIONNAME, POSITION, UPDATEDAT)
					VALUES (:Name, :NewPosition, SYSTIMESTAMP)
					""",
					new { Name = subscriptionName, NewPosition = newPosition },
					cancellationToken: cancellationToken)).ConfigureAwait(false);
#pragma warning restore CA2100

			return CheckpointAdvanceOutcome.Advanced;
		}
		catch (Exception ex) when (IsUniqueViolation(ex))
		{
			// A row already exists, so the caller's "there is no checkpoint" belief was wrong. An
			// ordinary outcome of the race, reported rather than thrown.
			return CheckpointAdvanceOutcome.Superseded;
		}
	}

	/// <inheritdoc />
	public async Task<IReadOnlyList<SubscriptionCheckpoint>> EnumerateCheckpointsAsync(
		CancellationToken cancellationToken)
	{
		await using var connection = _connectionFactory();
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

#pragma warning disable CA2100 // Table name validated by SqlIdentifierValidator in OracleTableName.Format
		var rows = await connection.QueryAsync<CheckpointRow>(
			new CommandDefinition(
				$"SELECT SUBSCRIPTIONNAME AS SubscriptionName, POSITION AS Position FROM {_table}",
				cancellationToken: cancellationToken)).ConfigureAwait(false);
#pragma warning restore CA2100

		return [.. rows.Select(static r => new SubscriptionCheckpoint(r.SubscriptionName, (long)r.Position))];
	}

	/// <summary>
	/// Recognises ORA-00001 anywhere in the exception chain.
	/// </summary>
	/// <remarks>
	/// The chain is walked rather than the outermost type matched, because the data-request seam wraps
	/// whatever the driver raised; matching only the outer type silently never fires.
	/// </remarks>
	private static bool IsUniqueViolation(Exception? ex)
	{
		for (var current = ex; current is not null; current = current.InnerException)
		{
			if (current is OracleException { Number: UniqueConstraintViolated })
			{
				return true;
			}
		}

		return false;
	}

	/// <summary>Row shape for <see cref="EnumerateCheckpointsAsync"/>.</summary>
	/// <remarks>
	/// <para>
	/// Named explicitly rather than materialized into <see cref="SubscriptionCheckpoint"/> directly: that
	/// type is a positional record struct, and mapping a SELECT onto one depends on column order rather
	/// than column name, which breaks silently the moment the projection changes.
	/// </para>
	/// <para>
	/// <c>Position</c> is <see cref="decimal"/>, not <see cref="long"/>: Oracle returns NUMBER(19) as a
	/// decimal, which does not bind to a <see cref="long"/> constructor parameter, so Dapper fails to
	/// materialize the row at all. This mirrors <c>LoadEventsRequest.OracleEventRow</c> and
	/// <c>GetLatestSnapshotRequest.SnapshotData</c> in this package, which solved the identical problem.
	/// </para>
	/// </remarks>
	private sealed record CheckpointRow(string SubscriptionName, decimal Position);
}
