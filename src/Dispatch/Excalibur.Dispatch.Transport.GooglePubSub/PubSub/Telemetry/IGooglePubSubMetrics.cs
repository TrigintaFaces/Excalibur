// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Transport.Google;

/// <summary>
/// Defines metrics collection for Google Pub/Sub operations.
/// </summary>
public interface IGooglePubSubMetrics
{
	/// <summary>
	/// Records that a message was enqueued for processing.
	/// </summary>
	void MessageEnqueued();

	/// <summary>
	/// Records that a message was dequeued for processing.
	/// </summary>
	/// <param name="queueTime"> Time the message spent in the queue. </param>
	void MessageDequeued(TimeSpan queueTime);

	/// <summary>
	/// Records successful message processing.
	/// </summary>
	/// <param name="duration"> Processing duration. </param>
	void MessageProcessed(TimeSpan duration);

	/// <summary>
	/// Records failed message processing.
	/// </summary>
	void MessageFailed();

	/// <summary>
	/// Records batch creation.
	/// </summary>
	/// <param name="size"> Batch size. </param>
	void BatchCreated(int size);

}
