// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Diagnostics;

namespace Excalibur.Dispatch.Performance;

/// <summary>
/// Middleware that collects performance metrics for message processing. Measures execution time, memory allocation, and success rates
/// across the pipeline.
/// </summary>
/// <remarks> Initializes a new instance of the <see cref="PerformanceMiddleware" /> class. </remarks>
/// <param name="metricsCollector"> The metrics collector to record performance data. </param>
/// <exception cref="ArgumentNullException"> Thrown when metricsCollector is null. </exception>
public sealed class PerformanceMiddleware(IPerformanceMetricsCollector metricsCollector) : IDispatchMiddleware
{
	private readonly IPerformanceMetricsCollector _metricsCollector =
		metricsCollector ?? throw new ArgumentNullException(nameof(metricsCollector));

	/// <inheritdoc />
	public DispatchMiddlewareStage? Stage => DispatchMiddlewareStage.Instrumentation;

	/// <inheritdoc />
	public async ValueTask<IMessageResult> InvokeAsync(
		IDispatchMessage message,
		IMessageContext context,
		DispatchRequestDelegate nextDelegate,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(message);
		ArgumentNullException.ThrowIfNull(context);
		ArgumentNullException.ThrowIfNull(nextDelegate);

		var stopwatch = ValueStopwatch.StartNew();
		var initialMemory = GC.GetTotalMemory(forceFullCollection: false);
		var middlewareCount = GetMiddlewareCount(context);

		var success = false;
		IMessageResult result;

		try
		{
			result = await nextDelegate(message, context, cancellationToken).ConfigureAwait(false);
			success = result.Succeeded;
			return result;
		}
		catch (Exception)
		{
			success = false;
			throw;
		}
		finally
		{
			var finalMemory = GC.GetTotalMemory(forceFullCollection: false);
			var memoryAllocated = Math.Max(0, finalMemory - initialMemory);

			// Record pipeline-level metrics
			_metricsCollector.RecordPipelineExecution(
				middlewareCount,
				stopwatch.Elapsed,
				memoryAllocated);

			// Record performance middleware execution
			_metricsCollector.RecordMiddlewareExecution(
				nameof(PerformanceMiddleware),
				stopwatch.Elapsed,
				success);
		}
	}

	/// <summary>
	/// Attempts to get the middleware count from the context. Falls back to a reasonable estimate if not available.
	/// </summary>
	/// <param name="context"> The message context. </param>
	/// <returns> The estimated middleware count. </returns>
	private static int GetMiddlewareCount(IMessageContext context)
	{
		// Try to get middleware count from context if available
		if (context.Items.TryGetValue("MiddlewareCount", out var countObj) &&
			countObj is int count)
		{
			return count;
		}

		// Default estimate based on typical pipeline depth
		return 5;
	}
}
