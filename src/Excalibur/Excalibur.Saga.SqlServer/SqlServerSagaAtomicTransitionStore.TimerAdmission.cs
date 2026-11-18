// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch.Messaging;

using Microsoft.Data.SqlClient;

namespace Excalibur.Saga.SqlServer;

internal sealed partial class SqlServerSagaAtomicTransitionStore
{
    public ValueTask ValidateTimerParticipantAsync(SagaScopeIdentity identity,
        ISagaAtomicTimeoutParticipant participant, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(identity);
        ArgumentNullException.ThrowIfNull(participant);
        ValidateTenant(identity.TenantId);
        cancellationToken.ThrowIfCancellationRequested();
        // Same trusted encoder and exact fixed writer table as Commit's actual open check.
        // This privately-created connection is never opened; no caller factory is reinterpreted.
        var expected = SqlSagaTimerBinding.CaptureClosed(() => new SqlConnection(connectionString), "dbo", "SagaTimeouts");
        if (!expected.AsSpan().SequenceEqual(participant.GetCanonicalBindingBytes()))
        {
            throw new InvalidOperationException("Selected timer backend/table does not match the atomic writer.");
        }

        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.CompletedTask;
    }
}
