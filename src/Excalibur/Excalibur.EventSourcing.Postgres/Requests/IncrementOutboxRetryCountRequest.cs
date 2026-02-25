// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Data;

using Dapper;

using Excalibur.Data.Abstractions;

namespace Excalibur.EventSourcing.Postgres.Requests;

/// <summary>
/// Data request to increment the retry count for an outbox message in the Postgres outbox.
/// </summary>
public sealed class IncrementOutboxRetryCountRequest : DataRequestBase<IDbConnection, int>
{
	private const string Sql = """
		UPDATE event_sourced_outbox
		SET retry_count = retry_count + 1
		WHERE id = @Id
		""";

	/// <summary>
	/// Initializes a new instance of the <see cref="IncrementOutboxRetryCountRequest"/> class.
	/// </summary>
	/// <param name="messageId">The unique identifier of the message.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	public IncrementOutboxRetryCountRequest(
		Guid messageId,
		CancellationToken cancellationToken)
	{
		var parameters = new DynamicParameters();
		parameters.Add("@Id", messageId);

		Command = CreateCommand(Sql, parameters, cancellationToken: cancellationToken);

		ResolveAsync = async connection =>
			await connection.ExecuteAsync(Command).ConfigureAwait(false);
	}
}
