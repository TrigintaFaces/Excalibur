// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

namespace Excalibur.A3.Authorization;

/// <summary>
/// Configures how long cached authorization data may be served.
/// </summary>
/// <remarks>
/// <para>
/// A user's grants and a tenant's activity-group catalogue are cached in the application-scoped distributed
/// cache. Each entry is invalidated whenever a sync changes the data it was read from, but a request that read
/// the previous state just before the change can still write that state to the cache just after it. An entry
/// that keeps being read would otherwise be kept alive by those reads indefinitely.
/// </para>
/// <para>
/// <see cref="AbsoluteExpirationRelativeToNow"/> caps that: every entry is reloaded from the store once this
/// long has passed since it was written, however often it is read in the meantime. It is therefore <b>the
/// longest a revoked grant can still authorize</b> through the cache. Shorter values narrow that window at the
/// cost of more store reads.
/// </para>
/// </remarks>
public sealed class AuthorizationCacheOptions
{
	/// <summary>
	/// Gets or sets the longest a cached authorization entry is served before it is reloaded from the store.
	/// </summary>
	/// <value>
	/// A positive duration. The default is five minutes. This is the longest a revoked grant can still
	/// authorize through the cache.
	/// </value>
	public TimeSpan AbsoluteExpirationRelativeToNow { get; set; } = TimeSpan.FromMinutes(5);
}
