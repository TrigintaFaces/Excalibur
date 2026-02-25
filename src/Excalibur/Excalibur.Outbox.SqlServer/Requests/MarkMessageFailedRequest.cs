// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Data;

using Dapper;

using Excalibur.Data.Abstractions;

namespace Excalibur.Outbox.SqlServer.Requests;

/// <summary>
/// Data request to mark a message as failed in the outbox.
/// </summary>
public sealed class MarkMessageFailedRequest : DataRequestBase<IDbConnection, int>
{
	/// <summary>
	/// Initializes a new instance of the <see cref="MarkMessageFailedRequest"/> class.
	/// </summary>
	/// <param name="tableName">The qualified outbox table name.</param>
	/// <param name="messageId">The message ID to mark as failed.</param>
	/// <param name="errorMessage">The error message.</param>
	/// <param name="retryCount">The current retry count.</param>
	/// <param name="commandTimeout">Command timeout in seconds.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	public MarkMessageFailedRequest(
		string tableName,
		string messageId,
		string errorMessage,
		int retryCount,
		int commandTimeout,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(tableName);
		ArgumentException.ThrowIfNullOrWhiteSpace(messageId);
		ArgumentNullException.ThrowIfNull(errorMessage);

		var sql = $"""
			UPDATE {tableName}
			SET Status = 3, LastError = @ErrorMessage, RetryCount = @RetryCount, LastAttemptAt = @LastAttemptAt
			WHERE Id = @MessageId
			""";

		var parameters = new DynamicParameters();
		parameters.Add("@MessageId", messageId);
		parameters.Add("@ErrorMessage", errorMessage);
		parameters.Add("@RetryCount", retryCount);
		parameters.Add("@LastAttemptAt", DateTimeOffset.UtcNow);

		Command = CreateCommand(sql, parameters, commandTimeout: commandTimeout, cancellationToken: cancellationToken);

		ResolveAsync = async connection =>
			await connection.ExecuteAsync(Command).ConfigureAwait(false);
	}
}
