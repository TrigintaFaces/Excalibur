// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Linq.Expressions;

namespace Excalibur.Saga.Correlation;

/// <summary>
/// Fluent builder for declaring how messages are correlated to saga instances.
/// </summary>
/// <typeparam name="TSaga">The type of saga being correlated.</typeparam>
/// <remarks>
/// <para>
/// Use this builder to define how incoming message properties map to saga instance
/// identifiers. This enables the saga infrastructure to route messages to the
/// correct saga instance without manual lookup code.
/// </para>
/// <para>
/// This follows the pattern established by MassTransit and NServiceBus for
/// declarative saga correlation, providing a strongly-typed, expression-based API.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// builder.CorrelateBy&lt;OrderPlaced&gt;(m => m.OrderId)
///        .CorrelateBy&lt;PaymentReceived&gt;(m => m.OrderId);
/// </code>
/// </example>
public interface ISagaCorrelationBuilder<TSaga>
	where TSaga : class
{
	/// <summary>
	/// Declares a correlation between a message type and a saga instance identifier.
	/// </summary>
	/// <typeparam name="TMessage">The type of message to correlate.</typeparam>
	/// <param name="correlationExpression">
	/// An expression that extracts the correlation value from the message.
	/// The returned value is matched against saga instance identifiers.
	/// </param>
	/// <returns>The builder for chaining additional correlations.</returns>
	/// <exception cref="ArgumentNullException">
	/// Thrown when <paramref name="correlationExpression"/> is null.
	/// </exception>
	ISagaCorrelationBuilder<TSaga> CorrelateBy<TMessage>(
		Expression<Func<TMessage, string>> correlationExpression)
		where TMessage : class;

	/// <summary>
	/// Declares a multi-property correlation between a message type and a saga instance identifier.
	/// </summary>
	/// <typeparam name="TMessage">The type of message to correlate.</typeparam>
	/// <param name="propertyExpressions">
	/// Expressions that extract correlation values from the message.
	/// The values are combined into a composite correlation key.
	/// </param>
	/// <returns>The builder for chaining additional correlations.</returns>
	/// <exception cref="ArgumentNullException">
	/// Thrown when <paramref name="propertyExpressions"/> is null.
	/// </exception>
	/// <exception cref="ArgumentException">
	/// Thrown when <paramref name="propertyExpressions"/> is empty.
	/// </exception>
	ISagaCorrelationBuilder<TSaga> CorrelateBy<TMessage>(
		params Expression<Func<TMessage, object>>[] propertyExpressions)
		where TMessage : class;

	/// <summary>
	/// Builds the correlation configuration into an immutable set of correlation rules.
	/// </summary>
	/// <returns>The compiled correlation configuration.</returns>
	SagaCorrelationConfiguration<TSaga> Build();
}
