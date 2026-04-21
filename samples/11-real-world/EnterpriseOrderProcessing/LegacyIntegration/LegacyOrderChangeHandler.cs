// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: Apache-2.0

using EnterpriseOrderProcessing.Commands;

using Excalibur.Cdc.SqlServer;
using Excalibur.Data.IdentityMap;
using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Messaging;

using Microsoft.Extensions.Logging;

namespace EnterpriseOrderProcessing.LegacyIntegration;

/// <summary>
/// Handles CDC data change events from the legacy Orders table.
/// Implements the Anti-Corruption Layer pattern by translating legacy row changes
/// into domain commands dispatched through the Excalibur pipeline.
/// </summary>
/// <remarks>
/// Uses <see cref="IIdentityMapStore"/> for:
/// <list type="bullet">
/// <item>Idempotent order creation (prevents duplicate aggregates from replayed CDC events)</item>
/// <item>Cross-aggregate reference resolution (resolving customer_id to Customer aggregate ID)</item>
/// </list>
/// </remarks>
public sealed class LegacyOrderChangeHandler : IDataChangeHandler
{
	private readonly IDispatcher _dispatcher;
	private readonly IIdentityMapStore _identityMap;
	private readonly ILogger<LegacyOrderChangeHandler> _logger;

	public LegacyOrderChangeHandler(
		IDispatcher dispatcher,
		IIdentityMapStore identityMap,
		ILogger<LegacyOrderChangeHandler> logger)
	{
		_dispatcher = dispatcher;
		_identityMap = identityMap;
		_logger = logger;
	}

	/// <inheritdoc/>
	public string[] TableNames => ["LegacyOrders"];

	/// <inheritdoc/>
	public async Task HandleAsync(DataChangeEvent changeEvent, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(changeEvent);

		switch (changeEvent.ChangeType)
		{
			case DataChangeType.Insert:
				await HandleInsertAsync(changeEvent, cancellationToken).ConfigureAwait(false);
				break;

			case DataChangeType.Update:
				_logger.LogInformation(
					"Legacy order update detected for table {TableName} -- update translation not yet implemented",
					changeEvent.TableName);
				break;

			case DataChangeType.Delete:
				_logger.LogInformation(
					"Legacy order delete detected for table {TableName} -- delete translation not yet implemented",
					changeEvent.TableName);
				break;
		}
	}

	private async Task HandleInsertAsync(DataChangeEvent changeEvent, CancellationToken cancellationToken)
	{
		// Anti-corruption: translate legacy column names to domain concepts
		var legacyOrderId = ExtractString(changeEvent, "order_id") ?? Guid.NewGuid().ToString();
		var legacyCustomerId = ExtractString(changeEvent, "customer_id");
		var customerName = ExtractString(changeEvent, "customer_name") ?? "Unknown";
		var productId = ExtractString(changeEvent, "product_id") ?? "UNKNOWN";
		var quantity = ExtractInt(changeEvent, "quantity") ?? 1;
		var unitPrice = ExtractDecimal(changeEvent, "unit_price") ?? 0m;

		// 1. Idempotent order binding -- prevents duplicate aggregates from replayed CDC events
		var bindResult = await _identityMap.TryBindAsync(
			"LegacyERP", legacyOrderId, "Order",
			Guid.NewGuid().ToString(), cancellationToken).ConfigureAwait(false);

		if (!bindResult.WasCreated)
		{
			_logger.LogDebug(
				"Order already imported for legacy order {LegacyOrderId} -- skipping",
				legacyOrderId);
			return;
		}

		// 2. Resolve customer aggregate ID from legacy customer ID
		var customerId = Guid.Empty;
		if (!string.IsNullOrEmpty(legacyCustomerId))
		{
			customerId = await _identityMap.ResolveAsync<Guid>(
				"LegacyERP", legacyCustomerId, "Customer", cancellationToken)
				.ConfigureAwait(false) ?? Guid.Empty;
		}

		var command = new CreateOrderCommand(
			customerId,
			customerName,
			[new OrderLineItem(productId, quantity, unitPrice)]);

		_logger.LogInformation(
			"Translating legacy order {LegacyOrderId} to CreateOrderCommand for customer {CustomerName}",
			legacyOrderId, customerName);

		// 3. Dispatch through the full pipeline (validation + resilience + handler)
		var context = DispatchContextInitializer.CreateDefaultContext(_dispatcher.ServiceProvider!);
		await _dispatcher.DispatchAsync<CreateOrderCommand, Guid>(command, context, cancellationToken).ConfigureAwait(false);
	}

	private static string? ExtractString(DataChangeEvent changeEvent, string columnName)
	{
		var change = changeEvent.Changes.FirstOrDefault(c =>
			string.Equals(c.ColumnName, columnName, StringComparison.OrdinalIgnoreCase));
		return change?.NewValue?.ToString();
	}

	private static Guid? ExtractGuid(DataChangeEvent changeEvent, string columnName)
	{
		var value = ExtractString(changeEvent, columnName);
		return Guid.TryParse(value, out var guid) ? guid : null;
	}

	private static int? ExtractInt(DataChangeEvent changeEvent, string columnName)
	{
		var value = ExtractString(changeEvent, columnName);
		return int.TryParse(value, out var result) ? result : null;
	}

	private static decimal? ExtractDecimal(DataChangeEvent changeEvent, string columnName)
	{
		var value = ExtractString(changeEvent, columnName);
		return decimal.TryParse(value, out var result) ? result : null;
	}
}
