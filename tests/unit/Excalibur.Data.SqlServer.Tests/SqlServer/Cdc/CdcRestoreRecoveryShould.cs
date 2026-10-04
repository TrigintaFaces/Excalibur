// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0
using Excalibur.Cdc;
using Excalibur.Cdc.SqlServer;
using Microsoft.Extensions.Logging.Abstractions;
namespace Excalibur.Data.Tests.SqlServer.Cdc;

[Trait("Category", "Unit")]
[Trait("Component", "Data.SqlServer")]
[Trait("Pattern", "Regression")]
public sealed class CdcRestoreRecoveryShould
{
    [Theory]
    [InlineData(StalePositionRecoveryStrategy.FallbackToEarliest, 2)]
    [InlineData(StalePositionRecoveryStrategy.FallbackToLatest, 10)]
    public async Task ResetOnlyCheckpointAheadOfRestoredMaximum(StalePositionRecoveryStrategy strategy, byte expected)
    {
        var (manager, _, store, options) = Create(strategy);
        CdcPositionResetEventArgs? notification = null;
        options.OnPositionReset = (args, _) => { notification = args; return Task.CompletedTask; };
        await manager.InitializeTrackingAsync(CancellationToken.None);
        manager.GetTracking("restored")!.Lsn.ShouldBe(Lsn(expected));
        manager.GetTracking("restored")!.SequenceValue.ShouldBeNull();
        manager.GetTracking("healthy")!.Lsn.ShouldBe(Lsn(5));
        notification.ShouldNotBeNull();
        notification.CaptureInstance.ShouldBe("restored");
        notification.StalePosition.ShouldBe(Lsn(20));
        A.CallTo(() => store.UpdateLastProcessedPositionAsync(A<string>._, A<string>._, A<string>._,
            A<byte[]>._, A<byte[]?>._, A<DateTime?>._, A<long?>._, A<CancellationToken>._)).MustNotHaveHappened();
    }
    [Fact]
    public async Task RefuseIncompatibleCheckpointWhenRecoveryIsDisabled()
    {
        var (manager, _, _, _) = Create(StalePositionRecoveryStrategy.Throw);
        await Should.ThrowAsync<SqlServerCdcStalePositionException>(() => manager.InitializeTrackingAsync(CancellationToken.None));
    }
    [Fact]
    public async Task AwaitRecoveryCallbackBeforeInstallingPosition()
    {
        var (manager, _, _, options) = Create(StalePositionRecoveryStrategy.InvokeCallback);
        options.OnPositionReset = (_, _) => throw new InvalidOperationException("repair refused");
        await Should.ThrowAsync<InvalidOperationException>(() => manager.InitializeTrackingAsync(CancellationToken.None));
        manager.GetTracking("restored").ShouldBeNull();
    }
    [Fact]
    public async Task RetainStateReturnedAsLazyEnumerable()
    {
        var (manager, repository, store, _) = Create(StalePositionRecoveryStrategy.Throw);
        A.CallTo(() => repository.GetMaxPositionAsync(A<CancellationToken>._)).Returns(Lsn(30));
        A.CallTo(() => store.GetLastProcessedPositionAsync(A<string>._, A<string>._, A<CancellationToken>._))
            .Returns(Enumerable.Range(0, 1).Select(_ => new CdcProcessingState { TableName = "restored", LastProcessedLsn = Lsn(20) }));
        await manager.InitializeTrackingAsync(CancellationToken.None);
        manager.GetTracking("restored")!.Lsn.ShouldBe(Lsn(20));
    }
    [Fact]
    public async Task KeepCallbackMutationsSeparateFromSelectedPositions()
    {
        var (manager, _, _, options) = Create(StalePositionRecoveryStrategy.FallbackToLatest);
        options.OnPositionReset = (args, _) =>
        {
            Array.Fill(args.NewPosition!, (byte)255);
            Array.Fill(args.LatestAvailablePosition!, (byte)0);
            return Task.CompletedTask;
        };
        await manager.InitializeTrackingAsync(CancellationToken.None);
        manager.GetTracking("restored")!.Lsn.ShouldBe(Lsn(10));
        manager.GetTracking("healthy")!.Lsn.ShouldBe(Lsn(5));
    }

    [Fact]
    public async Task LeaveUnavailableCaptureUnresetUntilItsMaximumIsReadable()
    {
        var (manager, repository, _, options) = Create(StalePositionRecoveryStrategy.FallbackToEarliest);
        var callbacks = 0;
        options.OnPositionReset = (_, _) => { callbacks++; return Task.CompletedTask; };
        // An all-zero maximum is the EMPTY sentinel, not position zero: a valid capture instance can
        // exist before the capture job has written its first LSN mapping entry, which is exactly the
        // state a freshly restored database is in. No readable upper bound means there is no work yet,
        // so initialization must return quietly and leave the durable checkpoint alone. Throwing here
        // would crash the worker on every poll until the capture job caught up.
        A.CallTo(() => repository.GetMaxPositionAsync(A<CancellationToken>._)).Returns(Lsn(0));
        await Should.NotThrowAsync(() => manager.InitializeTrackingAsync(CancellationToken.None));
        callbacks.ShouldBe(0);
        manager.GetTracking("restored").ShouldBeNull();
        A.CallTo(() => repository.GetMaxPositionAsync(A<CancellationToken>._)).Returns(Lsn(10));
        await manager.InitializeTrackingAsync(CancellationToken.None);
        callbacks.ShouldBe(1);
        manager.GetTracking("restored")!.Lsn.ShouldBe(Lsn(2));
    }

    [Fact]
    public async Task WaitForFirstMaximumWithoutResettingDurableCheckpoint()
    {
        var (manager, repository, store, _) = Create(StalePositionRecoveryStrategy.Throw);
        A.CallTo(() => repository.GetMaxPositionAsync(A<CancellationToken>._)).Returns((byte[])null!);
        await manager.InitializeTrackingAsync(CancellationToken.None);
        manager.GetTracking("restored").ShouldBeNull();
        A.CallTo(() => repository.GetMaxPositionAsync(A<CancellationToken>._)).Returns(Lsn(30));
        await manager.InitializeTrackingAsync(CancellationToken.None);
        manager.GetTracking("restored")!.Lsn.ShouldBe(Lsn(20));
        A.CallTo(() => store.UpdateLastProcessedPositionAsync(A<string>._, A<string>._, A<string>._,
            A<byte[]>._, A<byte[]?>._, A<DateTime?>._, A<long?>._, A<CancellationToken>._)).MustNotHaveHappened();
    }

    /// <summary>
    /// The range guard: an unusable minimum, or a minimum above the maximum, is not a position to recover
    /// from -- it means the window itself is incoherent, and no recovery strategy can repair it.
    /// </summary>
    /// <remarks>
    /// This throw had NO coverage. It is distinct from every other exception in this class: the stale-position
    /// refusal raises SqlServerCdcStalePositionException, and the callback arm's InvalidOperationException comes
    /// out of the consumer's own callback rather than the guard. Both disjuncts of the guard's condition are
    /// exercised, because each alone is satisfiable while the other is false, and an arm covering only one
    /// would stay green if the other were deleted.
    /// </remarks>
    [Theory]
    [InlineData(0, 10)]
    [InlineData(20, 10)]
    public async Task RefuseAnIncoherentReadableWindow(byte minimum, byte maximum)
    {
        var (manager, repository, _, _) = Create(StalePositionRecoveryStrategy.FallbackToEarliest);
        A.CallTo(() => repository.GetMinPositionAsync(A<string>._, A<CancellationToken>._)).Returns(Lsn(minimum));
        A.CallTo(() => repository.GetMaxPositionAsync(A<CancellationToken>._)).Returns(Lsn(maximum));

        _ = await Should.ThrowAsync<InvalidOperationException>(
            () => manager.InitializeTrackingAsync(CancellationToken.None));

        manager.GetTracking("restored").ShouldBeNull(
            "a refused window must install nothing, or the caller resumes from a position the window cannot serve");
    }

    private static (CdcCheckpointManager Manager, ICdcRepository Repository, ISqlServerCdcStateStore Store, CdcRecoveryOptions Options) Create(StalePositionRecoveryStrategy strategy)
    {
        var config = A.Fake<IDatabaseOptions>();
        var repository = A.Fake<ICdcRepository>();
        var store = A.Fake<ISqlServerCdcStateStore>();
        var options = new CdcRecoveryOptions { RecoveryStrategy = strategy };
        A.CallTo(() => config.CaptureInstances).Returns(["restored", "healthy"]);
        A.CallTo(() => config.RecoveryOptions).Returns(options);
        A.CallTo(() => repository.GetMaxPositionAsync(A<CancellationToken>._)).Returns(Lsn(10));
        A.CallTo(() => repository.GetMinPositionAsync(A<string>._, A<CancellationToken>._)).Returns(Lsn(2));
        A.CallTo(() => store.GetLastProcessedPositionAsync(A<string>._, A<string>._, A<CancellationToken>._))
            .Returns(new List<CdcProcessingState>
            {
                new() { TableName = "restored", LastProcessedLsn = Lsn(20), LastProcessedSequenceValue = Lsn(9) },
                new() { TableName = "healthy", LastProcessedLsn = Lsn(5) },
            });
        return (new CdcCheckpointManager(config, repository, store, NullLogger.Instance), repository, store, options);
    }
    private static byte[] Lsn(byte value) => [0, 0, 0, 0, 0, 0, 0, 0, 0, value];
}
