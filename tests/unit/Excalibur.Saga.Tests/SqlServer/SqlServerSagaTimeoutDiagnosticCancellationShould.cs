// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch;
using Excalibur.Saga.SqlServer;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Saga.Tests.SqlServer;

[Trait("Category", "Unit")]
[Trait("Component", "Saga.SqlServer")]
[Trait("Pattern", "Regression")]
public sealed class SqlServerSagaTimeoutDiagnosticCancellationShould
{
    [Fact]
    public async Task PreserveCallerCancellationBeforeDiagnosticConnectionExecution()
    {
        var services = new ServiceCollection();
        services.AddDefaultTenantContext();
        using var provider = services.BuildServiceProvider();
        var factoryCalls = 0;
        var store = new SqlServerSagaTimeoutStore(() =>
        {
            factoryCalls++;
            return new SqlConnection("Server=127.0.0.1,1;Database=unused;Integrated Security=true;Connect Timeout=1;Encrypt=false");
        }, NullLogger<SqlServerSagaTimeoutStore>.Instance, provider.GetRequiredService<ITenantContext>());
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        // Shouldly's canceled-task adapter creates a replacement exception; inspect the actual await.
        OperationCanceledException? error = null;
        try
        {
            _ = await store.GetDueTimeoutsAsync(DateTimeOffset.UtcNow, cancellation.Token);
        }
        catch (OperationCanceledException cancellationError)
        {
            error = cancellationError;
        }
        error.ShouldNotBeNull().CancellationToken.ShouldBe(cancellation.Token);
        cancellation.IsCancellationRequested.ShouldBeTrue();
        factoryCalls.ShouldBe(0);
    }
}
