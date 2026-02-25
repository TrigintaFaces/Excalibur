// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Abstractions;

/// <summary>
/// Strategies for handling connection acquisition and operation failures.
/// </summary>
public enum FailureHandlingStrategy
{
	/// <summary>
	/// Fail immediately without any retry attempts.
	/// </summary>
	FailFast = 0,

	/// <summary>
	/// Retry a limited number of times, then fail.
	/// </summary>
	RetryThenFail = 1,

	/// <summary>
	/// Keep retrying with exponential backoff until success or cancellation.
	/// </summary>
	RetryIndefinitely = 2,

	/// <summary>
	/// Create new connections on demand when the pool is exhausted.
	/// </summary>
	CreateOnDemand = 3,
}
