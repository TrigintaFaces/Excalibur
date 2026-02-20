// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Dispatch.Abstractions;

using Excalibur.Jobs.Abstractions;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

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
	public async Task ExecuteAsync(CancellationToken cancellationToken)
	{
		OutboxProcessorJobLog.JobStarting(_logger);

		using var scope = _scopeFactory.CreateScope();
		var outbox = scope.ServiceProvider.GetService<IOutboxDispatcher>();

		if (outbox == null)
		{
			OutboxProcessorJobLog.OutboxMissing(_logger);
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
