// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Transport;

/// <summary>
/// Declares whether a registered transport delivers messages within the current process or across a
/// network boundary. The locality is intrinsic registration metadata, supplied explicitly at
/// registration time and validated fail-closed at startup, so an application that requires a network
/// transport cannot silently start with only in-process transports.
/// </summary>
public enum TransportLocality
{
	/// <summary>
	/// The transport delivers messages within the current process (for example an in-memory or
	/// in-process timer transport). Local transports do not cross a network boundary.
	/// </summary>
	Local = 0,

	/// <summary>
	/// The transport delivers messages across a network boundary to an external broker or service (for
	/// example RabbitMQ, Kafka, Azure Service Bus, AWS SQS, or Google Pub/Sub).
	/// </summary>
	Remote = 1,
}
