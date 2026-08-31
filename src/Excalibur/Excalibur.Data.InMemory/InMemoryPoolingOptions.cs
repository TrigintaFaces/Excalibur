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
