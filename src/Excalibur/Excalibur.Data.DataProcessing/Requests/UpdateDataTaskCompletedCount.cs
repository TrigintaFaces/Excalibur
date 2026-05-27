// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Data;

using Dapper;

namespace Excalibur.Data.DataProcessing.Requests;

/// <summary>
/// Represents a data request to update the completed count and processed cursor for a data task
/// in the data processing system.
/// </summary>
internal sealed class UpdateDataTaskCompletedCount : DataRequest<int>
{
	/// <summary>
	/// Initializes a new instance of the <see cref="UpdateDataTaskCompletedCount"/> class.
	/// </summary>
	/// <param name="dataTaskId">The unique identifier of the data task to update.</param>
	/// <param name="completedCount">The new completed count.</param>
	/// <param name="processedCursor">
	/// The processed cursor to persist, or <see langword="null"/> to preserve the existing value.
	/// </param>
	/// <param name="configuration">The data processing configuration.</param>
	/// <param name="sqlTimeOutSeconds">The SQL command timeout in seconds.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	public UpdateDataTaskCompletedCount(
		Guid dataTaskId,
		long completedCount,
		string? processedCursor,
		DataProcessingOptions configuration,
		int sqlTimeOutSeconds,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(configuration);

		// COALESCE preserves the existing ProcessedCursor when @ProcessedCursor is NULL.
		// This allows per-record count-only checkpoints (cursor=null) without overwriting
		// the durable cursor that was set at page-boundary granularity.
		var sql = $"""
		UPDATE
		           {configuration.QualifiedTableName}
		           SET
		           CompletedCount = @CompletedCount,
		           ProcessedCursor = COALESCE(@ProcessedCursor, ProcessedCursor)
		           WHERE
		           DataTaskId = @DataTaskId
		""";

		var parameters = new DynamicParameters();
		parameters.Add("DataTaskId", dataTaskId, direction: ParameterDirection.Input);
		parameters.Add("CompletedCount", completedCount, direction: ParameterDirection.Input);
		parameters.Add("ProcessedCursor", processedCursor, direction: ParameterDirection.Input);

		Command = CreateCommand(sql, parameters: parameters, commandTimeout: sqlTimeOutSeconds, cancellationToken: cancellationToken);
		ResolveAsync = async conn => await conn.ExecuteAsync(Command).ConfigureAwait(false);
	}
}
