// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Data;

using Dapper;

using Excalibur.Data.Abstractions;

namespace Excalibur.Data.DataProcessing.Requests;

/// <summary>
/// Represents a data request to update the attempt count for a data task in the data processing system.
/// </summary>
public sealed class UpdateDataTaskAttempts : DataRequest<int>
{
	/// <summary>
	/// Initializes a new instance of the <see cref="UpdateDataTaskAttempts"/> class.
	/// </summary>
	/// <param name="dataTaskId">The unique identifier of the data task to update.</param>
	/// <param name="attempts">The new attempt count.</param>
	/// <param name="configuration">The data processing configuration.</param>
	/// <param name="sqlTimeOutSeconds">The SQL command timeout in seconds.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	public UpdateDataTaskAttempts(
		Guid dataTaskId,
		int attempts,
		DataProcessingConfiguration configuration,
		int sqlTimeOutSeconds,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(configuration);

		var sql = $"""
		UPDATE
		           {configuration.TableName}
		           SET
		           Attempts = @Attempts
		           WHERE
		           DataTaskId = @DataTaskId
		""";

		var parameters = new DynamicParameters();
		parameters.Add("DataTaskId", dataTaskId, direction: ParameterDirection.Input);
		parameters.Add("Attempts", attempts, direction: ParameterDirection.Input);

		Command = CreateCommand(sql, parameters: parameters, commandTimeout: sqlTimeOutSeconds, cancellationToken: cancellationToken);
		ResolveAsync = async conn => await conn.ExecuteAsync(Command).ConfigureAwait(false);
	}
}
