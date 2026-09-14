// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Transport.Mqtt;

/// <summary>
/// Provider-specific CloudEvent options for the MQTT transport.
/// </summary>
public sealed class MqttCloudEventOptions
{
	/// <summary>
	/// Gets or sets a value indicating whether outgoing CloudEvent messages should be published
	/// with the MQTT retain flag set.
	/// </summary>
	/// <value><see langword="true"/> to retain the last CloudEvent on the broker for the topic; otherwise <see langword="false"/>.</value>
	public bool Retain { get; set; }
}
