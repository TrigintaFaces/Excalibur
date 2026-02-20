// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.ComponentModel.DataAnnotations;

namespace Excalibur.Dispatch.Options.Pooling;

/// <summary>
/// Size bucket configuration for buffer pools.
/// </summary>
public sealed class SizeBucketOptions
{
	/// <summary>
	/// Gets or sets the tiny buffer size.
	/// </summary>
	/// <value>
	/// The tiny buffer size.
	/// </value>
	[Range(16, 256)]
	public int TinySize { get; set; } = 64;

	/// <summary>
	/// Gets or sets the small buffer size.
	/// </summary>
	/// <value>
	/// The small buffer size.
	/// </value>
	[Range(128, 1024)]
	public int SmallSize { get; set; } = 256;

	/// <summary>
	/// Gets or sets the medium buffer size.
	/// </summary>
	/// <value>
	/// The medium buffer size.
	/// </value>
	[Range(1024, 16384)]
	public int MediumSize { get; set; } = 4096;

	/// <summary>
	/// Gets or sets the large buffer size.
	/// </summary>
	/// <value>
	/// The large buffer size.
	/// </value>
	[Range(16384, 262144)]
	public int LargeSize { get; set; } = 65536;

	/// <summary>
	/// Gets or sets the huge buffer size.
	/// </summary>
	/// <value>
	/// The huge buffer size.
	/// </value>
	[Range(262144, 10485760)]
	public int HugeSize { get; set; } = 1048576;
}
