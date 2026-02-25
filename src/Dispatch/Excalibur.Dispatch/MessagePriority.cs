// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Messaging;

/// <summary>
/// Message priority levels for dispatching.
/// </summary>
public enum MessagePriority
{
	/// <summary>
	/// Low priority messages that can be processed when resources are available.
	/// </summary>
	Low = 0,

	/// <summary>
	/// Normal priority messages for standard processing order.
	/// </summary>
	Normal = 1,

	/// <summary>
	/// High priority messages that should be processed before normal priority messages.
	/// </summary>
	High = 2,

	/// <summary>
	/// Critical priority messages that require immediate processing.
	/// </summary>
	Critical = 3,
}
