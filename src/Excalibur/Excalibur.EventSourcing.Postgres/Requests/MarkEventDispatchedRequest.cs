// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Data;

using Dapper;

using Excalibur.Data.Abstractions;

namespace Excalibur.EventSourcing.Postgres.Requests;

/// <summary>
/// Data request to mark an event as dispatched in the Postgres event store.
/// </summary>
public sealed class MarkEventDispatchedRequest : DataRequestBase<IDbConnection, int>
{
	private const string Sql = """
		UPDATE events
		SET is_dispatched = true
		WHERE event_id = @EventId::uuid
		""";

	/// <summary>
	/// Initializes a new instance of the <see cref="MarkEventDispatchedRequest"/> class.
	/// </summary>
	/// <param name="eventId">The event identifier to mark as dispatched.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	public MarkEventDispatchedRequest(
		string eventId,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(eventId);

		var parameters = new DynamicParameters();
		parameters.Add("@EventId", eventId);

		Command = CreateCommand(Sql, parameters, cancellationToken: cancellationToken);

		ResolveAsync = async connection =>
			await connection.ExecuteAsync(Command).ConfigureAwait(false);
	}
}
