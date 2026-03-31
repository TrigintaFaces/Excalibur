// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.EventSourcing.Abstractions;

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
			c => c.Index(_indexName),
			cancellationToken).ConfigureAwait(false);

		return response.Count;
	}

	private static string GetIndexName(OpenSearchProjectionStoreOptions opts)
	{
		var name = opts.IndexName ?? typeof(TProjection).Name;
#pragma warning disable CA1308 // OpenSearch index names must be lowercase
		var lowerName = name.ToLowerInvariant();
#pragma warning restore CA1308

		if (string.IsNullOrWhiteSpace(opts.IndexPrefix))
		{
			return lowerName;
		}

		return $"{opts.IndexPrefix}-{lowerName}";
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
