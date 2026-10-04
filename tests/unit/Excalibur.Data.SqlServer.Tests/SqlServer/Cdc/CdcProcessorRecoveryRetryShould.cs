// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0
using Excalibur.Cdc.SqlServer;
using Microsoft.Extensions.Logging.Abstractions;
using Polly;
namespace Excalibur.Data.Tests.SqlServer.Cdc;

[Trait("Category", "Unit")]
[Trait("Component", "Data.SqlServer")]
[Trait("Pattern", "Regression")]
public sealed class CdcProcessorRecoveryRetryShould
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task RetryTransientBoundReadThroughConfiguredPolicy(bool minimumRead)
    {
        var config = A.Fake<IDatabaseOptions>();
        A.CallTo(() => config.CaptureInstances).Returns(["table"]);
        var repository = A.Fake<ICdcRepository>();
        var state = A.Fake<ISqlServerCdcStateStore>();
        var factory = A.Fake<IDataAccessPolicyFactory>();
        A.CallTo(() => factory.GetComprehensivePolicy()).Returns(Policy.Handle<TimeoutException>().RetryAsync(1));
        A.CallTo(() => repository.GetMinPositionAsync(A<string>._, A<CancellationToken>._)).Returns(new byte[] { 1 });
        A.CallTo(() => repository.GetMaxPositionAsync(A<CancellationToken>._)).Returns(new byte[] { 10 });
        var attempts = 0;
        if (minimumRead)
        {
            A.CallTo(() => repository.GetMinPositionAsync("table", A<CancellationToken>._))
                .ReturnsLazily(() => ++attempts == 1 ? throw new TimeoutException("transient") : new byte[] { 1 });
        }
        else
        {
            A.CallTo(() => repository.GetMaxPositionAsync(A<CancellationToken>._))
                .ReturnsLazily(() => ++attempts == 1 ? throw new TimeoutException("transient") : new byte[] { 10 });
        }
        var manager = new CdcCheckpointManager(config, repository, state, NullLogger.Instance, factory);
        await manager.InitializeTrackingAsync(CancellationToken.None);
        attempts.ShouldBe(2);
        manager.GetTracking("table")!.Lsn.ShouldBe(new byte[] { 1 });
        A.CallTo(() => factory.GetComprehensivePolicy()).MustHaveHappened();
    }
}
