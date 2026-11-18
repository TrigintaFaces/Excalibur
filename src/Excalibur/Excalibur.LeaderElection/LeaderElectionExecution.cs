// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Transactions;

namespace Excalibur.LeaderElection;

/// <summary>Runs owned election infrastructure independently of an incoming business transaction.</summary>
/// <remarks>Activity, logging scopes and other execution-context data continue to flow.</remarks>
internal static class LeaderElectionExecution
{
    internal static Task RunAsync(Func<Task> operation)
    {
        ArgumentNullException.ThrowIfNull(operation);
#pragma warning disable RS0030 // A tracked infrastructure worker must not invoke user/SDK code under a provider state gate; its owner joins the returned task.
        return Task.Run(() => InvokeAsync(operation));
#pragma warning restore RS0030
    }

    internal static async Task InvokeAsync(Func<Task> operation)
    {
        ArgumentNullException.ThrowIfNull(operation);
        // Suppress during execution and across awaits, rather than disposing a scope before
        // scheduling. Do not suppress all ExecutionContext: diagnostics must remain available.
        using var independent = new TransactionScope(TransactionScopeOption.Suppress, TransactionScopeAsyncFlowOption.Enabled);
        await operation().ConfigureAwait(false);
        independent.Complete();
    }
}
