// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Concurrent;

using Excalibur.Data.InMemory.Diagnostics;
using Excalibur.Data.Observability;
using Excalibur.Dispatch;
using Excalibur.Dispatch.Diagnostics;
using Excalibur.Domain.Model;
using Excalibur.EventSourcing;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.Data.InMemory.Snapshots;

/// <summary>
/// In-memory implementation of <see cref="ISnapshotStore"/> for testing and development.
/// </summary>
/// <remarks>
/// <para>
/// This implementation provides thread-safe snapshot storage using ConcurrentDictionary.
/// Snapshots are keyed by a composite of (AggregateId, AggregateType).
/// </para>
/// <para>
/// This store is intended for testing scenarios only. Data is lost on application restart.
/// </para>
/// <para>
/// Uses ValueTask for synchronous completion optimization.
/// In-memory operations complete synchronously without allocation overhead.
/// </para>
/// </remarks>
public sealed partial class InMemorySnapshotStore : ISnapshotStore, IAsyncDisposable, IDisposable
{
	private readonly ConcurrentDictionary<string, ISnapshot> _snapshots = new(StringComparer.Ordinal);
	private readonly InMemorySnapshotOptions _options;
	private readonly ILogger<InMemorySnapshotStore> _logger;
	private readonly ITenantContext? _tenantContext;
	private volatile bool _disposed;

	/// <summary>
	/// Initializes a new instance of the <see cref="InMemorySnapshotStore"/> class.
	/// </summary>
	/// <param name="options">The configuration options.</param>
	/// <param name="logger">The logger instance.</param>
	/// <param name="tenantContext">
	/// The ambient tenant context, or <see langword="null"/> in a single-tenant host. When supplied, the
	/// tenant becomes part of every snapshot key, so two tenants holding the same aggregate identifier
	/// occupy separate entries instead of overwriting one another.
	/// </param>
	public InMemorySnapshotStore(
		IOptions<InMemorySnapshotOptions> options,
		ILogger<InMemorySnapshotStore> logger,
		ITenantContext? tenantContext = null)
	{
		ArgumentNullException.ThrowIfNull(options);
		ArgumentNullException.ThrowIfNull(logger);

		_options = options.Value;
		_logger = logger;
		_tenantContext = tenantContext;
	}

	/// <inheritdoc/>
	public ValueTask<ISnapshot?> GetLatestSnapshotAsync(
		string aggregateId,
		string aggregateType,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(aggregateId);
		ArgumentException.ThrowIfNullOrWhiteSpace(aggregateType);
		ObjectDisposedException.ThrowIf(_disposed, this);

		var stopwatch = ValueStopwatch.StartNew();
		var result = WriteStoreTelemetry.Results.Success;

		var key = GetKey(aggregateId, aggregateType);

		if (_snapshots.TryGetValue(key, out var snapshot))
		{
			LogSnapshotRetrieved(aggregateId, aggregateType, snapshot.Version);
			WriteStoreTelemetry.RecordOperation(
				WriteStoreTelemetry.Stores.SnapshotStore,
				WriteStoreTelemetry.Providers.InMemory,
				"load",
				result,
				stopwatch.Elapsed);
			return new ValueTask<ISnapshot?>(snapshot);
		}

		result = WriteStoreTelemetry.Results.NotFound;
		LogSnapshotNotFound(aggregateId, aggregateType);
		WriteStoreTelemetry.RecordOperation(
			WriteStoreTelemetry.Stores.SnapshotStore,
			WriteStoreTelemetry.Providers.InMemory,
			"load",
			result,
			stopwatch.Elapsed);
		return new ValueTask<ISnapshot?>(result: null);
	}

	/// <inheritdoc/>
	public ValueTask SaveSnapshotAsync(
		ISnapshot snapshot,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(snapshot);
		ObjectDisposedException.ThrowIf(_disposed, this);

		var stopwatch = ValueStopwatch.StartNew();
		var result = WriteStoreTelemetry.Results.Success;

		var key = GetKey(snapshot.AggregateId, snapshot.AggregateType);

		// Enforce capacity limits
		if (_options.MaxSnapshots > 0 && _snapshots.Count >= _options.MaxSnapshots)
		{
			EvictOldestSnapshot();
		}

		// Upsert - replace if exists (always keep latest)
		_ = _snapshots.AddOrUpdate(
			key,
			snapshot,
			(_, existing) => snapshot.Version > existing.Version ? snapshot : existing);

		LogSnapshotSaved(snapshot.AggregateId, snapshot.AggregateType, snapshot.Version);

		WriteStoreTelemetry.RecordOperation(
			WriteStoreTelemetry.Stores.SnapshotStore,
			WriteStoreTelemetry.Providers.InMemory,
			"save",
			result,
			stopwatch.Elapsed);
		return default;
	}

	/// <inheritdoc/>
	public ValueTask DeleteSnapshotsAsync(
		string aggregateId,
		string aggregateType,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(aggregateId);
		ArgumentException.ThrowIfNullOrWhiteSpace(aggregateType);
		ObjectDisposedException.ThrowIf(_disposed, this);

		var stopwatch = ValueStopwatch.StartNew();
		var result = WriteStoreTelemetry.Results.Success;

		var key = GetKey(aggregateId, aggregateType);

		if (_snapshots.TryRemove(key, out _))
		{
			LogSnapshotsDeleted(aggregateId, aggregateType);
		}
		else
		{
			result = WriteStoreTelemetry.Results.NotFound;
		}

		WriteStoreTelemetry.RecordOperation(
			WriteStoreTelemetry.Stores.SnapshotStore,
			WriteStoreTelemetry.Providers.InMemory,
			"delete",
			result,
			stopwatch.Elapsed);
		return default;
	}

	/// <inheritdoc/>
	public ValueTask DeleteSnapshotsOlderThanAsync(
		string aggregateId,
		string aggregateType,
		long olderThanVersion,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(aggregateId);
		ArgumentException.ThrowIfNullOrWhiteSpace(aggregateType);
		ObjectDisposedException.ThrowIf(_disposed, this);

		var stopwatch = ValueStopwatch.StartNew();
		var result = WriteStoreTelemetry.Results.Success;

		var key = GetKey(aggregateId, aggregateType);

		// In memory store only keeps latest, so remove if version is below threshold
		if (_snapshots.TryGetValue(key, out var snapshot) && snapshot.Version < olderThanVersion)
		{
			if (_snapshots.TryRemove(key, out _))
			{
				LogSnapshotsDeletedOlderThan(aggregateId, aggregateType, olderThanVersion);
			}
		}
		else if (snapshot == null)
		{
			result = WriteStoreTelemetry.Results.NotFound;
		}

		WriteStoreTelemetry.RecordOperation(
			WriteStoreTelemetry.Stores.SnapshotStore,
			WriteStoreTelemetry.Providers.InMemory,
			"delete_older_than",
			result,
			stopwatch.Elapsed);
		return default;
	}

	/// <inheritdoc/>
	public void Dispose()
	{
		if (_disposed)
		{
			return;
		}

		_snapshots.Clear();
		_disposed = true;
	}

	/// <inheritdoc/>
	public ValueTask DisposeAsync()
	{
		Dispose();
		return ValueTask.CompletedTask;
	}

	/// <summary>
	/// Builds the key identifying a single aggregate's snapshot, including the tenant when the host is
	/// multi-tenant. Every read, save, and delete path routes through this one method, so the tenant
	/// cannot be applied to some operations and forgotten on others.
	/// </summary>
	/// <remarks>
	/// The tenant segment is added only when tenancy is active, so a single-tenant host's keys keep their
	/// existing shape. This mirrors the Redis store, and the tenant-first ordering matches the convention
	/// the grant stores already use across the document providers.
	/// </remarks>
	private string GetKey(string aggregateId, string aggregateType)
	{
		var scope = TenantScope.FromContext(_tenantContext);
		return scope.IsScoped
			? $"t:{scope.TenantId}:{aggregateType}:{aggregateId}"
			: $"{aggregateType}:{aggregateId}";
	}

	/// <summary>
	/// Evicts the oldest snapshot when the configured cap is reached.
	/// </summary>
	/// <remarks>
	/// <para>
	/// Removes by the entry's OWN key rather than rebuilding one from the aggregate. <see cref="GetKey"/>
	/// resolves the AMBIENT tenant — the tenant performing the save — while the eviction victim may belong
	/// to a different tenant entirely. Reconstructing the key would therefore compute the saver's key for
	/// the victim's aggregate, and in one shared dictionary that either removes the WRONG tenant's snapshot
	/// or removes nothing at all and lets the cap grow unbounded. The entry already carries its key; use it.
	/// </para>
	/// <para>
	/// The victim is selected across ALL tenants, and that is deliberate. The cap bounds one shared
	/// dictionary, so a per-tenant scan would let a single tenant fill the store and starve the others
	/// while the global cap silently stopped being enforced. Cross-tenant eviction is safe here because a
	/// snapshot is a CACHE, not a record: the repository loads from version 0 when none exists and the
	/// snapshot manager is optional, so evicting another tenant's entry costs that tenant a replay and
	/// never their data. Do not "fix" this into a per-tenant scan — it would trade a latency cost for a
	/// memory-bound failure.
	/// </para>
	/// </remarks>
	private void EvictOldestSnapshot()
	{
		var oldest = _snapshots
			.OrderBy(entry => entry.Value.CreatedAt)
			.FirstOrDefault();

		if (oldest.Key is not null)
		{
			_ = _snapshots.TryRemove(oldest.Key, out _);
			LogSnapshotEvicted(oldest.Value.AggregateId, oldest.Value.AggregateType);
		}
	}

	// High-performance logging using source generators
	[LoggerMessage(DataInMemoryEventId.SnapshotRetrieved, LogLevel.Debug,
		"Retrieved snapshot for aggregate {AggregateId} of type {AggregateType} at version {Version}")]
	private partial void LogSnapshotRetrieved(string aggregateId, string aggregateType, long version);

	[LoggerMessage(DataInMemoryEventId.SnapshotNotFound, LogLevel.Debug,
		"No snapshot found for aggregate {AggregateId} of type {AggregateType}")]
	private partial void LogSnapshotNotFound(string aggregateId, string aggregateType);

	[LoggerMessage(DataInMemoryEventId.SnapshotSaved, LogLevel.Debug,
		"Saved snapshot for aggregate {AggregateId} of type {AggregateType} at version {Version}")]
	private partial void LogSnapshotSaved(string aggregateId, string aggregateType, long version);

	[LoggerMessage(DataInMemoryEventId.SnapshotDeleted, LogLevel.Debug,
		"Deleted snapshots for aggregate {AggregateId} of type {AggregateType}")]
	private partial void LogSnapshotsDeleted(string aggregateId, string aggregateType);

	[LoggerMessage(DataInMemoryEventId.SnapshotOlderDeleted, LogLevel.Debug,
		"Deleted snapshots older than version {OlderThanVersion} for aggregate {AggregateId} of type {AggregateType}")]
	private partial void LogSnapshotsDeletedOlderThan(string aggregateId, string aggregateType, long olderThanVersion);

	[LoggerMessage(DataInMemoryEventId.SnapshotEvicted, LogLevel.Debug,
		"Evicted oldest snapshot for aggregate {AggregateId} of type {AggregateType}")]
	private partial void LogSnapshotEvicted(string aggregateId, string aggregateType);
}
