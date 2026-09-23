// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

namespace Excalibur.Dispatch.Transport.Kafka;

/// <summary>
/// The outcome of asking <see cref="KafkaPartitionProgress"/> to requeue an offset.
/// </summary>
internal enum KafkaRequeueOutcome
{
	/// <summary>The offset is owed and awaits redelivery; the caller should seek to the reported offset.</summary>
	Requeued,

	/// <summary>The partition was reassigned after the delivery; this caller can no longer seek it.</summary>
	StaleGeneration,

	/// <summary>
	/// The offset was delivered in this tenure and has already been settled, so there is nothing to redeliver.
	/// A caller that settles the same message twice lands here, and that is an idempotent success.
	/// </summary>
	NotOutstanding,

	/// <summary>
	/// The offset has not been delivered in this tenure, so the caller cannot hold a delivery of it. This is
	/// never a success: reporting it as one would confirm a settlement that nothing performed.
	/// </summary>
	NeverDelivered,
}
