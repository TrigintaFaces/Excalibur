// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Text.RegularExpressions;

using Npgsql;

namespace Excalibur.Outbox.Marten;

/// <summary>
/// The atomic claim behind the Marten outbox drain.
/// </summary>
/// <remarks>
/// <para>
/// Reading the eligible messages is not enough to hand a message to exactly one dispatcher. Two
/// dispatchers polling at the same time see the same rows and both dispatch them, so every message goes
/// out twice for as long as more than one instance is running. Claiming is what makes the drain safe to
/// run in more than one process.
/// </para>
/// <para>
/// The claim lives in a table this store owns rather than in the Marten document. Marten keeps a
/// document's fields inside a <c>jsonb</c> column whose property names come from the serializer the
/// CONSUMER configured on their own <c>IDocumentStore</c>; SQL reaching into that document would break
/// silently under a different casing convention, and this store cannot impose one because it does not own
/// the store it is given. A table defined here has columns under this store's control.
/// </para>
/// <para>
/// The claim is one statement — <c>INSERT … ON CONFLICT DO UPDATE … WHERE … RETURNING</c>. PostgreSQL
/// takes a row lock per key, so of two dispatchers presenting the same message exactly one gets it back:
/// the insert succeeds for whoever arrives first, and the second is routed to the update, whose
/// <c>WHERE</c> holds only if the existing claim has expired. A losing dispatcher's message is simply
/// absent from the returned set. There is no read-then-write window to lose.
/// </para>
/// </remarks>
internal static class MartenOutboxClaims
{
	/// <summary>
	/// The reserved dispatcher identity marking a message that has reached a terminal state.
	/// </summary>
	/// <remarks>
	/// <para>
	/// A settled message's claim row is kept as a tombstone under this identity rather than deleted, and
	/// that tombstone is what makes the terminal transition single-winner. Deleting the row on settle
	/// would re-open the race it exists to close: a second caller would find no row, insert one, and
	/// settle the same message again.
	/// </para>
	/// <para>
	/// Real dispatchers identify themselves with a GUID, so this value cannot collide with one: it
	/// contains characters a GUID never does.
	/// </para>
	/// </remarks>
	public const string TerminalDispatcherId = "__excalibur_settled__";

	/// <summary>
	/// Matches a bare SQL identifier: a letter or underscore, then letters, digits or underscores.
	/// </summary>
	private static readonly Regex SafeIdentifier = new("^[A-Za-z_][A-Za-z0-9_]*$", RegexOptions.Compiled, TimeSpan.FromSeconds(1));

	/// <summary>
	/// Creates the claim table if it does not already exist.
	/// </summary>
	/// <param name="connection">An open connection to the Marten database.</param>
	/// <param name="schema">The schema to create it in.</param>
	/// <param name="table">The table name.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	public static async Task EnsureTableAsync(
		NpgsqlConnection connection,
		string schema,
		string table,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(connection);

		var qualified = Qualify(schema, table);

		// Identifiers are validated by Qualify, not parameterised: PostgreSQL does not accept a parameter
		// where an object name belongs.
#pragma warning disable CA2100 // Query built from validated identifiers, not from user input
		await using var command = new NpgsqlCommand(
			$"""
			CREATE SCHEMA IF NOT EXISTS "{schema}";

			CREATE TABLE IF NOT EXISTS {qualified} (
			    message_id      text        NOT NULL PRIMARY KEY,
			    dispatcher_id   text        NOT NULL,
			    claimed_at      timestamptz NOT NULL,
			    next_attempt_at timestamptz NULL
			);

			-- Separate from the CREATE so a table created before the failure floor existed gains the
			-- column too. CREATE TABLE IF NOT EXISTS is a no-op against an existing table, so without
			-- this an upgraded deployment would keep the old shape and every statement below would
			-- fail on an unknown column.
			ALTER TABLE {qualified} ADD COLUMN IF NOT EXISTS next_attempt_at timestamptz NULL;
			""",
			connection);
#pragma warning restore CA2100

		_ = await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
	}

	/// <summary>
	/// Claims as many of <paramref name="messageIds"/> as are free, and reports which were won.
	/// </summary>
	/// <param name="connection">An open connection to the Marten database.</param>
	/// <param name="schema">The claim table's schema.</param>
	/// <param name="table">The claim table's name.</param>
	/// <param name="messageIds">The candidate message identifiers.</param>
	/// <param name="dispatcherId">The claiming dispatcher.</param>
	/// <param name="claimTimeout">How long a claim is honoured before it may be taken over.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>The identifiers this dispatcher may dispatch. Never contains an id claimed by another.</returns>
	public static async Task<HashSet<string>> ClaimAsync(
		NpgsqlConnection connection,
		string schema,
		string table,
		IReadOnlyCollection<string> messageIds,
		string dispatcherId,
		TimeSpan claimTimeout,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(connection);
		ArgumentNullException.ThrowIfNull(messageIds);

		var won = new HashSet<string>(StringComparer.Ordinal);
		if (messageIds.Count == 0)
		{
			return won;
		}

		var qualified = Qualify(schema, table);

		// One statement, so there is no window between deciding a message is free and taking it.
		//
		// DO UPDATE rather than DO NOTHING: an expired claim must be takeable, or a dispatcher that
		// crashed mid-send would strand its messages permanently. The WHERE is what keeps that honest --
		// it holds only for a claim older than the timeout, so a live claim is never stolen and the
		// statement returns nothing for it.
		//
		// The second WHERE term is the failure floor. A message reported failed carries a next_attempt_at,
		// and until that instant passes it is not handed to anybody -- which is what stops a persistently
		// failing destination being retried as fast as the drain can loop. Once it passes, the row is
		// claimable again on the ordinary path, so a failure delays a message rather than ending it.
		//
		// Every instant in this statement comes from clock_timestamp(), the database's clock, and never from
		// the dispatcher's. A lease is written by one machine and judged by another, so a cutoff computed
		// here would compare two unsynchronised clocks: a dispatcher running ahead by more than the timeout
		// evaluates a lease someone else is actively delivering under as expired, and takes it. The atomic
		// claim does not help -- it arbitrates simultaneous claimants, and under skew the two are not
		// simultaneous, so the second dispatcher's write is the only one at that instant and succeeds
		// legitimately on a predicate that was already wrong. Reading both sides of the comparison from the
		// one clock that orders the rows is what makes the predicate true rather than merely atomic.
		//
		// clock_timestamp() rather than now(): now() is the enclosing transaction's start time, which is
		// stale by however long the transaction has run.
		//
		// RETURNING reports only the rows actually written, which is exactly the set this dispatcher won.
#pragma warning disable CA2100 // Query built from validated identifiers, not from user input
		await using var command = new NpgsqlCommand(
			$"""
			INSERT INTO {qualified} (message_id, dispatcher_id, claimed_at, next_attempt_at)
			SELECT unnest(@MessageIds), @DispatcherId, clock_timestamp(), NULL
			ON CONFLICT (message_id) DO UPDATE
			    SET dispatcher_id   = EXCLUDED.dispatcher_id,
			        claimed_at      = EXCLUDED.claimed_at,
			        next_attempt_at = NULL
			    WHERE {qualified}.claimed_at < clock_timestamp() - make_interval(secs => @ClaimTimeoutSeconds)
			      AND ({qualified}.next_attempt_at IS NULL OR {qualified}.next_attempt_at <= clock_timestamp())
			RETURNING message_id;
			""",
			connection);
#pragma warning restore CA2100

		_ = command.Parameters.AddWithValue("MessageIds", messageIds.ToArray());
		_ = command.Parameters.AddWithValue("DispatcherId", dispatcherId);
		_ = command.Parameters.AddWithValue("ClaimTimeoutSeconds", claimTimeout.TotalSeconds);

		await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
		while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
		{
			_ = won.Add(reader.GetString(0));
		}

		return won;
	}

	/// <summary>
	/// Arbitrates a message's terminal transition, and reports whether this caller is the one that won it.
	/// </summary>
	/// <remarks>
	/// <para>
	/// Marking a message sent cannot be a read-then-write against the Marten document. Every caller opens
	/// its own session, and a session applies no optimistic concurrency, so ten callers all read a pending
	/// message and all write it sent — the message is settled ten times over. Nor can this be fixed with
	/// SQL against the document: its fields live in a <c>jsonb</c> column whose property names come from
	/// the CONSUMER's serializer, and this store does not own the store it is handed, so it cannot enable
	/// Marten's own concurrency either. The arbitration therefore lives in the claim table, whose columns
	/// this store does control.
	/// </para>
	/// <para>
	/// One statement decides it. PostgreSQL takes a row lock per key, so of any number of callers
	/// presenting the same message exactly one writes the row: whoever arrives first either inserts it or
	/// is routed to the update, whose <c>WHERE</c> holds only while the row is not already terminal. Every
	/// later caller matches neither, so nothing is returned and it has lost. The winner is not decided by
	/// reading state and acting on it — settling twice is not expressible.
	/// </para>
	/// </remarks>
	/// <param name="connection">An open connection to the Marten database.</param>
	/// <param name="schema">The claim table's schema.</param>
	/// <param name="table">The claim table's name.</param>
	/// <param name="messageId">The message being settled.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>
	/// <see langword="true"/> when this caller won the transition and must apply it; <see langword="false"/>
	/// when the message was already settled and this caller must not.
	/// </returns>
	public static async Task<bool> TrySettleAsync(
		NpgsqlConnection connection,
		string schema,
		string table,
		string messageId,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(connection);

		var qualified = Qualify(schema, table);

		// Claims a message that was never claimed (direct settle) and takes over one this dispatcher or
		// another still holds -- a live claim is no defence against a double settle, so unlike ClaimAsync
		// the takeover here is NOT gated on expiry. It is gated on the row not already being terminal,
		// which is the property that must hold exactly once.
#pragma warning disable CA2100 // Query built from validated identifiers, not from user input
		await using var command = new NpgsqlCommand(
			$"""
			INSERT INTO {qualified} (message_id, dispatcher_id, claimed_at)
			VALUES (@MessageId, @Terminal, clock_timestamp())
			ON CONFLICT (message_id) DO UPDATE
			    SET dispatcher_id = @Terminal,
			        claimed_at    = clock_timestamp()
			    WHERE {qualified}.dispatcher_id <> @Terminal
			RETURNING message_id;
			""",
			connection);
#pragma warning restore CA2100

		_ = command.Parameters.AddWithValue("MessageId", messageId);
		_ = command.Parameters.AddWithValue("Terminal", TerminalDispatcherId);

		var won = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
		return won is not null;
	}

	/// <summary>
	/// Records a delivery failure against a message, and reports whether this caller was entitled to.
	/// </summary>
	/// <remarks>
	/// <para>
	/// Two conditions have to hold together and cannot be checked separately. The caller must own the
	/// claim it is reporting against, and the message must not already be settled. Reading the row and
	/// then writing it would leave a window between the two in which another dispatcher takes the claim
	/// over, and the late report would then clear a lease its successor is actively delivering under --
	/// which is how the same message ends up sent twice.
	/// </para>
	/// <para>
	/// One statement decides it. A message that was never claimed has no row, so the insert lands and the
	/// failure is recorded -- that is the message failed straight from staged, which is legitimate. A
	/// message this dispatcher holds matches the update predicate. A message held by anyone else does
	/// not, and neither does a settled one, because the terminal identity is a value no real dispatcher
	/// can present. Both of those return nothing and the caller is told it lost.
	/// </para>
	/// <para>
	/// The winner's claim is released and a floor is stamped in its place, so what governs the next
	/// attempt is the failure instant rather than a lease left to age out. The floor is stamped from
	/// <c>clock_timestamp()</c> because <see cref="ClaimAsync"/> compares it against that same clock: a
	/// floor written from the reporting dispatcher's clock and judged against another's would expire
	/// early or late by whatever those two machines disagree by. The floor is anchored at the
	/// failure precisely because a message that failed without ever being claimed has no lease to derive
	/// one from.
	/// </para>
	/// </remarks>
	/// <param name="connection">An open connection to the Marten database.</param>
	/// <param name="transaction">
	/// The transaction this write joins. The caller commits it together with the document update, so the
	/// claim release and the recorded attempt count reach the database as one act — a crash between them
	/// would otherwise leave the message re-claimable with its attempt count unchanged, and a count that
	/// never advances is a message that never reaches the retry ceiling.
	/// </param>
	/// <param name="schema">The claim table's schema.</param>
	/// <param name="table">The claim table's name.</param>
	/// <param name="messageId">The message being reported as failed.</param>
	/// <param name="dispatcherId">The dispatcher reporting the failure.</param>
	/// <param name="floor">How long the message is withheld from the claim, measured from now.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>
	/// <see langword="true"/> when this caller was entitled to record the failure and it was recorded;
	/// <see langword="false"/> when the claim belongs to another dispatcher or the message is already
	/// settled, in which case nothing was written.
	/// </returns>
	public static async Task<bool> TryRecordFailureAsync(
		NpgsqlConnection connection,
		NpgsqlTransaction transaction,
		string schema,
		string table,
		string messageId,
		string dispatcherId,
		TimeSpan floor,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(connection);
		ArgumentNullException.ThrowIfNull(transaction);

		var qualified = Qualify(schema, table);

		// claimed_at is set to the epoch rather than to now: the claim is being given up, and leaving a
		// fresh timestamp there would make the message wait out the whole claim timeout on top of the
		// floor. Releasing it this way leaves next_attempt_at as the only thing holding the message back.
		#pragma warning disable CA2100 // Query built from validated identifiers, not from user input
		await using var command = new NpgsqlCommand(
		$"""
		INSERT INTO {qualified} (message_id, dispatcher_id, claimed_at, next_attempt_at)
		VALUES (@MessageId, @DispatcherId, @Released, clock_timestamp() + make_interval(secs => @FloorSeconds))
		ON CONFLICT (message_id) DO UPDATE
		    SET claimed_at      = @Released,
		        next_attempt_at = clock_timestamp() + make_interval(secs => @FloorSeconds)
		    WHERE {qualified}.dispatcher_id = @DispatcherId
		RETURNING message_id;
		""",
			connection,
			transaction);
		#pragma warning restore CA2100

		_ = command.Parameters.AddWithValue("MessageId", messageId);
		_ = command.Parameters.AddWithValue("DispatcherId", dispatcherId);
		_ = command.Parameters.AddWithValue("Released", DateTimeOffset.UnixEpoch);
		_ = command.Parameters.AddWithValue("FloorSeconds", floor.TotalSeconds);

		var recorded = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
		return recorded is not null;
	}

	/// <summary>
	/// Deletes the claim rows for messages that have been purged, tombstones included.
	/// </summary>
	/// <remarks>
	/// The terminal tombstones written by <see cref="TrySettleAsync"/> are deliberately not released on
	/// settle, so this is what bounds their growth: a message's claim row is removed when the message
	/// itself is purged, and never before. Without it every message ever sent would leave a row behind
	/// forever.
	/// </remarks>
	/// <param name="connection">An open connection to the Marten database.</param>
	/// <param name="schema">The claim table's schema.</param>
	/// <param name="table">The claim table's name.</param>
	/// <param name="messageIds">The messages whose claim rows are removed.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	public static async Task PurgeAsync(
		NpgsqlConnection connection,
		string schema,
		string table,
		IReadOnlyCollection<string> messageIds,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(connection);
		ArgumentNullException.ThrowIfNull(messageIds);

		if (messageIds.Count == 0)
		{
			return;
		}

		var qualified = Qualify(schema, table);

#pragma warning disable CA2100 // Query built from validated identifiers, not from user input
		await using var command = new NpgsqlCommand(
			$"DELETE FROM {qualified} WHERE message_id = ANY(@MessageIds);",
			connection);
#pragma warning restore CA2100

		_ = command.Parameters.AddWithValue("MessageIds", messageIds.ToArray());

		_ = await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
	}

	/// <summary>
	/// Releases a claim, so a message that comes back to the pool can be taken immediately.
	/// </summary>
	/// <remarks>
	/// Called when a message reaches a terminal state or is returned for retry. Without it a failed
	/// message would wait out the whole claim timeout before any dispatcher could retry it, and the claim
	/// rows for sent messages would accumulate forever.
	/// </remarks>
	/// <param name="connection">An open connection to the Marten database.</param>
	/// <param name="schema">The claim table's schema.</param>
	/// <param name="table">The claim table's name.</param>
	/// <param name="messageId">The message whose claim is released.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	public static async Task ReleaseAsync(
		NpgsqlConnection connection,
		string schema,
		string table,
		string messageId,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(connection);

		var qualified = Qualify(schema, table);

#pragma warning disable CA2100 // Query built from validated identifiers, not from user input
		// A terminal tombstone is never released: it is the record that the message was already settled,
		// and clearing it would let a later caller settle the same message a second time. Only a live
		// dispatcher's claim returns to the pool.
		await using var command = new NpgsqlCommand(
			$"DELETE FROM {qualified} WHERE message_id = @MessageId AND dispatcher_id <> @Terminal;",
			connection);
#pragma warning restore CA2100

		_ = command.Parameters.AddWithValue("MessageId", messageId);
		_ = command.Parameters.AddWithValue("Terminal", TerminalDispatcherId);

		_ = await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
	}

	/// <summary>
	/// Validates and quotes a schema-qualified table name.
	/// </summary>
	/// <remarks>
	/// The names come from configuration rather than from a request, but they are interpolated into SQL
	/// because PostgreSQL has no parameter form for an object name. Rejecting anything that is not a bare
	/// identifier keeps that interpolation safe by construction instead of by trust.
	/// </remarks>
	/// <param name="schema">The schema name.</param>
	/// <param name="table">The table name.</param>
	/// <returns>The quoted, qualified name.</returns>
	/// <exception cref="ArgumentException">Either name is not a bare SQL identifier.</exception>
	private static string Qualify(string schema, string table)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(schema);
		ArgumentException.ThrowIfNullOrWhiteSpace(table);

		if (!SafeIdentifier.IsMatch(schema))
		{
			throw new ArgumentException($"'{schema}' is not a valid SQL identifier.", nameof(schema));
		}

		if (!SafeIdentifier.IsMatch(table))
		{
			throw new ArgumentException($"'{table}' is not a valid SQL identifier.", nameof(table));
		}

		return $"\"{schema}\".\"{table}\"";
	}
}
