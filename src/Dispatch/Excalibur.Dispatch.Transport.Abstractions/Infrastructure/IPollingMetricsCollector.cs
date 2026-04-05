// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Transport;

/// <summary>
/// Common polling metrics collector interface for transport providers.
/// </summary>
public interface ITransportPollingMetrics
{
	void RecordPoll(string source, int messageCount, TimeSpan duration);

	void RecordError(string source, Exception exception);

	TransportPollingStatistics GetStatistics();
}
