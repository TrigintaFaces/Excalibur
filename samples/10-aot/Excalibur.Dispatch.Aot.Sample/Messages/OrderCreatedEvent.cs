// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;

namespace Excalibur.Dispatch.Aot.Sample.Messages;

/// <summary>
/// Event raised when an order is created.
/// Demonstrates AOT-compatible event dispatch.
/// </summary>
/// <remarks>
/// AOT Considerations:
/// - Events can have multiple handlers
/// - All handlers are discovered at compile time via source generators
/// - No runtime assembly scanning required
/// </remarks>
public sealed record OrderCreatedEvent : IDispatchEvent
{
	/// <summary>
	/// Gets or initializes the order ID.
	/// </summary>
	public required Guid OrderId { get; init; }

	/// <summary>
	/// Gets or initializes the customer ID.
	/// </summary>
	public required string CustomerId { get; init; }

	/// <summary>
	/// Gets or initializes the total amount.
	/// </summary>
	public required decimal TotalAmount { get; init; }

	/// <summary>
	/// Gets or initializes when the event occurred.
	/// </summary>
	public required DateTimeOffset OccurredAt { get; init; }
}
