// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Data;
using System.Data.Common;

namespace Excalibur.Data.Abstractions;

/// <summary>
/// Abstract base class that implements the <see cref="IDb" /> interface for managing a database connection.
/// </summary>
/// <remarks> Ensures the database connection is in a ready state before providing access to it. </remarks>
public abstract class Db : IDb, IAsyncDisposable, IDisposable
{
	private readonly IDbConnection _connection;
	private volatile bool _disposed;

	/// <summary>
	/// Initializes a new instance of the <see cref="Db" /> class with a provided <see cref="IDbConnection" />.
	/// </summary>
	/// <param name="connection"> The database connection to manage. </param>
	protected Db(IDbConnection connection)
	{
		ArgumentNullException.ThrowIfNull(connection);
		_connection = connection;
	}

	/// <inheritdoc />
	public IDbConnection Connection => _connection.Ready();

	/// <inheritdoc />
	public void Open() => _connection.Ready();

	/// <inheritdoc />
	public void Close() => _connection.Close();

	/// <summary>
	/// Asynchronously releases all resources used by the <see cref="Db" />.
	/// If the underlying connection is a <see cref="DbConnection"/>, it is disposed asynchronously;
	/// otherwise, the synchronous <see cref="IDisposable.Dispose"/> path is used.
	/// </summary>
	/// <returns>A <see cref="ValueTask"/> representing the asynchronous dispose operation.</returns>
	public async ValueTask DisposeAsync()
	{
		if (_disposed)
		{
			return;
		}

		_disposed = true;

		if (_connection is IAsyncDisposable asyncDisposable)
		{
			await asyncDisposable.DisposeAsync().ConfigureAwait(false);
		}
		else
		{
			_connection.Dispose();
		}

		GC.SuppressFinalize(this);
	}

	/// <summary>
	/// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
	/// </summary>
	public void Dispose()
	{
		Dispose(disposing: true);
		GC.SuppressFinalize(this);
	}

	/// <summary>
	/// Releases the unmanaged resources used by the <see cref="Db"/> and optionally releases the managed resources.
	/// </summary>
	/// <param name="disposing">true to release both managed and unmanaged resources; false to release only unmanaged resources.</param>
	protected virtual void Dispose(bool disposing)
	{
		if (_disposed)
		{
			return;
		}

		if (disposing)
		{
			_connection.Dispose();
		}

		_disposed = true;
	}
}
