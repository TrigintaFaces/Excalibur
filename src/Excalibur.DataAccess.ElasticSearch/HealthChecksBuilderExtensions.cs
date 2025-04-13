using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.Cluster;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

using HealthStatus = Elastic.Clients.Elasticsearch.HealthStatus;

namespace Excalibur.DataAccess.ElasticSearch;

public interface IElasticsearchHealthClient
{
	public Task<HealthResponse> GetClusterHealthAsync(CancellationToken cancellationToken = default);
}

/// <summary>
///     Provides extension methods for configuring health checks in an application.
/// </summary>
public static class HealthChecksBuilderExtensions
{
	/// <summary>
	///     Adds an Elasticsearch health check using the registered <see cref="ElasticsearchClient" />.
	/// </summary>
	/// <param name="healthChecks"> The <see cref="IHealthChecksBuilder" /> to which the health check is added. </param>
	/// <param name="name"> The name of the health check. </param>
	/// <param name="timeout"> The timeout duration for the health check operation. </param>
	/// <returns> The updated <see cref="IHealthChecksBuilder" /> instance for method chaining. </returns>
	/// <exception cref="ArgumentNullException">
	///     Thrown if <paramref name="healthChecks" />, <paramref name="name" />, or <paramref name="connectionString" /> is <c> null </c>.
	/// </exception>
	public static IHealthChecksBuilder AddElasticHealthCheck(
		this IHealthChecksBuilder healthChecks,
		string name,
		TimeSpan timeout)
	{
		ArgumentNullException.ThrowIfNull(healthChecks);
		ArgumentException.ThrowIfNullOrWhiteSpace(name);

		if (timeout <= TimeSpan.Zero)
		{
			throw new ArgumentOutOfRangeException(nameof(timeout), "Timeout must be a positive value.");
		}

		_ = healthChecks.AddCheck<ElasticClientHealthCheck>(
			name,
			failureStatus: Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus.Unhealthy,
			tags: ["Feedback", "Database"],
			timeout: timeout);

		return healthChecks;
	}
}

public sealed class ElasticClientHealthCheck(IElasticsearchHealthClient client) : IHealthCheck
{
	public async Task<HealthCheckResult> CheckHealthAsync(
		HealthCheckContext context,
		CancellationToken cancellationToken = default)
	{
		try
		{
			var response = await client.GetClusterHealthAsync(cancellationToken).ConfigureAwait(false);

			return response.Status switch
			{
				HealthStatus.Green or HealthStatus.Yellow => HealthCheckResult.Healthy($"Cluster status: {response.Status}"),
				HealthStatus.Red => HealthCheckResult.Unhealthy("Cluster is in RED state."),
				_ => HealthCheckResult.Unhealthy("Unknown cluster health status.")
			};
		}
		catch (Exception ex)
		{
			return HealthCheckResult.Unhealthy("Exception while checking Elasticsearch cluster health.", ex);
		}
	}
}

public sealed class ElasticsearchHealthClient(ElasticsearchClient client) : IElasticsearchHealthClient
{
	public Task<HealthResponse> GetClusterHealthAsync(CancellationToken cancellationToken = default)
		=> client.Cluster.HealthAsync(cancellationToken: cancellationToken);
}
