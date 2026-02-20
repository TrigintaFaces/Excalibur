// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Transport.Aws;

/// <summary>
/// Interface for retry strategies.
/// </summary>
public interface IRetryStrategy
{
	/// <summary>
	/// Determines if a retry should be attempted.
	/// </summary>
	/// <param name="attempt"> The current attempt number. </param>
	/// <param name="exception"> The exception that occurred. </param>
	/// <returns> True if a retry should be attempted; otherwise, false. </returns>
	bool ShouldRetry(int attempt, Exception exception);

	/// <summary>
	/// Gets the delay before the next retry attempt.
	/// </summary>
	/// <param name="attempt"> The current attempt number. </param>
	/// <returns> The delay before the next attempt. </returns>
	TimeSpan GetRetryDelay(int attempt);
}
