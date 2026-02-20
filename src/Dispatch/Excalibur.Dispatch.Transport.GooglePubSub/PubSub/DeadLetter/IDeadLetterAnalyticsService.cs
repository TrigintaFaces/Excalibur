// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Transport.Google;

/// <summary>
/// Service for collecting and analyzing dead letter message statistics.
/// </summary>
public interface IDeadLetterAnalyticsService
{
	/// <summary>
	/// Records a dead letter message for analytics.
	/// </summary>
	/// <param name="messageId"> The message identifier. </param>
	/// <param name="reason"> The reason the message became dead letter. </param>
	/// <param name="cancellationToken"> Cancellation token. </param>
	/// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
	Task RecordDeadLetterAsync(string messageId, string reason, CancellationToken cancellationToken);

	/// <summary>
	/// Gets current dead letter analytics.
	/// </summary>
	/// <param name="cancellationToken"> Cancellation token. </param>
	/// <returns> Current analytics data. </returns>
	Task<DeadLetterAnalytics> GetAnalyticsAsync(CancellationToken cancellationToken);
}
