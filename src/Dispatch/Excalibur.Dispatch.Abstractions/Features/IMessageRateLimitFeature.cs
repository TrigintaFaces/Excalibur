// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Abstractions.Features;

/// <summary>
/// Feature interface for rate limiting state.
/// </summary>
public interface IMessageRateLimitFeature
{
	/// <summary>
	/// Gets or sets a value indicating whether rate limiting was triggered.
	/// </summary>
	/// <value><see langword="true"/> if rate limited; otherwise, <see langword="false"/>.</value>
	bool RateLimitExceeded { get; set; }

	/// <summary>
	/// Gets or sets the retry-after duration when rate limited.
	/// </summary>
	/// <value>The retry-after duration, or <see langword="null"/> if not rate limited.</value>
	TimeSpan? RateLimitRetryAfter { get; set; }
}
