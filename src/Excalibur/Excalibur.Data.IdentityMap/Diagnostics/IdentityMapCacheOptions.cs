// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Data.IdentityMap.Diagnostics;

/// <summary>
/// Configuration options for the identity map caching decorator.
/// </summary>
public sealed class IdentityMapCacheOptions
{
	/// <summary>
	/// Gets or sets the absolute expiration for cached identity mappings.
	/// </summary>
	/// <value>The absolute expiration duration. Defaults to 1 hour.</value>
	public TimeSpan AbsoluteExpiration { get; set; } = TimeSpan.FromHours(1);

	/// <summary>
	/// Gets or sets the sliding expiration for cached identity mappings.
	/// </summary>
	/// <value>The sliding expiration duration. Defaults to 10 minutes.</value>
	public TimeSpan? SlidingExpiration { get; set; } = TimeSpan.FromMinutes(10);
}
