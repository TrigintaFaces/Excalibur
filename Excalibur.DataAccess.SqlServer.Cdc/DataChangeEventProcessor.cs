using Excalibur.DataAccess.SqlServer.Cdc.Exceptions;

using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Excalibur.DataAccess.SqlServer.Cdc;

/// <summary>
///     Processes CDC (Change Data Capture) events and delegates handling to registered data change handlers.
/// </summary>
public class DataChangeEventProcessor : CdcProcessor, IDataChangeEventProcessor
{
	private readonly IDatabaseConfig _dbConfig;

	private readonly IDataChangeHandlerFactory _dataChangeHandlerFactory;

	private readonly ILogger<DataChangeEventProcessor> _logger;

	/// <summary>
	///     Initializes a new instance of the <see cref="DataChangeEventProcessor" /> class.
	/// </summary>
	/// <param name="appLifetime">
	///     An instance of <see cref="IHostApplicationLifetime" /> that allows the application to perform actions during the application's
	///     lifecycle events, such as startup, shutdown, or when the application is stopping. This parameter is used to gracefully manage
	///     tasks that need to respond to application lifecycle events.
	/// </param>
	/// <param name="dbConfig"> The database configuration for the CDC processor. </param>
	/// <param name="cdcConnection"> The SQL connection for interacting with CDC data. </param>
	/// <param name="stateStoreConnection"> The SQL connection for persisting CDC state. </param>
	/// <param name="dataChangeHandlerFactory"> The factory for creating data change handlers. </param>
	/// <param name="logger"> The logger for capturing diagnostics and operational logs. </param>
	public DataChangeEventProcessor(
		IHostApplicationLifetime appLifetime,
		IDatabaseConfig dbConfig,
		SqlConnection cdcConnection,
		SqlConnection stateStoreConnection,
		IDataChangeHandlerFactory dataChangeHandlerFactory,
		IDataAccessPolicyFactory policyFactory,
		ILogger<DataChangeEventProcessor> logger)
		: base(appLifetime, dbConfig, cdcConnection, stateStoreConnection, policyFactory, logger)
	{
		ArgumentNullException.ThrowIfNull(dbConfig);
		ArgumentNullException.ThrowIfNull(dataChangeHandlerFactory);
		ArgumentNullException.ThrowIfNull(logger);

		_dbConfig = dbConfig;
		_dataChangeHandlerFactory = dataChangeHandlerFactory;
		_logger = logger;
	}

	/// <summary>
	///     Processes CDC changes by retrieving and handling data change events.
	/// </summary>
	/// <param name="cancellationToken"> A token to observe while waiting for the task to complete. </param>
	/// <returns> The total number of events processed. </returns>
	public Task<int> ProcessCdcChangesAsync(CancellationToken cancellationToken) =>
		ProcessCdcChangesAsync(
			(DataChangeEvent[] changeEvents, CancellationToken token) => HandleCdcDataChangeEvents(changeEvents, token).AsTask(),
			cancellationToken);

	/// <summary>
	///     Handles a collection of CDC data change events by invoking the appropriate handlers.
	/// </summary>
	/// <param name="changeEvents"> A collection of <see cref="DataChangeEvent" /> instances to process. </param>
	/// <param name="cancellationToken"> A token to observe while waiting for the task to complete. </param>
	/// <returns> The number of events successfully processed. </returns>
	/// <exception cref="CdcMissingTableHandlerException">
	///     Thrown when no handler is found for a table, and <see cref="IDatabaseConfig.StopOnMissingTableHandler" /> is true.
	/// </exception>
	private async ValueTask<int> HandleCdcDataChangeEvents(IEnumerable<DataChangeEvent> changeEvents, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(changeEvents);

		var events = changeEvents as ICollection<DataChangeEvent> ?? changeEvents.ToArray();

		if (events.Count == 0)
		{
			return 0;
		}

		return await ProcessEventsAsync(events, cancellationToken).ConfigureAwait(false);
	}

	private async Task<int> ProcessEventsAsync(ICollection<DataChangeEvent> changeEvents, CancellationToken cancellationToken)
	{
		var totalEvents = 0;

		_logger.LogInformation("Processing {EventCount} CDC events...", changeEvents.Count);

		var orderedEventProcessor = new OrderedEventProcessor();

		await orderedEventProcessor.ProcessEventsAsync(
			changeEvents,
			async (DataChangeEvent changeEvent) =>
			{
				if (changeEvent is null)
				{
					return;
				}

				try
				{
					var handler = _dataChangeHandlerFactory.GetHandler(changeEvent.TableName);

					await handler.Handle(changeEvent, cancellationToken).ConfigureAwait(false);

					_logger.LogInformation("Successfully processed change event for table '{TableName}'.", changeEvent.TableName);
					Interlocked.Increment(ref totalEvents);
				}
				catch (CdcMissingTableHandlerException ex)
				{
					_logger.LogWarning(ex, "No handler found for table '{TableName}'. Event skipped.", changeEvent.TableName);

					if (_dbConfig.StopOnMissingTableHandler)
					{
						throw;
					}
				}
				catch (Exception ex)
				{
					_logger.LogError(ex, "Unexpected error processing change event for table '{TableName}'.", changeEvent.TableName);
					throw;
				}
			}).ConfigureAwait(false);
		await orderedEventProcessor.DisposeAsync().ConfigureAwait(false);

		return totalEvents;
	}
}
