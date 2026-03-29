// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;

using Excalibur.Dispatch.Abstractions.Configuration;
using Excalibur.Dispatch.Resilience.Polly;
using Excalibur.Dispatch.Transport.Kafka;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Convenience extension that bundles Excalibur.Dispatch with Kafka transport, resilience,
/// and observability into a single registration call.
/// </summary>
public static class DispatchKafkaServiceCollectionExtensions
{
	/// <summary>
	/// Registers Excalibur.Dispatch with Kafka transport, Polly resilience, and OpenTelemetry observability.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configureKafka">Kafka transport configuration.</param>
	/// <param name="configureDispatch">Optional additional dispatch builder configuration.</param>
	/// <returns>The service collection for chaining.</returns>
	[RequiresUnreferencedCode("Kafka transport uses reflection for schema registry serialization")]
	[RequiresDynamicCode("Kafka transport requires dynamic code generation for serializer resolution")]
	public static IServiceCollection AddDispatchKafka(
		this IServiceCollection services,
		Action<IKafkaTransportBuilder> configureKafka,
		Action<IDispatchBuilder>? configureDispatch = null)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configureKafka);

		return services.AddDispatch(dispatch =>
		{
			dispatch.UseKafka(configureKafka);
			dispatch.UseResilience();
			dispatch.UseObservability();
			configureDispatch?.Invoke(dispatch);
		});
	}
}
