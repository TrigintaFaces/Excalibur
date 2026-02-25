// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Concurrent;
using System.Data;

using Excalibur.Data.Abstractions.Persistence;

using Microsoft.Extensions.Logging;

using MySqlConnector;

namespace Excalibur.Data.MySql;

/// <summary>
/// MySQL implementation of transaction scope.
/// </summary>
public sealed class MySqlTransactionScope : ITransactionScope
{
	private readonly ILogger<MySqlTransactionScope> _logger;
	private readonly List<IPersistenceProvider> _enlistedProviders = [];
	private readonly List<IDbConnection> _enlistedConnections = [];
	private readonly ConcurrentDictionary<string, MySqlTransaction> _transactions = new();
	private volatile bool _disposed;

	/// <summary>
	/// Initializes a new instance of the <see cref="MySqlTransactionScope"/> class.
	/// </summary>
	/// <param name="isolationLevel">The transaction isolation level.</param>
	/// <param name="logger">The logger for diagnostic output.</param>
	public MySqlTransactionScope(
		IsolationLevel isolationLevel,
		ILogger<MySqlTransactionScope> logger)
	{
		TransactionId = Guid.NewGuid().ToString("N");
		IsolationLevel = isolationLevel;
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
		Status = TransactionStatus.Active;
		StartTime = DateTimeOffset.UtcNow;
	}

	/// <inheritdoc/>
	public string TransactionId { get; }

	/// <inheritdoc/>
	public IsolationLevel IsolationLevel { get; }

	/// <inheritdoc/>
	public TransactionStatus Status { get; private set; }

	/// <inheritdoc/>
	public DateTimeOffset StartTime { get; }

	/// <inheritdoc/>
	public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(30);

	/// <inheritdoc/>
	public async Task EnlistProviderAsync(IPersistenceProvider provider, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(provider);
		ObjectDisposedException.ThrowIf(_disposed, this);

		if (!_enlistedProviders.Contains(provider))
		{
			_enlistedProviders.Add(provider);
		}

		await Task.CompletedTask.ConfigureAwait(false);
	}

	/// <inheritdoc/>
	public async Task EnlistConnectionAsync(IDbConnection connection, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(connection);
		ObjectDisposedException.ThrowIf(_disposed, this);

		if (!_enlistedConnections.Contains(connection))
		{
			_enlistedConnections.Add(connection);

			if (connection is MySqlConnection mySqlConnection)
			{
				if (mySqlConnection.State != ConnectionState.Open)
				{
					await mySqlConnection.OpenAsync(cancellationToken).ConfigureAwait(false);
				}

				var transaction = await mySqlConnection.BeginTransactionAsync(IsolationLevel, cancellationToken).ConfigureAwait(false);
				_transactions.TryAdd(connection.ConnectionString, transaction);
			}
		}
	}

	/// <inheritdoc/>
	public async Task CommitAsync(CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);

		foreach (var transaction in _transactions.Values)
		{
			await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
		}

		Status = TransactionStatus.Committed;
	}

	/// <inheritdoc/>
	public async Task RollbackAsync(CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);

		foreach (var transaction in _transactions.Values)
		{
			try
			{
				await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
			}
			catch (Exception ex)
			{
				_logger.LogWarning(ex, "Error rolling back MySQL transaction {TransactionId}", TransactionId);
			}
		}

		Status = TransactionStatus.RolledBack;
	}

	/// <inheritdoc/>
	public IEnumerable<IPersistenceProvider> GetEnlistedProviders() => _enlistedProviders;

	/// <inheritdoc/>
	public void Dispose()
	{
		if (_disposed)
		{
			return;
		}

		_disposed = true;

		foreach (var transaction in _transactions.Values)
		{
			transaction.Dispose();
		}

		_transactions.Clear();
	}

	/// <inheritdoc/>
	public async ValueTask DisposeAsync()
	{
		if (_disposed)
		{
			return;
		}

		_disposed = true;

		foreach (var transaction in _transactions.Values)
		{
			await transaction.DisposeAsync().ConfigureAwait(false);
		}

		_transactions.Clear();
	}
}
