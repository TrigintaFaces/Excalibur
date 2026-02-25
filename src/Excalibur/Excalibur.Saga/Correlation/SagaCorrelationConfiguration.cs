// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Saga.Correlation;

/// <summary>
/// Immutable configuration of saga correlation rules compiled from <see cref="ISagaCorrelationBuilder{TSaga}"/>.
/// </summary>
/// <typeparam name="TSaga">The type of saga this configuration applies to.</typeparam>
/// <remarks>
/// <para>
/// Contains the compiled set of message-type-to-correlation-value mappings.
/// Use <see cref="TryGetCorrelationId"/> to extract the saga instance identifier
/// from an incoming message.
/// </para>
/// </remarks>
public sealed class SagaCorrelationConfiguration<TSaga>
	where TSaga : class
{
	private readonly IReadOnlyDictionary<Type, Func<object, string>> _correlators;

	/// <summary>
	/// Initializes a new instance of the <see cref="SagaCorrelationConfiguration{TSaga}"/> class.
	/// </summary>
	/// <param name="correlators">The compiled correlation functions keyed by message type.</param>
	internal SagaCorrelationConfiguration(IReadOnlyDictionary<Type, Func<object, string>> correlators)
	{
		_correlators = correlators ?? throw new ArgumentNullException(nameof(correlators));
	}

	/// <summary>
	/// Attempts to extract a correlation identifier from a message.
	/// </summary>
	/// <param name="message">The message to extract the correlation ID from.</param>
	/// <param name="correlationId">
	/// When this method returns, contains the correlation ID if the message type
	/// has a registered correlator; otherwise, <see langword="null"/>.
	/// </param>
	/// <returns>
	/// <see langword="true"/> if a correlator was found and the correlation ID was extracted;
	/// otherwise, <see langword="false"/>.
	/// </returns>
	public bool TryGetCorrelationId(object message, out string? correlationId)
	{
		ArgumentNullException.ThrowIfNull(message);

		if (_correlators.TryGetValue(message.GetType(), out var correlator))
		{
			correlationId = correlator(message);
			return true;
		}

		correlationId = null;
		return false;
	}

	/// <summary>
	/// Gets the number of registered correlation rules.
	/// </summary>
	/// <value>The count of message types with registered correlators.</value>
	public int RuleCount => _correlators.Count;
}
