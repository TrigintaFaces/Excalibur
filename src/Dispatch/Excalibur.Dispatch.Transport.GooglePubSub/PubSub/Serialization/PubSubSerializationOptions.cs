// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Dispatch.Serialization;

namespace Excalibur.Dispatch.Transport.Google;

/// <summary>
/// Configuration options for Pub/Sub message serialization.
/// </summary>
public sealed class PubSubSerializationOptions
{
	/// <summary>
	/// Gets or sets the buffer pool configuration for serialization.
	/// </summary>
	/// <value>
	/// The buffer pool configuration.
	/// </value>
	public PubSubBufferOptions Buffer { get; set; } = new();

	/// <summary>
	/// Validates the configuration settings.
	/// </summary>
	/// <exception cref="InvalidOperationException"> Thrown when configuration is invalid. </exception>
	public void Validate()
	{
		if (Buffer.InitialBufferSize <= 0)
		{
			throw new InvalidOperationException("InitialBufferSize must be greater than zero.");
		}

		if (Buffer.MaxBufferSize < Buffer.InitialBufferSize)
		{
			throw new InvalidOperationException("MaxBufferSize must be greater than or equal to InitialBufferSize.");
		}

		if (Buffer.MaxBuffersPerBucket <= 0)
		{
			throw new InvalidOperationException("MaxBuffersPerBucket must be greater than zero.");
		}
	}
}

/// <summary>
/// Configuration options for buffer pooling in Pub/Sub serialization.
/// </summary>
public sealed class PubSubBufferOptions
{
	/// <summary>
	/// Gets or sets the initial buffer size for serialization.
	/// Default: 4096 bytes.
	/// </summary>
	/// <value>
	/// The initial buffer size for serialization.
	/// Default: 4096 bytes.
	/// </value>
	public int InitialBufferSize { get; set; } = 4096;

	/// <summary>
	/// Gets or sets the maximum buffer size for array pool.
	/// Default: 1MB.
	/// </summary>
	/// <value>
	/// The maximum buffer size for array pool.
	/// Default: 1MB.
	/// </value>
	public int MaxBufferSize { get; set; } = 1024 * 1024;

	/// <summary>
	/// Gets or sets the maximum number of buffers per bucket in array pool.
	/// Default: 50.
	/// </summary>
	/// <value>
	/// The maximum number of buffers per bucket in array pool.
	/// Default: 50.
	/// </value>
	public int MaxBuffersPerBucket { get; set; } = 50;
}
