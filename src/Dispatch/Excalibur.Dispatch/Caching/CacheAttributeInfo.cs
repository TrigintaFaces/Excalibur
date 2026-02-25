// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Caching;

/// <summary>
/// Information about cache attributes.
/// </summary>
public sealed class CacheAttributeInfo
{
	private string _name = string.Empty;
	private int _ttlSeconds;

	/// <summary>
	/// Gets or sets the attribute name.
	/// </summary>
	/// <value> The cache attribute identifier. </value>
	/// <exception cref="ArgumentNullException">Thrown when set to <see langword="null"/>.</exception>
	public string Name
	{
		get => _name;
		set
		{
			ArgumentNullException.ThrowIfNull(value);
			_name = value;
		}
	}

	/// <summary>
	/// Gets or sets the Time To Live in seconds.
	/// </summary>
	/// <value> The cache entry lifetime in seconds. </value>
	/// <exception cref="ArgumentOutOfRangeException">Thrown when set to a negative value.</exception>
	public int TtlSeconds
	{
		get => _ttlSeconds;
		set
		{
			ArgumentOutOfRangeException.ThrowIfNegative(value);
			_ttlSeconds = value;
		}
	}

	/// <summary>
	/// Gets or sets the cache key prefix.
	/// </summary>
	/// <value> The optional key prefix used when composing cache keys. </value>
	public string? KeyPrefix { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether to use sliding expiration.
	/// </summary>
	/// <value> <see langword="true" /> when the cache entry uses sliding expiration; otherwise, <see langword="false" />. </value>
	public bool SlidingExpiration { get; set; }
}
