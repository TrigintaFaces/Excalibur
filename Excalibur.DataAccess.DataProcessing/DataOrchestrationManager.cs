// Copyright (c) 2025 The Excalibur Project Authors
//
// Licensed under multiple licenses:
// - Excalibur License 1.0 (see LICENSE-EXCALIBUR.txt)
// - GNU Affero General Public License v3.0 or later (AGPL-3.0) (see LICENSE-AGPL-3.0.txt)
// - Server Side Public License v1.0 (SSPL-1.0) (see LICENSE-SSPL-1.0.txt)
// - Apache License 2.0 (see LICENSE-APACHE-2.0.txt)
//
// You may not use this file except in compliance with the License terms above. You may obtain copies of the licenses in
// the project root or online.
//
// Unless required by applicable law or agreed to in writing, software distributed under the License is distributed on
// an "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.

using System.Data;

using Dapper;

using Excalibur.Core.Extensions;
using Excalibur.DataAccess.SqlServer;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Polly;

namespace Excalibur.DataAccess.DataProcessing;

/// <summary>
///   Implements <see cref="IDataOrchestrationManager" /> for managing data tasks and delegating processing to
///   registered processors.
/// </summary>
public class DataOrchestrationManager : IDataOrchestrationManager
{
	private readonly IDataProcessorDb _db;

	private readonly IDataProcessorRegistry _processorRegistry;

	private readonly IOptions<DataProcessingConfiguration> _configuration;

	private readonly ILogger<DataOrchestrationManager> _logger;

	private readonly IAsyncPolicy _policy;

	/// <summary>
	///   Initializes a new instance of the <see cref="DataOrchestrationManager" /> class.
	/// </summary>
	/// <param name="db"> The database used for managing data tasks. </param>
	/// <param name="processorRegistry"> A registry for resolving processors for record types. </param>
	/// <param name="configuration"> Configuration options for data processing. </param>
	/// ///
	/// <param name="policyFactory"> Factory used to create SQL resiliency policies. </param>
	/// <param name="logger"> Logger for logging messages and errors. </param>
	public DataOrchestrationManager(
		IDataProcessorDb db,
		IDataProcessorRegistry processorRegistry,
		IOptions<DataProcessingConfiguration> configuration,
		IDataAccessPolicyFactory policyFactory,
		ILogger<DataOrchestrationManager> logger)
	{
		ArgumentNullException.ThrowIfNull(db);
		ArgumentNullException.ThrowIfNull(processorRegistry);
		ArgumentNullException.ThrowIfNull(configuration);
		ArgumentNullException.ThrowIfNull(policyFactory);
		ArgumentNullException.ThrowIfNull(logger);

		_db = db;
		_processorRegistry = processorRegistry;
		_configuration = configuration;
		_logger = logger;
		_policy = policyFactory.GetComprehensivePolicy();
	}

	/// <inheritdoc />
	public async Task<Guid> AddDataTaskForRecordType(string recordType, CancellationToken cancellationToken = default)
	{
		var dataTaskId = Uuid7Extensions.GenerateGuid();
		var command = DataTaskCommands.InsertDataTaskRequest(
			dataTaskId,
			recordType,
			_configuration.Value,
			DbTimeouts.RegularTimeoutSeconds,
			cancellationToken);

		_ = await _policy.ExecuteAsync(() =>
			_db.Connection
				.Ready()
				.ExecuteAsync(command))
			.ConfigureAwait(false);

		return dataTaskId;
	}

	/// <inheritdoc />
	public async ValueTask ProcessDataTasks(CancellationToken cancellationToken = default)
	{
		var command = DataTaskCommands.GetDataTaskRequests(_configuration.Value, DbTimeouts.RegularTimeoutSeconds, cancellationToken);
		var requests = (
			await _policy.ExecuteAsync(() =>
				_db.Connection
					.Ready()
					.QueryAsync<DataTaskRequest>(command))
				.ConfigureAwait(false)).ToList();

		if (requests.Count == 0)
		{
			await ValueTask.CompletedTask.ConfigureAwait(false);
			return;
		}

		await new ValueTask(ProcessRequestsAsync(requests, cancellationToken)).ConfigureAwait(false);
	}

	private async Task ProcessRequestsAsync(IList<DataTaskRequest> dataTaskRequests, CancellationToken cancellationToken)
	{
		foreach (var request in dataTaskRequests)
		{
			if (!_processorRegistry.TryGetProcessor(request.RecordType, out var dataProcessor))
			{
				request.Attempts++;
				await UpdateAttemptsAsync(request.DataTaskId, request.Attempts, cancellationToken).ConfigureAwait(false);

				_logger.LogWarning("Error processing back fill for table '{RecordType}. No processor found.'", request.RecordType);
			}
			else
			{
				try
				{
					_ = await dataProcessor.RunAsync(
						request.CompletedCount,
						(long complete, CancellationToken cancellation) =>
							UpdateCompletedCountAsync(request.DataTaskId, complete, cancellation),
						cancellationToken).ConfigureAwait(false);

					await dataProcessor.DisposeAsync().ConfigureAwait(false);

					await DeleteRequestAsync(request.DataTaskId, cancellationToken).ConfigureAwait(false);
				}
				catch (Exception ex)
				{
					request.Attempts++;
					await UpdateAttemptsAsync(request.DataTaskId, request.Attempts, cancellationToken).ConfigureAwait(false);
					_logger.LogError(
						ex,
						"Error processing data task for {RecordType}. Attempts: {Attempts}",
						request.RecordType,
						request.Attempts);
					throw;
				}
			}
		}
	}

	private async Task UpdateAttemptsAsync(Guid dataTaskId, int attempts, CancellationToken cancellationToken = default)
	{
		var command = DataTaskCommands.UpdateDataTaskRequest(
			dataTaskId,
			attempts,
			_configuration.Value,
			DbTimeouts.RegularTimeoutSeconds,
			cancellationToken);

		_ = await _policy.ExecuteAsync(() => _db.Connection.Ready().ExecuteAsync(command)).ConfigureAwait(false);
	}

	private async Task UpdateCompletedCountAsync(Guid dataTaskId, long complete, CancellationToken cancellationToken = default)
	{
		var command = DataTaskCommands.UpdateCompletedCountRequest(
			dataTaskId,
			complete,
			_configuration.Value,
			DbTimeouts.RegularTimeoutSeconds,
			cancellationToken);

		_ = await _policy.ExecuteAsync(() => _db.Connection.Ready().ExecuteAsync(command)).ConfigureAwait(false);
	}

	private async Task DeleteRequestAsync(Guid dataTaskId, CancellationToken cancellationToken = default)
	{
		var command = DataTaskCommands.DeleteDataTaskRequest(
			dataTaskId,
			_configuration.Value,
			DbTimeouts.RegularTimeoutSeconds,
			cancellationToken);

		_ = await _policy.ExecuteAsync(() => _db.Connection.Ready().ExecuteAsync(command)).ConfigureAwait(false);
	}

	internal static class DataTaskCommands
	{
		internal static CommandDefinition InsertDataTaskRequest(
			Guid dataTaskId,
			string recordType,
			DataProcessingConfiguration configuration,
			int sqlTimeOutSeconds,
			CancellationToken cancellationToken = default)
		{
			var commandText = $"""
			                   INSERT INTO {configuration.TableName}
			                        (DataTaskId, CreatedAt, RecordType, Attempts, MaxAttempts)
			                   VALUES
			                        (@DataTaskId, @CreatedAt, @RecordType, @Attempts, @MaxAttempts)
			                   """;

			var parameters = new DynamicParameters();
			parameters.Add("DataTaskId", dataTaskId, direction: ParameterDirection.Input);
			parameters.Add("CreatedAt", DateTime.UtcNow, direction: ParameterDirection.Input);
			parameters.Add("RecordType", recordType, direction: ParameterDirection.Input);
			parameters.Add("Attempts", 0, direction: ParameterDirection.Input);
			parameters.Add("MaxAttempts", configuration.MaxAttempts, direction: ParameterDirection.Input);

			return new CommandDefinition(commandText, parameters, commandTimeout: sqlTimeOutSeconds, cancellationToken: cancellationToken);
		}

		internal static CommandDefinition GetDataTaskRequests(
			DataProcessingConfiguration configuration,
			int sqlTimeOutSeconds,
			CancellationToken cancellationToken = default)
		{
			var commandText = $"""
			                   SELECT DataTaskId, CreatedAt, RecordType, Attempts, MaxAttempts, CompletedCount
			                   FROM
			                       {configuration.TableName}
			                   WHERE
			                       Attempts < MaxAttempts
			                   ORDER BY
			                       CreatedAt
			                   """;

			return new CommandDefinition(commandText, commandTimeout: sqlTimeOutSeconds, cancellationToken: cancellationToken);
		}

		internal static CommandDefinition UpdateDataTaskRequest(
			Guid dataTaskId,
			int attempts,
			DataProcessingConfiguration configuration,
			int sqlTimeOutSeconds,
			CancellationToken cancellationToken = default)
		{
			var commandText = $"""
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

			return new CommandDefinition(commandText, parameters, commandTimeout: sqlTimeOutSeconds, cancellationToken: cancellationToken);
		}

		internal static CommandDefinition UpdateCompletedCountRequest(
			Guid dataTaskId,
			long completedCount,
			DataProcessingConfiguration configuration,
			int sqlTimeOutSeconds,
			CancellationToken cancellationToken = default)
		{
			var commandText = $"""
			                   UPDATE
			                       {configuration.TableName}
			                   SET
			                       CompletedCount = @CompletedCount
			                   WHERE
			                       DataTaskId = @DataTaskId
			                   """;

			var parameters = new DynamicParameters();
			parameters.Add("DataTaskId", dataTaskId, direction: ParameterDirection.Input);
			parameters.Add("CompletedCount", completedCount, direction: ParameterDirection.Input);

			return new CommandDefinition(commandText, parameters, commandTimeout: sqlTimeOutSeconds, cancellationToken: cancellationToken);
		}

		internal static CommandDefinition DeleteDataTaskRequest(
			Guid dataTaskId,
			DataProcessingConfiguration configuration,
			int sqlTimeOutSeconds,
			CancellationToken cancellationToken = default)
		{
			var commandText = $"""
			                   DELETE
			                   FROM
			                       {configuration.TableName}
			                   WHERE
			                       DataTaskId = @DataTaskId
			                   """;

			var parameters = new DynamicParameters();
			parameters.Add("DataTaskId", dataTaskId, direction: ParameterDirection.Input);

			return new CommandDefinition(commandText, parameters, commandTimeout: sqlTimeOutSeconds, cancellationToken: cancellationToken);
		}
	}
}
