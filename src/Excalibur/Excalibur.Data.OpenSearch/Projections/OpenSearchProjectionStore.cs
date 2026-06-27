// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.EventSourcing;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using OpenSearch.Client;
using OpenSearch.Net;

namespace Excalibur.Data.OpenSearch.Projections;

/// <summary>
/// OpenSearch implementation of <see cref="IProjectionStore{TProjection}"/>.
/// </summary>
/// <remarks>
/// <para>
/// Stores projections as JSON documents in OpenSearch indices.
/// Each projection type gets a dedicated index (<c>{prefix}-{typename}</c>).
/// Uses IndexAsync with document ID for upsert operations.
/// </para>
/// <para>
/// Each projection type resolves its own named options instance keyed by
/// <c>typeof(TProjection).Name</c>, allowing multiple projection stores to
/// coexist with independent configurations.
/// </para>
/// </remarks>
/// <typeparam name="TProjection">The projection type to store.</typeparam>
public sealed class OpenSearchProjectionStore<TProjection> : IProjectionStore<TProjection>
	where TProjection : class
{
	/// <summary>
	/// The named options key used for this projection type.
	/// </summary>
	internal static readonly string OptionsName = typeof(TProjection).Name;

	private readonly OpenSearchProjectionStoreOptions _options;
	private readonly OpenSearchClient _client;
	private readonly string _indexName;
	private readonly ILogger<OpenSearchProjectionStore<TProjection>> _logger;
	private volatile bool _indexVerified;

	/// <summary>
	/// Initializes a new instance of the <see cref="OpenSearchProjectionStore{TProjection}"/> class
	/// using named options resolved via <see cref="IOptionsMonitor{TOptions}"/>.
	/// </summary>
	/// <param name="optionsMonitor">The options monitor for named options resolution.</param>
	/// <param name="logger">The logger instance.</param>
#pragma warning disable RS0016 // Analyzer cannot resolve nullable annotations for OpenSearch.Client types
	public OpenSearchProjectionStore(
		IOptionsMonitor<OpenSearchProjectionStoreOptions> optionsMonitor,
		ILogger<OpenSearchProjectionStore<TProjection>> logger)
	{
		ArgumentNullException.ThrowIfNull(optionsMonitor);
		ArgumentNullException.ThrowIfNull(logger);

		_options = optionsMonitor.Get(OptionsName);
		_options.Validate();
		_logger = logger;
		_indexName = GetIndexName(_options);

#pragma warning disable CA2000 // ConnectionSettings lifetime managed by OpenSearchClient
		var settings = new ConnectionSettings(new Uri(_options.NodeUri))
			.DefaultIndex(_indexName)
			.ThrowExceptions();
#pragma warning restore CA2000

		_client = new OpenSearchClient(settings);
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="OpenSearchProjectionStore{TProjection}"/> class
	/// with an existing client.
	/// </summary>
	/// <param name="client">An existing OpenSearch client.</param>
	/// <param name="optionsMonitor">The options monitor for named options resolution.</param>
	/// <param name="logger">The logger instance.</param>
	public OpenSearchProjectionStore(
		OpenSearchClient client,
		IOptionsMonitor<OpenSearchProjectionStoreOptions> optionsMonitor,
		ILogger<OpenSearchProjectionStore<TProjection>> logger)
#pragma warning restore RS0016
	{
		ArgumentNullException.ThrowIfNull(client);
		ArgumentNullException.ThrowIfNull(optionsMonitor);
		ArgumentNullException.ThrowIfNull(logger);

		_client = client;
		_options = optionsMonitor.Get(OptionsName);
		_options.Validate();
		_logger = logger;
		_indexName = GetIndexName(_options);
	}

	/// <inheritdoc/>
	public async Task<TProjection?> GetByIdAsync(string id, CancellationToken cancellationToken)
	{
		await EnsureIndexAsync(cancellationToken).ConfigureAwait(false);

		var response = await _client.GetAsync<TProjection>(
			id,
			g => g.Index(_indexName),
			cancellationToken).ConfigureAwait(false);

		if (!response.Found)
		{
			return null;
		}

		return response.Source;
	}

	/// <inheritdoc/>
	public async Task UpsertAsync(string id, TProjection projection, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(projection);
		await EnsureIndexAsync(cancellationToken).ConfigureAwait(false);

		await _client.IndexAsync(
			projection,
			i => i.Index(_indexName).Id(id).Refresh(Refresh.WaitFor),
			cancellationToken).ConfigureAwait(false);
	}

	/// <inheritdoc/>
	public async Task DeleteAsync(string id, CancellationToken cancellationToken)
	{
		await EnsureIndexAsync(cancellationToken).ConfigureAwait(false);

		await _client.DeleteAsync<TProjection>(
			id,
			d => d.Index(_indexName),
			cancellationToken).ConfigureAwait(false);
	}

	/// <inheritdoc/>
	public async Task<IReadOnlyList<TProjection>> QueryAsync(
		IDictionary<string, object>? filters,
		QueryOptions? options,
		CancellationToken cancellationToken)
	{
		await EnsureIndexAsync(cancellationToken).ConfigureAwait(false);

		var response = await _client.SearchAsync<TProjection>(s =>
		{
			s.Index(_indexName);

			// Apply the caller-supplied filters. Previously this parameter was ignored and the whole
			// index was returned regardless of filters (MS-A5 silent-wrong-results bug). Empty/null
			// filters match all (BuildQuery handles that).
			s.Query(BuildQuery(filters));

			if (options?.Take > 0)
			{
				s.Size(options.Take.Value);
			}

			if (options?.Skip > 0)
			{
				s.From(options.Skip.Value);
			}

			if (options?.OrderBy != null)
			{
				s.Sort(sort => sort.Field(
					options.OrderBy,
					options.Descending ? SortOrder.Descending : SortOrder.Ascending));
			}

			return s;
		}, cancellationToken).ConfigureAwait(false);

		return response.Documents.ToList();
	}

	/// <inheritdoc/>
	public async Task<long> CountAsync(
		IDictionary<string, object>? filters,
		CancellationToken cancellationToken)
	{
		await EnsureIndexAsync(cancellationToken).ConfigureAwait(false);

		var response = await _client.CountAsync<TProjection>(
			c => c.Index(_indexName).Query(BuildQuery(filters)),
			cancellationToken).ConfigureAwait(false);

		return response.Count;
	}

	// Translates the caller's filter dictionary into an OpenSearch query. Empty/null filters match all;
	// otherwise every (field, value) pair becomes an exact-match term, AND-combined (bool must). String
	// values target the `.keyword` sub-field produced by dynamic mapping (a term query against the
	// analyzed text field would not match an exact value) — mirroring the ES projection store's
	// GetExactMatchFieldName semantics.
	private static Func<QueryContainerDescriptor<TProjection>, QueryContainer> BuildQuery(
		IDictionary<string, object>? filters)
	{
		if (filters is null || filters.Count == 0)
		{
			return static q => q.MatchAll();
		}

		return q =>
		{
			QueryContainer? query = null;

			foreach (var filter in filters)
			{
				var fieldName = filter.Value is string ? $"{filter.Key}.keyword" : filter.Key;
				query &= q.Term(t => t.Field(fieldName).Value(filter.Value));
			}

			return query ?? q.MatchAll();
		};
	}

	private static string GetIndexName(OpenSearchProjectionStoreOptions opts)
	{
		var name = opts.IndexName ?? typeof(TProjection).Name;

		var composed = string.IsNullOrWhiteSpace(opts.IndexPrefix)
			? name
			: $"{opts.IndexPrefix}-{name}";

		// OpenSearch index names MUST be lowercase. Lowercase the entire composed name (prefix
		// included) so a consumer-supplied IndexPrefix/IndexName or environment-derived segment
		// (e.g. "Development") cannot produce an invalid index name.
#pragma warning disable CA1308 // OpenSearch index names must be lowercase
		return composed.ToLowerInvariant();
#pragma warning restore CA1308
	}

	private async Task EnsureIndexAsync(CancellationToken cancellationToken)
	{
		if (_indexVerified || !_options.CreateIndexOnInitialize)
		{
			return;
		}

		var exists = await _client.Indices.ExistsAsync(_indexName, ct: cancellationToken)
			.ConfigureAwait(false);

		if (!exists.Exists)
		{
			await _client.Indices.CreateAsync(
				_indexName,
				c => c.Settings(s => s
					.NumberOfShards(_options.NumberOfShards)
					.NumberOfReplicas(_options.NumberOfReplicas)),
				cancellationToken).ConfigureAwait(false);

			_logger.LogInformation("Created OpenSearch index {IndexName}", _indexName);
		}

		_indexVerified = true;
	}
}
