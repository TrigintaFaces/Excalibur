// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Transport.Azure;

/// <summary>
/// Collects and tracks metrics for Azure Storage Queue operations.
/// </summary>
public interface IQueueMetricsCollector
{
	/// <summary>
	/// Records a message processing operation.
	/// </summary>
	/// <param name="processingTimeMs"> The time taken to process the message in milliseconds. </param>
	/// <param name="success"> Whether the processing was successful. </param>
	/// <param name="messageType"> The type of message processed. </param>
	void RecordMessageProcessed(double processingTimeMs, bool success, string? messageType = null);

	/// <summary>
	/// Records a batch processing operation.
	/// </summary>
	/// <param name="batchSize"> The number of messages in the batch. </param>
	/// <param name="processingTimeMs"> The time taken to process the batch in milliseconds. </param>
	/// <param name="successCount"> The number of successfully processed messages. </param>
	/// <param name="errorCount"> The number of failed messages. </param>
	void RecordBatchProcessed(int batchSize, double processingTimeMs, int successCount, int errorCount);

	/// <summary>
	/// Records a queue receive operation.
	/// </summary>
	/// <param name="messageCount"> The number of messages received. </param>
	/// <param name="receiveTimeMs"> The time taken for the receive operation in milliseconds. </param>
	void RecordReceiveOperation(int messageCount, double receiveTimeMs);

	/// <summary>
	/// Records a message delete operation.
	/// </summary>
	/// <param name="success"> Whether the delete operation was successful. </param>
	/// <param name="deleteTimeMs"> The time taken for the delete operation in milliseconds. </param>
	void RecordDeleteOperation(bool success, double deleteTimeMs);

	/// <summary>
	/// Records a visibility timeout update operation.
	/// </summary>
	/// <param name="success"> Whether the update operation was successful. </param>
	/// <param name="updateTimeMs"> The time taken for the update operation in milliseconds. </param>
	void RecordVisibilityTimeoutUpdate(bool success, double updateTimeMs);

	/// <summary>
	/// Records queue health status information.
	/// </summary>
	/// <param name="queueName"> The name of the queue. </param>
	/// <param name="approximateMessageCount"> The approximate number of messages in the queue. </param>
	/// <param name="isHealthy"> Whether the queue is considered healthy. </param>
	void RecordQueueHealth(string queueName, long approximateMessageCount, bool isHealthy);

	/// <summary>
	/// Gets the current metrics snapshot.
	/// </summary>
	/// <returns> A snapshot of current metrics. </returns>
	QueueMetricsSnapshot GetMetricsSnapshot();
}
