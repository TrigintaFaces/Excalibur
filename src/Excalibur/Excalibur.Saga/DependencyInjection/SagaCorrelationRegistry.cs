// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Collections.Concurrent;

using Excalibur.Dispatch;

namespace Excalibur.Saga.DependencyInjection;

/// <summary>
/// The default <see cref="ISagaCorrelationRegistry"/>: a map from (saga type, message type) to the key
/// selector the consumer declared, populated at composition and read by the coordinator.
/// </summary>
internal sealed class SagaCorrelationRegistry : ISagaCorrelationRegistry
{
	private readonly ConcurrentDictionary<(Type Saga, Type Message), Correlation> _correlations = new();

	/// <summary>
	/// Declares that <paramref name="messageType"/> correlates to <paramref name="sagaType"/> through
	/// <paramref name="keySelector"/>.
	/// </summary>
	/// <param name="sagaType">The saga type.</param>
	/// <param name="messageType">The message type carrying the key.</param>
	/// <param name="sagaTypeName">The stable name scoping derived identifiers for this saga.</param>
	/// <param name="keySelector">Extracts the business key from the message.</param>
	/// <exception cref="InvalidOperationException">
	/// Thrown when a different correlation is already declared for the same saga and message type.
	/// </exception>
	public void Declare(Type sagaType, Type messageType, string sagaTypeName, Func<object, string?> keySelector)
	{
		ArgumentNullException.ThrowIfNull(sagaType);
		ArgumentNullException.ThrowIfNull(messageType);
		ArgumentException.ThrowIfNullOrWhiteSpace(sagaTypeName);
		ArgumentNullException.ThrowIfNull(keySelector);

        DeclareTyped(sagaType, messageType, key => SagaCorrelation.ToSagaId(sagaTypeName, key), keySelector);
    }

    /// <summary>Captures the same typed derivation a publisher invokes through SagaCorrelation.</summary>
    internal void DeclareTyped(Type sagaType, Type messageType, Func<string, Guid> derive, Func<object, string?> keySelector)
    {
        ArgumentNullException.ThrowIfNull(sagaType);
        ArgumentNullException.ThrowIfNull(messageType);
        ArgumentNullException.ThrowIfNull(derive);
        ArgumentNullException.ThrowIfNull(keySelector);
        var correlation = new Correlation(derive, keySelector);


		// LAST ONE IN DOES NOT WIN, because the two registrations would derive different identifiers from
		// the same message and only one of them finds the saga the other created. A duplicate declaration
		// is a composition mistake, and the cost of accepting it quietly is a saga that splits in two
		// under load with nothing recording why.
		if (!_correlations.TryAdd((sagaType, messageType), correlation))
		{
			throw new InvalidOperationException(
				$"A correlation is already declared for saga '{sagaType.Name}' and message "
				+ $"'{messageType.Name}'. Two selectors over the same message derive different identifiers "
				+ "from it, so messages would split across two instances. Declare it once.");
		}
	}

	/// <inheritdoc />
	public bool TryResolveSagaId(Type sagaType, object message, out Guid sagaId)
	{
		ArgumentNullException.ThrowIfNull(sagaType);
		ArgumentNullException.ThrowIfNull(message);

		sagaId = Guid.Empty;

		if (!_correlations.TryGetValue((sagaType, message.GetType()), out var correlation))
		{
			// Nothing declared. The caller falls back to the identifier the message carries, which is the
			// behaviour every saga had before correlation existed.
			return false;
		}

		var key = correlation.KeySelector(message);

		// DECLARED AND EMPTY IS A FAULT, NOT A FALLBACK. The consumer said this message correlates. If the
		// key is missing, falling back would route by whatever identifier the message happened to carry --
		// usually a fresh one -- and start a second saga per message, which is the exact failure
		// correlation exists to prevent and is invisible until someone counts instances.
		if (string.IsNullOrWhiteSpace(key))
		{
			throw new InvalidOperationException(
				$"Saga '{sagaType.Name}' declares a correlation for message '{message.GetType().Name}', but "
				+ "the selector returned no key for this instance, so the message cannot be routed to a "
				+ "saga. Either the key is unset on the message or the selector reads the wrong member.");
		}

		sagaId = correlation.Derive(key);
		return true;
	}

	private sealed record Correlation(Func<string, Guid> Derive, Func<object, string?> KeySelector);
}
