using Excalibur.DataAccess.SqlServer.Cdc.Exceptions;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Excalibur.DataAccess.SqlServer.Cdc;

/// <summary>
///     Processes CDC (Change Data Capture) events and delegates handling to registered data change handlers.
/// </summary>
public class DataChangeEventProcessor : CdcProcessor, IDataChangeEventProcessor
{
	private readonly IDatabaseConfig _dbConfig;
	private readonly IServiceProvider _serviceProvider;
	private readonly ILogger<DataChangeEventProcessor> _logger;

	/// <summary>
	///     Initializes a new instance of the <see cref="DataChangeEventProcessor" /> class.
	/// </summary>
	/// <param name="dbConfig"> The database configuration for the CDC processor. </param>
	/// <param name="cdcRepository"> The repository for interacting with CDC data. </param>
	/// <param name="stateStore"> The state store for persisting CDC processing state. </param>
	/// <param name="serviceProvider"> The root service provider for creating new scopes. </param>
	/// <param name="appLifetime">
	///     An instance of <see cref="IHostApplicationLifetime" /> that allows the application to perform actions during the application's
	///     lifecycle events, such as startup, shutdown, or when the application is stopping. This parameter is used to gracefully manage
	///     tasks that need to respond to application lifecycle events.
	/// </param>
	/// <param name="logger"> The logger for capturing diagnostics and operational logs. </param>
	public DataChangeEventProcessor(
		IDatabaseConfig dbConfig,
		ICdcRepository cdcRepository,
		ICdcStateStore stateStore,
		IServiceProvider serviceProvider,
		IHostApplicationLifetime appLifetime,
		ILogger<DataChangeEventProcessor> logger) : base(dbConfig, cdcRepository, stateStore, appLifetime, logger)
	{
		ArgumentNullException.ThrowIfNull(dbConfig);
		ArgumentNullException.ThrowIfNull(cdcRepository);
		ArgumentNullException.ThrowIfNull(stateStore);
		ArgumentNullException.ThrowIfNull(serviceProvider);
		ArgumentNullException.ThrowIfNull(appLifetime);
		ArgumentNullException.ThrowIfNull(logger);

		_dbConfig = dbConfig;
		_serviceProvider = serviceProvider;
		_logger = logger;
	}

	/// <summary>
	///     Processes CDC changes by retrieving and handling data change events.
	/// </summary>
	/// <param name="cancellationToken"> A token to observe while waiting for the task to complete. </param>
	/// <returns> The total number of events processed. </returns>
	public Task<int> ProcessCdcChangesAsync(CancellationToken cancellationToken) =>
		ProcessCdcChangesAsync(
			(changeEvents, token) => HandleCdcDataChangeEvents(changeEvents, token).AsTask(),
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

		var eventList = changeEvents as IList<DataChangeEvent> ?? changeEvents.ToList();

		if (eventList.Count == 0)
		{
			// If no events, return a completed ValueTask
			return await ValueTask.FromResult(0).ConfigureAwait(false);
		}

		return await new ValueTask<int>(ProcessEventsAsync(eventList, cancellationToken)).ConfigureAwait(false);
	}

	private async Task<int> ProcessEventsAsync(IList<DataChangeEvent> changeEvents, CancellationToken cancellationToken)
	{
		var totalEvents = 0;

		using var orderedEventProcessor = new OrderedEventProcessor();

		await orderedEventProcessor.ProcessEventsAsync(changeEvents, async changeEvent =>
		{
			try
			{
				using var scope = _serviceProvider.CreateScope();
				var scopedHandlerRegistry = scope.ServiceProvider.GetRequiredService<IDataChangeHandlerRegistry>();
				var handler = scopedHandlerRegistry.GetHandler(changeEvent.TableName);
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

		return totalEvents;
	}
}
