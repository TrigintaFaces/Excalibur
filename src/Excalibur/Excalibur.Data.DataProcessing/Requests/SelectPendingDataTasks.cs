// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Dapper;

using Excalibur.Data.Abstractions;

namespace Excalibur.Data.DataProcessing.Requests;

/// <summary>
/// Represents a data request to select pending data tasks from the data processing system.
/// </summary>
public sealed class SelectPendingDataTasks : DataRequest<IEnumerable<DataTaskRequest>>
{
	/// <summary>
	/// Initializes a new instance of the <see cref="SelectPendingDataTasks"/> class.
	/// </summary>
	/// <param name="configuration">The data processing configuration.</param>
	/// <param name="sqlTimeOutSeconds">The SQL command timeout in seconds.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	public SelectPendingDataTasks(
		DataProcessingConfiguration configuration,
		int sqlTimeOutSeconds,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(configuration);

		var sql = $"""
		SELECT DataTaskId, CreatedAt, RecordType, Attempts, MaxAttempts, CompletedCount
		           FROM
		           {configuration.TableName}
		           WHERE
		           Attempts < MaxAttempts
		           ORDER BY
		           CreatedAt
		""";

		Command = CreateCommand(sql, commandTimeout: sqlTimeOutSeconds, cancellationToken: cancellationToken);
		ResolveAsync = async conn => await conn.QueryAsync<DataTaskRequest>(Command).ConfigureAwait(false);
	}
}
