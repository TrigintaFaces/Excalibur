// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Abstractions.Transport;

/// <summary>
/// Defines the available retry strategies for failed message processing.
/// </summary>
public enum RetryStrategy
{
	/// <summary>
	/// Uses a fixed delay between retry attempts.
	/// </summary>
	FixedDelay = 0,

	/// <summary>
	/// Uses exponential backoff with increasing delays between retry attempts.
	/// </summary>
	ExponentialBackoff = 1,
}
