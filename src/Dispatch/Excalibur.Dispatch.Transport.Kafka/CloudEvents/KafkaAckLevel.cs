// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Transport.Kafka;

/// <summary>
/// Kafka acknowledgment levels.
/// </summary>
public enum KafkaAckLevel
{
	/// <summary>
	/// Fire and forget - no acknowledgment.
	/// </summary>
	None = 0,

	/// <summary>
	/// Wait for leader replica acknowledgment.
	/// </summary>
	Leader = 1,

	/// <summary>
	/// Wait for all in-sync replicas acknowledgment.
	/// </summary>
	All = 2,
}
