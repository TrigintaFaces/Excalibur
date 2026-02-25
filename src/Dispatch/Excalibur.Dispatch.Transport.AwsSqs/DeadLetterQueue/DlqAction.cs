// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Transport.Aws;

/// <summary>
/// Actions that can be taken on a DLQ message.
/// </summary>
public enum DlqAction
{
	/// <summary>
	/// No action taken.
	/// </summary>
	None = 0,

	/// <summary>
	/// Message was successfully redriven to source queue.
	/// </summary>
	Redriven = 1,

	/// <summary>
	/// Message was retried but failed again.
	/// </summary>
	RetryFailed = 2,

	/// <summary>
	/// Message was permanently failed and archived.
	/// </summary>
	Archived = 3,

	/// <summary>
	/// Message was deleted from DLQ.
	/// </summary>
	Deleted = 4,

	/// <summary>
	/// Message processing was skipped.
	/// </summary>
	Skipped = 5,
}
