// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Transport;

/// <summary>
/// Defines when messages should be acknowledged.
/// </summary>
public enum AcknowledgmentMode
{
	/// <summary>
	/// Acknowledge only on successful processing.
	/// </summary>
	OnSuccess = 0,

	/// <summary>
	/// Acknowledge immediately upon receipt.
	/// </summary>
	Immediate = 1,

	/// <summary>
	/// Manual acknowledgment required.
	/// </summary>
	Manual = 2,
}
