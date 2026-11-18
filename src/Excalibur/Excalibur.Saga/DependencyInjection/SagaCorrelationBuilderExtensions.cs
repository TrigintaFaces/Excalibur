// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch;
using Excalibur.Saga.DependencyInjection;


namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Declares how a saga correlates incoming messages to its instances.
/// </summary>
public static class SagaCorrelationBuilderExtensions
{
	/// <summary>
	/// Declares that <typeparamref name="TMessage"/> belongs to the <typeparamref name="TSaga"/> instance
	/// identified by the key <paramref name="keySelector"/> reads from it.
	/// </summary>
	/// <typeparam name="TSaga">The saga type.</typeparam>
	/// <typeparam name="TMessage">The message type carrying the business key.</typeparam>
	/// <param name="builder">The saga builder.</param>
	/// <param name="keySelector">Reads the business key from the message.</param>
	/// <returns>The builder, for chaining.</returns>
	/// <example>
	/// <code>
	/// services.AddExcalibur(excalibur => excalibur.AddSagas(saga => saga
	///     .CorrelateBy&lt;OrderSaga, OrderSubmitted&gt;(m => m.OrderId)
	///     .CorrelateBy&lt;OrderSaga, OrderShipped&gt;(m => m.OrderId)));
	/// </code>
	/// </example>
	/// <remarks>
	/// <para>
	/// With this declared, a publisher sends <typeparamref name="TMessage"/> carrying only the business key
	/// and does not compute or know the saga's identifier. Every message whose key is the same order
	/// number reaches the same instance, including the first one, which creates it.
	/// </para>
	/// <para>
	/// <b>A declaration here OUTRANKS an identifier the message carries</b>, and that precedence is the
	/// point rather than an accident. The alternative lets a publisher that sets an identifier out of habit
	/// silently create one saga per message while the correlation sits there looking configured. Nothing is
	/// lost by the precedence: derivation is deterministic, so a correct explicit identifier is the same
	/// value this produces.
	/// </para>
	/// <para>
	/// <b>The key uses a stable fully scoped type identity</b>, so two sagas correlating on the same order number
	/// address different instances. A renamed saga derives different identifiers and stops finding its own
	/// in-flight instances. Namespace, nested and generic type identities plus assembly simple names scope
    /// the key; assembly versions are excluded. Publishers can use SagaCorrelation.ToSagaId&lt;TSaga&gt;(key)
    /// to derive exactly the same identifier.
	/// </para>
	/// <para>
	/// <b>Declare it once per saga and message type.</b> A second declaration for the same pair throws at
	/// composition instead of overwriting, because two selectors over one message derive two identifiers
	/// and messages would split across both instances.
	/// </para>
	/// </remarks>
	public static ISagaBuilder CorrelateBy<TSaga, TMessage>(
		this ISagaBuilder builder,
		Func<TMessage, string?> keySelector)
		where TMessage : notnull
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(keySelector);

		// One registry instance for the whole container: the coordinator resolves ISagaCorrelationRegistry
		// and every declaration has to land in the same map for it to see them.
        var winner = builder.Services.LastOrDefault(static descriptor =>
            !descriptor.IsKeyedService && descriptor.ServiceType == typeof(ISagaCorrelationRegistry));
        SagaCorrelationRegistry registry;
        if (winner is null)
        {
            registry = new SagaCorrelationRegistry();
            builder.Services.AddSingleton<ISagaCorrelationRegistry>(registry);
        }
        else if (winner.GetImplementationInstance() is SagaCorrelationRegistry declared)
        {
            registry = declared;
        }
        else
        {
            throw new InvalidOperationException("CorrelateBy requires the framework-owned mutable correlation registry instance. "
                + "The last unkeyed ISagaCorrelationRegistry registration is a custom instance, type or factory and cannot accept declarations. "
                + "Remove that conflicting registration or configure correlation in your custom registry without CorrelateBy.");
        }

		// The closure is typed, so reading the key is a direct call on TMessage with no reflection. The
		// cast is safe because the registry only ever hands this selector a message of its own key type.
		registry.DeclareTyped(
			typeof(TSaga),
			typeof(TMessage),
			SagaCorrelation.ToSagaId<TSaga>,
			message => keySelector((TMessage)message));

		return builder;
	}
}
