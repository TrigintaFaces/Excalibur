// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: Apache-2.0

using Excalibur.Dispatch.Abstractions;

namespace RabbitMQSample.Messages;

/// <summary>
/// Event representing a placed order.
/// </summary>
/// <remarks>
/// This event demonstrates the basic structure of an integration event
/// that can be published to RabbitMQ via the Dispatch framework.
/// Uses <see cref="IIntegrationEvent"/> for cross-service routing to transports.
/// </remarks>
/// <param name="OrderId">The unique identifier for the order.</param>
/// <param name="CustomerId">The customer who placed the order.</param>
/// <param name="TotalAmount">The total order amount.</param>
public sealed record OrderPlacedEvent(
	string OrderId,
	string CustomerId,
	decimal TotalAmount) : IIntegrationEvent;
