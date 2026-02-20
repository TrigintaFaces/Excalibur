// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Elastic.Clients.Elasticsearch;

using Excalibur.Data.ElasticSearch.Resources;

using Microsoft.Extensions.DependencyInjection;

using HealthStatus = Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus;

namespace Excalibur.Data.ElasticSearch;

/// <summary>
/// Provides extension methods for configuring health checks in an application.
/// </summary>
public static class HealthChecksBuilderExtensions
{
	/// <summary>
	/// Adds an Elasticsearch health check using the registered <see cref="ElasticsearchClient" />.
	/// </summary>
	/// <param name="healthChecks"> The <see cref="IHealthChecksBuilder" /> to which the health check is added. </param>
	/// <param name="name"> The name of the health check. </param>
	/// <param name="timeout"> The timeout duration for the health check operation. </param>
	/// <returns> The updated <see cref="IHealthChecksBuilder" /> instance for method chaining. </returns>
	/// <exception cref="ArgumentNullException">
	/// Thrown if <paramref name="healthChecks" /> or <paramref name="name" /> is <c> null </c>.
	/// </exception>
	/// <exception cref="ArgumentOutOfRangeException"> Thrown if <paramref name="timeout" /> is less than or equal to zero. </exception>
	public static IHealthChecksBuilder AddElasticHealthCheck(
		this IHealthChecksBuilder healthChecks,
		string name,
		TimeSpan timeout)
	{
		ArgumentNullException.ThrowIfNull(healthChecks);
		ArgumentException.ThrowIfNullOrWhiteSpace(name);

		if (timeout <= TimeSpan.Zero)
		{
			throw new ArgumentOutOfRangeException(nameof(timeout), ErrorMessages.TimeoutMustBePositive);
		}

		_ = healthChecks.AddCheck<ElasticClientHealthCheck>(
			name,
			failureStatus: HealthStatus.Unhealthy,
			tags: ["Feedback", "Database"],
			timeout: timeout);

		return healthChecks;
	}
}
