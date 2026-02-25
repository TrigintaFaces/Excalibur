// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using CdcAntiCorruption.Commands;
using CdcAntiCorruption.SchemaAdapters;

using Excalibur.Data.SqlServer.Cdc;
using Excalibur.Dispatch.Abstractions;

using Microsoft.Extensions.Logging;

namespace CdcAntiCorruption.Handlers;

/// <summary>
/// CDC data change handler that translates legacy customer changes into domain commands.
/// </summary>
/// <remarks>
/// <para>
/// This handler implements the Anti-Corruption Layer (ACL) pattern for CDC events.
/// It sits between the external CDC source and the domain layer, providing:
/// </para>
/// <list type="bullet">
/// <item><description>Schema adaptation for legacy column names</description></item>
/// <item><description>Data validation at the boundary</description></item>
/// <item><description>Translation of CDC operations to domain commands</description></item>
/// <item><description>Isolation of the domain from external data formats</description></item>
/// </list>
/// <para>
/// The handler converts:
/// <list type="bullet">
/// <item><description>INSERT → <see cref="SyncCustomerCommand"/></description></item>
/// <item><description>UPDATE → <see cref="UpdateCustomerCommand"/></description></item>
/// <item><description>DELETE → <see cref="DeactivateCustomerCommand"/></description></item>
/// </list>
/// </para>
/// </remarks>
public sealed class CustomerSyncHandler : IDataChangeHandler
{
	private static readonly Action<ILogger, string, DataChangeType, Exception?> LogProcessingChange =
		LoggerMessage.Define<string, DataChangeType>(
			LogLevel.Debug,
			new EventId(1, nameof(LogProcessingChange)),
			"Processing CDC {TableName} {ChangeType} event");

	private static readonly Action<ILogger, string, string, Exception?> LogCommandDispatched =
		LoggerMessage.Define<string, string>(
			LogLevel.Information,
			new EventId(2, nameof(LogCommandDispatched)),
			"Dispatched {CommandType} for customer {ExternalId}");

	private static readonly Action<ILogger, string, Exception?> LogInvalidData =
		LoggerMessage.Define<string>(
			LogLevel.Warning,
			new EventId(3, nameof(LogInvalidData)),
			"Invalid CDC data for table {TableName}, skipping");

	private readonly IDispatcher _dispatcher;
	private readonly ILegacyCustomerSchemaAdapter _schemaAdapter;
	private readonly ILogger<CustomerSyncHandler> _logger;

	/// <summary>
	/// Initializes a new instance of the <see cref="CustomerSyncHandler"/> class.
	/// </summary>
	/// <param name="dispatcher">The dispatcher for sending domain commands.</param>
	/// <param name="schemaAdapter">The schema adapter for legacy data formats.</param>
	/// <param name="logger">The logger instance.</param>
	public CustomerSyncHandler(
		IDispatcher dispatcher,
		ILegacyCustomerSchemaAdapter schemaAdapter,
		ILogger<CustomerSyncHandler> logger)
	{
		_dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
		_schemaAdapter = schemaAdapter ?? throw new ArgumentNullException(nameof(schemaAdapter));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
	}

	/// <inheritdoc />
	/// <remarks>
	/// This handler processes changes from the LegacyCustomers table and any aliases
	/// that may exist in different schema versions.
	/// </remarks>
	public string[] TableNames => ["LegacyCustomers", "Customers", "dbo.LegacyCustomers"];

	/// <inheritdoc />
	public async Task HandleAsync(DataChangeEvent changeEvent, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(changeEvent);

		LogProcessingChange(_logger, changeEvent.TableName, changeEvent.ChangeType, null);

		// 1. Adapt schema (handle evolution)
		var adaptedData = _schemaAdapter.Adapt(changeEvent);

		// 2. Validate at boundary
		if (adaptedData is null || !IsValid(adaptedData))
		{
			LogInvalidData(_logger, changeEvent.TableName, null);
			return;
		}

		// 3. Create command (anti-corruption translation)
		IDispatchMessage? command = changeEvent.ChangeType switch
		{
			DataChangeType.Insert => new SyncCustomerCommand(adaptedData),
			DataChangeType.Update => new UpdateCustomerCommand(adaptedData),
			DataChangeType.Delete => new DeactivateCustomerCommand(adaptedData),
			_ => null,
		};

		// 4. Dispatch through pipeline (business rules applied there)
		if (command is not null)
		{
			_ = await _dispatcher.DispatchAsync(command, cancellationToken).ConfigureAwait(false);
			LogCommandDispatched(_logger, command.GetType().Name, adaptedData.ExternalId, null);
		}
	}

	/// <summary>
	/// Validates the adapted customer data at the domain boundary.
	/// </summary>
	/// <param name="data">The adapted customer data to validate.</param>
	/// <returns>
	/// <see langword="true"/> if the data is valid for domain processing;
	/// otherwise, <see langword="false"/>.
	/// </returns>
	private static bool IsValid(Models.AdaptedCustomerData data)
	{
		// Validate required fields
		if (string.IsNullOrWhiteSpace(data.ExternalId))
		{
			return false;
		}

		if (string.IsNullOrWhiteSpace(data.Name))
		{
			return false;
		}

		// Validate email format if present and not the default
		if (!string.IsNullOrEmpty(data.Email) &&
			data.Email != "unknown@legacy.system" &&
			!data.Email.Contains('@', StringComparison.Ordinal))
		{
			return false;
		}

		return true;
	}
}
