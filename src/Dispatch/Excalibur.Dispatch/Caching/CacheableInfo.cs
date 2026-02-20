// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Caching;

/// <summary>
/// Information about a cacheable type.
/// </summary>
public sealed class CacheableInfo
{
	private string _typeName = string.Empty;
	private CacheAttributeInfo[] _attributes = [];

	/// <summary>
	/// Gets or sets the type name.
	/// </summary>
	/// <value> The cacheable type's fully qualified name. </value>
	/// <exception cref="ArgumentNullException">Thrown when set to <see langword="null"/>.</exception>
	public string TypeName
	{
		get => _typeName;
		set
		{
			ArgumentNullException.ThrowIfNull(value);
			_typeName = value;
		}
	}

	/// <summary>
	/// Gets or sets a value indicating whether the type is cacheable.
	/// </summary>
	/// <value> <see langword="true" /> when caching is enabled; otherwise, <see langword="false" />. </value>
	public bool IsCacheable { get; set; }

	/// <summary>
	/// Gets or sets the cache attributes.
	/// </summary>
	/// <value> The set of cache attribute descriptors applied to the type. </value>
	/// <exception cref="ArgumentNullException">Thrown when set to <see langword="null"/>.</exception>
	public CacheAttributeInfo[] Attributes
	{
		get => _attributes;
		set
		{
			ArgumentNullException.ThrowIfNull(value);
			_attributes = value;
		}
	}
}
