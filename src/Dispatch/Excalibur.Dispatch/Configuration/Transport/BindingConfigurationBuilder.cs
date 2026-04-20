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
internal sealed class BindingConfigurationBuilder(ITransportRegistry transportRegistry, TransportBindingRegistry bindingRegistry)
	: ITransportBindingBuilder
{
	private readonly ITransportRegistry _transportRegistry = transportRegistry ?? throw new ArgumentNullException(nameof(transportRegistry));

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

		// Accept either a materialized adapter or a pending factory registration.
		// When only a factory is present, the binding defers adapter resolution to
		// startup (via LazyTransportBinding) — this keeps AddEventBindings order-
		// independent from AddXTransport and lets ValidateOnStart surface any
		// truly-missing-transport references at host start. [bd-20ft0e FIX 4]
		var adapter = _transportRegistry.GetTransportAdapter(transportName);
		if (adapter is null)
		{
			var hasPendingFactory = false;
			if (_transportRegistry is TransportRegistry concrete)
			{
				foreach (var pending in concrete.GetPendingFactoryNames())
				{
					if (string.Equals(pending, transportName, StringComparison.Ordinal))
					{
						hasPendingFactory = true;
						break;
					}
				}
			}

			if (!hasPendingFactory)
			{
				// Unknown transport — defer to ValidateOnStart by recording a pending
				// name so the startup validator can fail with a clear message.
				_bindingRegistry.RegisterPendingTransportReference(transportName);
			}
		}

		return new TransportBindingBuilder(transportName, adapter, _transportRegistry, _bindingRegistry);
	}
}
