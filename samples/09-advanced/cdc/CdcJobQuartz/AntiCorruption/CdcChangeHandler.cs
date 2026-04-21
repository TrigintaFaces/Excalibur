// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: Apache-2.0

using CdcJobQuartz.Domain;

using Excalibur.Cdc.SqlServer;
using Excalibur.Data.IdentityMap;
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
/// <item>Uses <see cref="IIdentityMapStore"/> for idempotent external-to-internal ID resolution</item>
/// <item>Translates to domain operations on <see cref="CustomerAggregate"/></item>
/// <item>Persists events to SQL Server #2 (Event Store)</item>
/// </list>
/// </remarks>
public sealed class CdcChangeHandler : IDataChangeHandler
{
	private readonly IEventSourcedRepository<CustomerAggregate, Guid> _customerRepository;
	private readonly LegacyCustomerAdapter _adapter;
	private readonly IIdentityMapStore _identityMap;
	private readonly ILogger<CdcChangeHandler> _logger;

	/// <inheritdoc/>
	public string[] TableNames => ["LegacyCustomers"];

	/// <summary>
	/// Initializes a new instance of the <see cref="CdcChangeHandler"/> class.
	/// </summary>
	public CdcChangeHandler(
		IEventSourcedRepository<CustomerAggregate, Guid> customerRepository,
		LegacyCustomerAdapter adapter,
		IIdentityMapStore identityMap,
		ILogger<CdcChangeHandler> logger)
	{
		_customerRepository = customerRepository;
		_adapter = adapter;
		_identityMap = identityMap;
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
		// Idempotent bind: if the external ID was already imported, reuse the existing aggregate ID.
		var bindResult = await _identityMap.TryBindAsync(
			"LegacyDB", data.ExternalId, "Customer",
			Guid.NewGuid().ToString(), cancellationToken).ConfigureAwait(false);

		var customerId = Guid.Parse(bindResult.AggregateId);

		if (!bindResult.WasCreated)
		{
			_logger.LogDebug(
				"Customer {CustomerId} already exists for external ID {ExternalId} -- skipping insert",
				customerId, data.ExternalId);
			return;
		}

		var customer = CustomerAggregate.Create(
			customerId,
			data.ExternalId,
			data.Name ?? "Unknown",
			data.Email ?? "unknown@example.com",
			data.Phone);

		await _customerRepository.SaveAsync(customer, cancellationToken).ConfigureAwait(false);

		_logger.LogInformation(
			"Created customer {CustomerId} from legacy customer {ExternalId}",
			customerId,
			data.ExternalId);
	}

	private async Task HandleUpdateAsync(AdaptedCustomerData data, CancellationToken cancellationToken)
	{
		// Resolve existing customer by external ID using the identity map
		var customerId = await _identityMap.ResolveAsync<Guid>(
			"LegacyDB", data.ExternalId, "Customer", cancellationToken).ConfigureAwait(false);

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
		// Resolve existing customer by external ID using the identity map
		var customerId = await _identityMap.ResolveAsync<Guid>(
			"LegacyDB", data.ExternalId, "Customer", cancellationToken).ConfigureAwait(false);

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
