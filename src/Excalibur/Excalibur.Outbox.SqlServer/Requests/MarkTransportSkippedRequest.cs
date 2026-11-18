// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0


using System.Data;

using Dapper;

using Excalibur.Data;

namespace Excalibur.Outbox.SqlServer.Requests;

/// <summary>
/// Data request to mark a transport delivery as skipped.
/// </summary>
[NoTenantTerm(
	TenantConfinement.ForeignKeyConfined,
	"the transport rows are reached through MessageId, a foreign key to the globally-unique outbox Id, so every row this statement can match belongs to the one message the cross-tenant claim returned, and therefore to one tenant. Neither filtered column is a key, so the statement is not restricted to a single row; a tenant term still could not exclude another tenant's row, because no other tenant's row can match that MessageId")]
public sealed class MarkTransportSkippedRequest : DataRequestBase<IDbConnection, int>
{
	/// <summary>
	/// Initializes a new instance of the <see cref="MarkTransportSkippedRequest"/> class.
	/// </summary>
	/// <param name="tableName">The qualified transports table name.</param>
	/// <param name="messageId">The message ID.</param>
	/// <param name="transportName">The transport name.</param>
	/// <param name="reason">Optional reason for skipping.</param>
	/// <param name="commandTimeout">Command timeout in seconds.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <remarks>
	/// This framework request binds its fixed primitive input and output schema without runtime emit.
	/// Replacing a known parameter value is observed at execution; custom parameter metadata, templates
	/// and output callbacks are not an extension contract. Custom IDataRequest implementations are unchanged.
	/// </remarks>
	public MarkTransportSkippedRequest(
		string tableName,
		string messageId,
		string transportName,
		string? reason,
		int commandTimeout,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(tableName);
		ArgumentException.ThrowIfNullOrWhiteSpace(messageId);
		ArgumentException.ThrowIfNullOrWhiteSpace(transportName);

		var sql = $"""
			UPDATE {tableName}
			SET Status = 4, LastError = @Reason, AttemptedAt = @AttemptedAt
			WHERE MessageId = @MessageId AND TransportName = @TransportName
			""";

		var parameters = new DynamicParameters();
		parameters.Add("@MessageId", messageId);
		parameters.Add("@TransportName", transportName);
		parameters.Add("@Reason", reason);
		parameters.Add("@AttemptedAt", DateTimeOffset.UtcNow);

		Command = CreateCommand(sql, parameters, commandTimeout: commandTimeout, cancellationToken: cancellationToken);

		ResolveAsync = async connection =>
			await StaticDbCommand.ExecuteAsync(connection, Command, command =>
			{
				StaticDbCommand.AddParameter(command, "MessageId", StaticDbCommand.Input<string>(Parameters, "MessageId"), DbType.String);
				StaticDbCommand.AddParameter(command, "TransportName", StaticDbCommand.Input<string>(Parameters, "TransportName"), DbType.String);
				StaticDbCommand.AddNullableStringParameter(command, "Reason", StaticDbCommand.Input<string?>(Parameters, "Reason"));
				StaticDbCommand.AddParameter(command, "AttemptedAt", StaticDbCommand.Input<DateTimeOffset>(Parameters, "AttemptedAt"), DbType.DateTimeOffset);
			}).ConfigureAwait(false);
	}
}
