// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Data;
using Microsoft.Data.SqlClient;

namespace Excalibur.Saga.SqlServer;

public sealed partial class SqlServerSagaTimeoutStore
{
    // Keep the existing private retained-cancellation request and its SQL on the owning facade.
    internal static Task<int> ExecuteRetainedTimerCancellationAsync(SqlConnection connection, SqlTransaction transaction,
        string table, bool owned, string tenant, string sagaId, string? timeoutId, bool normalizeSagaId, CancellationToken token)
        => connection.ResolveAsync(new CancelRetainedTimeoutRequest(table, owned, tenant, sagaId, timeoutId,
            transaction, token, normalizeSagaId));

    internal static void PreserveTimerRecoveryFailure(ref Exception? primary, Exception cleanup, string phase)
        => PreserveCancellationFailure(ref primary, cleanup, phase);
}
