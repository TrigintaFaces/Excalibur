// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Transport.Kafka;

/// <summary>
/// Specifies the processing guarantee level for Kafka stream processing.
/// </summary>
public enum ProcessingGuarantee
{
	/// <summary>
	/// At-least-once processing. Messages may be processed more than once
	/// in the event of failures, but no messages will be lost.
	/// This is the default and most common guarantee level.
	/// </summary>
	AtLeastOnce = 0,

	/// <summary>
	/// Exactly-once processing using Kafka transactions. Ensures each message
	/// is processed exactly once, even in the event of failures. Requires
	/// idempotent producers and transactional consumers.
	/// </summary>
	ExactlyOnce = 1,
}
