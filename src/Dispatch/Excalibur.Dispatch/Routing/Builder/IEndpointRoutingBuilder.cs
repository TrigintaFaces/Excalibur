// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;

namespace Excalibur.Dispatch.Routing.Builder;

/// <summary>
/// Fluent builder for configuring endpoint routing rules.
/// </summary>
/// <remarks>
/// <para>
/// Endpoint routing determines which services should receive a message.
/// This is the second tier of the two-tier routing architecture and supports
/// multicast delivery to multiple endpoints.
/// </para>
/// <para>
/// Rules can include conditional logic based on message content, enabling
/// content-based routing patterns like sending high-value orders to fraud detection.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// routing.Endpoints
///     .Route&lt;OrderCreated&gt;()
///         .To("billing-service", "inventory-service")
///         .When(msg => msg.Amount > 1000).AlsoTo("fraud-detection")
///         .When(msg => msg.IsInternational).AlsoTo("customs-service");
/// </code>
/// </example>
public interface IEndpointRoutingBuilder
{
	/// <summary>
	/// Begins defining an endpoint routing rule for a specific message type.
	/// </summary>
	/// <typeparam name="TMessage">The message type to route.</typeparam>
	/// <returns>A rule builder for specifying target endpoints.</returns>
	IEndpointRuleBuilder<TMessage> Route<TMessage>() where TMessage : IDispatchMessage;

	/// <summary>
	/// Gets all configured endpoint rules.
	/// </summary>
	/// <returns>A read-only collection of endpoint routing rules.</returns>
	IReadOnlyList<EndpointRoutingRule> GetRules();
}

/// <summary>
/// Builder for specifying target endpoints in a routing rule.
/// </summary>
/// <typeparam name="TMessage">The message type being routed.</typeparam>
public interface IEndpointRuleBuilder<TMessage> where TMessage : IDispatchMessage
{
	/// <summary>
	/// Specifies the target endpoints for this message type.
	/// </summary>
	/// <param name="endpoints">One or more endpoint names.</param>
	/// <returns>A builder for adding conditional routing rules.</returns>
	/// <exception cref="ArgumentException">
	/// Thrown when <paramref name="endpoints"/> is empty or contains null/empty values.
	/// </exception>
	IEndpointRuleChainBuilder<TMessage> To(params string[] endpoints);
}

/// <summary>
/// Builder for chaining additional conditional endpoint rules.
/// </summary>
/// <typeparam name="TMessage">The message type being routed.</typeparam>
public interface IEndpointRuleChainBuilder<TMessage> where TMessage : IDispatchMessage
{
	/// <summary>
	/// Adds a conditional routing rule based on message content.
	/// </summary>
	/// <param name="predicate">The condition to evaluate.</param>
	/// <returns>A builder for specifying additional endpoints when the condition is met.</returns>
	/// <exception cref="ArgumentNullException">
	/// Thrown when <paramref name="predicate"/> is null.
	/// </exception>
	IConditionalEndpointBuilder<TMessage> When(Func<TMessage, bool> predicate);

	/// <summary>
	/// Adds a conditional routing rule using the message context.
	/// </summary>
	/// <param name="predicate">The condition to evaluate against message and context.</param>
	/// <returns>A builder for specifying additional endpoints when the condition is met.</returns>
	/// <exception cref="ArgumentNullException">
	/// Thrown when <paramref name="predicate"/> is null.
	/// </exception>
	IConditionalEndpointBuilder<TMessage> When(Func<TMessage, IMessageContext, bool> predicate);

	/// <summary>
	/// Begins defining a new endpoint routing rule for another message type.
	/// </summary>
	/// <typeparam name="TOther">The other message type to route.</typeparam>
	/// <returns>A rule builder for the new message type.</returns>
	IEndpointRuleBuilder<TOther> Route<TOther>() where TOther : IDispatchMessage;

	/// <summary>
	/// Gets all configured endpoint rules.
	/// </summary>
	/// <returns>A read-only collection of endpoint routing rules.</returns>
	IReadOnlyList<EndpointRoutingRule> GetRules();
}

/// <summary>
/// Builder for completing a conditional endpoint routing rule.
/// </summary>
/// <typeparam name="TMessage">The message type being routed.</typeparam>
public interface IConditionalEndpointBuilder<TMessage> where TMessage : IDispatchMessage
{
	/// <summary>
	/// Specifies additional endpoints to route to when the condition is satisfied.
	/// </summary>
	/// <param name="endpoints">One or more additional endpoint names.</param>
	/// <returns>The rule chain builder for adding more conditions.</returns>
	/// <exception cref="ArgumentException">
	/// Thrown when <paramref name="endpoints"/> is empty or contains null/empty values.
	/// </exception>
	IEndpointRuleChainBuilder<TMessage> AlsoTo(params string[] endpoints);
}

/// <summary>
/// Represents a configured endpoint routing rule.
/// </summary>
/// <param name="MessageType">The message type this rule applies to.</param>
/// <param name="Endpoints">The target endpoint names.</param>
/// <param name="Predicate">Optional predicate for conditional routing.</param>
/// <param name="Priority">Rule priority (lower = higher precedence).</param>
/// <param name="StopOnMatch">Whether to stop evaluating further rules on match.</param>
public sealed record EndpointRoutingRule(
	Type MessageType,
	IReadOnlyList<string> Endpoints,
	Func<IDispatchMessage, IMessageContext, bool>? Predicate = null,
	int Priority = 0,
	bool StopOnMatch = false);
