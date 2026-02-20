// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Transport.Aws;

/// <summary>
/// Represents a pooled connection with metadata.
/// </summary>
/// <typeparam name="TConnection"> The type of connection. </typeparam>
public sealed class PooledConnection<TConnection>
	where TConnection : class
{
	/// <summary>
	/// Gets or sets the connection instance.
	/// </summary>
	/// <value>
	/// The connection instance.
	/// </value>
	public required TConnection Client { get; set; }

	/// <summary>
	/// Gets or sets when the connection was created.
	/// </summary>
	/// <value>
	/// When the connection was created.
	/// </value>
	public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

	/// <summary>
	/// Gets or sets when the connection was last used.
	/// </summary>
	/// <value>
	/// When the connection was last used.
	/// </value>
	public DateTimeOffset LastUsedAt { get; set; } = DateTimeOffset.UtcNow;

	/// <summary>
	/// Gets or sets the number of times this connection has been used.
	/// </summary>
	/// <value>
	/// The number of times this connection has been used.
	/// </value>
	public int UseCount { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether the connection is currently in use.
	/// </summary>
	/// <value>
	/// A value indicating whether the connection is currently in use.
	/// </value>
	public bool InUse { get; set; }

	/// <summary>
	/// Gets custom metadata associated with the connection.
	/// </summary>
	/// <value>
	/// Custom metadata associated with the connection.
	/// </value>
	public Dictionary<string, object> Metadata { get; } = [];
}
