using Excalibur.DataAccess.SqlServer.Cdc.Exceptions;

using Microsoft.Data.SqlClient;
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
	/// <param name="appLifetime">
	///     An instance of <see cref="IHostApplicationLifetime" /> that allows the application to perform actions during the application's
	///     lifecycle events, such as startup, shutdown, or when the application is stopping. This parameter is used to gracefully manage
	///     tasks that need to respond to application lifecycle events.
	/// </param>
	/// <param name="dbConfig"> The database configuration for the CDC processor. </param>
	/// <param name="cdcConnection"> The SQL connection for interacting with CDC data. </param>
	/// <param name="stateStoreConnection"> The SQL connection for persisting CDC state. </param>
	/// <param name="serviceProvider"> The service provider for dependency resolution. </param>
	/// <param name="logger"> The logger for capturing diagnostics and operational logs. </param>
	public DataChangeEventProcessor(
		IHostApplicationLifetime appLifetime,
		IDatabaseConfig dbConfig,
		SqlConnection cdcConnection,
		SqlConnection stateStoreConnection,
		IServiceProvider serviceProvider,
		IDataAccessPolicyFactory policyFactory,
		ILogger<DataChangeEventProcessor> logger)
		: base(appLifetime, dbConfig, cdcConnection, stateStoreConnection, policyFactory, logger)
	{
		ArgumentNullException.ThrowIfNull(dbConfig);
		ArgumentNullException.ThrowIfNull(serviceProvider, nameof(serviceProvider));
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
			(DataChangeEvent changeEvent, CancellationToken token) => HandleCdcDataChangeEvents(changeEvent, token).AsTask(),
			cancellationToken);

	/// <summary>
	///     Handles a collection of CDC data change event by invoking the appropriate handler.
	/// </summary>
	/// <param name="changeEvent"> A <see cref="DataChangeEvent" /> instance to process. </param>
	/// <param name="cancellationToken"> A token to observe while waiting for the task to complete. </param>
	/// <returns> The number of events successfully processed. </returns>
	/// <exception cref="CdcMissingTableHandlerException">
	///     Thrown when no handler is found for a table, and <see cref="IDatabaseConfig.StopOnMissingTableHandler" /> is true.
	/// </exception>
	private async ValueTask HandleCdcDataChangeEvents(DataChangeEvent changeEvent, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(changeEvent);

		try
		{
			using var scope = _serviceProvider.CreateScope();
			var handler = GetHandler(scope.ServiceProvider, changeEvent.TableName);

			await handler.Handle(changeEvent, cancellationToken).ConfigureAwait(false);

			_logger.LogInformation("Successfully processed change event for table '{TableName}'.", changeEvent.TableName);
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
	}

	private IDataChangeHandler GetHandler(IServiceProvider serviceProvider, string tableName)
	{
		if (string.IsNullOrWhiteSpace(tableName))
		{
			throw new ArgumentNullException(nameof(tableName));
		}

		try
		{
			var handlers = serviceProvider.GetServices<IDataChangeHandler>();

			var handler = handlers.SingleOrDefault(
				(IDataChangeHandler h) => h.TableNames.Contains(tableName, StringComparer.OrdinalIgnoreCase));

			if (handler == null)
			{
				throw new CdcMissingTableHandlerException(tableName);
			}

			return handler;
		}
		catch (InvalidOperationException e)
		{
			throw new CdcMultipleTableHandlerException(tableName);
		}
	}
}
