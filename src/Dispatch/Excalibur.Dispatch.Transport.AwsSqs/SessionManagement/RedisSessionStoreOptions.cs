// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Transport.Aws;

/// <summary>
/// Redis session store options.
/// </summary>
public sealed class RedisSessionStoreOptions
{
	/// <summary>
	/// Gets or sets the Redis key prefix.
	/// </summary>
	/// <value>
	/// The Redis key prefix.
	/// </value>
	public string KeyPrefix { get; set; } = "dispatch";
}
