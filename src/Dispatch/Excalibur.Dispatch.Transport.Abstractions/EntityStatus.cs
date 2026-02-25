// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Transport;

/// <summary>
/// Represents the status of a messaging entity.
/// </summary>
public enum EntityStatus
{
	/// <summary>
	/// The entity is active.
	/// </summary>
	Active = 0,

	/// <summary>
	/// The entity is disabled.
	/// </summary>
	Disabled = 1,

	/// <summary>
	/// The entity is in receive-only mode.
	/// </summary>
	ReceiveDisabled = 2,

	/// <summary>
	/// The entity is in send-only mode.
	/// </summary>
	SendDisabled = 3,
}
