// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0
using Excalibur.Dispatch.LeaderElection;
using Excalibur.LeaderElection.SqlServer;
using Excalibur.LeaderElection.Postgres;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Excalibur.LeaderElection.Tests;

[Trait("Category", "Unit")]
[Trait("Component", "LeaderElection")]
public sealed class HealthWrapperFreshLifecycleShould
{
    [Fact]
    public async Task StopAndDisposeAFreshSqlWrapperWithoutRequiringItsHealthTable()
    {
        var wrapper = new SqlServerHealthBasedLeaderElection(Options.Create(new LeaderElectionOptions()),
            Options.Create(new SqlServerHealthBasedLeaderElectionOptions
            { ConnectionString = "Server=127.0.0.1,1;Database=missing;User Id=missing;Password=missing;Connect Timeout=1;TrustServerCertificate=true", LockResource = "never-started" }),
            NullLogger<SqlServerHealthBasedLeaderElection>.Instance, NullLogger<SqlServerLeaderElection>.Instance);
        await wrapper.StopAsync(TestContext.Current.CancellationToken);
        await wrapper.DisposeAsync(); await wrapper.DisposeAsync();
        wrapper.IsLeader.ShouldBeFalse();
    }

    [Fact]
    public async Task StopAndDisposeAFreshPostgresWrapperWithoutRequiringItsHealthTable()
    {
        var wrapper = new PostgresHealthBasedLeaderElection(
            Options.Create(new PostgresLeaderElectionOptions { ConnectionString = "Host=127.0.0.1;Port=1;Database=missing;Username=missing;Password=missing;Timeout=1" }),
            Options.Create(new LeaderElectionOptions()), Options.Create(new PostgresHealthBasedLeaderElectionOptions()),
            NullLogger<PostgresHealthBasedLeaderElection>.Instance, NullLogger<PostgresLeaderElection>.Instance);
        await wrapper.StopAsync(TestContext.Current.CancellationToken);
        await wrapper.DisposeAsync(); await wrapper.DisposeAsync();
        wrapper.IsLeader.ShouldBeFalse();
    }
}
