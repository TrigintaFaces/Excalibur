// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Abstractions.Diagnostics;

/// <summary>
/// High-level interface for collecting message processing metrics.
/// This is the canonical interface for domain-specific message metrics.
/// </summary>
public interface IMessageMetrics
{
	/// <summary>
	/// Records that a message has been processed (synchronous).
	/// </summary>
	/// <param name="messageType"> The type of message processed. </param>
	/// <param name="duration"> The processing duration. </param>
	void RecordMessageProcessed(string messageType, TimeSpan duration);

	/// <summary>
	/// Records that a message processing failed (synchronous).
	/// </summary>
	/// <param name="messageType"> The type of message that failed. </param>
	/// <param name="errorMessage"> The error that occurred. </param>
	void RecordMessageFailed(string messageType, string errorMessage);

	/// <summary>
	/// Records that a message has been processed (asynchronous).
	/// </summary>
	/// <param name="context"> Processing context. </param>
	/// <param name="duration"> Processing duration. </param>
	/// <param name="success"> Whether processing was successful. </param>
	/// <param name="cancellationToken"> Cancellation token. </param>
	/// <returns> A task representing the metrics recording operation. </returns>
	Task RecordMessageProcessedAsync(
		object context,
		TimeSpan duration,
		bool success,
		CancellationToken cancellationToken);
}
