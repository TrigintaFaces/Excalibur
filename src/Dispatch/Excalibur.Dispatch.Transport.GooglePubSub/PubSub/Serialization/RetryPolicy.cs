// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Transport.Google;

/// <summary>
/// Represents a retry policy for message processing.
/// </summary>
public sealed record RetryPolicy(
	int MaxAttempts,
	TimeSpan InitialDelay,
	TimeSpan MaxDelay,
	double BackoffMultiplier,
	bool ExponentialBackoff = true);
