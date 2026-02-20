// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport.Kafka;

using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for registering Kafka OTel metrics.
/// </summary>
public static class KafkaOtelMetricsServiceCollectionExtensions
{
	/// <summary>
	/// Adds Kafka OpenTelemetry metrics instrumentation to the service collection.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <returns>The service collection for chaining.</returns>
	/// <remarks>
	/// <para>
	/// Registers <see cref="KafkaOtelMetrics"/> which provides the following instruments:
	/// <list type="bullet">
	/// <item><c>dispatch.kafka.messages.produced</c> - Counter of messages produced</item>
	/// <item><c>dispatch.kafka.messages.consumed</c> - Counter of messages consumed</item>
	/// <item><c>dispatch.kafka.consumer.lag</c> - Observable gauge for consumer lag</item>
	/// <item><c>dispatch.kafka.partition.count</c> - UpDownCounter for assigned partitions</item>
	/// </list>
	/// </para>
	/// <para>
	/// To export these metrics, add the Kafka meter to your OpenTelemetry configuration:
	/// <code>
	/// services.AddOpenTelemetry()
	///     .WithMetrics(metrics => metrics.AddMeter("Excalibur.Dispatch.Transport.Kafka"));
	/// </code>
	/// </para>
	/// </remarks>
	public static IServiceCollection AddKafkaOtelMetrics(this IServiceCollection services)
	{
		ArgumentNullException.ThrowIfNull(services);

		services.TryAddSingleton<KafkaOtelMetrics>();

		return services;
	}
}
