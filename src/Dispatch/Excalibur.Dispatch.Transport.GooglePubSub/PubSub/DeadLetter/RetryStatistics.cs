// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Transport.Google;

internal sealed class RetryStatistics
{
	public long TotalAttempts { get; set; }

	public long SuccessfulAttempts { get; set; }

	public long FailedAttempts { get; set; }

	public TimeSpan TotalDuration { get; set; }

	public TimeSpan AverageDuration { get; set; }

	public double AverageRetryCount { get; set; }
}
