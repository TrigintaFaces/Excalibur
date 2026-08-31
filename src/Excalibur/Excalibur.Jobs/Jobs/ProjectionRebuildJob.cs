// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics.CodeAnalysis;

using Excalibur.EventSourcing;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Excalibur.Jobs.Jobs;

/// <summary>
/// Background job that triggers a full projection rebuild via <see cref="IMaterializedViewProcessor"/>.
/// </summary>
/// <remarks>
/// <para>
/// Schedule this job periodically to rebuild materialized views from event history.
/// Uses <see cref="IMaterializedViewProcessor.RebuildAsync"/> which replays all events
/// and regenerates projection data.
/// </para>
/// <para>
/// <b>Usage:</b>
/// <code>
/// configurator.AddJob&lt;ProjectionRebuildJob&gt;("0 0 3 * * ?"); // Daily at 3 AM
/// </code>
/// </para>
/// <para>
/// <b>Caution:</b> Rebuild is a long-running operation that clears and regenerates
/// all materialized views. Schedule during low-traffic periods.
/// </para>
/// </remarks>
public sealed class ProjectionRebuildJob(
	IServiceScopeFactory scopeFactory,
	ILogger<ProjectionRebuildJob> logger)
	: IBackgroundJob
{
	private readonly IServiceScopeFactory _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
	private readonly ILogger<ProjectionRebuildJob> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

	/// <inheritdoc />
	[UnconditionalSuppressMessage(
		"Trimming",
		"IL2026:RequiresUnreferencedCode",
		Justification =
			"This job drives a materialized view rebuild, which serializes view types reflectively. " +
			"The job is therefore not trim-safe by contract, and IBackgroundJob.ExecuteAsync is kept clean " +
			"so hosts composing trim-safe jobs are not warned about a condition they cannot act on.")]
	[UnconditionalSuppressMessage(
		"AOT",
		"IL3050:RequiresDynamicCode",
		Justification =
			"This job drives a materialized view rebuild, which serializes view types reflectively. " +
			"The job is therefore not AOT-safe by contract; the package does not claim AOT compatibility.")]
	public async Task ExecuteAsync(CancellationToken cancellationToken)
	{
		ProjectionRebuildJobLog.JobStarting(_logger);

		await using var scope = _scopeFactory.CreateAsyncScope();
		var processor = scope.ServiceProvider.GetService<IMaterializedViewProcessor>();

		if (processor is null)
		{
			ProjectionRebuildJobLog.ProcessorMissing(_logger);
			return;
		}

		try
		{
			await processor.RebuildAsync(cancellationToken).ConfigureAwait(false);
			ProjectionRebuildJobLog.JobCompleted(_logger);
		}
		catch (Exception ex)
		{
			ProjectionRebuildJobLog.JobFailed(_logger, ex);
			throw;
		}
	}
}
