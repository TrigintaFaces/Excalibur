// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Transport;

/// <summary>
/// Message priority levels for publishing operations.
/// </summary>
public enum MessagePriority
{
	/// <summary>
	/// Low priority. Messages are processed after all higher-priority messages.
	/// </summary>
	Low = 0,

	/// <summary>
	/// Normal priority. The default priority for messages.
	/// </summary>
	Normal = 1,

	/// <summary>
	/// High priority. Messages are processed before normal and low-priority messages.
	/// </summary>
	High = 2,

	/// <summary>
	/// Critical priority. Messages are processed with the highest urgency.
	/// </summary>
	Critical = 3,
}
