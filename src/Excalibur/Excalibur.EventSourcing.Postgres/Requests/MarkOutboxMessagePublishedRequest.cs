// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Data;

using Dapper;

using Excalibur.Data.Abstractions;

namespace Excalibur.EventSourcing.Postgres.Requests;

/// <summary>
/// Data request to mark an outbox message as published in the Postgres outbox.
/// </summary>
public sealed class MarkOutboxMessagePublishedRequest : DataRequestBase<IDbConnection, int>
{
	private const string Sql = """
		UPDATE event_sourced_outbox
		SET published_at = @PublishedAt
		WHERE id = @Id AND published_at IS NULL
		""";

	/// <summary>
	/// Initializes a new instance of the <see cref="MarkOutboxMessagePublishedRequest"/> class.
	/// </summary>
	/// <param name="messageId">The unique identifier of the message to mark as published.</param>
	/// <param name="transaction">Optional database transaction.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	public MarkOutboxMessagePublishedRequest(
		Guid messageId,
		IDbTransaction? transaction,
		CancellationToken cancellationToken)
	{
		var parameters = new DynamicParameters();
		parameters.Add("@Id", messageId);
		parameters.Add("@PublishedAt", DateTimeOffset.UtcNow);

		Command = CreateCommand(Sql, parameters, transaction: transaction, cancellationToken: cancellationToken);

		ResolveAsync = async connection =>
			await connection.ExecuteAsync(Command).ConfigureAwait(false);
	}
}
