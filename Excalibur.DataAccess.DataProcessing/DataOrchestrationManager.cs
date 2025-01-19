using System.Data;

using Dapper;

using Excalibur.Extensions;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.DataAccess.DataProcessing;

/// <summary>
///     Implements <see cref="IDataOrchestrationManager" /> for managing data tasks and delegating processing to registered processors.
/// </summary>
public class DataOrchestrationManager : IDataOrchestrationManager
{
	private readonly IDataProcessorDb db;
	private readonly IDataProcessorRegistry processorRegistry;
	private readonly IOptions<DataProcessingConfiguration> configuration;
	private readonly ILogger<DataOrchestrationManager> logger;

	/// <summary>
	///     Initializes a new instance of the <see cref="DataOrchestrationManager" /> class.
	/// </summary>
	/// <param name="db"> The database used for managing data tasks. </param>
	/// <param name="processorRegistry"> A registry for resolving processors for record types. </param>
	/// <param name="configuration"> Configuration options for data processing. </param>
	/// <param name="logger"> Logger for logging messages and errors. </param>
	public DataOrchestrationManager(
		IDataProcessorDb db,
		IDataProcessorRegistry processorRegistry,
		IOptions<DataProcessingConfiguration> configuration,
		ILogger<DataOrchestrationManager> logger)
	{
		ArgumentNullException.ThrowIfNull(db);
		ArgumentNullException.ThrowIfNull(processorRegistry);
		ArgumentNullException.ThrowIfNull(configuration);
		ArgumentNullException.ThrowIfNull(logger);

		this.db = db;
		this.processorRegistry = processorRegistry;
		this.configuration = configuration;
		this.logger = logger;
	}

	/// <inheritdoc />
	public async Task<Guid> AddDataTaskForRecordType(string recordType, DateTime dataTaskTimeoutUtc,
		CancellationToken cancellationToken = default)
	{
		var dataTaskId = Uuid7Extensions.GenerateGuid();
		var command = DataTaskCommands.InsertDataTaskRequest(dataTaskId, recordType, configuration.Value, DbTimeouts.RegularTimeoutSeconds,
			cancellationToken);

		_ = await db.Connection.Ready().ExecuteAsync(command).ConfigureAwait(false);

		return dataTaskId;
	}

	/// <inheritdoc />
	public async Task ProcessDataTask(CancellationToken cancellationToken = default)
	{
		var command = DataTaskCommands.GetDataTaskRequests(configuration.Value, DbTimeouts.RegularTimeoutSeconds, cancellationToken);
		var requests = (await db.Connection.Ready().QueryAsync<DataTaskRequest>(command).ConfigureAwait(false)).ToList();

		foreach (var request in requests)
		{
			cancellationToken.ThrowIfCancellationRequested();

			if (!processorRegistry.TryGetProcessor(request.RecordType, out var processor))
			{
				request.Attempts++;
				await UpdateAttemptsAsync(request.DataTaskId, request.Attempts, cancellationToken).ConfigureAwait(false);

				logger.LogWarning("Error processing back fill for table '{RecordType}. No processor found.'", request.RecordType);
				continue;
			}

			try
			{
				await processor.RunAsync(
					request.CompletedCount,
					(complete, cancellation) => UpdateCompletedCountAsync(request.DataTaskId, complete, cancellation),
					cancellationToken).ConfigureAwait(false);
				await DeleteRequestAsync(request.DataTaskId, cancellationToken).ConfigureAwait(false);
			}
			catch (Exception)
			{
				request.Attempts++;
				await UpdateAttemptsAsync(request.DataTaskId, request.Attempts, cancellationToken).ConfigureAwait(false);
			}
		}
	}

	private async Task UpdateAttemptsAsync(
		Guid dataTaskId,
		int attempts,
		CancellationToken cancellationToken = default)
	{
		var command = DataTaskCommands.UpdateDataTaskRequest(
			dataTaskId,
			attempts,
			configuration.Value,
			DbTimeouts.RegularTimeoutSeconds,
			cancellationToken);

		_ = await db.Connection.Ready().ExecuteAsync(command).ConfigureAwait(false);
	}

	private async Task UpdateCompletedCountAsync(
		Guid dataTaskId,
		long complete,
		CancellationToken cancellationToken = default)
	{
		var command = DataTaskCommands.UpdateCompletedCountRequest(
			dataTaskId,
			complete,
			configuration.Value,
			DbTimeouts.RegularTimeoutSeconds,
			cancellationToken);

		_ = await db.Connection.Ready().ExecuteAsync(command).ConfigureAwait(false);
	}

	private async Task DeleteRequestAsync(Guid dataTaskId, CancellationToken cancellationToken = default)
	{
		var command = DataTaskCommands.DeleteDataTaskRequest(
			dataTaskId,
			configuration.Value,
			DbTimeouts.RegularTimeoutSeconds,
			cancellationToken);

		_ = await db.Connection.Ready().ExecuteAsync(command).ConfigureAwait(false);
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
