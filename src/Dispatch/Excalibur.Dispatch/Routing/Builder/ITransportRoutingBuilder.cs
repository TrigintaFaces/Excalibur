// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;

namespace Excalibur.Dispatch.Routing.Builder;

/// <summary>
/// Fluent builder for configuring transport selection rules.
/// </summary>
/// <remarks>
/// <para>
/// Transport routing determines which message bus (e.g., "local", "rabbitmq", "kafka")
/// should handle a given message type. This is the first tier of the two-tier routing
/// architecture.
/// </para>
/// <para>
/// Rules are evaluated in order of registration with support for conditional routing
/// and a default fallback transport.
/// </para>
/// <para>
/// <strong>Important:</strong> Only <see cref="IIntegrationEvent"/> types can be routed
/// to remote transports. Commands and domain events are handled locally by design.
/// This constraint is enforced at compile time to prevent architectural violations.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// routing.Transport
///     .Route&lt;OrderCreatedEvent&gt;().To("rabbitmq")     // IIntegrationEvent - OK
///     .Route&lt;PaymentProcessedEvent&gt;().To("kafka")    // IIntegrationEvent - OK
///     .Default("local");
///
/// // The following would NOT compile:
/// // routing.Transport.Route&lt;PlaceOrderCommand&gt;().To("rabbitmq");
/// // Error: PlaceOrderCommand does not implement IIntegrationEvent
/// </code>
/// </example>
public interface ITransportRoutingBuilder
{
	/// <summary>
	/// Begins defining a transport routing rule for a specific integration event type.
	/// </summary>
	/// <typeparam name="TEvent">
	/// The integration event type to route. Must implement <see cref="IIntegrationEvent"/>
	/// to ensure only events designed for cross-service communication are sent to remote transports.
	/// </typeparam>
	/// <returns>A rule builder for specifying the target transport.</returns>
	/// <remarks>
	/// Commands and domain events cannot be routed to remote transports. This is enforced
	/// at compile time through the <see cref="IIntegrationEvent"/> constraint.
	/// </remarks>
	ITransportRuleBuilder<TEvent> Route<TEvent>() where TEvent : IIntegrationEvent;

	/// <summary>
	/// Sets the default transport for messages that don't match any routing rules.
	/// </summary>
	/// <param name="transport">The default transport name (e.g., "local", "rabbitmq").</param>
	/// <returns>The builder for chaining.</returns>
	/// <exception cref="ArgumentException">
	/// Thrown when <paramref name="transport"/> is null or whitespace.
	/// </exception>
	ITransportRoutingBuilder Default(string transport);

	/// <summary>
	/// Gets all configured transport rules.
	/// </summary>
	/// <returns>A read-only collection of transport routing rules.</returns>
	IReadOnlyList<TransportRoutingRule> GetRules();

	/// <summary>
	/// Gets the default transport name.
	/// </summary>
	/// <value>The default transport name, or <see langword="null"/> if not configured.</value>
	string? DefaultTransport { get; }
}

/// <summary>
/// Builder for specifying the target transport in a routing rule.
/// </summary>
/// <typeparam name="TEvent">The integration event type being routed.</typeparam>
public interface ITransportRuleBuilder<TEvent> where TEvent : IIntegrationEvent
{
	/// <summary>
	/// Specifies the target transport for this message type.
	/// </summary>
	/// <param name="transport">The transport name (e.g., "rabbitmq", "kafka", "local").</param>
	/// <returns>The parent builder for chaining additional rules.</returns>
	/// <exception cref="ArgumentException">
	/// Thrown when <paramref name="transport"/> is null or whitespace.
	/// </exception>
	ITransportRoutingBuilder To(string transport);

	/// <summary>
	/// Adds a condition that must be satisfied for this rule to match.
	/// </summary>
	/// <param name="predicate">The condition to evaluate against the event.</param>
	/// <returns>A conditional builder for specifying the target transport.</returns>
	/// <exception cref="ArgumentNullException">
	/// Thrown when <paramref name="predicate"/> is null.
	/// </exception>
	IConditionalTransportRuleBuilder<TEvent> When(Func<TEvent, bool> predicate);

	/// <summary>
	/// Adds a condition using the message context for evaluation.
	/// </summary>
	/// <param name="predicate">The condition to evaluate against the event and context.</param>
	/// <returns>A conditional builder for specifying the target transport.</returns>
	/// <exception cref="ArgumentNullException">
	/// Thrown when <paramref name="predicate"/> is null.
	/// </exception>
	IConditionalTransportRuleBuilder<TEvent> When(Func<TEvent, IMessageContext, bool> predicate);
}

/// <summary>
/// Builder for completing a conditional transport routing rule.
/// </summary>
/// <typeparam name="TEvent">The integration event type being routed.</typeparam>
public interface IConditionalTransportRuleBuilder<TEvent> where TEvent : IIntegrationEvent
{
	/// <summary>
	/// Specifies the target transport when the condition is satisfied.
	/// </summary>
	/// <param name="transport">The transport name.</param>
	/// <returns>The parent builder for chaining additional rules.</returns>
	/// <exception cref="ArgumentException">
	/// Thrown when <paramref name="transport"/> is null or whitespace.
	/// </exception>
	ITransportRoutingBuilder To(string transport);
}

/// <summary>
/// Represents a configured transport routing rule.
/// </summary>
/// <param name="MessageType">The message type this rule applies to.</param>
/// <param name="Transport">The target transport name.</param>
/// <param name="Predicate">Optional predicate for conditional routing.</param>
/// <param name="Priority">Rule priority (lower = higher precedence).</param>
public sealed record TransportRoutingRule(
	Type MessageType,
	string Transport,
	Func<IDispatchMessage, IMessageContext, bool>? Predicate = null,
	int Priority = 0);
