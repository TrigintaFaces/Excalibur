// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;

using Excalibur.Dispatch.Configuration;
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
	[RequiresUnreferencedCode(
		"Resilience configuration binding may reference types not preserved during trimming.")]
	[RequiresDynamicCode(
		"Resilience configuration binding requires dynamic code generation for property reflection and value conversion.")]
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
