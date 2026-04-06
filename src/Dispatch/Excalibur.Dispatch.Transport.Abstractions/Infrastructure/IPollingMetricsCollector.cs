// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Transport;

/// <summary>
/// Common polling metrics collector interface for transport providers.
/// </summary>
public interface ITransportPollingMetrics
{
	/// <summary>
	/// Records the result of a polling operation.
	/// </summary>
	/// <param name="source">The source identifier for the poll.</param>
	/// <param name="messageCount">The number of messages retrieved during the poll.</param>
	/// <param name="duration">The duration of the polling operation.</param>
	void RecordPoll(string source, int messageCount, TimeSpan duration);

	/// <summary>
	/// Records an error that occurred during a polling operation.
	/// </summary>
	/// <param name="source">The source identifier for the poll.</param>
	/// <param name="exception">The exception that occurred.</param>
	void RecordError(string source, Exception exception);

	/// <summary>
	/// Gets the accumulated polling statistics.
	/// </summary>
	/// <returns>The current <see cref="TransportPollingStatistics"/>.</returns>
	TransportPollingStatistics GetStatistics();
}
