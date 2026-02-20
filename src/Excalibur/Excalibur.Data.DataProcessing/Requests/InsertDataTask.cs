// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Data;

using Dapper;

using Excalibur.Data.Abstractions;

namespace Excalibur.Data.DataProcessing.Requests;

/// <summary>
/// Represents a data request to insert a new data task into the data processing system.
/// </summary>
public sealed class InsertDataTask : DataRequest<int>
{
	/// <summary>
	/// Initializes a new instance of the <see cref="InsertDataTask" /> class.
	/// </summary>
	/// <param name="dataTaskId"> The unique identifier of the data task. </param>
	/// <param name="recordType"> The type of record being processed. </param>
	/// <param name="configuration"> The data processing configuration. </param>
	/// <param name="sqlTimeOutSeconds"> The SQL command timeout in seconds. </param>
	/// <param name="cancellationToken"> The cancellation token. </param>
	public InsertDataTask(
		Guid dataTaskId,
		string recordType,
		DataProcessingConfiguration configuration,
		int sqlTimeOutSeconds,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(configuration);

		var sql = $"""
		INSERT INTO {configuration.TableName}
		           (DataTaskId, CreatedAt, RecordType, Attempts, MaxAttempts)
		           VALUES
		           (@DataTaskId, @CreatedAt, @RecordType, @Attempts, @MaxAttempts)
		""";

		var parameters = new DynamicParameters();
		parameters.Add("DataTaskId", dataTaskId, direction: ParameterDirection.Input);
		parameters.Add("CreatedAt", DateTimeOffset.UtcNow, direction: ParameterDirection.Input);
		parameters.Add("RecordType", recordType, direction: ParameterDirection.Input);
		parameters.Add("Attempts", 0, direction: ParameterDirection.Input);
		parameters.Add("MaxAttempts", configuration.MaxAttempts, direction: ParameterDirection.Input);

		Command = CreateCommand(sql, parameters: parameters, commandTimeout: sqlTimeOutSeconds, cancellationToken: cancellationToken);
		ResolveAsync = async conn => await conn.ExecuteAsync(Command).ConfigureAwait(false);
	}
}
