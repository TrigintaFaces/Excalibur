using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Excalibur.DataAccess.ElasticSearch;

/// <summary>
///     Provides extension methods for configuring health checks in an application.
/// </summary>
public static class HealthChecksBuilderExtensions
{
	/// <summary>
	///     Adds an Elasticsearch health check to the health checks builder.
	/// </summary>
	/// <param name="healthChecks"> The <see cref="IHealthChecksBuilder" /> to which the health check is added. </param>
	/// <param name="name"> The name of the health check. </param>
	/// <param name="timeout"> The timeout duration for the health check operation. </param>
	/// <param name="connectionString"> The connection string for the Elasticsearch cluster. </param>
	/// <returns> The updated <see cref="IHealthChecksBuilder" /> instance for method chaining. </returns>
	/// <exception cref="ArgumentNullException">
	///     Thrown if <paramref name="healthChecks" />, <paramref name="name" />, or <paramref name="connectionString" /> is <c> null </c>.
	/// </exception>
	public static IHealthChecksBuilder AddElasticHealthCheck(
		this IHealthChecksBuilder healthChecks,
		string name,
		TimeSpan timeout,
		string connectionString)
	{
		ArgumentNullException.ThrowIfNull(healthChecks, nameof(healthChecks));
		ArgumentNullException.ThrowIfNull(name, nameof(name));
		ArgumentNullException.ThrowIfNull(connectionString, nameof(connectionString));

		if (timeout <= TimeSpan.Zero)
		{
			throw new ArgumentOutOfRangeException(nameof(timeout), "Timeout must be a positive value.");
		}

		_ = healthChecks.AddElasticsearch(
			connectionString,
			name: name,
			failureStatus: HealthStatus.Unhealthy,
			tags: ["Feedback", "Database"],
			timeout: timeout);

		return healthChecks;
	}
}
