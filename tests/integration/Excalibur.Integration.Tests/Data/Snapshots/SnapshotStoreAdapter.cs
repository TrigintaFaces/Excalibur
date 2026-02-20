// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Domain.Model;
using Excalibur.EventSourcing.Abstractions;

namespace Excalibur.Integration.Tests.Data.Snapshots;

/// <summary>
/// Adapts provider-specific snapshot stores to the common ISnapshotStore interface for conformance testing.
/// </summary>
/// <remarks>
/// This adapter wraps provider implementations that may have additional functionality and
/// ensures they conform to the standard ISnapshotStore contract used by the conformance test suite.
/// </remarks>
internal sealed class SnapshotStoreAdapter : ISnapshotStore
{
	private readonly ISnapshotStore _inner;

	/// <summary>
	/// Initializes a new instance of the <see cref="SnapshotStoreAdapter"/> class.
	/// </summary>
	/// <param name="inner">The snapshot store to adapt.</param>
	public SnapshotStoreAdapter(ISnapshotStore inner)
	{
		_inner = inner ?? throw new ArgumentNullException(nameof(inner));
	}

	/// <inheritdoc/>
	public ValueTask<ISnapshot?> GetLatestSnapshotAsync(
		string aggregateId,
		string aggregateType,
		CancellationToken cancellationToken)
		=> _inner.GetLatestSnapshotAsync(aggregateId, aggregateType, cancellationToken);

	/// <inheritdoc/>
	public ValueTask SaveSnapshotAsync(ISnapshot snapshot, CancellationToken cancellationToken)
		=> _inner.SaveSnapshotAsync(snapshot, cancellationToken);

	/// <inheritdoc/>
	public ValueTask DeleteSnapshotsAsync(
		string aggregateId,
		string aggregateType,
		CancellationToken cancellationToken)
		=> _inner.DeleteSnapshotsAsync(aggregateId, aggregateType, cancellationToken);

	/// <inheritdoc/>
	public ValueTask DeleteSnapshotsOlderThanAsync(
		string aggregateId,
		string aggregateType,
		long olderThanVersion,
		CancellationToken cancellationToken)
		=> _inner.DeleteSnapshotsOlderThanAsync(aggregateId, aggregateType, olderThanVersion, cancellationToken);
}
