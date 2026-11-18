// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0


using System.Data;

using Dapper;

using Excalibur.Data;

namespace Excalibur.Outbox.SqlServer.Requests;

/// <summary>
/// Data request to get failed messages from the outbox.
/// </summary>
/// <remarks>
/// This framework-owned request uses its fixed constructor-defined primitive input schema. Replacing a
/// known parameter value through <c>Parameters</c> is observed at execution; parameter DbType,
/// direction, size, templates and output callbacks are not an extensibility contract of this request.
/// Custom <c>IDataRequest</c> implementations retain their own execution behavior.
/// </remarks>
[NoTenantTerm(
	TenantConfinement.EstateWide,
	"the outbox drain is cross-tenant by design: one dispatcher serves every tenant, and the row carries TenantId so the handler can re-establish the owning tenant per message. Scoping this read to the ambient tenant would stall delivery for every other tenant")]
public sealed class GetFailedMessagesRequest : DataRequestBase<IDbConnection, IEnumerable<OutboxMessageRow>>
{
	/// <summary>
	/// Initializes a new instance of the <see cref="GetFailedMessagesRequest"/> class.
	/// </summary>
	/// <param name="tableName">The qualified outbox table name.</param>
	/// <param name="maxRetries">Maximum number of retries to consider.</param>
	/// <param name="olderThan">Only return messages that failed before this timestamp.</param>
	/// <param name="batchSize">Maximum number of messages to retrieve.</param>
	/// <param name="commandTimeout">Command timeout in seconds.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	public GetFailedMessagesRequest(
		string tableName,
		int maxRetries,
		DateTimeOffset? olderThan,
		int batchSize,
		int commandTimeout,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(tableName);

		var sql = $"""
			SELECT TOP (@BatchSize)
				Id, MessageType, Payload, Headers, Destination, CreatedAt, ScheduledAt, SentAt,
				Status, RetryCount, LastError, LastAttemptAt, CorrelationId, CausationId,
				TenantId, Priority, TargetTransports, IsMultiTransport,
				PartitionKey, GroupKey, SequenceNumber
			FROM {tableName}
			WHERE Status IN (3, 4) -- Failed, PartiallyFailed
				AND RetryCount < @MaxRetries
				AND (@OlderThan IS NULL OR LastAttemptAt < @OlderThan)
			ORDER BY LastAttemptAt ASC
			""";

		var parameters = new DynamicParameters();
		parameters.Add("@BatchSize", batchSize);
		parameters.Add("@MaxRetries", maxRetries);
		parameters.Add("@OlderThan", olderThan);

		Command = CreateCommand(sql, parameters, commandTimeout: commandTimeout, cancellationToken: cancellationToken);

		ResolveAsync = async connection =>
			await StaticDbCommand.QueryAsync(connection, Command, command =>
			{
				StaticDbCommand.AddParameter(command, "BatchSize", StaticDbCommand.Input<int>(Parameters, "BatchSize"), DbType.Int32);
				StaticDbCommand.AddParameter(command, "MaxRetries", StaticDbCommand.Input<int>(Parameters, "MaxRetries"), DbType.Int32);
				StaticDbCommand.AddParameter(command, "OlderThan", StaticDbCommand.Input<DateTimeOffset?>(Parameters, "OlderThan"), DbType.DateTimeOffset);
			}, reader => SqlServerOutboxRowReader.Read(reader, includeDispatcher: false)).ConfigureAwait(false);
	}
}
