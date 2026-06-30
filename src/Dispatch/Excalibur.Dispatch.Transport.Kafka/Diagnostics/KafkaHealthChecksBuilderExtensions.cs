// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport.Kafka.Diagnostics;

using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for registering Kafka transport health checks.
/// </summary>
public static class KafkaHealthChecksBuilderExtensions
{
	/// <summary>
	/// Adds a health check that probes Kafka broker connectivity.
	/// </summary>
	/// <param name="builder">The health checks builder.</param>
	/// <param name="name">The health check name. Default is "kafka-transport".</param>
	/// <param name="failureStatus">The failure status. Default is <see langword="null"/> (context default).</param>
	/// <param name="tags">Optional tags for filtering health checks.</param>
	/// <returns>The health checks builder for chaining.</returns>
	public static IHealthChecksBuilder AddKafkaTransportHealthCheck(
		this IHealthChecksBuilder builder,
		string name = "kafka-transport",
		HealthStatus? failureStatus = null,
		IEnumerable<string>? tags = null)
	{
		ArgumentNullException.ThrowIfNull(builder);

		return builder.Add(new HealthCheckRegistration(
			name,
			sp => ActivatorUtilities.CreateInstance<KafkaTransportHealthCheck>(sp),
			failureStatus,
			tags));
	}
}
