// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Data;

using Dapper;

using Excalibur.Data;

namespace Excalibur.Outbox.SqlServer.Requests;

/// <summary>
/// Data request to mark a transport delivery as sent.
/// </summary>
[NoTenantTerm(
	TenantConfinement.ForeignKeyConfined,
	"the transport rows are reached through MessageId, a foreign key to the globally-unique outbox Id, so every row this statement can match belongs to the one message the cross-tenant claim returned, and therefore to one tenant. Neither filtered column is a key, so the statement is not restricted to a single row; a tenant term still could not exclude another tenant's row, because no other tenant's row can match that MessageId")]
public sealed class MarkTransportSentRequest : DataRequestBase<IDbConnection, int>
{
	/// <summary>
	/// Initializes a new instance of the <see cref="MarkTransportSentRequest"/> class.
	/// </summary>
	/// <param name="tableName">The qualified transports table name.</param>
	/// <param name="messageId">The message ID.</param>
	/// <param name="transportName">The transport name.</param>
	/// <param name="commandTimeout">Command timeout in seconds.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	public MarkTransportSentRequest(
		string tableName,
		string messageId,
		string transportName,
		int commandTimeout,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(tableName);
		ArgumentException.ThrowIfNullOrWhiteSpace(messageId);
		ArgumentException.ThrowIfNullOrWhiteSpace(transportName);

		var sql = $"""
			UPDATE {tableName}
			SET Status = 2, SentAt = @SentAt, LastError = NULL
			WHERE MessageId = @MessageId AND TransportName = @TransportName
			""";

		var parameters = new DynamicParameters();
		parameters.Add("@MessageId", messageId);
		parameters.Add("@TransportName", transportName);
		parameters.Add("@SentAt", DateTimeOffset.UtcNow);

		Command = CreateCommand(sql, parameters, commandTimeout: commandTimeout, cancellationToken: cancellationToken);

		ResolveAsync = async connection =>
			await connection.ExecuteAsync(Command).ConfigureAwait(false);
	}
}
