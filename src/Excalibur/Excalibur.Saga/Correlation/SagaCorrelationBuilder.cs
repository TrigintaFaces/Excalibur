// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Linq.Expressions;

namespace Excalibur.Saga.Correlation;

/// <summary>
/// Default implementation of <see cref="ISagaCorrelationBuilder{TSaga}"/>.
/// Collects correlation rules and compiles them into a configuration.
/// </summary>
/// <typeparam name="TSaga">The type of saga being correlated.</typeparam>
public sealed class SagaCorrelationBuilder<TSaga> : ISagaCorrelationBuilder<TSaga>
	where TSaga : class
{
	private readonly Dictionary<Type, Func<object, string>> _correlators = [];

	/// <inheritdoc />
	public ISagaCorrelationBuilder<TSaga> CorrelateBy<TMessage>(
		Expression<Func<TMessage, string>> correlationExpression)
		where TMessage : class
	{
		ArgumentNullException.ThrowIfNull(correlationExpression);

		var compiled = correlationExpression.Compile();
		_correlators[typeof(TMessage)] = message => compiled((TMessage)message);

		return this;
	}

	/// <inheritdoc />
	public ISagaCorrelationBuilder<TSaga> CorrelateBy<TMessage>(
		params Expression<Func<TMessage, object>>[] propertyExpressions)
		where TMessage : class
	{
		ArgumentNullException.ThrowIfNull(propertyExpressions);

		if (propertyExpressions.Length == 0)
		{
			throw new ArgumentException("At least one property expression is required.", nameof(propertyExpressions));
		}

		var compiledExpressions = propertyExpressions
			.Select(static expr => expr.Compile())
			.ToArray();

		_correlators[typeof(TMessage)] = message =>
		{
			var typedMessage = (TMessage)message;
			var values = compiledExpressions
				.Select(fn => fn(typedMessage)?.ToString() ?? string.Empty);
			return string.Join("|", values);
		};

		return this;
	}

	/// <inheritdoc />
	public SagaCorrelationConfiguration<TSaga> Build()
	{
		return new SagaCorrelationConfiguration<TSaga>(
			new Dictionary<Type, Func<object, string>>(_correlators));
	}
}
