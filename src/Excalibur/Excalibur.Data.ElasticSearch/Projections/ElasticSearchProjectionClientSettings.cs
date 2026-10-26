// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.Serialization;
using Elastic.Transport;

namespace Excalibur.Data.ElasticSearch.Projections;

/// <summary>
/// Turns projection-store options into the client settings the store connects with.
/// </summary>
/// <remarks>
/// <para>
/// THE REASON THIS IS A SHARED FUNCTION RATHER THAN A CONVENIENT COPY. The start-up connectivity
/// probe has to address the cluster the store will actually use, and "the cluster" is not only the
/// node list: it is the node list, the connection-pool kind, the request timeout and the credential
/// together. A probe that rebuilt any of those independently would be able to succeed against a
/// cluster the store then fails on -- an anonymous ping passing while the store's API key is wrong,
/// or a probe reaching a node the store's sniffing pool replaces. Both failures look like a healthy
/// start-up followed by an unexplained runtime error, which is the shape this probe exists to remove.
/// </para>
/// <para>
/// So there is one function and both callers use it. A divergence is then not something a reviewer
/// has to notice; it is something nobody can express without editing this file.
/// </para>
/// </remarks>
internal static class ElasticSearchProjectionClientSettings
{
	private static string GetScope(IEnumerable<Uri> nodes)
	{
		var descriptor = string.Join("\n", nodes.Select(static uri => uri.GetComponents(
			UriComponents.SchemeAndServer | UriComponents.Path, UriFormat.UriEscaped)).Order(StringComparer.Ordinal));
		return "elasticsearch/" + Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
			System.Text.Encoding.UTF8.GetBytes(descriptor)));
	}
	/// <summary>
	/// Builds the client settings described by the supplied store options.
	/// </summary>
	/// <param name="options">The projection-store options describing the cluster.</param>
	/// <returns>Settings addressing that cluster, authenticated as the store authenticates.</returns>
	internal static ElasticsearchClientSettings Create(ElasticSearchProjectionStoreOptions options) => Create(options, out _);

	internal static ElasticsearchClientSettings Create(ElasticSearchProjectionStoreOptions options, out string destinationScope)
        => Create(options, out destinationScope, out _);

    internal static ElasticsearchClientSettings Create(ElasticSearchProjectionStoreOptions options, out string destinationScope,
        out System.Text.Json.Serialization.JsonSerializerContext? sourceContext)
    {
		ArgumentNullException.ThrowIfNull(options);
        var resolver = options.SourceTypeInfoResolver;
        sourceContext = resolver as System.Text.Json.Serialization.JsonSerializerContext;
		var configuredNodes = options.NodeUris?.ToArray();
		var nodes = configuredNodes is { Length: > 0 } ? configuredNodes : [new Uri(options.NodeUri)];
		destinationScope = GetScope(nodes);

		ElasticsearchClientSettings settings;

		if (configuredNodes is { Length: > 0 })
		{
			NodePool pool = options.ConnectionPoolType switch
			{
				ConnectionPoolType.Sniffing => new SniffingNodePool(nodes),
				_ => new StaticNodePool(nodes),
			};
			settings = CreateSettings(pool, resolver)
				.RequestTimeout(TimeSpan.FromSeconds(options.RequestTimeoutSeconds));
		}
		else
		{
			settings = CreateSettings(new SingleNodePool(nodes[0]), resolver)
				.RequestTimeout(TimeSpan.FromSeconds(options.RequestTimeoutSeconds));
		}

		if (options.EnableDebugMode)
		{
			settings = settings.DisableDirectStreaming();
		}

		if (!string.IsNullOrWhiteSpace(options.Auth.ApiKey))
		{
			settings = settings.Authentication(new ApiKey(options.Auth.ApiKey));
		}
		else if (!string.IsNullOrWhiteSpace(options.Auth.Username) && !string.IsNullOrWhiteSpace(options.Auth.Password))
		{
			settings = settings.Authentication(new BasicAuthentication(options.Auth.Username, options.Auth.Password));
		}

		return settings;
	}

	private static ElasticsearchClientSettings CreateSettings(NodePool pool, System.Text.Json.Serialization.Metadata.IJsonTypeInfoResolver? resolver)
	{
		return resolver is null ? new ElasticsearchClientSettings(pool)
			: new ElasticsearchClientSettings(pool, sourceSerializer: (_, settings) => new DefaultSourceSerializer(settings,
				System.Text.Json.Serialization.Metadata.JsonTypeInfoResolver.Combine(resolver, ProjectionDocumentJsonContext.Default)));
	}

	/// <summary>
	/// The node set these options address, without the credential used to reach it.
	/// </summary>
	/// <param name="options">The options describing the cluster.</param>
	/// <returns>An ordered, stable, NORMALISED description of the node set.</returns>
	/// <remarks>
	/// <para>
	/// Separate from <see cref="DescribeCluster"/> because two questions need different identities. For
	/// "can this cluster be reached", the credential is part of the question -- the same nodes under two
	/// credentials are two separate probes. For "would these two projections write to the same place",
	/// the credential is irrelevant: two stores on one node set collide in the same index whoever they
	/// authenticate as, so including the credential there would separate them and miss the collision.
	/// Derived in one place so the two cannot drift.
	/// </para>
	/// <para>
	/// NORMALISED, because this value is compared for EQUALITY and a raw consumer string is not safe to
	/// compare. <c>NodeUri</c> is a <see cref="string"/> that nothing rewrites, so one cluster can be
	/// spelled several ways -- a trailing slash, an upper-case host, or the same address supplied through
	/// <c>NodeUris</c> on one projection and <c>NodeUri</c> on another. Comparing those raw would read as
	/// two clusters, and anything keyed on this value would then treat two stores on ONE cluster as
	/// unable to affect each other. That is the silent direction, so the spellings are folded here.
	/// </para>
	/// <para>
	/// Scheme, host and port are compared, and so is the path, because a node behind a reverse proxy is
	/// distinguished by its base path -- only a TRAILING slash is insignificant. An address that will not
	/// parse is folded by case and whitespace instead of being dropped, so an unparseable pair still
	/// GROUPS rather than separating: where normalisation is uncertain, the conservative answer is "these
	/// might be the same cluster".
	/// </para>
	/// </remarks>
	internal static string DescribeClusterNodes(ElasticSearchProjectionStoreOptions options)
	{
		ArgumentNullException.ThrowIfNull(options);

		var nodes = options.NodeUris is { Count: > 0 } uris
			? uris.Select(static u => NormaliseNode(u?.ToString()))
			: [NormaliseNode(options.NodeUri)];

		return string.Join(
			", ",
			nodes.Where(static n => n.Length > 0).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal));
	}

	/// <summary>One node address, reduced to the form two spellings of the same node share.</summary>
	/// <param name="value">The address as the consumer supplied it.</param>
	/// <returns>The normalised address, or an empty string when none was supplied.</returns>
	private static string NormaliseNode(string? value)
	{
		if (string.IsNullOrWhiteSpace(value))
		{
			return string.Empty;
		}

		var trimmed = value.Trim();

		// Uri lower-cases the scheme and a DNS host for us; the port is resolved to the scheme's default
		// when absent, so "http://es" and "http://es:80" agree. Only the trailing slash is left to drop.
		return Uri.TryCreate(trimmed, UriKind.Absolute, out var uri)
			? $"{uri.Scheme}://{uri.Host}:{uri.Port}{uri.AbsolutePath.TrimEnd('/')}"
			: trimmed.ToLowerInvariant();
	}

	/// <summary>
	/// Describes the cluster these options address, for grouping probes and for error messages.
	/// </summary>
	/// <param name="options">The projection-store options describing the cluster.</param>
	/// <returns>A stable description of the node addresses.</returns>
	/// <remarks>
	/// Used to probe a cluster once rather than once per projection: a host registering seven read
	/// models against one cluster should make one request at start-up, not seven. The credential is
	/// deliberately part of the key -- two projections on the same nodes with different credentials
	/// are two different reachability questions -- and is reduced to a presence marker so no secret
	/// can reach a log through this string.
	/// </remarks>
	internal static string DescribeCluster(ElasticSearchProjectionStoreOptions options)
	{
		ArgumentNullException.ThrowIfNull(options);

		var nodes = DescribeClusterNodes(options);

		var credential = !string.IsNullOrWhiteSpace(options.Auth.ApiKey)
			? "api-key"
			: !string.IsNullOrWhiteSpace(options.Auth.Username) ? $"user:{options.Auth.Username}" : "anonymous";

		return $"{nodes} ({credential})";
	}
}
