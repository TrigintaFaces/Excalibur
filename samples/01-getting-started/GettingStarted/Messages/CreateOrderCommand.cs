// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;

namespace GettingStarted.Messages;

/// <summary>
/// Command to create a new order.
/// </summary>
/// <remarks>
/// Commands represent intent to change state. They typically:
/// - Have a verb in their name (Create, Update, Delete)
/// - Return a result (the created entity ID, success/failure, etc.)
/// - Are handled by exactly one handler
/// </remarks>
/// <param name="ProductId">The product identifier to order.</param>
/// <param name="Quantity">The quantity to order.</param>
public record CreateOrderCommand(string ProductId, int Quantity) : IDispatchAction<Guid>;
