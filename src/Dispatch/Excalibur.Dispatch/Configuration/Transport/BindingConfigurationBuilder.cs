// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Dispatch.Abstractions.Configuration;
using Excalibur.Dispatch.Transport;

using IInboundRouteBuilder = Excalibur.Dispatch.Abstractions.Configuration.IInboundRouteBuilder;

namespace Excalibur.Dispatch.Configuration.Transport;

/// <summary>
/// Default implementation of transport binding builder.
/// </summary>
/// <remarks> Initializes a new instance of the <see cref="BindingConfigurationBuilder" /> class. </remarks>
/// <param name="transportRegistry"> The transport registry. </param>
/// <param name="bindingRegistry"> The binding registry. </param>
public sealed class BindingConfigurationBuilder(TransportRegistry transportRegistry, TransportBindingRegistry bindingRegistry)
	: ITransportBindingBuilder
{
	private readonly TransportRegistry _transportRegistry = transportRegistry ?? throw new ArgumentNullException(nameof(transportRegistry));

	private readonly TransportBindingRegistry
		_bindingRegistry = bindingRegistry ?? throw new ArgumentNullException(nameof(bindingRegistry));

	/// <inheritdoc />
	public IInboundRouteBuilder FromQueue(string queueName)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(queueName);

		return FromTransport(queueName);
	}

	/// <inheritdoc />
	public IInboundRouteBuilder FromTimer(string timerName)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(timerName);

		return FromTransport(timerName);
	}

	/// <inheritdoc />
	public IInboundRouteBuilder FromTransport(string transportName)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(transportName);

		var adapter = _transportRegistry.GetTransportAdapter(transportName)
					  ?? throw new ArgumentException($"Transport '{transportName}' is not registered", nameof(transportName));

		return new TransportBindingBuilder(transportName, adapter, _bindingRegistry);
	}
}
