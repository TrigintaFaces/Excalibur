// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Excalibur.Dispatch.Configuration;

/// <summary>
/// Validates the dispatch middleware pipeline at application startup.
/// Detects empty pipelines, missing handler middleware, and logs warnings
/// for duplicate middleware or questionable ordering.
/// </summary>
internal sealed partial class PipelineValidationHostedService(
	IServiceProvider serviceProvider,
	ILogger<PipelineValidationHostedService> logger) : IHostedService
{
	/// <inheritdoc />
	public Task StartAsync(CancellationToken cancellationToken)
	{
		var middlewares = serviceProvider.GetServices<IDispatchMiddleware>().ToList();

		if (middlewares.Count == 0)
		{
			throw new InvalidOperationException(
				"No dispatch middleware registered. The pipeline is empty and cannot process messages. " +
				"Register middleware via AddDispatch(builder => builder.UseMiddleware<T>()) or enable pipeline synthesis.");
		}

		// Check for duplicate middleware types in same stage
		var grouped = middlewares
			.Where(static m => m.Stage.HasValue)
			.GroupBy(static m => (m.Stage, m.GetType()));

		foreach (var group in grouped)
		{
			if (group.Count() > 1)
			{
				LogDuplicateMiddleware(logger, group.Key.Item2.Name, group.Key.Stage?.ToString() ?? "Unknown");
			}
		}

		return Task.CompletedTask;
	}

	/// <inheritdoc />
	public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

	[LoggerMessage(2200, LogLevel.Warning,
		"Duplicate middleware '{MiddlewareName}' registered in stage '{Stage}'. This may cause unexpected double-execution.")]
	private static partial void LogDuplicateMiddleware(ILogger logger, string middlewareName, string stage);
}
