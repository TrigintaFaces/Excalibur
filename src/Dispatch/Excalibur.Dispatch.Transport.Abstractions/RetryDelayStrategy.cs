// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Transport;

/// <summary>
/// Defines the retry delay strategies.
/// </summary>
public enum RetryDelayStrategy
{
	/// <summary>
	/// Fixed delay between retries.
	/// </summary>
	Fixed = 0,

	/// <summary>
	/// Exponential backoff between retries.
	/// </summary>
	Exponential = 1,

	/// <summary>
	/// Linear increase in delay between retries.
	/// </summary>
	Linear = 2,
}
