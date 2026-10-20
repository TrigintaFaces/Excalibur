// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Elastic.Clients.Elasticsearch;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.Data.ElasticSearch.Projections;

/// <summary>
/// Verifies at start-up that the clusters the registered projection stores write to can be reached.
/// </summary>
/// <remarks>
/// <para>
/// WHAT THIS EXISTS TO PREVENT. A projection store builds its client on first use, so a host that
/// cannot reach its cluster starts normally, reports nothing, and discovers the problem when a
/// background job writes its first document -- as one error per document, inside a job, however long
/// after start-up that job happens to run. The host is up, the logs are quiet, and the read models
/// are not being written. Asking once at start-up turns that into a single answer at the moment a
/// human is watching.
/// </para>
/// <para>
/// IT IS A SEPARATE CALL, NOT PART OF REGISTRATION, and deliberately so. Refusing to start on an
/// unreachable dependency is a trade, not an improvement: under an orchestrator it converts a
/// transient cluster blip into a crash-loop, and a read-model host that starts degraded and recovers
/// is often the better outcome. The consumer owns that choice, so the consumer makes the call.
/// </para>
/// <para>
/// IT IS ALSO NOT OPTIONS VALIDATION, which is why it is not expressed as
/// <see cref="IValidateOptions{TOptions}"/> with <c>ValidateOnStart</c> alongside the configuration
/// checks that already run there. Those answer whether the configuration is well-formed -- a question
/// with the same answer every time it is asked. Reachability has a different answer at two different
/// moments with identical configuration, and reporting it as an invalid-options failure would tell a
/// consumer to go and fix settings that are correct.
/// </para>
/// </remarks>
public static class ElasticSearchProjectionHostExtensions
{
	/// <summary>
	/// Pings every distinct Elasticsearch cluster the registered projection stores are configured to
	/// use, and throws when one of them cannot be reached.
	/// </summary>
	/// <param name="host">The application host, after its services are built.</param>
	/// <param name="cancellationToken">A token that cancels the probe.</param>
	/// <returns>The asynchronous operation.</returns>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="host"/> is <see langword="null"/>.</exception>
	/// <exception cref="InvalidOperationException">
	/// Thrown when no Elasticsearch projection store is registered, or when a configured cluster does
	/// not answer. Both are failures: a probe that finds nothing to probe has verified nothing, and
	/// reporting success for it would be the silence this method exists to remove.
	/// </exception>
	/// <example>
	/// <code>
	/// var host = builder.Build();
	/// await host.VerifyElasticSearchProjectionConnectivityAsync();
	/// await host.RunAsync();
	/// </code>
	/// </example>
	public static async Task VerifyElasticSearchProjectionConnectivityAsync(
		this IHost host,
		CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(host);

		await using var scope = host.Services.CreateAsyncScope();
		var services = scope.ServiceProvider;
		var logger = services.GetService<ILoggerFactory>()?.CreateLogger(typeof(ElasticSearchProjectionHostExtensions));

		var claims = services.GetService<ElasticSearchProjectionStoreExtensions.ProjectionOptionsNameClaims>();

		if (claims is null || claims.Owners.Count == 0)
		{
			throw new InvalidOperationException(
				"No Elasticsearch projection store is registered, so start-up connectivity could not be "
				+ "verified. Register your read models with AddElasticSearchProjections(...) before calling "
				+ "this, or remove the call -- reporting success here would mean a host with no projection "
				+ "stores passed the same check as a host whose cluster is reachable.");
		}

		var monitor = services.GetRequiredService<IOptionsMonitor<ElasticSearchProjectionStoreOptions>>();

		// One request per CLUSTER, not per projection: a host with seven read models on one cluster asks
		// once. The credential is part of the identity, so two projections sharing nodes under different
		// credentials remain two separate questions.
		var probed = new Dictionary<string, (ElasticSearchProjectionStoreOptions Options, List<string> Projections)>(
			StringComparer.Ordinal);

		foreach (var (optionsName, projectionType) in claims.Owners)
		{
			var options = monitor.Get(optionsName);
			var cluster = ElasticSearchProjectionClientSettings.DescribeCluster(options);

			if (!probed.TryGetValue(cluster, out var entry))
			{
				probed[cluster] = entry = (options, []);
			}

			entry.Projections.Add(projectionType.Name);
		}

		foreach (var (cluster, entry) in probed)
		{
			var client = new ElasticsearchClient(ElasticSearchProjectionClientSettings.Create(entry.Options));
			var unreachable = await PingAsync(client, cancellationToken).ConfigureAwait(false);

			if (unreachable is not null)
			{
				throw new InvalidOperationException(
					$"The Elasticsearch cluster at {cluster} did not answer at start-up, so the projections "
					+ $"that write to it ({string.Join(", ", entry.Projections.Order(StringComparer.Ordinal))}) "
					+ "would fail one document at a time once they begin. Check that the cluster is running "
					+ $"and that these node addresses are reachable from this host. ({unreachable})");
			}

			logger?.LogInformation(
				"Elasticsearch cluster at {Cluster} answered at start-up for {ProjectionCount} projection(s).",
				cluster,
				entry.Projections.Count);
		}
	}

	/// <summary>
	/// Pings a cluster.
	/// </summary>
	/// <param name="client">The client addressing the cluster.</param>
	/// <param name="cancellationToken">A token that cancels the ping.</param>
	/// <returns>
	/// <see langword="null"/> when the cluster answered; otherwise the reason it did not.
	/// </returns>
	/// <remarks>
	/// The transport raises for an unreachable node rather than returning an unsuccessful response, so
	/// both shapes are reduced to the same answer here. Cancellation is NOT one of them: a cancelled
	/// probe has measured nothing, and converting it into "unreachable" would report a shutdown as a
	/// broken cluster.
	/// </remarks>
	private static async Task<string?> PingAsync(ElasticsearchClient client, CancellationToken cancellationToken)
	{
		try
		{
			var response = await client.PingAsync(cancellationToken).ConfigureAwait(false);

			return response.IsValidResponse
				? null
				: response.ApiCallDetails?.OriginalException?.Message
					?? response.ApiCallDetails?.ToString()
					?? "the cluster returned an unsuccessful response";
		}
		catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
		{
			throw;
		}
		catch (Exception ex)
		{
			return ex.Message;
		}
	}
}
