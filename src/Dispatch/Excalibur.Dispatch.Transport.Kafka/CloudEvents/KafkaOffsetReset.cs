// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Transport.Kafka;

/// <summary>
/// Kafka offset reset behavior.
/// </summary>
public enum KafkaOffsetReset
{
	/// <summary>
	/// Start from the earliest available offset.
	/// </summary>
	Earliest = 0,

	/// <summary>
	/// Start from the latest offset (skip existing messages).
	/// </summary>
	Latest = 1,
}
