// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Abstractions.Transport;

/// <summary>
/// Fluent builder for configuring message mapping between transports.
/// </summary>
/// <remarks>
/// <para>
/// Follows the <c>Microsoft.AspNetCore.Builder.IEndpointConventionBuilder</c> pattern:
/// a single <see cref="Add"/> method with all fluent configuration via extension methods.
/// </para>
/// <example>
/// <code>
/// builder.WithMessageMapping(mapping => mapping
///     .MapMessage&lt;OrderCreatedEvent&gt;()
///         .ToRabbitMq(ctx => ctx.RoutingKey = "orders.created")
///         .ToKafka(ctx => ctx.Topic = "orders")
///     .MapMessage&lt;PaymentProcessedEvent&gt;()
///         .ToRabbitMq(ctx => ctx.Exchange = "payments")
///         .ToAzureServiceBus(ctx => ctx.TopicOrQueueName = "payments"));
/// </code>
/// </example>
/// </remarks>
public interface IMessageMappingBuilder
{
	/// <summary>
	/// Adds a convention to the message mapping builder.
	/// </summary>
	/// <param name="convention">The convention to add.</param>
	void Add(Action<IMessageMappingConventions> convention);
}
