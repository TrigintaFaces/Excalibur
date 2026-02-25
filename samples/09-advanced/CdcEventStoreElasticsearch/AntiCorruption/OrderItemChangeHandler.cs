// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using CdcEventStoreElasticsearch.Domain;

using Excalibur.Data.SqlServer.Cdc;
using Excalibur.EventSourcing.Abstractions;

namespace CdcEventStoreElasticsearch.AntiCorruption;

/// <summary>
/// Service for looking up order item IDs by external legacy IDs.
/// </summary>
public interface IOrderItemLookupService
{
	/// <summary>
	/// Registers a mapping between external item ID and item ID.
	/// </summary>
	void RegisterMapping(string externalItemId, Guid itemId);

	/// <summary>
	/// Gets the item ID for an external item ID.
	/// </summary>
	Guid? GetItemId(string externalItemId);
}

/// <summary>
/// Handles CDC data change events for Order Items by translating them to domain commands.
/// </summary>
/// <remarks>
/// <para>
/// Order items are NOT separate aggregates - they are part of the <see cref="OrderAggregate"/>.
/// This handler loads the parent order aggregate and modifies it, maintaining transactional
/// consistency within the aggregate boundary.
/// </para>
/// <para>
/// The CDC change handler:
/// </para>
/// <list type="bullet">
/// <item>Receives raw CDC events from SQL Server #1 (legacy database)</item>
/// <item>Uses <see cref="LegacyOrderItemAdapter"/> to normalize schema differences</item>
/// <item>Loads the parent <see cref="OrderAggregate"/> and applies line item changes</item>
/// <item>Persists events to SQL Server #2 (Event Store)</item>
/// </list>
/// </remarks>
public sealed class OrderItemChangeHandler : IDataChangeHandler
{
	private readonly IEventSourcedRepository<OrderAggregate, Guid> _orderRepository;
	private readonly LegacyOrderItemAdapter _adapter;
	private readonly IOrderLookupService _orderLookupService;
	private readonly IOrderItemLookupService _itemLookupService;
	private readonly ILogger<OrderItemChangeHandler> _logger;

	/// <summary>
	/// Initializes a new instance of the <see cref="OrderItemChangeHandler"/> class.
	/// </summary>
	public OrderItemChangeHandler(
		IEventSourcedRepository<OrderAggregate, Guid> orderRepository,
		LegacyOrderItemAdapter adapter,
		IOrderLookupService orderLookupService,
		IOrderItemLookupService itemLookupService,
		ILogger<OrderItemChangeHandler> logger)
	{
		_orderRepository = orderRepository;
		_adapter = adapter;
		_orderLookupService = orderLookupService;
		_itemLookupService = itemLookupService;
		_logger = logger;
	}

	/// <inheritdoc/>
	public string[] TableNames => ["LegacyOrderItems"];

	/// <inheritdoc/>
	public async Task HandleAsync(DataChangeEvent changeEvent, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(changeEvent);

		// Adapt CDC event to normalized domain data
		var adaptedData = _adapter.Adapt(changeEvent);
		if (adaptedData is null)
		{
			_logger.LogDebug(
				"Skipping CDC event for table {TableName} - not an order item table or missing required data",
				changeEvent.TableName);
			return;
		}

		_logger.LogInformation(
			"Processing CDC {ChangeType} for order item {ExternalItemId} on order {ExternalOrderId}",
			adaptedData.ChangeType,
			adaptedData.ExternalItemId,
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

	private async Task HandleInsertAsync(AdaptedOrderItemData data, CancellationToken cancellationToken)
	{
		// Lookup the parent order by external ID
		var orderId = _orderLookupService.GetOrderId(data.ExternalOrderId);
		if (orderId is null)
		{
			_logger.LogWarning(
				"Parent order not found for external ID {ExternalOrderId} - cannot add item {ExternalItemId}",
				data.ExternalOrderId,
				data.ExternalItemId);
			return;
		}

		// Load the parent order aggregate
		var order = await _orderRepository.GetByIdAsync(orderId.Value, cancellationToken).ConfigureAwait(false);
		if (order is null)
		{
			_logger.LogWarning(
				"Order aggregate {OrderId} not found - cannot add item {ExternalItemId}",
				orderId,
				data.ExternalItemId);
			return;
		}

		// Create new item ID
		var itemId = Guid.NewGuid();

		// Add the line item to the order
		order.AddLineItem(
			itemId,
			data.ExternalItemId,
			data.ProductName ?? "Unknown Product",
			data.Quantity > 0 ? data.Quantity : 1,
			data.UnitPrice >= 0 ? data.UnitPrice : 0);

		await _orderRepository.SaveAsync(order, cancellationToken).ConfigureAwait(false);

		// Register the mapping for future lookups
		_itemLookupService.RegisterMapping(data.ExternalItemId, itemId);

		_logger.LogInformation(
			"Added line item {ItemId} to order {OrderId} from legacy item {ExternalItemId}",
			itemId,
			orderId,
			data.ExternalItemId);
	}

	private async Task HandleUpdateAsync(AdaptedOrderItemData data, CancellationToken cancellationToken)
	{
		// Lookup the parent order by external ID
		var orderId = _orderLookupService.GetOrderId(data.ExternalOrderId);
		if (orderId is null)
		{
			_logger.LogWarning(
				"Parent order not found for external ID {ExternalOrderId} - cannot update item {ExternalItemId}",
				data.ExternalOrderId,
				data.ExternalItemId);
			return;
		}

		// Lookup the item ID by external ID
		var itemId = _itemLookupService.GetItemId(data.ExternalItemId);
		if (itemId is null)
		{
			_logger.LogWarning(
				"Item not found for external ID {ExternalItemId} - treating as insert",
				data.ExternalItemId);
			await HandleInsertAsync(data, cancellationToken).ConfigureAwait(false);
			return;
		}

		// Load the parent order aggregate
		var order = await _orderRepository.GetByIdAsync(orderId.Value, cancellationToken).ConfigureAwait(false);
		if (order is null)
		{
			_logger.LogWarning(
				"Order aggregate {OrderId} not found - cannot update item {ExternalItemId}",
				orderId,
				data.ExternalItemId);
			return;
		}

		// Update the line item quantity if changed
		if (data.PreviousQuantity.HasValue && data.Quantity != data.PreviousQuantity.Value)
		{
			order.UpdateLineItem(itemId.Value, data.Quantity);
			await _orderRepository.SaveAsync(order, cancellationToken).ConfigureAwait(false);

			_logger.LogInformation(
				"Updated line item {ItemId} quantity from {OldQty} to {NewQty} on order {OrderId}",
				itemId,
				data.PreviousQuantity,
				data.Quantity,
				orderId);
		}
	}

	private async Task HandleDeleteAsync(AdaptedOrderItemData data, CancellationToken cancellationToken)
	{
		// Lookup the parent order by external ID
		var orderId = _orderLookupService.GetOrderId(data.ExternalOrderId);
		if (orderId is null)
		{
			_logger.LogWarning(
				"Parent order not found for external ID {ExternalOrderId} - ignoring delete for item {ExternalItemId}",
				data.ExternalOrderId,
				data.ExternalItemId);
			return;
		}

		// Lookup the item ID by external ID
		var itemId = _itemLookupService.GetItemId(data.ExternalItemId);
		if (itemId is null)
		{
			_logger.LogWarning(
				"Item not found for external ID {ExternalItemId} - ignoring delete",
				data.ExternalItemId);
			return;
		}

		// Load the parent order aggregate
		var order = await _orderRepository.GetByIdAsync(orderId.Value, cancellationToken).ConfigureAwait(false);
		if (order is null)
		{
			_logger.LogWarning(
				"Order aggregate {OrderId} not found - ignoring delete for item {ExternalItemId}",
				orderId,
				data.ExternalItemId);
			return;
		}

		// Remove the line item
		order.RemoveLineItem(itemId.Value);
		await _orderRepository.SaveAsync(order, cancellationToken).ConfigureAwait(false);

		_logger.LogInformation(
			"Removed line item {ItemId} from order {OrderId} (legacy item {ExternalItemId})",
			itemId,
			orderId,
			data.ExternalItemId);
	}
}

/// <summary>
/// In-memory implementation of <see cref="IOrderItemLookupService"/>.
/// In production, this would be backed by a database table.
/// </summary>
public sealed class InMemoryOrderItemLookupService : IOrderItemLookupService
{
	private readonly Dictionary<string, Guid> _mappings = new(StringComparer.OrdinalIgnoreCase);

	/// <inheritdoc/>
	public void RegisterMapping(string externalItemId, Guid itemId)
	{
		_mappings[externalItemId] = itemId;
	}

	/// <inheritdoc/>
	public Guid? GetItemId(string externalItemId)
	{
		return _mappings.TryGetValue(externalItemId, out var id) ? id : null;
	}
}
