// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Abstractions.Configuration;

/// <summary>
/// Builder interface for configuring transport bindings.
/// </summary>
public interface ITransportBindingBuilder
{
	/// <summary>
	/// Binds from a queue transport.
	/// </summary>
	/// <param name="queueName"> The name of the queue transport. </param>
	/// <returns> The inbound route builder. </returns>
	IInboundRouteBuilder FromQueue(string queueName);

	/// <summary>
	/// Binds from a timer transport.
	/// </summary>
	/// <param name="timerName"> The name of the timer transport. </param>
	/// <returns> The inbound route builder. </returns>
	IInboundRouteBuilder FromTimer(string timerName);

	/// <summary>
	/// Binds from a named transport.
	/// </summary>
	/// <param name="transportName"> The name of the transport. </param>
	/// <returns> The inbound route builder. </returns>
	IInboundRouteBuilder FromTransport(string transportName);
}
