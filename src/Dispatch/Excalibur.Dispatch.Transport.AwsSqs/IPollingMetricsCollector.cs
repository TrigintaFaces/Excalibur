// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Transport.Aws;

/// <summary>
/// Interface for polling metrics collector.
/// </summary>
public interface IPollingMetricsCollector : IAwsMetricsCollector
{
	/// <summary>
	/// Records polling attempt.
	/// </summary>
	void RecordPollingAttempt(int messagesReceived, TimeSpan duration);

	/// <summary>
	/// Records polling error.
	/// </summary>
	void RecordPollingError(Exception exception);

	/// <summary>
	/// Gets the polling statistics.
	/// </summary>
	PollingStatistics GetStatistics();
}
