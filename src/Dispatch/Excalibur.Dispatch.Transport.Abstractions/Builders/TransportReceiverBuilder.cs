// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Transport.Builders;

/// <summary>
/// Builds a composed <see cref="ITransportReceiver"/> by stacking decorators around an inner receiver.
/// Follows the <c>ChatClientBuilder</c> pattern from Microsoft.Extensions.AI.
/// </summary>
/// <remarks>
/// Decorators are applied in registration order: the first registered decorator is the outermost wrapper.
/// <code>
/// var receiver = new TransportReceiverBuilder(nativeReceiver)
///     .Use(inner => new TelemetryTransportReceiver(inner, meter, activitySource, "Kafka"))
///     .Use(inner => new DeadLetterTransportReceiver(inner, dlqManager))
///     .Build();
/// </code>
/// </remarks>
public sealed class TransportReceiverBuilder
{
	private readonly ITransportReceiver _innerReceiver;
	private readonly List<Func<ITransportReceiver, ITransportReceiver>> _decorators = [];

	/// <summary>
	/// Initializes a new instance of the <see cref="TransportReceiverBuilder"/> class.
	/// </summary>
	/// <param name="innerReceiver">The native transport receiver to decorate.</param>
	public TransportReceiverBuilder(ITransportReceiver innerReceiver) =>
		_innerReceiver = innerReceiver ?? throw new ArgumentNullException(nameof(innerReceiver));

	/// <summary>
	/// Adds a decorator to the receiver pipeline.
	/// </summary>
	/// <param name="decorator">A factory that wraps the current receiver with a decorator.</param>
	/// <returns>This builder for chaining.</returns>
	public TransportReceiverBuilder Use(Func<ITransportReceiver, ITransportReceiver> decorator)
	{
		ArgumentNullException.ThrowIfNull(decorator);
		_decorators.Add(decorator);
		return this;
	}

	/// <summary>
	/// Builds the composed <see cref="ITransportReceiver"/> by applying all registered decorators.
	/// </summary>
	/// <returns>The fully composed transport receiver.</returns>
	public ITransportReceiver Build()
	{
		var receiver = _innerReceiver;
		foreach (var decorator in _decorators)
		{
			receiver = decorator(receiver);
		}

		return receiver;
	}
}
