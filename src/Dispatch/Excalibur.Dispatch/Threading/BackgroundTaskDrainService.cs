// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch.Diagnostics;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Excalibur.Dispatch.Threading;

/// <summary>
/// Awaits work started through <see cref="BackgroundTaskRunner"/> when the host stops gracefully.
/// </summary>
/// <remarks>
/// <para>
/// <b>WHY THIS EXISTS.</b> Background-executed messages were started on the thread pool and never tracked,
/// so a deploy or a scale-in abandoned them — after the caller had already been told the dispatch
/// succeeded. The consumer was left with a message that was never processed and no record that it was
/// lost, which is the worst shape a failure can take: silent, and indistinguishable from success.
/// </para>
/// <para>
/// <b>THE BUDGET IS THE HOST'S, NOT OURS.</b> <see cref="IHostedService.StopAsync"/> is already handed a
/// token bounded by <c>HostOptions.ShutdownTimeout</c> (30 seconds by default), so passing it straight to
/// the drain gives a bounded wait without inventing a second timeout for an operator to discover, tune and
/// disagree with. Bounded is the operative word — a drain that can hang shutdown indefinitely would be a
/// worse defect than the one it fixes.
/// </para>
/// <para>
/// <b>WHAT THIS DOES NOT MAKE TRUE.</b> A graceful drain is not durability. An abrupt termination, an
/// out-of-memory kill or a node eviction still loses in-flight work, and that is inherent to executing
/// in-process rather than a gap left open. Work that must survive a crash belongs in the outbox, which is
/// what the outbox is for. Background execution means decoupled, not durable.
/// </para>
/// </remarks>
/// <param name="logger">Reports how much work the drain waited for, so an operator can size the budget.</param>
internal sealed partial class BackgroundTaskDrainService(ILogger<BackgroundTaskDrainService> logger) : IHostedService
{
	private readonly ILogger<BackgroundTaskDrainService> _logger =
		logger ?? throw new ArgumentNullException(nameof(logger));

	/// <inheritdoc />
	/// <remarks>Nothing to start: this service exists only for its stop behaviour.</remarks>
	public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

	/// <inheritdoc />
	public async Task StopAsync(CancellationToken cancellationToken)
	{
		var (waited, abandoned) = await BackgroundTaskRunner.DrainAsync(cancellationToken).ConfigureAwait(false);

		if (abandoned > 0)
		{
			LogBackgroundWorkAbandoned(abandoned, waited);
		}
		else if (waited > 0)
		{
			LogDrainedBackgroundWork(waited);
		}
	}

	[LoggerMessage(CoreEventId.BackgroundWorkDrainedOnShutdown, LogLevel.Information,
		"Waited for {Waited} in-flight background task(s) at shutdown; all of them finished within the host's shutdown budget.")]
	private partial void LogDrainedBackgroundWork(int waited);

	[LoggerMessage(CoreEventId.BackgroundWorkAbandonedOnShutdown, LogLevel.Error,
		"The host's shutdown budget elapsed with {Abandoned} of {Waited} background task(s) still running. That work is lost: background execution is in-process and best-effort, not durable. Raise HostOptions.ShutdownTimeout if the work is expected to finish, or route work that must survive a restart through the outbox.")]
	private partial void LogBackgroundWorkAbandoned(int abandoned, int waited);
}
