// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;

namespace ExcaliburCqrs.Messages;

/// <summary>
/// Command to create a new order.
/// </summary>
/// <param name="ProductId">The initial product identifier.</param>
/// <param name="Quantity">The initial quantity.</param>
public sealed record CreateOrderCommand(string ProductId, int Quantity) : IDispatchAction<Guid>;

/// <summary>
/// Command to add an item to an existing order.
/// </summary>
/// <param name="OrderId">The order identifier.</param>
/// <param name="ProductId">The product identifier to add.</param>
/// <param name="Quantity">The quantity to add.</param>
public sealed record AddOrderItemCommand(Guid OrderId, string ProductId, int Quantity) : IDispatchAction;

/// <summary>
/// Command to confirm an order for processing.
/// </summary>
/// <param name="OrderId">The order identifier.</param>
public sealed record ConfirmOrderCommand(Guid OrderId) : IDispatchAction;

/// <summary>
/// Command to ship an order.
/// </summary>
/// <param name="OrderId">The order identifier.</param>
/// <param name="TrackingNumber">The shipping tracking number.</param>
public sealed record ShipOrderCommand(Guid OrderId, string TrackingNumber) : IDispatchAction;

/// <summary>
/// Query to get order details.
/// </summary>
/// <param name="OrderId">The order identifier.</param>
public sealed record GetOrderQuery(Guid OrderId) : IDispatchDocument;
