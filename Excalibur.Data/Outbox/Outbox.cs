using System.Data;
using System.Text.Json;

using Dapper;

using Excalibur.Core;
using Excalibur.Core.Diagnostics;
using Excalibur.Core.Domain.Events;
using Excalibur.Core.Extensions;
using Excalibur.Data.Outbox.Serialization;
using Excalibur.DataAccess;
using Excalibur.Domain;
using Excalibur.Domain.Model;

using Microsoft.ApplicationInsights;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.Data.Outbox;

/// <summary>
///     Provides functionality for managing and dispatching outbox messages to ensure reliable delivery.
/// </summary>
public class Outbox : IOutbox
{
	private static readonly JsonSerializerOptions SerializerOptions = new() { Converters = { new OutboxMessageJsonConverter() } };

	private readonly OutboxConfiguration _configuration;

	private readonly IDbConnection _connection;

	private readonly IActivityContext _context;

	private readonly IServiceProvider _serviceProvider;

	private readonly ILogger<Outbox> _logger;

	private readonly TelemetryClient? _telemetryClient;

	/// <summary>
	///     Initializes a new instance of the <see cref="Outbox" /> class.
	/// </summary>
	/// <param name="context"> The activity context containing tenant and correlation information. </param>
	/// <param name="domainDb"> The domain database connection. </param>
	/// <param name="serviceProvider"> The root service provider for creating new scopes. </param>
	/// <param name="configuration"> The outbox configuration options. </param>
	/// <param name="logger"> The logger instance for logging events. </param>
	/// <param name="telemetryClient">
	///     The Application Insights TelemetryClient used to record metrics, events, and exceptions for monitoring and analysis.
	/// </param>
	public Outbox(
		IActivityContext context,
		IDomainDb domainDb,
		IServiceProvider serviceProvider,
		IOptions<OutboxConfiguration> configuration,
		ILogger<Outbox> logger,
		TelemetryClient? telemetryClient = null)
	{
		ArgumentNullException.ThrowIfNull(context);
		ArgumentNullException.ThrowIfNull(domainDb);
		ArgumentNullException.ThrowIfNull(serviceProvider);
		ArgumentNullException.ThrowIfNull(configuration);
		ArgumentNullException.ThrowIfNull(logger);

		_context = context;
		_serviceProvider = serviceProvider;
		_logger = logger;
		_telemetryClient = telemetryClient;
		_configuration = configuration.Value;
		_connection = domainDb.Connection;
	}

	/// <inheritdoc />
	public Task TryUnReserveOneRecordsAsync(string dispatcherId, CancellationToken cancellationToken) =>
		_connection.ExecuteAsync(OutboxCommands.UnReserveOutboxRecords(dispatcherId, DbTimeouts.RegularTimeoutSeconds, _configuration));

	/// <inheritdoc />
	public async Task<IEnumerable<OutboxRecord>> TryReserveOneRecordsAsync(
		string dispatcherId,
		int batchSize,
		CancellationToken cancellationToken)
	{
		var stopwatch = ValueStopwatch.StartNew();

		try
		{
			var records = await _connection.QueryAsync<OutboxRecord>(
							  OutboxCommands.ReserveOutboxRecords(
								  dispatcherId,
								  batchSize,
								  DbTimeouts.LongRunningTimeoutSeconds,
								  _configuration)).ConfigureAwait(false);

			_telemetryClient?.TrackMetric("Outbox.ReservedOutboxBatchSize", records.Count());
			return records;
		}
		finally
		{
			_telemetryClient?.TrackMetric("Outbox.OutboxReservationTime", stopwatch.Elapsed.TotalMilliseconds);
		}
	}

	/// <inheritdoc />
	public async Task<int> DispatchReservedRecordAsync(string dispatcherId, OutboxRecord record)
	{
		ArgumentException.ThrowIfNullOrEmpty(dispatcherId);
		ArgumentNullException.ThrowIfNull(record);

		var stopwatch = ValueStopwatch.StartNew();
		try
		{
			_telemetryClient?.TrackEvent(
				"Outbox.OutboxRecordDispatchStarted",
				new Dictionary<string, string> { { "DispatcherId", dispatcherId }, { "OutboxId", record.OutboxId.ToString() } });

			_logger.LogInformation(
				"Dispatching OutboxRecord with Id {OutboxId} from dispatcher {DispatcherId}",
				record.OutboxId,
				dispatcherId);

			var result = await Dispatch(record).ConfigureAwait(false);

			_logger.LogInformation(
				"Successfully dispatched OutboxRecord with Id {OutboxId} from dispatcher {DispatcherId}",
				record.OutboxId,
				dispatcherId);

			_telemetryClient?.TrackMetric("Outbox.MessageProcessingDuration", stopwatch.Elapsed.TotalMilliseconds);
			_telemetryClient?.TrackEvent(
				"Outbox.OutboxRecordDispatchSucceeded",
				new Dictionary<string, string> { { "DispatcherId", dispatcherId }, { "OutboxId", record.OutboxId.ToString() } });

			_ = await _connection.ExecuteAsync(
						OutboxCommands.DeleteOutboxRecord(record.OutboxId, DbTimeouts.RegularTimeoutSeconds, _configuration))
					.ConfigureAwait(false);
			_logger.LogInformation("Deleted OutboxRecord with Id {OutboxId} after successful dispatch", record.OutboxId);

			return result;
		}
		catch (Exception ex)
		{
			_logger.LogError(
				ex,
				"Error dispatching OutboxRecord with Id {OutboxId} from dispatcher {DispatcherId}",
				record.OutboxId,
				dispatcherId);

			_telemetryClient?.TrackException(
				ex,
				new Dictionary<string, string>
				{
					{ "DispatcherId", dispatcherId }, { "OutboxId", record.OutboxId.ToString() }, { "ErrorType", ex.GetType().Name }
				});

			_ = await _connection.ExecuteAsync(
					OutboxCommands.IncrementAttemptsOrMoveToDeadLetter(
						record.OutboxId,
						record.EventData,
						ex.Message,
						DbTimeouts.LongRunningTimeoutSeconds,
						_configuration)).ConfigureAwait(false);

			return 0;
		}
	}

	/// <inheritdoc />
	public async Task<int> SaveEventsAsync<TKey>(
		IAggregateRoot<TKey> aggregate,
		IReadOnlyDictionary<string, string> messageHeaders,
		string destination)
	{
		ArgumentNullException.ThrowIfNull(aggregate);

		if (!aggregate.DomainEvents.Any())
		{
			_logger.LogInformation("No domain events to save for {AggregateType} Aggregate.", aggregate.GetType());
			return 0;
		}

		var messages = aggregate.DomainEvents.Select(
			(IDomainEvent evt) => new OutboxMessage
			{
				MessageId = Uuid7Extensions.GenerateString(),
				MessageBody = evt,
				MessageHeaders = GetDefaultHeaders()
			}).ToArray();
		var result = await SaveMessagesAsync(messages).ConfigureAwait(false);

		if (messages.Length > 0)
		{
			_logger.LogInformation(
				"Saved OutboxRecord with {MessageCount} outbox messages for {AggregateType} Aggregate.",
				messages.Length,
				aggregate.GetType());
		}

		(aggregate.DomainEvents as ICollection<IDomainEvent>)?.Clear();

		return result;
	}

	/// <inheritdoc />
	public Task<int> SaveMessagesAsync(IEnumerable<OutboxMessage> messages)
	{
		var outboxMessages = messages as OutboxMessage[] ?? messages.ToArray();
		if (outboxMessages.Length == 0)
		{
			_logger.LogInformation("No messages to save to the outbox.");
			return Task.FromResult(0);
		}

		var outboxRecordId = Uuid7Extensions.GenerateGuid();
		var eventData = JsonSerializer.Serialize(outboxMessages, SerializerOptions);

		_logger.LogInformation(
			"Saving {MessageCount} messages to the {OutboxTable} with Id {OutboxId}.",
			outboxMessages.Length,
			_configuration.TableName,
			outboxRecordId);

		return _connection.ExecuteAsync(
			OutboxCommands.InsertOutboxRecord(outboxRecordId, eventData, DbTimeouts.RegularTimeoutSeconds, _configuration));
	}

	/// <summary>
	///     Retrieves default message headers for outbox messages.
	/// </summary>
	/// <returns> A dictionary containing default headers such as CorrelationId and TenantId. </returns>
	public IReadOnlyDictionary<string, string> GetDefaultHeaders() =>
		new Dictionary<string, string>
		{
			{ ExcaliburHeaderNames.CorrelationId, _context.CorrelationId().ToString() },
			{ ExcaliburHeaderNames.TenantId, _context.TenantId() }
		};

	/// <summary>
	///     Dispatches the outbox messages for a given record.
	/// </summary>
	/// <param name="outboxRecord"> The outbox record containing event data. </param>
	/// <returns> The count of dispatched messages. </returns>
	private async Task<int> Dispatch(OutboxRecord outboxRecord)
	{
		var messages = JsonSerializer.Deserialize<List<OutboxMessage>>(outboxRecord.EventData, SerializerOptions) ?? [];

		if (messages.Count <= 0)
		{
			_telemetryClient?.TrackMetric("Outbox.EmptyOutboxMessages", 1);
			return messages.Count;
		}

		var successCount = 0;
		foreach (var message in messages)
		{
			try
			{
				using var scope = _serviceProvider.CreateScope();
				var scopedDispatcher = scope.ServiceProvider.GetRequiredService<IOutboxMessageDispatcher>();
				await scopedDispatcher.DispatchAsync(message).ConfigureAwait(false);

				successCount++;
			}
			catch (Exception ex)
			{
				_telemetryClient?.TrackException(
					ex,
					new Dictionary<string, string>
					{
						{ "MessageId", message.MessageId }, { "OutboxId", outboxRecord.OutboxId.ToString() }
					});

				_logger.LogError(ex, "Failed to dispatch message with ID {MessageId}", message.MessageId);
				throw;
			}
		}

		_telemetryClient?.TrackMetric("OutboxMessagesDispatched", successCount);
		return successCount;
	}

	internal static class OutboxCommands
	{
		internal static CommandDefinition DeleteOutboxRecord(Guid outboxRecordId, int sqlTimeOutSeconds, OutboxConfiguration configuration)
		{
			var sql = $"DELETE FROM {configuration.TableName} WHERE OutboxId = @OutboxId";

			var parameters = new DynamicParameters();
			parameters.Add("OutboxId", outboxRecordId, direction: ParameterDirection.Input);

			return new CommandDefinition(sql, parameters, commandTimeout: sqlTimeOutSeconds);
		}

		internal static CommandDefinition InsertOutboxRecord(
			Guid outboxRecordId,
			string eventData,
			int sqlTimeOutSeconds,
			OutboxConfiguration configuration)
		{
			var sql = $"INSERT INTO {configuration.TableName} (OutboxId, EventData, CreatedAt) VALUES (@OutboxId, @EventData, @CreatedAt)";

			var parameters = new DynamicParameters();
			parameters.Add("OutboxId", outboxRecordId, direction: ParameterDirection.Input);
			parameters.Add("EventData", eventData, direction: ParameterDirection.Input);
			parameters.Add("CreatedAt", DateTime.UtcNow, direction: ParameterDirection.Input);

			return new CommandDefinition(sql, parameters, commandTimeout: sqlTimeOutSeconds);
		}

		internal static CommandDefinition UnReserveOutboxRecords(
			string dispatcherId,
			int sqlTimeOutSeconds,
			OutboxConfiguration configuration)
		{
			var sql = $"""
			           UPDATE
			               {configuration.TableName}
			           SET
			               DispatcherId = NULL,
			               DispatcherTimeout = NULL
			           WHERE
			               DispatcherId = @DispatcherId;
			           """;

			var parameters = new DynamicParameters();
			parameters.Add("DispatcherId", dispatcherId, direction: ParameterDirection.Input);
			parameters.Add("TimeoutMilliseconds", configuration.DispatcherTimeoutMilliseconds, direction: ParameterDirection.Input);

			return new CommandDefinition(sql, parameters, commandTimeout: sqlTimeOutSeconds);
		}

		internal static CommandDefinition ReserveOutboxRecords(
			string dispatcherId,
			int batchSize,
			int sqlTimeOutSeconds,
			OutboxConfiguration configuration)
		{
			var sql = $"""
			           DECLARE @CurrentUtcDate DATETIME = GETUTCDATE();

			           ;WITH CTE_Outbox AS
			           (
			               SELECT TOP {batchSize}
			                   OutboxId
			               FROM
			                   {configuration.TableName}
			               WHERE
			                   (DispatcherId IS NULL)
			               OR
			                   (@CurrentUtcDate > DispatcherTimeout)
			               ORDER BY
			                   CreatedAt
			           )
			           UPDATE
			               {configuration.TableName}
			           SET
			               DispatcherId = @DispatcherId,
			               DispatcherTimeout = DATEADD(MILLISECOND, @TimeoutMilliseconds, @CurrentUtcDate)
			           OUTPUT
			               inserted.OutboxId, inserted.CreatedAt, inserted.EventData, inserted.DispatcherId, inserted.DispatcherTimeout, inserted.Attempts
			           WHERE
			               OutboxId IN (SELECT OutboxId FROM CTE_Outbox);
			           """;

			var parameters = new DynamicParameters();
			parameters.Add("DispatcherId", dispatcherId, direction: ParameterDirection.Input);
			parameters.Add("TimeoutMilliseconds", configuration.DispatcherTimeoutMilliseconds, direction: ParameterDirection.Input);

			return new CommandDefinition(sql, parameters, commandTimeout: sqlTimeOutSeconds);
		}

		internal static CommandDefinition IncrementAttemptsOrMoveToDeadLetter(
			Guid outboxRecordId,
			string eventData,
			string errorMessage,
			int sqlTimeOutSeconds,
			OutboxConfiguration configuration)
		{
			var sql = $"""
			           IF (
			             SELECT
			                 Attempts + 1
			             FROM
			                 {configuration.TableName}
			             WHERE
			                 OutboxId = @OutboxId

			             ) >= @MaxAttempts
			           BEGIN
			             INSERT INTO {configuration.DeadLetterTableName}
			                 (OutboxId, EventData, ErrorMessage, OriginalAttempts)
			             SELECT
			                 OutboxId, EventData, @ErrorMessage, Attempts + 1
			             FROM
			                 {configuration.TableName}
			             WHERE
			                 OutboxId = @OutboxId;

			             DELETE FROM
			                 {configuration.TableName}
			             WHERE
			                 OutboxId = @OutboxId;
			           END
			           ELSE
			           BEGIN
			             UPDATE
			                 {configuration.TableName}
			             SET
			                 Attempts = Attempts + 1
			             WHERE
			                 OutboxId = @OutboxId;
			           END
			           """;

			var parameters = new DynamicParameters();
			parameters.Add("OutboxId", outboxRecordId, direction: ParameterDirection.Input);
			parameters.Add("MaxAttempts", configuration.MaxAttempts, direction: ParameterDirection.Input);
			parameters.Add("EventData", eventData, direction: ParameterDirection.Input);
			parameters.Add("ErrorMessage", errorMessage, direction: ParameterDirection.Input);

			return new CommandDefinition(sql, parameters, commandTimeout: sqlTimeOutSeconds);
		}
	}
}
