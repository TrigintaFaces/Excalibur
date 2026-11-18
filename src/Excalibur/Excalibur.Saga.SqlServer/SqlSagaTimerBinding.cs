// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Data;
using System.Runtime.ExceptionServices;

using Microsoft.Data.SqlClient;

namespace Excalibur.Saga.SqlServer;

/// <summary>One trusted encoding shared by the actual SQL timer writer and selected participant.</summary>
internal static class SqlSagaTimerBinding
{
    internal static byte[] CaptureClosed(Func<SqlConnection> factory, string schema, string table)
    {
        var connection = factory() ?? throw new InvalidOperationException("The timer connection factory returned null.");
        if (connection.State != ConnectionState.Closed)
        {
            // A live handout can be someone else's operation. Never dispose it while refusing.
            throw new InvalidOperationException("Timer participation requires an unopened connection handout.");
        }

        byte[]? binding = null;
        ExceptionDispatchInfo? primary = null;
        try
        {
            binding = ForWriter(connection, schema, table);
        }
        catch (Exception error)
        {
            primary = ExceptionDispatchInfo.Capture(error);
        }

        try
        {
            connection.Dispose();
        }
        catch (Exception error)
        {
            primary ??= ExceptionDispatchInfo.Capture(error);
        }

        primary?.Throw();
        return binding!;
    }

    internal static byte[] ForWriter(SqlConnection connection, string schema, string table)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connection.DataSource);
        ArgumentException.ThrowIfNullOrWhiteSpace(connection.Database);
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        WriteExact(writer, "sql-saga-timer-logical-binding-v1");
        WriteExact(writer, connection.DataSource);
        WriteExact(writer, connection.Database);
        WriteExact(writer, schema);
        WriteExact(writer, table);
        writer.Flush();
        return stream.ToArray();
    }

    private static void WriteExact(BinaryWriter writer, string value)
    {
        writer.Write(value.Length);
        foreach (var character in value)
        {
            writer.Write((ushort)character);
        }
    }
}
