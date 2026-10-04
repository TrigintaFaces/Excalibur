// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using DispatchMinimal.Messages;

using Excalibur.Dispatch.Delivery;

namespace DispatchMinimal.Handlers;

/// <summary>
/// Handles GetOrderQuery - retrieves order details.
/// </summary>
public sealed class GetOrderHandler(OrderStore store) : IDocumentHandler<GetOrderQuery>
{
	public Task HandleAsync(GetOrderQuery document, CancellationToken cancellationToken)
	{
		Console.WriteLine($"[GetOrderHandler] Looking up order {document.OrderId}...");

		var orderData = store.ReadModel[document.OrderId];
		store.Documents[document.OrderId] = orderData;

		Console.WriteLine($"  Found order: {orderData.ProductId} x{orderData.Quantity}");
		Console.WriteLine($"  Order details: {orderData}");

		return Task.CompletedTask;
	}
}
