// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Domain.Model;
using Excalibur.EventSourcing.Abstractions;

namespace Excalibur.EventSourcing.Decorators;

/// <summary>
/// Abstract base class for <see cref="ISnapshotStore"/> decorators.
/// All methods are virtual and forward to the inner store by default.
/// </summary>
/// <remarks>
/// <para>
/// Follows the <c>DelegatingHandler</c> / <c>DelegatingChatClient</c> pattern from Microsoft.
/// Subclasses override only the methods they need to intercept.
/// </para>
/// </remarks>
public abstract class DelegatingSnapshotStore : ISnapshotStore
{
	/// <summary>
	/// Gets the inner snapshot store being decorated.
	/// </summary>
	protected ISnapshotStore Inner { get; }

	/// <summary>
	/// Initializes a new instance of the <see cref="DelegatingSnapshotStore"/> class.
	/// </summary>
	/// <param name="inner">The inner snapshot store to delegate to.</param>
	protected DelegatingSnapshotStore(ISnapshotStore inner)
	{
		Inner = inner ?? throw new ArgumentNullException(nameof(inner));
	}

	/// <inheritdoc />
	public virtual ValueTask<ISnapshot?> GetLatestSnapshotAsync(
		string aggregateId,
		string aggregateType,
		CancellationToken cancellationToken)
		=> Inner.GetLatestSnapshotAsync(aggregateId, aggregateType, cancellationToken);

	/// <inheritdoc />
	public virtual ValueTask SaveSnapshotAsync(
		ISnapshot snapshot,
		CancellationToken cancellationToken)
		=> Inner.SaveSnapshotAsync(snapshot, cancellationToken);

	/// <inheritdoc />
	public virtual ValueTask DeleteSnapshotsAsync(
		string aggregateId,
		string aggregateType,
		CancellationToken cancellationToken)
		=> Inner.DeleteSnapshotsAsync(aggregateId, aggregateType, cancellationToken);

	/// <inheritdoc />
	public virtual ValueTask DeleteSnapshotsOlderThanAsync(
		string aggregateId,
		string aggregateType,
		long olderThanVersion,
		CancellationToken cancellationToken)
		=> Inner.DeleteSnapshotsOlderThanAsync(aggregateId, aggregateType, olderThanVersion, cancellationToken);
}
