// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Caching;

/// <summary>
/// Configuration specific to distributed caching.
/// </summary>
public sealed class DistributedCacheConfiguration
{
	/// <summary>
	/// Gets or sets the key prefix to use for all distributed cache entries. Helps avoid key collisions in shared cache stores.
	/// </summary>
	/// <value>The key prefix to use for all distributed cache entries.</value>
	public string KeyPrefix { get; set; } = "dispatch:";

	/// <summary>
	/// Gets or sets a value indicating whether to use binary serialization for cached values. More efficient than JSON but less debuggable.
	/// </summary>
	/// <value><see langword="true"/> if binary serialization should be used for cached values; otherwise, <see langword="false"/>.</value>
	public bool UseBinarySerialization { get; set; }

	/// <summary>
	/// Gets or sets the maximum retry attempts for distributed cache operations. Default is 3.
	/// </summary>
	/// <value>The maximum retry attempts for distributed cache operations.</value>
	public int MaxRetryAttempts { get; set; } = 3;

	/// <summary>
	/// Gets or sets the delay between retry attempts. Default is 100ms.
	/// </summary>
	/// <value>The delay between retry attempts.</value>
	public TimeSpan RetryDelay { get; set; } = TimeSpan.FromMilliseconds(100);
}
