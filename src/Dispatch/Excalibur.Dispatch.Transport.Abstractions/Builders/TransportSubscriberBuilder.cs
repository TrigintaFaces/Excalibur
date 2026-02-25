// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Transport.Builders;

/// <summary>
/// Builds a composed <see cref="ITransportSubscriber"/> by stacking decorators around an inner subscriber.
/// Follows the <c>ChatClientBuilder</c> pattern from Microsoft.Extensions.AI.
/// </summary>
/// <remarks>
/// Decorators are applied in registration order: the first registered decorator is the outermost wrapper.
/// <code>
/// var subscriber = new TransportSubscriberBuilder(nativeSubscriber)
///     .Use(inner => new TelemetryTransportSubscriber(inner, meter, activitySource, "Kafka"))
///     .Use(inner => new DeadLetterTransportSubscriber(inner, dlqHandler))
///     .Build();
/// </code>
/// </remarks>
public sealed class TransportSubscriberBuilder
{
	private readonly ITransportSubscriber _innerSubscriber;
	private readonly List<Func<ITransportSubscriber, ITransportSubscriber>> _decorators = [];

	/// <summary>
	/// Initializes a new instance of the <see cref="TransportSubscriberBuilder"/> class.
	/// </summary>
	/// <param name="innerSubscriber">The native transport subscriber to decorate.</param>
	public TransportSubscriberBuilder(ITransportSubscriber innerSubscriber) =>
		_innerSubscriber = innerSubscriber ?? throw new ArgumentNullException(nameof(innerSubscriber));

	/// <summary>
	/// Adds a decorator to the subscriber pipeline.
	/// </summary>
	/// <param name="decorator">A factory that wraps the current subscriber with a decorator.</param>
	/// <returns>This builder for chaining.</returns>
	public TransportSubscriberBuilder Use(Func<ITransportSubscriber, ITransportSubscriber> decorator)
	{
		ArgumentNullException.ThrowIfNull(decorator);
		_decorators.Add(decorator);
		return this;
	}

	/// <summary>
	/// Builds the composed <see cref="ITransportSubscriber"/> by applying all registered decorators.
	/// </summary>
	/// <returns>The fully composed transport subscriber.</returns>
	public ITransportSubscriber Build()
	{
		var subscriber = _innerSubscriber;
		foreach (var decorator in _decorators)
		{
			subscriber = decorator(subscriber);
		}

		return subscriber;
	}
}
