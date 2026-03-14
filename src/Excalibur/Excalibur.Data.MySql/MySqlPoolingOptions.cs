// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.ComponentModel.DataAnnotations;

namespace Excalibur.Data.MySql;

/// <summary>
/// Connection pooling options for the MySQL/MariaDB persistence provider.
/// </summary>
public sealed class MySqlPoolingOptions
{
	/// <summary>
	/// Gets or sets the maximum pool size.
	/// </summary>
	/// <value>The maximum pool size. Defaults to 100.</value>
	[Range(1, int.MaxValue)]
	public int MaxPoolSize { get; set; } = 100;

	/// <summary>
	/// Gets or sets the minimum pool size.
	/// </summary>
	/// <value>The minimum pool size. Defaults to 0.</value>
	[Range(0, int.MaxValue)]
	public int MinPoolSize { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether to enable connection pooling.
	/// </summary>
	/// <value><see langword="true"/> if connection pooling is enabled; otherwise, <see langword="false"/>. Defaults to <see langword="true"/>.</value>
	public bool EnablePooling { get; set; } = true;

	/// <summary>
	/// Gets or sets a value indicating whether to clear the connection pool on dispose.
	/// </summary>
	/// <value><see langword="true"/> if the connection pool is cleared on dispose; otherwise, <see langword="false"/>. Defaults to <see langword="false"/>.</value>
	public bool ClearPoolOnDispose { get; set; }
}
