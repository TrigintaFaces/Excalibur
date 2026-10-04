// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0
using System.Threading.Channels;
using Excalibur.Cdc.SqlServer;
using Microsoft.Extensions.Logging.Abstractions;
using Polly;
namespace Excalibur.Data.Tests.SqlServer.Cdc;

[Trait("Category", "Unit")]
[Trait("Component", "Data.SqlServer")]
[Trait("Pattern", "Regression")]
public sealed class CdcChangeDetectorStaleLsnShould
{
    [Theory]
    [InlineData(1, 2, 10)]
    [InlineData(20, 2, 10)]
    public async Task RefuseChangedBoundsWithoutResettingLiveTracking(byte saved, byte minimum, byte maximum)
    {
        var repo = A.Fake<ICdcRepository>();
        var state = A.Fake<ISqlServerCdcStateStore>();
        var config = A.Fake<IDatabaseOptions>();
        var policy = A.Fake<IDataAccessPolicyFactory>();
        A.CallTo(() => policy.GetComprehensivePolicy()).Returns(Policy.NoOpAsync());
        A.CallTo(() => repo.GetMaxPositionAsync(A<CancellationToken>._)).Returns(new byte[] { maximum });
        A.CallTo(() => repo.GetMinPositionAsync(A<string>._, A<CancellationToken>._)).Returns(new byte[] { minimum });
        var checkpoint = new CdcCheckpointManager(config, repo, state, NullLogger.Instance);
        checkpoint.UpdateLsnTracking("changed", [saved], [7]);
        var detector = new CdcChangeDetector(repo, A.Fake<ICdcRepositoryLsnMapping>(), config, policy, checkpoint, NullLogger.Instance);
        var queue = Channel.CreateUnbounded<DataChangeEvent>();
        await Should.ThrowAsync<SqlServerCdcStalePositionException>(() => detector.ProducerLoopCoreAsync([saved], queue.Writer, 2, CancellationToken.None));
        checkpoint.GetTracking("changed")!.Lsn.ShouldBe(new byte[] { saved });
        checkpoint.GetTracking("changed")!.SequenceValue.ShouldBe(new byte[] { 7 });
        queue.Reader.Count.ShouldBe(0);
        A.CallTo(() => repo.FetchChangesAsync(A<string>._, A<int>._, A<byte[]>._, A<byte[]>._, A<byte[]?>._,
            A<CdcOperationCodes>._, A<CancellationToken>._, A<string?>._)).MustNotHaveHappened();
    }

    [Fact]
    public async Task CheckEveryTrackedStartAgainstTheNewMaximum()
    {
        var repo = A.Fake<ICdcRepository>();
        var config = A.Fake<IDatabaseOptions>();
        A.CallTo(() => repo.GetMaxPositionAsync(A<CancellationToken>._)).Returns(new byte[] { 10 });
        var checkpoint = new CdcCheckpointManager(config, repo, A.Fake<ISqlServerCdcStateStore>(), NullLogger.Instance);
        checkpoint.UpdateLsnTracking("healthy", [5], null);
        checkpoint.UpdateLsnTracking("restored", [20], null);
        var detector = new CdcChangeDetector(repo, A.Fake<ICdcRepositoryLsnMapping>(), config,
            A.Fake<IDataAccessPolicyFactory>(), checkpoint, NullLogger.Instance);
        await Should.ThrowAsync<SqlServerCdcStalePositionException>(() => detector.ProducerLoopCoreAsync([5],
            Channel.CreateUnbounded<DataChangeEvent>().Writer, 2, CancellationToken.None));
        checkpoint.GetTracking("healthy")!.Lsn.ShouldBe(new byte[] { 5 });
    }
}
