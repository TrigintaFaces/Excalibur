// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: Apache-2.0

using CdcJobQuartz.Domain;

using Excalibur.Data.SqlServer.Cdc;
using Excalibur.EventSourcing.Abstractions;

using Microsoft.Extensions.Logging;

namespace CdcJobQuartz.AntiCorruption;

/// <summary>
/// Handles CDC data change events by translating them to domain commands.
/// This is the core of the Anti-Corruption Layer pattern.
/// </summary>
/// <remarks>
/// <para>
/// The CDC change handler:
/// </para>
/// <list type="bullet">
/// <item>Receives raw CDC events from SQL Server #1 (legacy database)</item>
/// <item>Uses <see cref="LegacyCustomerAdapter"/> to normalize schema differences</item>
/// <item>Translates to domain operations on <see cref="CustomerAggregate"/></item>
/// <item>Persists events to SQL Server #2 (Event Store)</item>
/// </list>
/// </remarks>
public sealed class CdcChangeHandler : IDataChangeHandler
{
	private readonly IEventSourcedRepository<CustomerAggregate, Guid> _customerRepository;
	private readonly LegacyCustomerAdapter _adapter;
	private readonly ICustomerLookupService _lookupService;
	private readonly ILogger<CdcChangeHandler> _logger;

	/// <inheritdoc/>
	public string[] TableNames => ["LegacyCustomers"];

	/// <summary>
	/// Initializes a new instance of the <see cref="CdcChangeHandler"/> class.
	/// </summary>
	public CdcChangeHandler(
		IEventSourcedRepository<CustomerAggregate, Guid> customerRepository,
		LegacyCustomerAdapter adapter,
		ICustomerLookupService lookupService,
		ILogger<CdcChangeHandler> logger)
	{
		_customerRepository = customerRepository;
		_adapter = adapter;
		_lookupService = lookupService;
		_logger = logger;
	}

	/// <inheritdoc/>
	public async Task HandleAsync(DataChangeEvent changeEvent, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(changeEvent);

		// Adapt CDC event to normalized domain data
		var adaptedData = _adapter.Adapt(changeEvent);
		if (adaptedData is null)
		{
			_logger.LogDebug(
				"Skipping CDC event for table {TableName} - not a customer table or missing required data",
				changeEvent.TableName);
			return;
		}

		_logger.LogInformation(
			"Processing CDC {ChangeType} for customer {ExternalId}",
			adaptedData.ChangeType,
			adaptedData.ExternalId);

		// Translate to domain operations
		switch (adaptedData.ChangeType)
		{
			case DataChangeType.Insert:
				await HandleInsertAsync(adaptedData, cancellationToken).ConfigureAwait(false);
				break;

			case DataChangeType.Update:
				await HandleUpdateAsync(adaptedData, cancellationToken).ConfigureAwait(false);
				break;

			case DataChangeType.Delete:
				await HandleDeleteAsync(adaptedData, cancellationToken).ConfigureAwait(false);
				break;
		}
	}

	private async Task HandleInsertAsync(AdaptedCustomerData data, CancellationToken cancellationToken)
	{
		// Create new customer aggregate
		var customerId = Guid.NewGuid();

		var customer = CustomerAggregate.Create(
			customerId,
			data.ExternalId,
			data.Name ?? "Unknown",
			data.Email ?? "unknown@example.com",
			data.Phone);

		await _customerRepository.SaveAsync(customer, cancellationToken).ConfigureAwait(false);

		// Register the mapping for future lookups
		_lookupService.RegisterMapping(data.ExternalId, customerId);

		_logger.LogInformation(
			"Created customer {CustomerId} from legacy customer {ExternalId}",
			customerId,
			data.ExternalId);
	}

	private async Task HandleUpdateAsync(AdaptedCustomerData data, CancellationToken cancellationToken)
	{
		// Lookup existing customer by external ID
		var customerId = _lookupService.GetCustomerId(data.ExternalId);
		if (customerId is null)
		{
			_logger.LogWarning(
				"Customer not found for external ID {ExternalId} - treating as insert",
				data.ExternalId);
			await HandleInsertAsync(data, cancellationToken).ConfigureAwait(false);
			return;
		}

		// Load and update the aggregate
		var customer = await _customerRepository.GetByIdAsync(customerId.Value, cancellationToken).ConfigureAwait(false);
		if (customer is null)
		{
			_logger.LogWarning(
				"Customer aggregate {CustomerId} not found - treating as insert",
				customerId);
			await HandleInsertAsync(data, cancellationToken).ConfigureAwait(false);
			return;
		}

		customer.UpdateInfo(
			data.Name ?? customer.Name,
			data.Email ?? customer.Email,
			data.Phone);

		await _customerRepository.SaveAsync(customer, cancellationToken).ConfigureAwait(false);

		_logger.LogInformation(
			"Updated customer {CustomerId} from legacy customer {ExternalId}",
			customerId,
			data.ExternalId);
	}

	private async Task HandleDeleteAsync(AdaptedCustomerData data, CancellationToken cancellationToken)
	{
		// Lookup existing customer by external ID
		var customerId = _lookupService.GetCustomerId(data.ExternalId);
		if (customerId is null)
		{
			_logger.LogWarning(
				"Customer not found for external ID {ExternalId} - ignoring delete",
				data.ExternalId);
			return;
		}

		// Load and deactivate the aggregate (soft delete)
		var customer = await _customerRepository.GetByIdAsync(customerId.Value, cancellationToken).ConfigureAwait(false);
		if (customer is null)
		{
			_logger.LogWarning(
				"Customer aggregate {CustomerId} not found - ignoring delete",
				customerId);
			return;
		}

		if (!customer.IsActive)
		{
			_logger.LogDebug("Customer {CustomerId} already deactivated", customerId);
			return;
		}

		customer.Deactivate("Deleted from legacy system via CDC");
		await _customerRepository.SaveAsync(customer, cancellationToken).ConfigureAwait(false);

		_logger.LogInformation(
			"Deactivated customer {CustomerId} from legacy delete of {ExternalId}",
			customerId,
			data.ExternalId);
	}
}

/// <summary>
/// Service for looking up customer IDs by external legacy IDs.
/// </summary>
public interface ICustomerLookupService
{
	/// <summary>
	/// Registers a mapping between external ID and customer ID.
	/// </summary>
	void RegisterMapping(string externalId, Guid customerId);

	/// <summary>
	/// Gets the customer ID for an external ID.
	/// </summary>
	Guid? GetCustomerId(string externalId);
}

/// <summary>
/// In-memory implementation of <see cref="ICustomerLookupService"/>.
/// In production, this would be backed by a database table.
/// </summary>
public sealed class InMemoryCustomerLookupService : ICustomerLookupService
{
	private readonly Dictionary<string, Guid> _mappings = new(StringComparer.OrdinalIgnoreCase);

	/// <inheritdoc/>
	public void RegisterMapping(string externalId, Guid customerId)
	{
		_mappings[externalId] = customerId;
	}

	/// <inheritdoc/>
	public Guid? GetCustomerId(string externalId)
	{
		return _mappings.TryGetValue(externalId, out var id) ? id : null;
	}
}
