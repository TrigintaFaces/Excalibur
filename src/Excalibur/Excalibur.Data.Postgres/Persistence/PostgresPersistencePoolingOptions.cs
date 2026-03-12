// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.ComponentModel.DataAnnotations;

namespace Excalibur.Data.Postgres.Persistence;

/// <summary>
/// Connection pooling options for the Postgres persistence provider.
/// </summary>
public sealed class PostgresPersistencePoolingOptions
{
	/// <summary>
	/// Gets or sets a value indicating whether to enable connection pooling. Default is true.
	/// </summary>
	/// <value>
	/// A value indicating whether to enable connection pooling. Default is true.
	/// </value>
	public bool EnableConnectionPooling { get; set; } = true;

	/// <summary>
	/// Gets or sets the maximum pool size when connection pooling is enabled. Default is 100.
	/// </summary>
	/// <value>
	/// The maximum pool size when connection pooling is enabled. Default is 100.
	/// </value>
	[Range(1, 1000, ErrorMessage = "Max pool size must be between 1 and 1000")]
	public int MaxPoolSize { get; set; } = 100;

	/// <summary>
	/// Gets or sets the minimum pool size when connection pooling is enabled. Default is 0.
	/// </summary>
	/// <value>
	/// The minimum pool size when connection pooling is enabled. Default is 0.
	/// </value>
	[Range(0, 100, ErrorMessage = "Min pool size must be between 0 and 100")]
	public int MinPoolSize { get; set; }
}
