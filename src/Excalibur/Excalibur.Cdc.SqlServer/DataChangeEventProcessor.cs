// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0



using System.Collections.Frozen;

using Excalibur.Data.SqlServer.Diagnostics;

using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.Cdc.SqlServer;

/// <summary>
/// Processes CDC (Change Data Capture) events and delegates handling to registered data change handlers.
/// </summary>
public partial class DataChangeEventProcessor : CdcProcessor, IDataChangeEventProcessor
{
	private readonly IDatabaseOptions _dbConfig;

	private readonly IServiceProvider _serviceProvider;

	private readonly ILogger<DataChangeEventProcessor> _logger;

	/// <summary>
	/// Initializes a new instance of the <see cref="DataChangeEventProcessor" /> class.
	/// </summary>
	/// <param name="appLifetime">
	/// An instance of <see cref="IHostApplicationLifetime" /> that allows the application to perform actions during the application's
	/// lifecycle events, such as startup, shutdown, or when the application is stopping. This parameter is used to gracefully manage
	/// tasks that need to respond to application lifecycle events.
	/// </param>
	/// <param name="dbConfig"> The database configuration for the CDC processor. </param>
	/// <param name="cdcConnection"> The SQL connection for interacting with CDC data. </param>
	/// <param name="stateStoreConnection"> The SQL connection for persisting CDC state. </param>
	/// <param name="stateStoreOptions"> The CDC state store options. </param>
	/// <param name="serviceProvider"> The service provider for dependency injection. </param>
	/// <param name="policyFactory"> The factory for creating data access policies. </param>
	/// <param name="logger"> The logger for capturing diagnostics and operational logs. </param>
	/// <param name="fatalErrorOptions">
	/// Options containing an optional delegate that is invoked if a fatal error occurs during event processing.
	/// This allows the host application to react to unrecoverable conditions (e.g., log, shut down, alert).
	/// </param>
	public DataChangeEventProcessor(
			IHostApplicationLifetime appLifetime,
			IDatabaseOptions dbConfig,
			SqlConnection cdcConnection,
			SqlConnection stateStoreConnection,
			IOptions<SqlServerCdcStateStoreOptions>? stateStoreOptions,
			IServiceProvider serviceProvider,
			IDataAccessPolicyFactory policyFactory,
			ILogger<DataChangeEventProcessor> logger,
			IOptions<CdcFatalErrorOptions>? fatalErrorOptions = null)
			: base(appLifetime, dbConfig, cdcConnection, stateStoreConnection, stateStoreOptions, policyFactory, logger, fatalErrorOptions)
	{
		ArgumentNullException.ThrowIfNull(dbConfig);
		ArgumentNullException.ThrowIfNull(serviceProvider);
		ArgumentNullException.ThrowIfNull(logger);

		_dbConfig = dbConfig;
		_serviceProvider = serviceProvider;
		_logger = logger;
	}

	/// <summary>
	/// Processes CDC changes by retrieving and handling data change events.
	/// </summary>
	/// <param name="cancellationToken"> A token to observe while waiting for the task to complete. </param>
	/// <returns> The total number of events processed. </returns>
	public Task<int> ProcessCdcChangesAsync(CancellationToken cancellationToken) =>
		ProcessCdcChangesAsync(
			(changeEvent, token) => HandleCdcDataChangeEventsAsync(changeEvent, token).AsTask(),
			cancellationToken);

	/// <summary>
	/// Lazy-initialized, frozen table-to-handler-type map. Caches handler TYPES (not instances)
	/// so that fresh handler instances are resolved from each scoped ServiceProvider,
	/// avoiding references to disposed services from previous scopes.
	/// </summary>
	private volatile FrozenDictionary<string, Type>? _handlerTypeMap;

	private IDataChangeHandler GetHandler(IServiceProvider serviceProvider, string tableName)
	{
		if (string.IsNullOrWhiteSpace(tableName))
		{
			throw new ArgumentNullException(nameof(tableName));
		}

		var map = _handlerTypeMap ??= BuildHandlerTypeMap(serviceProvider);

		if (!map.TryGetValue(tableName, out var handlerType))
		{
			throw new CdcMissingTableHandlerException(tableName);
		}

		// Resolve a fresh handler instance from the current scope to avoid
		// holding references to disposed scoped services.
		var handlers = serviceProvider.GetServices<IDataChangeHandler>();
		foreach (var handler in handlers)
		{
			if (handler.GetType() == handlerType)
			{
				return handler;
			}
		}

		throw new CdcMissingTableHandlerException(tableName);
	}

	private static FrozenDictionary<string, Type> BuildHandlerTypeMap(IServiceProvider serviceProvider)
	{
		var handlers = serviceProvider.GetServices<IDataChangeHandler>();
		var dict = new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase);

		foreach (var handler in handlers)
		{
			foreach (var tableName in handler.TableNames)
			{
				if (dict.ContainsKey(tableName))
				{
					throw new CdcMultipleTableHandlerException(tableName);
				}

				dict[tableName] = handler.GetType();
			}
		}

		return dict.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);
	}

	/// <summary>
	/// Handles a collection of CDC data change event by invoking the appropriate handler.
	/// </summary>
	/// <param name="changeEvent"> A <see cref="DataChangeEvent" /> instance to process. </param>
	/// <param name="cancellationToken"> A token to observe while waiting for the task to complete. </param>
	/// <returns> The number of events successfully processed. </returns>
	/// <exception cref="CdcMissingTableHandlerException">
	/// Thrown when no handler is found for a table, and <see cref="IDatabaseOptions.StopOnMissingTableHandler" /> is true.
	/// </exception>
	private async ValueTask HandleCdcDataChangeEventsAsync(DataChangeEvent changeEvent, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(changeEvent);

		try
		{
			using var scope = _serviceProvider.CreateScope();
			var handler = GetHandler(scope.ServiceProvider, changeEvent.TableName);

			await handler.HandleAsync(changeEvent, cancellationToken).ConfigureAwait(false);

			if (_logger.IsEnabled(LogLevel.Information))
			{
				LogChangeEventProcessed(changeEvent.TableName);
			}
		}
		catch (CdcMissingTableHandlerException ex)
		{
			if (_logger.IsEnabled(LogLevel.Warning))
			{
				LogMissingTableHandler(changeEvent.TableName, ex);
			}

			if (_dbConfig.StopOnMissingTableHandler)
			{
				throw;
			}
		}
		catch (Exception ex)
		{
			if (_logger.IsEnabled(LogLevel.Error))
			{
				LogChangeEventError(changeEvent.TableName, ex);
			}

			throw;
		}
	}

	// Source-generated logging methods
	[LoggerMessage(DataSqlServerEventId.DataChangeEventProcessed, LogLevel.Information,
		"Successfully processed change event for table '{TableName}'.")]
	private partial void LogChangeEventProcessed(string tableName);

	[LoggerMessage(DataSqlServerEventId.DataChangeMissingTableHandler, LogLevel.Warning,
		"No handler found for table '{TableName}'. Event skipped.")]
	private partial void LogMissingTableHandler(string tableName, Exception ex);

	[LoggerMessage(DataSqlServerEventId.DataChangeEventError, LogLevel.Error,
		"Unexpected error processing change event for table '{TableName}'.")]
	private partial void LogChangeEventError(string tableName, Exception ex);
}
