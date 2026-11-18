// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch.Messaging;
using Microsoft.Data.SqlClient;

namespace Excalibur.Saga.SqlServer;

public sealed partial class SqlServerSagaTimeoutStore
{
    internal static Task RecordAtomicTimerMutationAsync(SqlConnection connection, SqlTransaction transaction,
        SagaTransitionIdentity identity, SagaPreparedTimerAction action, string? timeoutId, CancellationToken token)
        => SqlSagaTimerRecoveryReceiver.RecordAtomicTimerMutationAsync(connection, transaction, identity, action, timeoutId, token);
}
