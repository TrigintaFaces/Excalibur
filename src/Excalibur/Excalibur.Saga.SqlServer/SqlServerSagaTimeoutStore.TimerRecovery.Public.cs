// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Saga.Abstractions;
using Microsoft.Data.SqlClient;

namespace Excalibur.Saga.SqlServer;

public sealed partial class SqlServerSagaTimeoutStore
{
    private Task<bool> TryScheduleWithRecoveryAuthorityAsync(SqlConnection connection, string table, SagaTimeout timeout, CancellationToken token)
        => _timerRecovery.TryScheduleWithRecoveryAuthorityAsync(connection, table, timeout, token);

    private Task<(bool Handled, int Count)> TryCancelWithRecoveryAuthorityAsync(SqlConnection connection, string table, string sagaId, string? timeoutId, CancellationToken token)
        => _timerRecovery.TryCancelWithRecoveryAuthorityAsync(connection, table, sagaId, timeoutId, token);
}
