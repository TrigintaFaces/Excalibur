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
			    message_id    text        NOT NULL PRIMARY KEY,
			    dispatcher_id text        NOT NULL,
			    claimed_at    timestamptz NOT NULL
			);
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
		var now = DateTimeOffset.UtcNow;

		// One statement, so there is no window between deciding a message is free and taking it.
		//
		// DO UPDATE rather than DO NOTHING: an expired claim must be takeable, or a dispatcher that
		// crashed mid-send would strand its messages permanently. The WHERE is what keeps that honest --
		// it holds only for a claim older than the timeout, so a live claim is never stolen and the
		// statement returns nothing for it.
		//
		// RETURNING reports only the rows actually written, which is exactly the set this dispatcher won.
#pragma warning disable CA2100 // Query built from validated identifiers, not from user input
		await using var command = new NpgsqlCommand(
			$"""
			INSERT INTO {qualified} (message_id, dispatcher_id, claimed_at)
			SELECT unnest(@MessageIds), @DispatcherId, @Now
			ON CONFLICT (message_id) DO UPDATE
			    SET dispatcher_id = EXCLUDED.dispatcher_id,
			        claimed_at    = EXCLUDED.claimed_at
			    WHERE {qualified}.claimed_at < @Cutoff
			RETURNING message_id;
			""",
			connection);
#pragma warning restore CA2100

		_ = command.Parameters.AddWithValue("MessageIds", messageIds.ToArray());
		_ = command.Parameters.AddWithValue("DispatcherId", dispatcherId);
		_ = command.Parameters.AddWithValue("Now", now);
		_ = command.Parameters.AddWithValue("Cutoff", now - claimTimeout);

		await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
		while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
		{
			_ = won.Add(reader.GetString(0));
		}

		return won;
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
		await using var command = new NpgsqlCommand(
			$"DELETE FROM {qualified} WHERE message_id = @MessageId;",
			connection);
#pragma warning restore CA2100

		_ = command.Parameters.AddWithValue("MessageId", messageId);

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
