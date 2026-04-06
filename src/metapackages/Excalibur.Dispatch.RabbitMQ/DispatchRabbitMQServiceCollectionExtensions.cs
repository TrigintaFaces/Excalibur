// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;

using Excalibur.Dispatch.Abstractions.Configuration;
using Excalibur.Dispatch.Resilience.Polly;
using Excalibur.Dispatch.Transport.RabbitMQ;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Convenience extension that bundles Excalibur.Dispatch with RabbitMQ transport, resilience,
/// and observability into a single registration call.
/// </summary>
public static class DispatchRabbitMQServiceCollectionExtensions
{
	/// <summary>
	/// Registers Excalibur.Dispatch with RabbitMQ transport, Polly resilience, and OpenTelemetry observability.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configureRabbitMQ">RabbitMQ transport configuration.</param>
	/// <param name="configureDispatch">Optional additional dispatch builder configuration.</param>
	/// <returns>The service collection for chaining.</returns>
	[UnconditionalSuppressMessage("AOT", "IL2026:RequiresUnreferencedCode",
		Justification = "Resilience configuration binding is expected to use reflection in this convenience metapackage")]
	[UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode",
		Justification = "Resilience configuration binding is expected to use dynamic code in this convenience metapackage")]
	public static IServiceCollection AddDispatchRabbitMQ(
		this IServiceCollection services,
		Action<IRabbitMQTransportBuilder> configureRabbitMQ,
		Action<IDispatchBuilder>? configureDispatch = null)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configureRabbitMQ);

		return services.AddDispatch(dispatch =>
		{
			dispatch.UseRabbitMQ(configureRabbitMQ);
			dispatch.UseResilience();
			dispatch.UseObservability();
			configureDispatch?.Invoke(dispatch);
		});
	}
}
