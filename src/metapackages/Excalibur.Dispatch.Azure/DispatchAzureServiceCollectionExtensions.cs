// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions.Configuration;
using Excalibur.Dispatch.Resilience.Polly;
using Excalibur.Dispatch.Transport.Azure;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Convenience extension that bundles Excalibur.Dispatch with Azure Service Bus transport,
/// resilience, and observability into a single registration call.
/// </summary>
public static class DispatchAzureServiceCollectionExtensions
{
	/// <summary>
	/// Registers Excalibur.Dispatch with Azure Service Bus transport, Polly resilience, and OpenTelemetry observability.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configureAzure">Azure Service Bus transport configuration.</param>
	/// <param name="configureDispatch">Optional additional dispatch builder configuration.</param>
	/// <returns>The service collection for chaining.</returns>
	public static IServiceCollection AddDispatchAzure(
		this IServiceCollection services,
		Action<IAzureServiceBusTransportBuilder> configureAzure,
		Action<IDispatchBuilder>? configureDispatch = null)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configureAzure);

		return services.AddDispatch(dispatch =>
		{
			dispatch.UseAzureServiceBus(configureAzure);
			dispatch.UseResilience();
			dispatch.UseObservability();
			configureDispatch?.Invoke(dispatch);
		});
	}
}
