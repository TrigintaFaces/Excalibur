// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Transport.Mqtt;

/// <summary>
/// MQTT quality-of-service level, which determines the delivery guarantee for published messages.
/// </summary>
public enum MqttQualityOfService
{
	/// <summary>QoS 0 — at-most-once (fire and forget); no acknowledgement, messages may be lost.</summary>
	AtMostOnce = 0,

	/// <summary>QoS 1 — at-least-once; acknowledged, but duplicates are possible (handlers must be idempotent).</summary>
	AtLeastOnce = 1,

	/// <summary>QoS 2 — exactly-once; four-way handshake, no loss and no duplication.</summary>
	ExactlyOnce = 2,
}
