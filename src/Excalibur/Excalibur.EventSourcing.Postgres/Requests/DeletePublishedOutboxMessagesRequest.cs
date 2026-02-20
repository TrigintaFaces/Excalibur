// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Data;

using Dapper;

using Excalibur.Data.Abstractions;

namespace Excalibur.EventSourcing.Postgres.Requests;

/// <summary>
/// Data request to delete published outbox messages older than a specified retention period from the Postgres outbox.
/// </summary>
public sealed class DeletePublishedOutboxMessagesRequest : DataRequestBase<IDbConnection, int>
{
	private const string Sql = """
		DELETE FROM event_sourced_outbox
		WHERE published_at IS NOT NULL
		  AND published_at < @CutoffDate
		""";

	/// <summary>
	/// Initializes a new instance of the <see cref="DeletePublishedOutboxMessagesRequest"/> class.
	/// </summary>
	/// <param name="retentionPeriod">The retention period for published messages.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	public DeletePublishedOutboxMessagesRequest(
		TimeSpan retentionPeriod,
		CancellationToken cancellationToken)
	{
		var parameters = new DynamicParameters();
		parameters.Add("@CutoffDate", DateTimeOffset.UtcNow - retentionPeriod);

		Command = CreateCommand(Sql, parameters, cancellationToken: cancellationToken);

		ResolveAsync = async connection =>
			await connection.ExecuteAsync(Command).ConfigureAwait(false);
	}
}
