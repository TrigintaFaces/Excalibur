// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.ComponentModel.DataAnnotations;

namespace Excalibur.Data.InMemory;

/// <summary>
/// Pooling and timeout options for the in-memory provider.
/// </summary>
public sealed class InMemoryPoolingOptions
{
	/// <summary>
	/// Gets or sets a value indicating whether connection pooling is enabled.
	/// </summary>
	/// <value>
	/// <see langword="true"/> if connection pooling is enabled; otherwise, <c>false</c>.
	/// </value>
	public bool EnableConnectionPooling { get; set; }

	/// <summary>
	/// Gets or sets the maximum pool size.
	/// </summary>
	/// <value>
	/// The maximum pool size.
	/// </value>
	[Range(1, int.MaxValue)]
	public int MaxPoolSize { get; set; } = 100;

	/// <summary>
	/// Gets or sets the minimum pool size.
	/// </summary>
	/// <value>
	/// The minimum pool size.
	/// </value>
	[Range(0, int.MaxValue)]
	public int MinPoolSize { get; set; }

	/// <summary>
	/// Gets or sets the connection timeout in seconds.
	/// </summary>
	/// <value>
	/// The connection timeout in seconds.
	/// </value>
	[Range(1, int.MaxValue)]
	public int ConnectionTimeout { get; set; } = 30;

	/// <summary>
	/// Gets or sets the command timeout in seconds.
	/// </summary>
	/// <value>
	/// The command timeout in seconds.
	/// </value>
	[Range(1, int.MaxValue)]
	public int CommandTimeout { get; set; } = 30;
}
