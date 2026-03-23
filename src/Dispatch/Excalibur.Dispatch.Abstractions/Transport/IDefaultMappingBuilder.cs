// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Abstractions.Transport;

/// <summary>
/// Builder for configuring default mapping behavior.
/// </summary>
/// <remarks>
/// The core interface provides <see cref="ForTransport"/> for custom/generic transport
/// default configuration. Transport-specific methods (ForRabbitMq, ForKafka, etc.)
/// are provided as extension methods.
/// </remarks>
public interface IDefaultMappingBuilder
{
	/// <summary>
	/// Configures the default mapping for a specific transport.
	/// </summary>
	/// <param name="transportName">The name of the transport.</param>
	/// <param name="configure">Action to configure the transport context.</param>
	/// <returns>This builder for fluent configuration.</returns>
	IDefaultMappingBuilder ForTransport(string transportName, Action<ITransportMessageContext> configure);
}
