// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Dispatch;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using System.Diagnostics.CodeAnalysis;

namespace Excalibur.Jobs.Jobs;

/// <summary>
/// Background job for processing outbox messages.
/// </summary>
public sealed class OutboxProcessorJob(
	IServiceScopeFactory scopeFactory,
	ILogger<OutboxProcessorJob> logger)
	: IBackgroundJob
{
	private readonly IServiceScopeFactory _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
	private readonly ILogger<OutboxProcessorJob> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

	/// <inheritdoc />
	[UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "Bucket D: the outbox drain reaches the reflective serializer, but IBackgroundJob.ExecuteAsync has 13 implementations of which only this one touches the outbox, so annotating the interface would mislabel twelve unrelated jobs. Tracked for a source-generated outbox serialization seam.")]
	[UnconditionalSuppressMessage("AOT", "IL3050", Justification = "Bucket D: the outbox drain reaches the reflective serializer, but IBackgroundJob.ExecuteAsync has 13 implementations of which only this one touches the outbox, so annotating the interface would mislabel twelve unrelated jobs. Tracked for a source-generated outbox serialization seam.")]
	public async Task ExecuteAsync(CancellationToken cancellationToken)
	{
		OutboxProcessorJobLog.JobStarting(_logger);

		await using var scope = _scopeFactory.CreateAsyncScope();
		var outbox = scope.ServiceProvider.GetService<IOutboxDispatcher>();

		if (outbox == null)
		{
			OutboxProcessorJobLog.OutboxMissing(_logger);
			return;
		}

		// Fail-closed leadership gate. When an IProcessingGate is registered (e.g. via WithLeaderElection),
		// only the instance the gate authorizes runs the dispatch cycle; the ILeaderProcessingGate impl
		// fail-closes on a null leadership snapshot, so a lost/absent tenure never dispatches. Absent a gate
		// (single-instance / no leader election) the job dispatches unconditionally, exactly as before.
		var gate = scope.ServiceProvider.GetService<IProcessingGate>();
		if (gate is not null && !gate.ShouldProcess)
		{
			OutboxProcessorJobLog.SkippedNotLeader(_logger);
			return;
		}

		try
		{
			// Generate a unique dispatcher ID for this job instance
			var dispatcherId = $"job-{Environment.MachineName}-{Guid.NewGuid():N}";

			// Run the outbox dispatch process
			var processedCount = await outbox.RunOutboxDispatchAsync(dispatcherId, cancellationToken).ConfigureAwait(false);

			if (processedCount > 0)
			{
				OutboxProcessorJobLog.JobCompleted(_logger, processedCount);
			}
			else
			{
				OutboxProcessorJobLog.NoMessages(_logger);
			}
		}
		catch (Exception ex)
		{
			OutboxProcessorJobLog.JobFailed(_logger, ex);
			throw;
		}
	}
}
