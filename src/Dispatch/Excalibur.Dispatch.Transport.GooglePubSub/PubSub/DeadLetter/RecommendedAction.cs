// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Transport.Google;

/// <summary>
/// Recommended actions for poison messages.
/// </summary>
public enum RecommendedAction
{
	/// <summary>
	/// Retry the message after a delay.
	/// </summary>
	Retry = 0,

	/// <summary>
	/// Move to dead letter queue immediately.
	/// </summary>
	DeadLetter = 1,

	/// <summary>
	/// Quarantine for manual inspection.
	/// </summary>
	Quarantine = 2,

	/// <summary>
	/// Skip and acknowledge the message.
	/// </summary>
	Skip = 3,
}
