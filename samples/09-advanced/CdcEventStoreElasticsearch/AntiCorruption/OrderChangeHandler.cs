// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using CdcEventStoreElasticsearch.Domain;

using Excalibur.Data.SqlServer.Cdc;
using Excalibur.EventSourcing.Abstractions;

namespace CdcEventStoreElasticsearch.AntiCorruption;

/// <summary>
/// Service for looking up order IDs by external legacy IDs.
/// </summary>
public interface IOrderLookupService
{
	/// <summary>
	/// Registers a mapping between external order ID and order ID.
	/// </summary>
	void RegisterMapping(string externalOrderId, Guid orderId);

	/// <summary>
	/// Gets the order ID for an external order ID.
	/// </summary>
	Guid? GetOrderId(string externalOrderId);

	/// <summary>
	/// Gets the external order ID for an order ID.
	/// </summary>
	string? GetExternalOrderId(Guid orderId);
}

/// <summary>
/// Handles CDC data change events for Orders by translating them to domain commands.
/// This is the core of the Anti-Corruption Layer pattern for orders.
/// </summary>
/// <remarks>
/// <para>
/// The CDC change handler:
/// </para>
/// <list type="bullet">
/// <item>Receives raw CDC events from SQL Server #1 (legacy database)</item>
/// <item>Uses <see cref="LegacyOrderAdapter"/> to normalize schema differences</item>
/// <item>Translates to domain operations on <see cref="OrderAggregate"/></item>
/// <item>Persists events to SQL Server #2 (Event Store)</item>
/// </list>
/// </remarks>
public sealed class OrderChangeHandler : IDataChangeHandler
{
	private readonly IEventSourcedRepository<OrderAggregate, Guid> _orderRepository;
	private readonly LegacyOrderAdapter _adapter;
	private readonly IOrderLookupService _orderLookupService;
	private readonly ICustomerLookupService _customerLookupService;
	private readonly ILogger<OrderChangeHandler> _logger;

	/// <summary>
	/// Initializes a new instance of the <see cref="OrderChangeHandler"/> class.
	/// </summary>
	public OrderChangeHandler(
		IEventSourcedRepository<OrderAggregate, Guid> orderRepository,
		LegacyOrderAdapter adapter,
		IOrderLookupService orderLookupService,
		ICustomerLookupService customerLookupService,
		ILogger<OrderChangeHandler> logger)
	{
		_orderRepository = orderRepository;
		_adapter = adapter;
		_orderLookupService = orderLookupService;
		_customerLookupService = customerLookupService;
		_logger = logger;
	}

	/// <inheritdoc/>
	public string[] TableNames => ["LegacyOrders"];

	/// <inheritdoc/>
	public async Task HandleAsync(DataChangeEvent changeEvent, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(changeEvent);

		// Adapt CDC event to normalized domain data
		var adaptedData = _adapter.Adapt(changeEvent);
		if (adaptedData is null)
		{
			_logger.LogDebug(
				"Skipping CDC event for table {TableName} - not an order table or missing required data",
				changeEvent.TableName);
			return;
		}

		_logger.LogInformation(
			"Processing CDC {ChangeType} for order {ExternalOrderId}",
			adaptedData.ChangeType,
			adaptedData.ExternalOrderId);

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

	private static OrderStatus ParseOrderStatus(string status) => status.ToUpperInvariant() switch
	{
		"PENDING" or "NEW" or "CREATED" => OrderStatus.Pending,
		"CONFIRMED" or "APPROVED" or "PROCESSING" => OrderStatus.Confirmed,
		"SHIPPED" or "IN_TRANSIT" => OrderStatus.Shipped,
		"DELIVERED" or "COMPLETE" or "COMPLETED" => OrderStatus.Delivered,
		"CANCELLED" or "CANCELED" or "VOIDED" => OrderStatus.Cancelled,
		_ => OrderStatus.Pending
	};

	private async Task HandleInsertAsync(AdaptedOrderData data, CancellationToken cancellationToken)
	{
		// Lookup the customer ID from external ID
		var customerId = _customerLookupService.GetCustomerId(data.CustomerExternalId ?? string.Empty);
		if (customerId is null)
		{
			_logger.LogWarning(
				"Customer not found for external ID {CustomerExternalId} - creating order with placeholder customer",
				data.CustomerExternalId);
			customerId = Guid.Empty; // Placeholder for unknown customer
		}

		// Create new order aggregate
		var orderId = Guid.NewGuid();

		var order = OrderAggregate.Create(
			orderId,
			data.ExternalOrderId,
			customerId.Value,
			data.CustomerExternalId ?? "unknown",
			data.OrderDate ?? DateTime.UtcNow);

		// Apply initial status if provided
		if (!string.IsNullOrEmpty(data.Status))
		{
			var status = ParseOrderStatus(data.Status);
			if (status != OrderStatus.Pending)
			{
				order.UpdateStatus(status);
			}
		}

		// Handle shipped date if present
		if (data.ShippedDate.HasValue)
		{
			order.Ship(data.ShippedDate.Value);
		}

		// Handle delivered date if present
		if (data.DeliveredDate.HasValue)
		{
			order.Deliver(data.DeliveredDate.Value);
		}

		await _orderRepository.SaveAsync(order, cancellationToken).ConfigureAwait(false);

		// Register the mapping for future lookups
		_orderLookupService.RegisterMapping(data.ExternalOrderId, orderId);

		_logger.LogInformation(
			"Created order {OrderId} from legacy order {ExternalOrderId}",
			orderId,
			data.ExternalOrderId);
	}

	private async Task HandleUpdateAsync(AdaptedOrderData data, CancellationToken cancellationToken)
	{
		// Lookup existing order by external ID
		var orderId = _orderLookupService.GetOrderId(data.ExternalOrderId);
		if (orderId is null)
		{
			_logger.LogWarning(
				"Order not found for external ID {ExternalOrderId} - treating as insert",
				data.ExternalOrderId);
			await HandleInsertAsync(data, cancellationToken).ConfigureAwait(false);
			return;
		}

		// Load and update the aggregate
		var order = await _orderRepository.GetByIdAsync(orderId.Value, cancellationToken).ConfigureAwait(false);
		if (order is null)
		{
			_logger.LogWarning(
				"Order aggregate {OrderId} not found - treating as insert",
				orderId);
			await HandleInsertAsync(data, cancellationToken).ConfigureAwait(false);
			return;
		}

		// Update status if changed
		if (!string.IsNullOrEmpty(data.Status) && data.Status != data.PreviousStatus)
		{
			var newStatus = ParseOrderStatus(data.Status);
			order.UpdateStatus(newStatus);
		}

		// Handle shipped date if newly set
		if (data.ShippedDate.HasValue && order.ShippedDate is null)
		{
			order.Ship(data.ShippedDate.Value);
		}

		// Handle delivered date if newly set
		if (data.DeliveredDate.HasValue && order.Status != OrderStatus.Delivered)
		{
			order.Deliver(data.DeliveredDate.Value);
		}

		await _orderRepository.SaveAsync(order, cancellationToken).ConfigureAwait(false);

		_logger.LogInformation(
			"Updated order {OrderId} from legacy order {ExternalOrderId}",
			orderId,
			data.ExternalOrderId);
	}

	private async Task HandleDeleteAsync(AdaptedOrderData data, CancellationToken cancellationToken)
	{
		// Lookup existing order by external ID
		var orderId = _orderLookupService.GetOrderId(data.ExternalOrderId);
		if (orderId is null)
		{
			_logger.LogWarning(
				"Order not found for external ID {ExternalOrderId} - ignoring delete",
				data.ExternalOrderId);
			return;
		}

		// Load and cancel the aggregate
		var order = await _orderRepository.GetByIdAsync(orderId.Value, cancellationToken).ConfigureAwait(false);
		if (order is null)
		{
			_logger.LogWarning(
				"Order aggregate {OrderId} not found - ignoring delete",
				orderId);
			return;
		}

		if (order.Status == OrderStatus.Cancelled)
		{
			_logger.LogDebug("Order {OrderId} already cancelled", orderId);
			return;
		}

		order.Cancel("Deleted from legacy system via CDC");
		await _orderRepository.SaveAsync(order, cancellationToken).ConfigureAwait(false);

		_logger.LogInformation(
			"Cancelled order {OrderId} from legacy delete of {ExternalOrderId}",
			orderId,
			data.ExternalOrderId);
	}
}

/// <summary>
/// In-memory implementation of <see cref="IOrderLookupService"/>.
/// In production, this would be backed by a database table.
/// </summary>
public sealed class InMemoryOrderLookupService : IOrderLookupService
{
	private readonly Dictionary<string, Guid> _externalToInternal = new(StringComparer.OrdinalIgnoreCase);
	private readonly Dictionary<Guid, string> _internalToExternal = [];

	/// <inheritdoc/>
	public void RegisterMapping(string externalOrderId, Guid orderId)
	{
		_externalToInternal[externalOrderId] = orderId;
		_internalToExternal[orderId] = externalOrderId;
	}

	/// <inheritdoc/>
	public Guid? GetOrderId(string externalOrderId)
	{
		return _externalToInternal.TryGetValue(externalOrderId, out var id) ? id : null;
	}

	/// <inheritdoc/>
	public string? GetExternalOrderId(Guid orderId)
	{
		return _internalToExternal.TryGetValue(orderId, out var externalId) ? externalId : null;
	}
}
