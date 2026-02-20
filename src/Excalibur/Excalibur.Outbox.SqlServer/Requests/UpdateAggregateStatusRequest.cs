// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Data;

using Dapper;

using Excalibur.Data.Abstractions;

namespace Excalibur.Outbox.SqlServer.Requests;

/// <summary>
/// Data request to update the aggregate status of a message based on transport delivery statuses.
/// </summary>
public sealed class UpdateAggregateStatusRequest : DataRequestBase<IDbConnection, int>
{
	/// <summary>
	/// Initializes a new instance of the <see cref="UpdateAggregateStatusRequest"/> class.
	/// </summary>
	/// <param name="outboxTableName">The qualified outbox table name.</param>
	/// <param name="transportsTableName">The qualified transports table name.</param>
	/// <param name="messageId">The message ID to update.</param>
	/// <param name="commandTimeout">Command timeout in seconds.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	public UpdateAggregateStatusRequest(
		string outboxTableName,
		string transportsTableName,
		string messageId,
		int commandTimeout,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(outboxTableName);
		ArgumentException.ThrowIfNullOrWhiteSpace(transportsTableName);
		ArgumentException.ThrowIfNullOrWhiteSpace(messageId);

		var sql = $"""
			DECLARE @AllSent BIT, @AnyFailed BIT, @AllFailed BIT, @AnySending BIT;

			SELECT
				@AllSent = CASE WHEN COUNT(*) = SUM(CASE WHEN Status IN (2, 4) THEN 1 ELSE 0 END) THEN 1 ELSE 0 END,
				@AllFailed = CASE WHEN COUNT(*) = SUM(CASE WHEN Status = 3 THEN 1 ELSE 0 END) THEN 1 ELSE 0 END,
				@AnyFailed = CASE WHEN SUM(CASE WHEN Status = 3 THEN 1 ELSE 0 END) > 0 THEN 1 ELSE 0 END,
				@AnySending = CASE WHEN SUM(CASE WHEN Status = 1 THEN 1 ELSE 0 END) > 0 THEN 1 ELSE 0 END
			FROM {transportsTableName}
			WHERE MessageId = @MessageId;

			UPDATE {outboxTableName}
			SET Status = CASE
				WHEN @AllSent = 1 THEN 2 -- Sent
				WHEN @AnySending = 1 THEN 1 -- Sending
				WHEN @AllFailed = 1 THEN 3 -- Failed
				WHEN @AnyFailed = 1 THEN 4 -- PartiallyFailed
				ELSE Status
			END,
			SentAt = CASE WHEN @AllSent = 1 THEN SYSDATETIMEOFFSET() ELSE SentAt END
			WHERE Id = @MessageId;
			""";

		var parameters = new DynamicParameters();
		parameters.Add("@MessageId", messageId);

		Command = CreateCommand(sql, parameters, commandTimeout: commandTimeout, cancellationToken: cancellationToken);

		ResolveAsync = async connection =>
			await connection.ExecuteAsync(Command).ConfigureAwait(false);
	}
}
