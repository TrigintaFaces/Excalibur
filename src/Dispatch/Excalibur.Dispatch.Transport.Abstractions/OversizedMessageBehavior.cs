// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Transport;

/// <summary>
/// Specifies the behavior when a message is too large for a batch.
/// </summary>
public enum OversizedMessageBehavior
{
	/// <summary>
	/// Send the oversized message in its own separate batch.
	/// </summary>
	SendSeparately = 0,

	/// <summary>
	/// Skip the oversized message and continue with others.
	/// </summary>
	Skip = 1,

	/// <summary>
	/// Throw an exception when an oversized message is encountered.
	/// </summary>
	ThrowException = 2,
}
