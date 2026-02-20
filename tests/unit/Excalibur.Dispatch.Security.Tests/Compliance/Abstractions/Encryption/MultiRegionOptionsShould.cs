// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Compliance;
using Shouldly;
using Xunit;

namespace Excalibur.Dispatch.Security.Tests.Compliance.Abstractions.Encryption;

[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class MultiRegionOptionsShould
{
    [Fact]
    public void HaveCorrectDefaults()
    {
        var primary = new RegionConfiguration
        {
            RegionId = "westeurope",
            Endpoint = new Uri("https://vault1.vault.azure.net/")
        };
        var secondary = new RegionConfiguration
        {
            RegionId = "northeurope",
            Endpoint = new Uri("https://vault2.vault.azure.net/")
        };

        var options = new MultiRegionOptions
        {
            Primary = primary,
            Secondary = secondary
        };

        options.ReplicationMode.ShouldBe(ReplicationMode.Asynchronous);
        options.RpoTarget.ShouldBe(TimeSpan.FromMinutes(15));
        options.RtoTarget.ShouldBe(TimeSpan.FromMinutes(5));
        options.HealthCheckInterval.ShouldBe(TimeSpan.FromSeconds(30));
        options.FailoverThreshold.ShouldBe(3);
        options.EnableAutomaticFailover.ShouldBeTrue();
        options.OperationTimeout.ShouldBe(TimeSpan.FromSeconds(10));
        options.AsyncReplicationInterval.ShouldBe(TimeSpan.FromMinutes(5));
        options.EnableMetrics.ShouldBeTrue();
        options.EnableAuditEvents.ShouldBeTrue();
    }

    [Fact]
    public void ConfigureRegionWithDefaults()
    {
        var region = new RegionConfiguration
        {
            RegionId = "us-east-1",
            Endpoint = new Uri("https://kms.us-east-1.amazonaws.com")
        };

        region.DisplayName.ShouldBeNull();
        region.ProviderConfiguration.ShouldBeNull();
        region.Priority.ShouldBe(0);
        region.MaxAcceptableLatency.ShouldBe(TimeSpan.FromMilliseconds(500));
        region.Enabled.ShouldBeTrue();
    }

    [Fact]
    public void ConfigureFailoverOptionsDefaults()
    {
        var options = new FailoverOptions();

        options.Strategy.ShouldBe(FailoverStrategy.GracePeriod);
        options.GracePeriod.ShouldBe(TimeSpan.FromSeconds(30));
        options.EnableNotifications.ShouldBeTrue();
        options.FailoverCooldown.ShouldBe(TimeSpan.FromMinutes(5));
    }

    [Theory]
    [InlineData(FailoverStrategy.Immediate)]
    [InlineData(FailoverStrategy.GracePeriod)]
    [InlineData(FailoverStrategy.Quorum)]
    public void SupportAllFailoverStrategies(FailoverStrategy strategy)
    {
        var options = new FailoverOptions { Strategy = strategy };
        options.Strategy.ShouldBe(strategy);
    }
}
