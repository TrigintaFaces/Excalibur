// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Transport.Builders;

/// <summary>
/// Builds a composed <see cref="ITransportSender"/> by stacking decorators around an inner sender.
/// Follows the <c>ChatClientBuilder</c> pattern from Microsoft.Extensions.AI.
/// </summary>
/// <remarks>
/// Decorators are applied in registration order: the first registered decorator is the outermost wrapper.
/// <code>
/// var sender = new TransportSenderBuilder(nativeSender)
///     .Use(inner => new TelemetryTransportSender(inner, meter, activitySource, "Kafka"))
///     .Use(inner => new OrderingTransportSender(inner, msg => msg.Properties.GetValueOrDefault("key")?.ToString()))
///     .Build();
/// </code>
/// </remarks>
public sealed class TransportSenderBuilder
{
	private readonly ITransportSender _innerSender;
	private readonly List<Func<ITransportSender, ITransportSender>> _decorators = [];

	/// <summary>
	/// Initializes a new instance of the <see cref="TransportSenderBuilder"/> class.
	/// </summary>
	/// <param name="innerSender">The native transport sender to decorate.</param>
	public TransportSenderBuilder(ITransportSender innerSender) =>
		_innerSender = innerSender ?? throw new ArgumentNullException(nameof(innerSender));

	/// <summary>
	/// Adds a decorator to the sender pipeline.
	/// </summary>
	/// <param name="decorator">A factory that wraps the current sender with a decorator.</param>
	/// <returns>This builder for chaining.</returns>
	public TransportSenderBuilder Use(Func<ITransportSender, ITransportSender> decorator)
	{
		ArgumentNullException.ThrowIfNull(decorator);
		_decorators.Add(decorator);
		return this;
	}

	/// <summary>
	/// Builds the composed <see cref="ITransportSender"/> by applying all registered decorators.
	/// </summary>
	/// <returns>The fully composed transport sender.</returns>
	public ITransportSender Build()
	{
		var sender = _innerSender;
		foreach (var decorator in _decorators)
		{
			sender = decorator(sender);
		}

		return sender;
	}
}
