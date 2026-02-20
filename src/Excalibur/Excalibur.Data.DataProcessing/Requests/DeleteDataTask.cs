// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Data;

using Dapper;

using Excalibur.Data.Abstractions;

namespace Excalibur.Data.DataProcessing.Requests;

/// <summary>
/// Represents a data request to delete a data task from the data processing system.
/// </summary>
public sealed class DeleteDataTask : DataRequest<int>
{
	/// <summary>
	/// Initializes a new instance of the <see cref="DeleteDataTask" /> class.
	/// </summary>
	/// <param name="dataTaskId"> The unique identifier of the data task to delete. </param>
	/// <param name="configuration"> The data processing configuration. </param>
	/// <param name="sqlTimeOutSeconds"> The SQL command timeout in seconds. </param>
	/// <param name="cancellationToken"> The cancellation token. </param>
	public DeleteDataTask(
		Guid dataTaskId,
		DataProcessingConfiguration configuration,
		int sqlTimeOutSeconds,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(configuration);

		var sql = $"""
		DELETE
		           FROM
		           {configuration.TableName}
		           WHERE
		           DataTaskId = @DataTaskId
		""";

		var parameters = new DynamicParameters();
		parameters.Add("DataTaskId", dataTaskId, direction: ParameterDirection.Input);

		Command = CreateCommand(sql, parameters: parameters, commandTimeout: sqlTimeOutSeconds, cancellationToken: cancellationToken);
		ResolveAsync = async conn => await conn.ExecuteAsync(Command).ConfigureAwait(false);
	}
}
