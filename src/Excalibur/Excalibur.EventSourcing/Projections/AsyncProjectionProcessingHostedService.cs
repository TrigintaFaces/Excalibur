// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Microsoft.Extensions.Hosting;

namespace Excalibur.EventSourcing.Projections;

/// <summary>
/// Hosts <see cref="AsyncProjectionProcessingHost"/> as a background service, without owning it.
/// </summary>
/// <remarks>
/// <para>
/// <b>This type exists so there is exactly ONE processing host.</b> The host serves two entry points --
/// the background loop and <see cref="IProjectionProcessor"/>'s pull API -- and both must drive the same
/// position cursor. Registering the host itself as <c>IHostedService</c> by type would have the
/// container construct a SECOND instance, so a consumer who both hosted the service and called the pull
/// API would get two independent readers of one subscription, which is the interleaving the checkpoint
/// compare-and-set exists to catch rather than something to create deliberately.
/// </para>
/// <para>
/// <b>A named type rather than a factory delegate, because the container needs to tell registrations
/// apart.</b> <c>TryAddEnumerable</c> deduplicates on the implementation TYPE, and a factory registered
/// against <c>IHostedService</c> erases that type -- the container refuses it as indistinguishable from
/// every other hosted service. This wrapper gives it something to distinguish, so registering projection
/// processing twice still yields one hosted service.
/// </para>
/// </remarks>
/// <param name="host">The single processing host this service runs.</param>
internal sealed class AsyncProjectionProcessingHostedService(AsyncProjectionProcessingHost host) : IHostedService
{
	/// <inheritdoc />
	public Task StartAsync(CancellationToken cancellationToken) => host.StartAsync(cancellationToken);

	/// <inheritdoc />
	public Task StopAsync(CancellationToken cancellationToken) => host.StopAsync(cancellationToken);
}
