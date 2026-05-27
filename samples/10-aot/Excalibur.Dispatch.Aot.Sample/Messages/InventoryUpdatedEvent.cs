// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;

namespace Excalibur.Dispatch.Aot.Sample.Messages;

/// <summary>
/// Event raised when inventory is updated, demonstrating transport publish/subscribe.
/// </summary>
/// <remarks>
/// S3 - Transport scenario: This event is published through the InMemory transport
/// to verify that message send/receive works end-to-end in a PublishAot=true binary.
/// </remarks>
public sealed record InventoryUpdatedEvent : IDispatchEvent
{
	/// <summary>
	/// Gets or initializes the SKU of the updated product.
	/// </summary>
	public required string Sku { get; init; }

	/// <summary>
	/// Gets or initializes the new quantity.
	/// </summary>
	public required int NewQuantity { get; init; }

	/// <summary>
	/// Gets or initializes the warehouse location.
	/// </summary>
	public required string Warehouse { get; init; }

	/// <summary>
	/// Gets or initializes when the update occurred.
	/// </summary>
	public required DateTimeOffset UpdatedAt { get; init; }
}
