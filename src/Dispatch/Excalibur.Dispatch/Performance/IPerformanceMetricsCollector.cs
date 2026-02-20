// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Performance;

/// <summary>
/// Collects and tracks performance metrics for the Excalibur framework.
/// </summary>
public interface IPerformanceMetricsCollector
{
	/// <summary>
	/// Records the execution time of a middleware component.
	/// </summary>
	/// <param name="middlewareName"> Name of the middleware component. </param>
	/// <param name="duration"> Time taken for execution. </param>
	/// <param name="success"> Whether execution was successful. </param>
	void RecordMiddlewareExecution(string middlewareName, TimeSpan duration, bool success = true);

	/// <summary>
	/// Records pipeline execution metrics.
	/// </summary>
	/// <param name="middlewareCount"> Number of middleware components in pipeline. </param>
	/// <param name="totalDuration"> Total pipeline execution time. </param>
	/// <param name="memoryAllocated"> Memory allocated during pipeline execution (if available). </param>
	void RecordPipelineExecution(int middlewareCount, TimeSpan totalDuration, long memoryAllocated = 0);

	/// <summary>
	/// Records batch processing performance metrics.
	/// </summary>
	/// <param name="processorType"> Type of processor (Inbox, Outbox, etc.). </param>
	/// <param name="batchSize"> Number of items processed in the batch. </param>
	/// <param name="processingTime"> Time taken to process the batch. </param>
	/// <param name="parallelDegree"> Degree of parallelism used. </param>
	/// <param name="successCount"> Number of successful items. </param>
	/// <param name="failureCount"> Number of failed items. </param>
	void RecordBatchProcessing(string processorType, int batchSize, TimeSpan processingTime,
		int parallelDegree, int successCount, int failureCount);

	/// <summary>
	/// Records handler registry lookup performance.
	/// </summary>
	/// <param name="messageType"> Type of message being looked up. </param>
	/// <param name="lookupTime"> Time taken for handler lookup. </param>
	/// <param name="handlersFound"> Number of handlers found. </param>
	void RecordHandlerLookup(string messageType, TimeSpan lookupTime, int handlersFound);

	/// <summary>
	/// Records queue operation performance.
	/// </summary>
	/// <param name="queueName"> Name of the queue. </param>
	/// <param name="operation"> Type of operation (enqueue, dequeue, etc.). </param>
	/// <param name="itemCount"> Number of items involved in operation. </param>
	/// <param name="duration"> Time taken for the operation. </param>
	/// <param name="queueDepth"> Current queue depth after operation. </param>
	void RecordQueueOperation(string queueName, string operation, int itemCount,
		TimeSpan duration, int queueDepth);

	/// <summary>
	/// Gets a snapshot of current performance metrics.
	/// </summary>
	/// <returns> Performance metrics snapshot. </returns>
	PerformanceSnapshot GetSnapshot();

	/// <summary>
	/// Resets all collected metrics.
	/// </summary>
	void Reset();
}
