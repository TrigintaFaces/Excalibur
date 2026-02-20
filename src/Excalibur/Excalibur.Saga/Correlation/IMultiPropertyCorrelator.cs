// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Linq.Expressions;

namespace Excalibur.Saga.Correlation;

/// <summary>
/// Provides multi-property correlation for saga message routing.
/// </summary>
/// <remarks>
/// <para>
/// When a single property is insufficient to uniquely identify a saga instance,
/// this interface allows correlating on multiple message properties simultaneously.
/// For example, correlating by both OrderId and CustomerId.
/// </para>
/// <para>
/// Follows the MassTransit pattern of expression-based correlation declarations,
/// extended to support composite keys from multiple properties.
/// </para>
/// </remarks>
/// <typeparam name="TMessage">The type of message to correlate.</typeparam>
public interface IMultiPropertyCorrelator<TMessage>
	where TMessage : class
{
	/// <summary>
	/// Declares multiple properties to use for correlation.
	/// </summary>
	/// <param name="propertyExpressions">
	/// Expressions that extract correlation values from the message.
	/// All values are combined to produce a composite correlation key.
	/// </param>
	/// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
	/// <returns>The composite correlation key produced from the property values.</returns>
	/// <exception cref="ArgumentNullException">
	/// Thrown when <paramref name="propertyExpressions"/> is null.
	/// </exception>
	/// <exception cref="ArgumentException">
	/// Thrown when <paramref name="propertyExpressions"/> is empty.
	/// </exception>
	Task<string> CorrelateByAsync(
		Expression<Func<TMessage, object>>[] propertyExpressions,
		CancellationToken cancellationToken);
}
