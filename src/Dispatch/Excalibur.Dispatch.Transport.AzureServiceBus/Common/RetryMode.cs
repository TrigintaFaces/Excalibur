// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Transport.Azure;

/// <summary>
/// Retry mode for Azure operations.
/// </summary>
public enum RetryMode
{
	/// <summary>
	/// Fixed delay between retries.
	/// </summary>
	Fixed = 0,

	/// <summary>
	/// Exponential backoff between retries.
	/// </summary>
	Exponential = 1,
}
