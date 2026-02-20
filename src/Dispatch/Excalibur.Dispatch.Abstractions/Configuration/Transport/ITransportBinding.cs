// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Dispatch.Abstractions.Options;

namespace Excalibur.Dispatch.Abstractions.Configuration;

/// <summary>
/// Builder interface for configuring inbound transport route rules.
/// </summary>
public interface IInboundRouteBuilder
{
	/// <summary>
	/// Routes messages with a specific name.
	/// </summary>
	/// <param name="messageName"> The message name to route. </param>
	/// <returns> The inbound route builder. </returns>
	IInboundRouteBuilder RouteName(string messageName);

	/// <summary>
	/// Routes messages of a specific type.
	/// </summary>
	/// <typeparam name="TMessage"> The message type to route. </typeparam>
	/// <returns> The inbound route builder. </returns>
	IInboundRouteBuilder RouteType<TMessage>()
		where TMessage : IDispatchMessage;

	/// <summary>
	/// Routes messages of a specific type.
	/// </summary>
	/// <param name="messageType"> The message type to route. </param>
	/// <returns> The inbound route builder. </returns>
	IInboundRouteBuilder RouteType(Type messageType);

	/// <summary>
	/// Routes to a dispatcher with a specific profile.
	/// </summary>
	/// <param name="profile"> The dispatcher profile to use (default: "default"). </param>
	/// <returns> The inbound route builder. </returns>
	IInboundRouteBuilder ToDispatcher(string profile = "default");

	/// <summary>
	/// Configures additional options for the binding.
	/// </summary>
	/// <param name="configure"> The configuration action. </param>
	/// <returns> The inbound route builder. </returns>
	IInboundRouteBuilder WithOptions(Action<TransportBindingOptions> configure);
}
