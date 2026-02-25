// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Observability.Metrics;

/// <summary>
/// Defines the contract for Dispatch metrics collection.
/// </summary>
public interface IDispatchMetrics
{
	/// <summary>
	/// Records a processed message metric.
	/// </summary>
	/// <param name="messageType"> The type of message processed. </param>
	/// <param name="handlerType"> The type of handler that processed the message. </param>
	/// <param name="tags"> Optional additional tags. </param>
	void RecordMessageProcessed(string messageType, string handlerType, params (string Key, object? Value)[] tags);

	/// <summary>
	/// Records message processing duration.
	/// </summary>
	/// <param name="duration"> The processing duration in milliseconds. </param>
	/// <param name="messageType"> The type of message processed. </param>
	/// <param name="success"> Whether the processing was successful. </param>
	void RecordProcessingDuration(double duration, string messageType, bool success);

	/// <summary>
	/// Records a published message metric.
	/// </summary>
	/// <param name="messageType"> The type of message published. </param>
	/// <param name="destination"> The destination where the message was published. </param>
	void RecordMessagePublished(string messageType, string destination);

	/// <summary>
	/// Records a failed message metric.
	/// </summary>
	/// <param name="messageType"> The type of message that failed. </param>
	/// <param name="errorType"> The type of error that occurred. </param>
	/// <param name="retryAttempt"> The retry attempt number. </param>
	void RecordMessageFailed(string messageType, string errorType, int retryAttempt);

	/// <summary>
	/// Updates the active sessions counter.
	/// </summary>
	/// <param name="delta"> The change in active session count (positive for connect, negative for disconnect). </param>
	void UpdateActiveSessions(int delta);
}
