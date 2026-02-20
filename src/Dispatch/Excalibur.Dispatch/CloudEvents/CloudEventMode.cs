// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.CloudEvents;

/// <summary>
/// Defines the CloudEvents serialization Mode.
/// </summary>
public enum CloudEventMode
{
	/// <summary>
	/// Structured Mode using application/cloudevents+json content type.
	/// </summary>
	Structured = 0,

	/// <summary>
	/// Binary Mode mapping CE attributes to transport headers/attributes.
	/// </summary>
	Binary = 1,
}
