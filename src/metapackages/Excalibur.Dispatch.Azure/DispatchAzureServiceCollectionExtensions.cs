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
	/// <param name="configureDispatch">Optional additional dispatch builder configuration. Supplying it takes over handler registration: when it is omitted, handlers are discovered by scanning the entry assembly; when it is supplied, only the handlers it names are registered.</param>
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

		// The Azure Service Bus message bus takes IPayloadSerializer, whose only registration is
		// AddPluggableSerialization. Nothing else in this bundle seats it, so the one-line call below
		// built a container in which the transport it had just registered could not be constructed --
		// and because the bus is created by a factory delegate, container validation could not see it
		// either. Seated here rather than in the transport package, which treats serialization as a
		// consumer concern; this is the batteries-included bundle, so supplying the default is its job.
		// All TryAdd, so a consumer who registers their own serializer still wins.
		_ = services.AddPluggableSerialization();

		return services.AddDispatch(dispatch =>
		{
			dispatch.UseAzureServiceBus(configureAzure);
			dispatch.UseResilience();
			dispatch.UseObservability();
			// The consumer supplied no configuration of their own, so nothing has named a handler and
			// nothing will. Discover them from the entry assembly, which is what this call did before the
			// lambda was synthesised. A consumer who DOES supply a lambda owns handler registration.
			if (configureDispatch is null)
			{
				_ = dispatch.AddHandlersFromEntryAssembly();
			}
			else
			{
				configureDispatch(dispatch);
			}
		});
	}
}
