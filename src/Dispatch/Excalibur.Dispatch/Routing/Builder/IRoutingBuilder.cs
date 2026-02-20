// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Routing.Builder;

/// <summary>
/// Fluent builder interface for configuring message routing in the Dispatch framework.
/// </summary>
/// <remarks>
/// <para>
/// This builder provides a unified entry point for configuring both transport selection
/// (which message bus to use) and endpoint routing (which services receive messages).
/// </para>
/// <para>
/// The two-tier routing architecture separates concerns:
/// <list type="bullet">
/// <item><see cref="Transport"/>: Determines the message bus (local, RabbitMQ, Kafka, etc.)</item>
/// <item><see cref="Endpoints"/>: Determines which services/endpoints receive the message</item>
/// </list>
/// </para>
/// </remarks>
/// <example>
/// <code>
/// services.AddDispatch(dispatch =>
/// {
///     dispatch.UseRouting(routing =>
///     {
///         routing.Transport
///             .Route&lt;OrderCreated&gt;().To("rabbitmq")
///             .Route&lt;PaymentProcessed&gt;().To("kafka")
///             .Default("local");
///
///         routing.Endpoints
///             .Route&lt;OrderCreated&gt;()
///                 .To("billing-service", "inventory-service")
///                 .When(msg => msg.Amount > 1000).AlsoTo("fraud-detection");
///
///         routing.Fallback.To("dead-letter-queue");
///     });
/// });
/// </code>
/// </example>
public interface IRoutingBuilder
{
	/// <summary>
	/// Gets the transport routing builder for configuring message bus selection rules.
	/// </summary>
	/// <value>
	/// A builder for defining transport routing rules that determine which
	/// message bus handles each message type.
	/// </value>
	ITransportRoutingBuilder Transport { get; }

	/// <summary>
	/// Gets the endpoint routing builder for configuring service routing rules.
	/// </summary>
	/// <value>
	/// A builder for defining endpoint routing rules that determine which
	/// services receive each message type.
	/// </value>
	IEndpointRoutingBuilder Endpoints { get; }

	/// <summary>
	/// Gets the fallback routing builder for configuring fallback behavior.
	/// </summary>
	/// <value>
	/// A builder for defining fallback routes when no rules match.
	/// </value>
	IFallbackRoutingBuilder Fallback { get; }

}
