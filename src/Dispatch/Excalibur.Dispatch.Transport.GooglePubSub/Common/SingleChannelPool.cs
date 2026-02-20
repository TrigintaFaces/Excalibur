// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Grpc.Net.Client;

namespace Excalibur.Dispatch.Transport.Google;

/// <summary>
/// A simple channel pool that maintains a single gRPC channel.
/// </summary>
public sealed class SingleChannelPool : IDisposable
{
	private volatile bool _disposed;

	/// <summary>
	/// Initializes a new instance of the <see cref="SingleChannelPool" /> class.
	/// </summary>
	/// <param name="address"> The target address. </param>
	public SingleChannelPool(string address)
	{
		ArgumentNullException.ThrowIfNull(address);

		UnderlyingChannel = GrpcChannel.ForAddress(address);
	}

	/// <summary>
	/// Gets the underlying gRPC channel.
	/// </summary>
	/// <value>
	/// The underlying gRPC channel.
	/// </value>
	public GrpcChannel UnderlyingChannel { get; }

	/// <summary>
	/// Gets a channel from the pool.
	/// </summary>
	/// <returns> The gRPC channel. </returns>
	/// <exception cref="ObjectDisposedException"></exception>
	public GrpcChannel GetChannel()
	{
		ObjectDisposedException.ThrowIf(_disposed, this);

		return UnderlyingChannel;
	}

	/// <inheritdoc />
	public void Dispose()
	{
		if (_disposed)
		{
			return;
		}

		_disposed = true;
		UnderlyingChannel?.Dispose();
		GC.SuppressFinalize(this);
	}
}
