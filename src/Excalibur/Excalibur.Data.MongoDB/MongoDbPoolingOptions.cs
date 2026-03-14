// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.ComponentModel.DataAnnotations;

namespace Excalibur.Data.MongoDB;

/// <summary>
/// Connection pooling options for the MongoDB provider.
/// </summary>
public sealed class MongoDbPoolingOptions
{
	/// <summary>
	/// Gets or sets the maximum connection pool size.
	/// </summary>
	/// <value>
	/// The maximum connection pool size. Defaults to 100.
	/// </value>
	[Range(1, int.MaxValue)]
	public int MaxPoolSize { get; set; } = 100;

	/// <summary>
	/// Gets or sets the minimum connection pool size.
	/// </summary>
	/// <value>
	/// The minimum connection pool size. Defaults to 0.
	/// </value>
	[Range(0, int.MaxValue)]
	public int MinPoolSize { get; set; }
}
