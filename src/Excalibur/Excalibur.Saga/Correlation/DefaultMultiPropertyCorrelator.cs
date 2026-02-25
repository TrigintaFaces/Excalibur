// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Linq.Expressions;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.Saga.Correlation;

/// <summary>
/// Default implementation of <see cref="IMultiPropertyCorrelator{TMessage}"/> that
/// builds composite correlation keys from multiple message properties.
/// </summary>
/// <remarks>
/// <para>
/// This correlator evaluates the provided property expressions against a message
/// instance and combines the extracted values into a single composite key using
/// a pipe separator (e.g., <c>"OrderId-123|CustomerId-456"</c>).
/// </para>
/// <para>
/// Thread-safe. Expression compilation is performed on each call; for high-throughput
/// scenarios, consider caching compiled delegates externally.
/// </para>
/// </remarks>
/// <typeparam name="TMessage">The type of message to correlate.</typeparam>
public sealed partial class DefaultMultiPropertyCorrelator<TMessage> : IMultiPropertyCorrelator<TMessage>
	where TMessage : class
{
	private const char Separator = '|';

	private readonly IOptions<MultiPropertyCorrelationOptions> _options;
	private readonly ILogger<DefaultMultiPropertyCorrelator<TMessage>> _logger;

	/// <summary>
	/// Initializes a new instance of the <see cref="DefaultMultiPropertyCorrelator{TMessage}"/> class.
	/// </summary>
	/// <param name="options">The multi-property correlation configuration options.</param>
	/// <param name="logger">The logger instance.</param>
	public DefaultMultiPropertyCorrelator(
		IOptions<MultiPropertyCorrelationOptions> options,
		ILogger<DefaultMultiPropertyCorrelator<TMessage>> logger)
	{
		_options = options ?? throw new ArgumentNullException(nameof(options));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
	}

	/// <inheritdoc />
	public Task<string> CorrelateByAsync(
		Expression<Func<TMessage, object>>[] propertyExpressions,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(propertyExpressions);

		if (propertyExpressions.Length == 0)
		{
			throw new ArgumentException("At least one property expression is required.", nameof(propertyExpressions));
		}

		var options = _options.Value;

		// Compile expressions and extract values
		// Note: The CorrelateByAsync signature receives expressions but no message instance.
		// This method produces a composite key template from the expression metadata.
		// In practice, callers use the SagaCorrelationBuilder which compiles and evaluates
		// expressions against actual message instances. Here we extract member names
		// to form a structural key that consumers can use for correlation configuration.
		var propertyNames = new string[propertyExpressions.Length];

		for (var i = 0; i < propertyExpressions.Length; i++)
		{
			var expression = propertyExpressions[i];
			var memberName = ExtractMemberName(expression);

			if (options.RequireAllProperties && string.IsNullOrEmpty(memberName))
			{
				throw new InvalidOperationException(
					$"Property expression at index {i} does not reference a valid member.");
			}

			propertyNames[i] = memberName ?? $"expr_{i}";
		}

		var compositeKey = options.UseCompositeKey
			? string.Join(Separator, propertyNames)
			: propertyNames[0];

		Log.CompositeKeyGenerated(_logger, compositeKey, propertyExpressions.Length);

		return Task.FromResult(compositeKey);
	}

	/// <summary>
	/// Evaluates property expressions against a message instance to produce a composite correlation key.
	/// </summary>
	/// <param name="message">The message instance to extract property values from.</param>
	/// <param name="propertyExpressions">The property expressions to evaluate.</param>
	/// <returns>The composite correlation key formed from the extracted values.</returns>
	/// <exception cref="ArgumentNullException">
	/// Thrown when <paramref name="message"/> or <paramref name="propertyExpressions"/> is null.
	/// </exception>
	/// <exception cref="ArgumentException">
	/// Thrown when <paramref name="propertyExpressions"/> is empty.
	/// </exception>
	public string CorrelateMessage(
		TMessage message,
		Expression<Func<TMessage, object>>[] propertyExpressions)
	{
		ArgumentNullException.ThrowIfNull(message);
		ArgumentNullException.ThrowIfNull(propertyExpressions);

		if (propertyExpressions.Length == 0)
		{
			throw new ArgumentException("At least one property expression is required.", nameof(propertyExpressions));
		}

		var options = _options.Value;
		var values = new string[propertyExpressions.Length];

		for (var i = 0; i < propertyExpressions.Length; i++)
		{
			var compiled = propertyExpressions[i].Compile();
			var value = compiled(message);

			if (options.RequireAllProperties && value is null)
			{
				throw new InvalidOperationException(
					$"Property expression at index {i} returned null, but RequireAllProperties is enabled.");
			}

			values[i] = value?.ToString() ?? string.Empty;
		}

		var compositeKey = options.UseCompositeKey
			? string.Join(Separator, values)
			: values[0];

		Log.MessageCorrelated(_logger, compositeKey, propertyExpressions.Length);

		return compositeKey;
	}

	private static string? ExtractMemberName(Expression<Func<TMessage, object>> expression)
	{
		var body = expression.Body;

		// Unwrap Convert/ConvertChecked for value types boxed to object
		if (body is UnaryExpression { NodeType: ExpressionType.Convert or ExpressionType.ConvertChecked } unary)
		{
			body = unary.Operand;
		}

		return body is MemberExpression member
			? member.Member.Name
			: null;
	}

	private static partial class Log
	{
		[LoggerMessage(
			EventId = 3910,
			Level = LogLevel.Debug,
			Message = "Generated composite correlation key '{CompositeKey}' from {PropertyCount} property expression(s)")]
		public static partial void CompositeKeyGenerated(
			ILogger logger,
			string compositeKey,
			int propertyCount);

		[LoggerMessage(
			EventId = 3911,
			Level = LogLevel.Debug,
			Message = "Correlated message to composite key '{CompositeKey}' from {PropertyCount} property value(s)")]
		public static partial void MessageCorrelated(
			ILogger logger,
			string compositeKey,
			int propertyCount);
	}
}
