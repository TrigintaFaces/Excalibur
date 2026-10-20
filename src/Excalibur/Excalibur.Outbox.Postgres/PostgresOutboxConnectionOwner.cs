// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Data;

using Npgsql;

namespace Excalibur.Outbox.Postgres;

/// <summary>Owns the outbox connection and, when supplied by its factory, its pool.</summary>
internal sealed class PostgresOutboxConnectionOwner : IDisposable, IAsyncDisposable
{
    private readonly NpgsqlDataSource? _ownedSource;
    private int _disposed;

    private PostgresOutboxConnectionOwner(OutboxDb db, NpgsqlDataSource? ownedSource)
    {
        Db = db;
        _ownedSource = ownedSource;
    }

    internal OutboxDb Db { get; }

    internal static PostgresOutboxConnectionOwner Create(NpgsqlDataSource source, bool ownsSource)
    {
        NpgsqlConnection? connection = null;
        try
        {
            connection = source.CreateConnection();
            connection.Open();
            return new PostgresOutboxConnectionOwner(new OutboxDb(connection), ownsSource ? source : null);
        }
        catch
        {
            try
            {
                connection?.Dispose();
            }
            finally
            {
                if (ownsSource)
                {
                    source.Dispose();
                }
            }

            throw;
        }
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        try
        {
            Db.Dispose();
        }
        finally
        {
            _ownedSource?.Dispose();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        try
        {
            await Db.DisposeAsync().ConfigureAwait(false);
        }
        finally
        {
            if (_ownedSource is not null)
            {
                await _ownedSource.DisposeAsync().ConfigureAwait(false);
            }
        }
    }
}
