// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Data;

using Dapper;

using Excalibur.Data.Abstractions;

namespace Excalibur.Outbox.SqlServer.Requests;

/// <summary>
/// Data request to delete transport deliveries for sent messages older than a specified date.
/// This is the first step in the cleanup process.
/// </summary>
public sealed class CleanupTransportDeliveriesRequest : DataRequestBase<IDbConnection, int>
{
	/// <summary>
	/// Initializes a new instance of the <see cref="CleanupTransportDeliveriesRequest"/> class.
	/// </summary>
	/// <param name="outboxTableName">The qualified outbox table name.</param>
	/// <param name="transportsTableName">The qualified transports table name.</param>
	/// <param name="olderThan">Delete deliveries for messages sent before this time.</param>
	/// <param name="batchSize">Maximum number of messages to process.</param>
	/// <param name="transaction">Optional transaction to participate in.</param>
	/// <param name="commandTimeout">Command timeout in seconds.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	public CleanupTransportDeliveriesRequest(
		string outboxTableName,
		string transportsTableName,
		DateTimeOffset olderThan,
		int batchSize,
		IDbTransaction? transaction,
		int commandTimeout,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(outboxTableName);
		ArgumentException.ThrowIfNullOrWhiteSpace(transportsTableName);

		var sql = $"""
			DELETE FROM {transportsTableName}
			WHERE MessageId IN (
				SELECT TOP (@BatchSize) Id
				FROM {outboxTableName}
				WHERE Status = 2 AND SentAt < @OlderThan
			)
			""";

		var parameters = new DynamicParameters();
		parameters.Add("@BatchSize", batchSize);
		parameters.Add("@OlderThan", olderThan);

		Command = CreateCommand(sql, parameters, transaction, commandTimeout, cancellationToken: cancellationToken);

		ResolveAsync = async connection =>
			await connection.ExecuteAsync(Command).ConfigureAwait(false);
	}
}

/// <summary>
/// Data request to delete sent messages older than a specified date.
/// This is the second step in the cleanup process, after transport deliveries are deleted.
/// </summary>
public sealed class CleanupSentMessagesRequest : DataRequestBase<IDbConnection, int>
{
	/// <summary>
	/// Initializes a new instance of the <see cref="CleanupSentMessagesRequest"/> class.
	/// </summary>
	/// <param name="tableName">The qualified outbox table name.</param>
	/// <param name="olderThan">Delete messages sent before this time.</param>
	/// <param name="batchSize">Maximum number of messages to delete.</param>
	/// <param name="transaction">Optional transaction to participate in.</param>
	/// <param name="commandTimeout">Command timeout in seconds.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	public CleanupSentMessagesRequest(
		string tableName,
		DateTimeOffset olderThan,
		int batchSize,
		IDbTransaction? transaction,
		int commandTimeout,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(tableName);

		var sql = $"""
			DELETE TOP (@BatchSize) FROM {tableName}
			WHERE Status = 2 AND SentAt < @OlderThan
			""";

		var parameters = new DynamicParameters();
		parameters.Add("@BatchSize", batchSize);
		parameters.Add("@OlderThan", olderThan);

		Command = CreateCommand(sql, parameters, transaction, commandTimeout, cancellationToken: cancellationToken);

		ResolveAsync = async connection =>
			await connection.ExecuteAsync(Command).ConfigureAwait(false);
	}
}
