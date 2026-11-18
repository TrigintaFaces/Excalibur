// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Data;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Transactions;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Excalibur.Outbox.Oracle.Diagnostics;

using Oracle.ManagedDataAccess.Client;

namespace Excalibur.Outbox.Oracle;

/// <summary>Owns fresh worker leases, never the borrowed staging IDb connection.</summary>
internal sealed class OracleWorkerConnectionSource
{
	private static readonly ConditionalWeakTable<OracleConnection, object> Issued = new();
	private readonly string? _connectionString;
	private readonly IServiceScopeFactory? _scopeFactory;
	private readonly Func<IServiceProvider, CancellationToken, ValueTask<OracleConnection>>? _factory;

	private OracleWorkerConnectionSource(string connectionString) => _connectionString = connectionString;

	private OracleWorkerConnectionSource(IServiceScopeFactory scopeFactory,
		Func<IServiceProvider, CancellationToken, ValueTask<OracleConnection>> factory)
	{
		_scopeFactory = scopeFactory;
		_factory = factory;
	}

	internal static OracleWorkerConnectionSource FromConnectionString(string connectionString)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
		return new(connectionString);
	}

	internal static OracleWorkerConnectionSource FromFactory(IServiceScopeFactory scopeFactory,
		Func<IServiceProvider, CancellationToken, ValueTask<OracleConnection>> factory)
	{
		ArgumentNullException.ThrowIfNull(scopeFactory);
		ArgumentNullException.ThrowIfNull(factory);
		return new(scopeFactory, factory);
	}

	// Acquires ownership only. The operation opens/begins after transfer so failed or duplicate
	// transfer can be tested without opening a database, and no borrowed connection is inspected.
	[System.Diagnostics.CodeAnalysis.SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope",
		Justification = "The privately created connection transfers to the returned lease. Failure cleanup disposes privately owned references; reused factory references remain owned by their original lease.")]
	internal async ValueTask<OracleWorkerConnectionLease> AcquireAsync(ILogger logger, CancellationToken token)
	{
		token.ThrowIfCancellationRequested();
		RejectAmbientTransaction();
		AsyncServiceScope? scope = null;
		OracleConnection? connection = null;
		var ownsConnection = false;
		try
		{
			if (_factory is not null)
			{
				scope = _scopeFactory!.CreateAsyncScope();
				connection = await _factory(scope.Value.ServiceProvider, token).ConfigureAwait(false);
			}
			else
			{
				connection = CreatePrivateConnection();
				// This reference was created privately here, so cleanup owns it even if issuance bookkeeping throws.
				ownsConnection = true;
			}

			if (connection is null) { throw new InvalidOperationException("Oracle worker connection factory returned null."); }
			// A duplicate remains owned by the FIRST lease. Never dispose it from this acquisition.
			if (!Issued.TryAdd(connection, new object()))
			{
				throw new InvalidOperationException("Oracle worker connection factory reused a connection. Return a new, closed, exclusively owned connection each time.");
			}
			ownsConnection = true;
			if (connection.State != ConnectionState.Closed)
			{
				throw new InvalidOperationException("Oracle worker connection factory must transfer a new, closed, un-enlisted connection.");
			}
			RejectAmbientTransaction();
			token.ThrowIfCancellationRequested();
			return new OracleWorkerConnectionLease(connection, scope, logger);
		}
		catch (Exception primary)
		{
			// Callback owns anything it never returned. Only a successfully transferred reference
			// belongs here; a duplicate reference belongs to its original outstanding lease.
			await OracleWorkerConnectionLease.DisposeTransferredAsync(
				ownsConnection ? connection : null, scope, logger, primary).ConfigureAwait(false);
			throw;
		}
	}

	// Returning the newly created disposable transfers it to AcquireAsync's explicit owner/cleanup path.
	private OracleConnection CreatePrivateConnection() => new(_connectionString!);

	internal static void RejectAmbientTransaction()
	{
		if (Transaction.Current is not null)
		{
			throw new InvalidOperationException("Oracle outbox workers require an independent local transaction. Do not invoke a worker inside an ambient TransactionScope; caller-transaction Stage remains supported.");
		}
	}
}

internal sealed partial class OracleWorkerConnectionLease(
	OracleConnection connection, AsyncServiceScope? scope, ILogger logger) : IAsyncDisposable
{
	private int _disposed;
	internal OracleConnection Connection { get; } = connection;

	public ValueTask DisposeAsync() => DisposePreservingAsync(null);

	internal ValueTask DisposePreservingAsync(Exception? primary) =>
		Interlocked.Exchange(ref _disposed, 1) == 0
			? DisposeTransferredAsync(Connection, scope, logger, primary)
			: ValueTask.CompletedTask;

	internal static async ValueTask DisposeTransferredAsync(OracleConnection? connection,
		AsyncServiceScope? scope, ILogger logger, Exception? primary)
	{
		Exception? cleanup = null;
		if (connection is not null)
		{
			try { await connection.DisposeAsync().ConfigureAwait(false); }
			catch (Exception error) { cleanup = error; LogCleanup(logger, error, "connection"); }
		}
		// Scoped factory dependencies remain alive until connection disposal has completed.
		if (scope is { } ownedScope)
		{
			try { await ownedScope.DisposeAsync().ConfigureAwait(false); }
			catch (Exception error) { cleanup ??= error; LogCleanup(logger, error, "factory scope"); }
		}
		if (primary is null && cleanup is not null) { ExceptionDispatchInfo.Capture(cleanup).Throw(); }
	}

	[LoggerMessage(OutboxOracleEventId.OutboxWorkerLeaseCleanup, LogLevel.Warning, "Oracle worker cleanup failed for {Resource}.")]
	private static partial void LogCleanup(ILogger logger, Exception error, string resource);
}
