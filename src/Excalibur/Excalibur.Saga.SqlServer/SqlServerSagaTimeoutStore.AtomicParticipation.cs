// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Data;

using Excalibur.Dispatch.Messaging;

using Microsoft.Data.SqlClient;

namespace Excalibur.Saga.SqlServer;

public sealed partial class SqlServerSagaTimeoutStore
{
    object? IServiceProvider.GetService(Type serviceType)
    {
        ArgumentNullException.ThrowIfNull(serviceType);
        if (serviceType == typeof(ISagaAtomicTimeoutParticipant))
        {
            var schema = _options.SchemaName;
            var table = _options.TableName;
            _options.Validate();
            return new AtomicTimerParticipant(this, schema, table,
                SqlSagaTimerBinding.CaptureClosed(_connectionFactory, schema, table));
        }

        return serviceType.IsInstanceOfType(this) ? this : null;
    }

    private sealed class AtomicTimerParticipant(
        SqlServerSagaTimeoutStore owner, string schema, string table, byte[] binding)
        : ISagaAtomicTimeoutParticipant
    {
        public byte[] GetCanonicalBindingBytes() => (byte[])binding.Clone();

        public void ValidateTransaction(IDbTransaction transaction, string authoritativeTenantId)
        {
            ArgumentNullException.ThrowIfNull(transaction);
            ArgumentNullException.ThrowIfNull(authoritativeTenantId);
            if (!string.Equals(owner.CurrentPartition().TenantId, authoritativeTenantId, StringComparison.Ordinal)
                || !string.Equals(owner._options.SchemaName, schema, StringComparison.Ordinal)
                || !string.Equals(owner._options.TableName, table, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Selected timer authority or configured table changed.");
            }

            if (transaction is not SqlTransaction { Connection: { State: ConnectionState.Open } connection }
                || !binding.AsSpan().SequenceEqual(SqlSagaTimerBinding.ForWriter(connection, schema, table)))
            {
                throw new InvalidOperationException("The atomic transaction does not own the selected timer backend/table.");
            }
        }
    }
}
