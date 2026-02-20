// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Transport.AwsSqs;

/// <summary>
/// Specifies the retry backoff strategy for AWS SQS operations.
/// </summary>
public enum RetryStrategy
{
	/// <summary>
	/// Exponential backoff with jitter. Each retry waits exponentially longer
	/// (base * 2^attempt) with randomized jitter to avoid thundering herd.
	/// This is the default and recommended strategy.
	/// </summary>
	Exponential = 0,

	/// <summary>
	/// Linear backoff. Each retry waits a fixed increment longer
	/// (base + increment * attempt). Useful for predictable delay patterns.
	/// </summary>
	Linear = 1,

	/// <summary>
	/// Fixed delay between retries. Every retry waits the same duration.
	/// Simple but may cause correlated retries across instances.
	/// </summary>
	Fixed = 2,
}
