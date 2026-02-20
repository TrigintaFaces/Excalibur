// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Data;
using System.Data.Common;

namespace Excalibur.Data.Abstractions;

/// <summary>
/// Provides a simple implementation of <see cref="IUnitOfWork" /> using an <see cref="IDbConnection" />.
/// </summary>
public sealed class UnitOfWork : IUnitOfWork
{
	private readonly IDbConnection _connection;
	private volatile bool _disposed;

	/// <summary>
	/// Initializes a new instance of the <see cref="UnitOfWork" /> class.
	/// </summary>
	/// <param name="connection"> The database connection to manage. </param>
	public UnitOfWork(IDbConnection connection)
	{
		ArgumentNullException.ThrowIfNull(connection);
		_connection = connection;
	}

	/// <inheritdoc />
	public IDbConnection Connection => _connection.Ready();

	/// <inheritdoc />
	public IDbTransaction? Transaction { get; private set; }

	/// <inheritdoc />
	public async Task BeginTransactionAsync(CancellationToken cancellationToken)
	{
		var connection = Connection;

		if (connection is DbConnection dbConnection)
		{
			Transaction = await dbConnection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
			return;
		}

		// Synchronous fallback for providers that don't derive from DbConnection
		Transaction = connection.BeginTransaction();
	}

	/// <inheritdoc />
	public async Task CommitAsync(CancellationToken cancellationToken)
	{
		if (Transaction is DbTransaction dbTransaction)
		{
			await dbTransaction.CommitAsync(cancellationToken).ConfigureAwait(false);
			await dbTransaction.DisposeAsync().ConfigureAwait(false);
		}
		else
		{
			Transaction?.Commit();
			Transaction?.Dispose();
		}

		Transaction = null;
	}

	/// <inheritdoc />
	public async Task RollbackAsync(CancellationToken cancellationToken)
	{
		if (Transaction is DbTransaction dbTransaction)
		{
			await dbTransaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
			await dbTransaction.DisposeAsync().ConfigureAwait(false);
		}
		else
		{
			Transaction?.Rollback();
			Transaction?.Dispose();
		}

		Transaction = null;
	}

	/// <inheritdoc />
	public async ValueTask DisposeAsync()
	{
		if (!_disposed)
		{
			if (Transaction is DbTransaction dbTransaction)
			{
				await dbTransaction.DisposeAsync().ConfigureAwait(false);
			}
			else
			{
				Transaction?.Dispose();
			}

			if (_connection is DbConnection dbConnection)
			{
				await dbConnection.DisposeAsync().ConfigureAwait(false);
			}
			else
			{
				_connection.Dispose();
			}

			_disposed = true;
		}
	}
}
