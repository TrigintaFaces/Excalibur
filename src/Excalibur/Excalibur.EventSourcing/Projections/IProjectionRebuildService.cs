// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.EventSourcing.Projections;

/// <summary>
/// Provides projection rebuilding capabilities for replaying events and
/// regenerating projection state from scratch.
/// </summary>
/// <remarks>
/// <para>
/// Projection rebuilding is necessary when:
/// <list type="bullet">
/// <item>A projection's event handlers have been modified</item>
/// <item>A projection has become corrupted</item>
/// <item>A new projection type is introduced for existing events</item>
/// </list>
/// </para>
/// <para>
/// The rebuild process replays all events through the projection handlers,
/// replacing the existing projection state. Use <see cref="GetStatusAsync"/>
/// or <see cref="GetAllStatusesAsync"/> to monitor progress.
/// </para>
/// </remarks>
public interface IProjectionRebuildService
{
	/// <summary>
	/// Rebuilds a projection by replaying all events through its handlers.
	/// </summary>
	/// <typeparam name="TProjection">The projection type to rebuild.</typeparam>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>A task representing the asynchronous rebuild operation.</returns>
	Task RebuildAsync<TProjection>(CancellationToken cancellationToken)
		where TProjection : class, new();

	/// <summary>
	/// Gets the current rebuild status for a specific projection type.
	/// </summary>
	/// <typeparam name="TProjection">The projection type to query.</typeparam>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>The rebuild status for the specified projection, or a status with
	/// <see cref="ProjectionRebuildState.Idle"/> if no rebuild has been initiated for this type.</returns>
	Task<ProjectionRebuildStatus> GetStatusAsync<TProjection>(CancellationToken cancellationToken)
		where TProjection : class;

	/// <summary>
	/// Gets the current rebuild status for all projections that have been rebuilt
	/// or are currently rebuilding.
	/// </summary>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>A read-only collection of rebuild statuses. Empty if no rebuilds have been initiated.</returns>
	Task<IReadOnlyList<ProjectionRebuildStatus>> GetAllStatusesAsync(CancellationToken cancellationToken);
}
