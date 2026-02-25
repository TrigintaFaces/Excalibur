// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Options.Pooling;

/// <summary>
/// Configuration options for pool infrastructure. Implements PERF-001.4: Pool configuration with validation and defaults.
/// </summary>
public sealed class PoolOptions
{
	/// <summary>
	/// Gets or sets the buffer pool configuration.
	/// </summary>
	/// <value>
	/// The buffer pool configuration.
	/// </value>
	public BufferPoolOptions BufferPool { get; set; } = new();

	/// <summary>
	/// Gets or sets the message pool configuration.
	/// </summary>
	/// <value>
	/// The message pool configuration.
	/// </value>
	public MessagePoolOptions MessagePool { get; set; } = new();

	/// <summary>
	/// Gets or sets the global pool behavior options.
	/// </summary>
	/// <value>
	/// The global pool behavior options.
	/// </value>
	public GlobalPoolOptions Global { get; set; } = new();
}
