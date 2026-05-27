// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Delivery;
using Excalibur.Dispatch.Aot.Sample.Messages;

namespace Excalibur.Dispatch.Aot.Sample.Handlers;

/// <summary>
/// Handles inventory update events received via transport.
/// </summary>
/// <remarks>
/// S3 - Transport scenario: Demonstrates that event handlers discovered by source generators
/// can process messages that flow through the InMemory transport adapter in AOT mode.
/// </remarks>
public sealed class InventoryUpdatedHandler : IEventHandler<InventoryUpdatedEvent>
{
	/// <summary>
	/// Tracks the last received event for verification in the demo.
	/// </summary>
	internal static InventoryUpdatedEvent? LastReceived { get; private set; }

	/// <inheritdoc />
	public Task HandleAsync(InventoryUpdatedEvent evt, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(evt);

		LastReceived = evt;
		Console.WriteLine($"[InventoryUpdatedHandler] SKU={evt.Sku}, Qty={evt.NewQuantity}, Warehouse={evt.Warehouse}");

		return Task.CompletedTask;
	}
}
