// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using DispatchMinimal.Messages;

using Excalibur.Dispatch.Abstractions.Delivery;

namespace DispatchMinimal.Handlers;

/// <summary>
/// Handles GetOrderQuery - retrieves order details.
/// </summary>
public class GetOrderHandler : IDocumentHandler<GetOrderQuery>
{
	public Task HandleAsync(GetOrderQuery document, CancellationToken cancellationToken)
	{
		Console.WriteLine($"[GetOrderHandler] Looking up order {document.OrderId}...");

		// In a real app, this would query a database
		// For demo, return mock data
		var orderData = new OrderDto(
			Id: document.OrderId,
			ProductId: "MOCK-PRODUCT",
			Quantity: 1,
			Status: "Confirmed");

		Console.WriteLine($"  Found order: {orderData.ProductId} x{orderData.Quantity}");
		Console.WriteLine($"  Order details: {orderData}");

		return Task.CompletedTask;
	}
}
