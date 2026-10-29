// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization.Metadata;
using Excalibur.EventSourcing.Subscriptions;

namespace Excalibur.EventSourcing.Projections;

/// <summary>Reads the active generation without changing the store used by projection writers.</summary>
/// <typeparam name="TProjection">The supported read schema.</typeparam>
/// <remarks>Each operation resolves routing afresh and confirms the pointer revision after reading.
/// A generation change retries the read up to three times; sustained contention fails explicitly.
/// This confirms the selected generation, not freshness relative to newly appended events.
/// Retain retired physical destinations until in-flight readers have drained.</remarks>
public sealed class ProjectionGenerationReader<TProjection> where TProjection : class
{
    internal ProjectionCapacity? Capacity { get; init; }
    internal Func<string?>? SourceResource { get; init; }

	private readonly IProjectionGenerationStore _generations;
	private readonly IProjectionStore<TProjection> _store;
	private readonly string _subscriptionName;
	private readonly string _readerName;
	private readonly HashSet<string> _compatibleKeys;

	/// <summary>Creates a reader over the outer configured store, preserving its capability boundary.</summary>
	/// <param name="checkpoints">The checkpoint store providing atomic generation routing.</param>
	/// <param name="store">The configured store, including tenant and encryption decorators.</param>
	/// <param name="subscriptionName">The stable subscription name.</param>
	/// <param name="readerName">The stable reader name.</param>
	/// <param name="compatibleProjectionKeys">Explicitly compatible member keys. Defaults to the declared projection version only.</param>
	public ProjectionGenerationReader(ISubscriptionCheckpointStore checkpoints, IProjectionStore<TProjection> store,
		string subscriptionName, string readerName, IReadOnlyCollection<string>? compatibleProjectionKeys = null)
	{
		ArgumentNullException.ThrowIfNull(checkpoints);
		ArgumentNullException.ThrowIfNull(store);
		RelationalProjectionGenerationStore.Validate(subscriptionName, readerName);
		_generations = checkpoints as IProjectionGenerationStore
			?? throw new ArgumentException("The checkpoint store must provide atomic generation routing.", nameof(checkpoints));
		_store = store;
		_subscriptionName = subscriptionName;
		_readerName = readerName;
		_compatibleKeys = new HashSet<string>(compatibleProjectionKeys ?? [SubscriptionKeys.ProjectionKey(typeof(TProjection))], StringComparer.Ordinal);
		if (_compatibleKeys.Count == 0 || _compatibleKeys.Any(string.IsNullOrWhiteSpace))
		{
			throw new ArgumentException("At least one nonblank compatible projection key is required.", nameof(compatibleProjectionKeys));
		}
	}

	/// <summary>Reads by identifier using the provider's conventional serializer.</summary>
	/// <param name="id">Projection identifier.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>The projection, or null when absent.</returns>
	[RequiresUnreferencedCode("The conventional reader requires reflection. Supply generated metadata for trimming.")]
	[RequiresDynamicCode("The conventional reader can require dynamic code. Supply generated metadata for NativeAOT.")]
	public Task<TProjection?> GetByIdAsync(string id, CancellationToken cancellationToken) =>
		ExecuteAsync(destination => LegacyFactory().OpenReader(destination), (reader, token) => reader.GetByIdAsync(id, token), cancellationToken);

	/// <summary>Reads by identifier using generated metadata, refusing providers without that capability.</summary>
	/// <param name="id">Projection identifier.</param>
	/// <param name="typeInfo">Generated metadata for the supported read schema.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>The projection, or null when absent.</returns>
	public Task<TProjection?> GetByIdAsync(string id, JsonTypeInfo<TProjection> typeInfo, CancellationToken cancellationToken) =>
		ExecuteAsync(destination => GeneratedFactory().OpenReader(destination, typeInfo), (reader, token) => reader.GetByIdAsync(id, token), cancellationToken);

	/// <summary>Queries the active generation using the conventional serializer.</summary>
	/// <param name="filters">Filters compatible with every explicitly supported generation.</param>
	/// <param name="options">Sorting and pagination.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>Matching projections.</returns>
	[RequiresUnreferencedCode("The conventional reader requires reflection. Supply generated metadata for trimming.")]
	[RequiresDynamicCode("The conventional reader can require dynamic code. Supply generated metadata for NativeAOT.")]
	public Task<IReadOnlyList<TProjection>> QueryAsync(IDictionary<string, object>? filters, QueryOptions? options, CancellationToken cancellationToken) =>
		ExecuteAsync(destination => LegacyFactory().OpenReader(destination), (reader, token) => reader.QueryAsync(filters, options, token), cancellationToken);

	/// <summary>Queries the active generation using generated metadata.</summary>
	/// <param name="filters">Filters compatible with every explicitly supported generation.</param>
	/// <param name="options">Sorting and pagination.</param>
	/// <param name="typeInfo">Generated metadata for the supported read schema.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>Matching projections.</returns>
	public Task<IReadOnlyList<TProjection>> QueryAsync(IDictionary<string, object>? filters, QueryOptions? options,
		JsonTypeInfo<TProjection> typeInfo, CancellationToken cancellationToken) =>
		ExecuteAsync(destination => GeneratedFactory().OpenReader(destination, typeInfo), (reader, token) => reader.QueryAsync(filters, options, token), cancellationToken);

	/// <summary>Counts the active generation using the conventional provider capability.</summary>
	/// <param name="filters">Filters compatible with every explicitly supported generation.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>The matching count.</returns>
	[RequiresUnreferencedCode("Opening a conventional reader requires reflection. Supply generated metadata for trimming.")]
	[RequiresDynamicCode("Opening a conventional reader can require dynamic code. Supply generated metadata for NativeAOT.")]
	public Task<long> CountAsync(IDictionary<string, object>? filters, CancellationToken cancellationToken) =>
		ExecuteAsync(destination => LegacyFactory().OpenReader(destination), (reader, token) => reader.CountAsync(filters, token), cancellationToken);

	/// <summary>Counts the active generation through the generated-reader capability.</summary>
	/// <param name="filters">Filters compatible with every explicitly supported generation.</param>
	/// <param name="typeInfo">Generated metadata for the supported read schema.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>The matching count.</returns>
	public Task<long> CountAsync(IDictionary<string, object>? filters, JsonTypeInfo<TProjection> typeInfo, CancellationToken cancellationToken) =>
		ExecuteAsync(destination => GeneratedFactory().OpenReader(destination, typeInfo), (reader, token) => reader.CountAsync(filters, token), cancellationToken);

	private IProjectionGenerationReaderFactory<TProjection> LegacyFactory() =>
		_store.GetService(typeof(IProjectionGenerationReaderFactory<TProjection>)) as IProjectionGenerationReaderFactory<TProjection>
		?? throw new NotSupportedException("The configured projection store does not provide generation readers. Its outer decorators must preserve this capability.");

	private IAotProjectionGenerationReaderFactory<TProjection> GeneratedFactory() =>
		_store.GetService(typeof(IAotProjectionGenerationReaderFactory<TProjection>)) as IAotProjectionGenerationReaderFactory<TProjection>
		?? throw new NotSupportedException("The configured projection store does not provide generated-metadata generation readers. Reflective fallback is not allowed.");

	private async Task<TResult> ExecuteAsync<TResult>(Func<string, IProjectionGenerationReader<TProjection>> open,
		Func<IProjectionGenerationReader<TProjection>, CancellationToken, Task<TResult>> read, CancellationToken cancellationToken)
	{
		for (var attempt = 0; attempt < 3; attempt++)
		{
			cancellationToken.ThrowIfCancellationRequested();
			var before = await ReadGenerationAsync(cancellationToken).ConfigureAwait(false)
				?? throw new InvalidOperationException($"Reader '{_readerName}' has no active generation. Initialize it through the cutover gate after population.");
			if (!_compatibleKeys.Contains(before.Member.ProjectionKey))
			{
				throw new InvalidOperationException($"Reader '{_readerName}' does not support active projection schema '{before.Member.ProjectionKey}'. Deploy a compatible reader before cutover.");
			}
			var boundary = ProjectionTenantValidation.Read(_store);
			await using var reader = open(before.Member.Destination);
            var resource = await ReadStorageAsync(null, reader.GetDestinationScopeAsync, cancellationToken).ConfigureAwait(false);
            var actualScope = ProjectionTenantValidation.Scope(boundary, resource);
            if (string.IsNullOrWhiteSpace(before.Member.DestinationScope)
                || !string.Equals(before.Member.DestinationScope, actualScope, StringComparison.Ordinal))
            {
                throw new InvalidOperationException($"Reader '{_readerName}' cannot verify the active generation's storage scope against its configured client.");
            }
            var value = await ReadStorageAsync(resource, token => new ValueTask<TResult>(read(reader, token)), cancellationToken).ConfigureAwait(false);
			var after = await ReadGenerationAsync(cancellationToken).ConfigureAwait(false);
			if (after?.Revision == before.Revision && after.Member.BindingId == before.Member.BindingId
                && ProjectionTenantValidation.Read(_store) == boundary)
			{
				return value;
			}
		}
		throw new InvalidOperationException($"Reader '{_readerName}' changed generation during three consecutive reads. Retry the operation.");
	}
    private ValueTask<ProjectionGeneration?> ReadGenerationAsync(CancellationToken token) =>
        ReadStorageAsync(SourceResource?.Invoke(), ct => new ValueTask<ProjectionGeneration?>(
            _generations.ReadGenerationAsync(_subscriptionName, _readerName, ct)), token);

    private ValueTask<TResult> ReadStorageAsync<TResult>(string? resource, Func<CancellationToken, ValueTask<TResult>> read, CancellationToken token) =>
        Capacity is { } capacity ? capacity.ExecuteAsync(resource, ProjectionCapacityKind.Read, read, token) : read(token);

}
