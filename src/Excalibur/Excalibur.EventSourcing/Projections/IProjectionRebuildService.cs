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
/// to monitor progress.
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
	/// Gets the current status of the projection rebuild.
	/// </summary>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>The current rebuild status, or a status with <see cref="ProjectionRebuildState.Idle"/> if no rebuild is active.</returns>
	Task<ProjectionRebuildStatus> GetStatusAsync(CancellationToken cancellationToken);
}
