using Excalibur.DataAccess.SqlServer.Cdc.Exceptions;

using Microsoft.Extensions.DependencyInjection;
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
	/// <param name="logger"> The logger for capturing diagnostics and operational logs. </param>
	public DataChangeEventProcessor(
		IDatabaseConfig dbConfig,
		ICdcRepository cdcRepository,
		ICdcStateStore stateStore,
		IServiceProvider serviceProvider,
		ILogger<DataChangeEventProcessor> logger) : base(dbConfig, cdcRepository, stateStore, logger)
	{
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
		ProcessCdcChangesAsync(HandleCdcDataChangeEvents, cancellationToken);

	/// <summary>
	///     Handles a collection of CDC data change events by invoking the appropriate handlers.
	/// </summary>
	/// <param name="changeEvents"> A collection of <see cref="DataChangeEvent" /> instances to process. </param>
	/// <param name="cancellationToken"> A token to observe while waiting for the task to complete. </param>
	/// <returns> The number of events successfully processed. </returns>
	/// <exception cref="CdcMissingTableHandlerException">
	///     Thrown when no handler is found for a table, and <see cref="IDatabaseConfig.StopOnMissingTableHandler" /> is true.
	/// </exception>
	private async Task<int> HandleCdcDataChangeEvents(IEnumerable<DataChangeEvent> changeEvents, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(changeEvents);

		var totalEvents = 0;

		foreach (var changeEvent in changeEvents)
		{
			try
			{
				using var scope = _serviceProvider.CreateScope();
				var scopedHandlerRegistry = scope.ServiceProvider.GetRequiredService<IDataChangeHandlerRegistry>();
				var handler = scopedHandlerRegistry.GetHandler(changeEvent.TableName);
				await handler.Handle(changeEvent, cancellationToken).ConfigureAwait(false);

				_logger.LogInformation("Successfully processed change event for table '{TableName}'.", changeEvent.TableName);
				totalEvents++;
			}
			catch (CdcMissingTableHandlerException ex)
			{
				_logger.LogError(ex, "Error handling data change event for table '{TableName}. No handler found.'", changeEvent.TableName);

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
		}

		return totalEvents;
	}
}
