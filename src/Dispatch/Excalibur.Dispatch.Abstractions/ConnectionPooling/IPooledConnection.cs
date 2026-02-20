// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Abstractions;

/// <summary>
/// Represents a connection acquired from a pool with automatic lifecycle management.
/// </summary>
/// <typeparam name="TConnection"> The type of connection being pooled. </typeparam>
public interface IPooledConnection<out TConnection> : IAsyncDisposable
	where TConnection : class
{
	/// <summary>
	/// Gets the underlying connection instance.
	/// </summary>
	/// <value>
	/// The underlying connection instance.
	/// </value>
	TConnection Connection { get; }

	/// <summary>
	/// Gets the time when this connection was acquired from the pool.
	/// </summary>
	/// <value>
	/// The time when this connection was acquired from the pool.
	/// </value>
	DateTime AcquiredAt { get; }

	/// <summary>
	/// Gets a value indicating whether this connection is still valid for use.
	/// </summary>
	/// <value>
	/// A value indicating whether this connection is still valid for use.
	/// </value>
	bool IsValid { get; }

	/// <summary>
	/// Gets the number of times this connection has been used.
	/// </summary>
	/// <value>
	/// The number of times this connection has been used.
	/// </value>
	int UseCount { get; }
}
