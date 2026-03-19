// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;

using Microsoft.Extensions.Logging;

namespace Excalibur.Cdc.SqlServer;

/// <summary>
/// Internal handler that bridges configured EventMappings to <see cref="ICdcEventMapper{TEvent}"/>
/// instances and dispatches the resulting domain events via <see cref="IDispatcher"/>.
/// </summary>
/// <remarks>
/// <para>
/// Auto-registered when <c>MapInsert&lt;TEvent, TMapper&gt;()</c> or similar builder methods
/// are used to configure event mapper delegates. If a manual <see cref="IDataChangeHandler"/>
/// is also registered for the same table, the manual handler takes priority.
/// </para>
/// </remarks>
internal sealed partial class AutoMappingDataChangeHandler : IDataChangeHandler
{
	private readonly IServiceProvider _serviceProvider;
	private readonly IDispatcher _dispatcher;
	private readonly CdcTableTrackingOptions _tableOptions;
	private readonly ILogger<AutoMappingDataChangeHandler> _logger;

	/// <summary>
	/// Initializes a new instance of the <see cref="AutoMappingDataChangeHandler"/> class.
	/// </summary>
	internal AutoMappingDataChangeHandler(
		IServiceProvider serviceProvider,
		IDispatcher dispatcher,
		CdcTableTrackingOptions tableOptions,
		ILogger<AutoMappingDataChangeHandler> logger)
	{
		_serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
		_dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
		_tableOptions = tableOptions ?? throw new ArgumentNullException(nameof(tableOptions));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
	}

	/// <inheritdoc/>
	public string[] TableNames => [_tableOptions.TableName];

	/// <inheritdoc/>
	public async Task HandleAsync(DataChangeEvent changeEvent, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(changeEvent);

		var cdcChangeType = changeEvent.ChangeType.ToCdcChangeType();

		if (!_tableOptions.EventMapperDelegates.TryGetValue(cdcChangeType, out var mapperDelegate))
		{
			LogNoMapperForChangeType(_logger, _tableOptions.TableName, cdcChangeType);
			return;
		}

		// Convert SqlServer DataChange list to core CdcDataChange list
		var cdcChanges = ConvertToCdcDataChanges(changeEvent.Changes);

		try
		{
			var domainEvent = mapperDelegate(_serviceProvider, cdcChanges, cdcChangeType);

			if (domainEvent is IDispatchMessage message)
			{
				await _dispatcher.DispatchAsync(message, cancellationToken).ConfigureAwait(false);
			}
			else
			{
				LogMappedEventNotDispatchable(_logger, _tableOptions.TableName, domainEvent.GetType().Name);
			}
		}
		catch (CdcMappingException ex)
		{
			LogMappingFailed(_logger, _tableOptions.TableName, cdcChangeType, ex);
			throw;
		}
	}

	private static IReadOnlyList<CdcDataChange> ConvertToCdcDataChanges(IList<DataChange> changes)
	{
		var result = new CdcDataChange[changes.Count];
		for (var i = 0; i < changes.Count; i++)
		{
			var dc = changes[i];
			result[i] = new CdcDataChange
			{
				ColumnName = dc.ColumnName,
				OldValue = dc.OldValue,
				NewValue = dc.NewValue,
				DataType = dc.DataType
			};
		}

		return result;
	}

	[LoggerMessage(2400, LogLevel.Debug, "No event mapper registered for table '{TableName}' change type '{ChangeType}' -- skipping auto-mapping")]
	private static partial void LogNoMapperForChangeType(ILogger logger, string tableName, CdcChangeType changeType);

	[LoggerMessage(2401, LogLevel.Warning, "Mapped event for table '{TableName}' (type '{EventTypeName}') does not implement IDispatchMessage -- event will not be dispatched")]
	private static partial void LogMappedEventNotDispatchable(ILogger logger, string tableName, string eventTypeName);

	[LoggerMessage(2402, LogLevel.Error, "CDC event mapping failed for table '{TableName}' change type '{ChangeType}'")]
	private static partial void LogMappingFailed(ILogger logger, string tableName, CdcChangeType changeType, Exception ex);
}
