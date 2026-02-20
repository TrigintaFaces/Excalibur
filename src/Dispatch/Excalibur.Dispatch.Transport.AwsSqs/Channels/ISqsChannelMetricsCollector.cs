// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Transport.Aws;

/// <summary>
/// Interface for collecting SQS channel metrics.
/// </summary>
public interface ISqsChannelMetricsCollector
{
	void RecordMessageReceived(string queueUrl, TimeSpan processingTime);

	void RecordMessageSent(string queueUrl, TimeSpan sendTime);

	void RecordError(string queueUrl, string errorType);

	/// <summary>
	/// </summary>
	/// <returns> A <see cref="Task{TResult}" /> representing the result of the asynchronous operation. </returns>
	Task<SqsChannelMetricsSummary> GetSummaryAsync();
}
