// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;

namespace GettingStarted.Messages;

/// <summary>
/// Event raised when an order has been shipped.
/// </summary>
/// <remarks>
/// Events represent something that has already happened. They typically:
/// - Have a past-tense verb in their name (Created, Updated, Shipped)
/// - Can be handled by multiple handlers (notifications, auditing, analytics)
/// - Do not return a value
/// </remarks>
/// <param name="OrderId">The order that was shipped.</param>
/// <param name="ShippedAt">When the order was shipped.</param>
public record OrderShippedEvent(Guid OrderId, DateTimeOffset ShippedAt) : IDispatchEvent;
