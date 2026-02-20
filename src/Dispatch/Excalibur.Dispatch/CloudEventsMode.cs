// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Messaging;

/// <summary>
/// <summary>
/// Specifies the Cloud Events processing mode.
/// </summary>
/// </summary>
public enum CloudEventsMode
{
	/// <summary>
	/// No Cloud Events processing.
	/// </summary>
	None = 0,

	/// <summary>
	/// Structured Cloud Events mode - events are serialized as complete JSON objects.
	/// </summary>
	Structured = 1,

	/// <summary>
	/// Binary Cloud Events mode - event data is separate from metadata headers.
	/// </summary>
	Binary = 2,
}
