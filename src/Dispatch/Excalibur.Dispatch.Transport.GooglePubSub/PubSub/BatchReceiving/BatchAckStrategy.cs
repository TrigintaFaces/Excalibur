// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Transport.Google;

/// <summary>
/// Defines strategies for acknowledging message batches.
/// </summary>
public enum BatchAckStrategy
{
	/// <summary>
	/// Acknowledge messages immediately after successful processing.
	/// </summary>
	OnSuccess = 0,

	/// <summary>
	/// Acknowledge all messages in the batch together after all are processed.
	/// </summary>
	BatchComplete = 1,

	/// <summary>
	/// Acknowledge messages individually as they are processed.
	/// </summary>
	Individual = 2,

	/// <summary>
	/// Manual acknowledgment controlled by the application.
	/// </summary>
	Manual = 3,
}
