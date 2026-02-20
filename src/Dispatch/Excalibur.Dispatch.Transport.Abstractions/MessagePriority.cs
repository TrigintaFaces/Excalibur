// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Transport;

/// <summary>
/// Message priority levels for publishing operations.
/// </summary>
public enum MessagePriority
{
	Low = 0,
	Normal = 1,
	High = 2,
	Critical = 3,
}
