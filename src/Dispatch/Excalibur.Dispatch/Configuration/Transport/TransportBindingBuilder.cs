// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Options;
using Excalibur.Dispatch.Abstractions.Transport;
using Excalibur.Dispatch.Transport;

using IInboundRouteBuilder = Excalibur.Dispatch.Abstractions.Configuration.IInboundRouteBuilder;

namespace Excalibur.Dispatch.Configuration.Transport;

/// <summary>
/// Builder implementation for configuring transport bindings.
/// </summary>
internal sealed class TransportBindingBuilder(
	string transportName,
	ITransportAdapter? adapter,
	ITransportRegistry transportRegistry,
	TransportBindingRegistry bindingRegistry)
	: IInboundRouteBuilder
{
	private readonly TransportBindingOptions _options = new();

	private string? _routeName;
	private Type? _routeType;

	/// <inheritdoc />
	public IInboundRouteBuilder RouteName(string messageName)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(messageName);

		_routeName = messageName;
		return this;
	}

	/// <inheritdoc />
	public IInboundRouteBuilder RouteType<TMessage>()
		where TMessage : IDispatchMessage
	{
		_routeType = typeof(TMessage);
		return this;
	}

	/// <inheritdoc />
	public IInboundRouteBuilder RouteType(Type messageType)
	{
		ArgumentNullException.ThrowIfNull(messageType);

		if (!typeof(IDispatchMessage).IsAssignableFrom(messageType))
		{
			throw new ArgumentException(
				ErrorMessages.MessageTypeMustImplementIDispatchMessage,
				nameof(messageType));
		}

		_routeType = messageType;
		return this;
	}

	/// <inheritdoc />
	public IInboundRouteBuilder ToDispatcher(string profile = "default")
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(profile);

		// Register the binding
		RegisterBinding();

		return this;
	}

	/// <inheritdoc />
	public IInboundRouteBuilder WithOptions(Action<TransportBindingOptions> configure)
	{
		ArgumentNullException.ThrowIfNull(configure);

		configure(_options);
		return this;
	}

	private void RegisterBinding()
	{
		// Determine the endpoint pattern
		var endpointPattern = _routeName ?? _routeType?.Name ?? "*";

		// When the adapter is already materialized we create an eager TransportBinding
		// (preserves the previous behavior). When the adapter is only pending (factory
		// registered via AddXTransport before the host is built), we emit a
		// LazyTransportBinding which resolves the adapter from the registry on first
		// access — after TransportAdapterHostedService.InitializeFactories runs.
		ITransportBinding binding = adapter is not null
			? new TransportBinding(
				name: $"{transportName}:{endpointPattern}",
				transportAdapter: adapter,
				endpointPattern: endpointPattern,
				pipelineProfile: null,
				acceptedMessageKinds: MessageKinds.All,
				priority: _options.Priority)
			: new LazyTransportBinding(
				name: $"{transportName}:{endpointPattern}",
				transportName: transportName,
				registry: transportRegistry,
				endpointPattern: endpointPattern,
				pipelineProfile: null,
				acceptedMessageKinds: MessageKinds.All,
				priority: _options.Priority);

		bindingRegistry.RegisterBinding(binding);
	}
}
