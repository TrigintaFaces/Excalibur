// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Security.Cryptography;
using Excalibur.Data.InMemory.Snapshots;
using Excalibur.EventSourcing.Diagnostics;
using Excalibur.EventSourcing.Snapshots.Compression;
using Excalibur.EventSourcing.Snapshots.Security;
using Excalibur.Testing.Conformance;
using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Tests.Testing.Conformance;

/// <summary>Runs every snapshot contract arm through each shipping snapshot decorator.</summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
[Trait("Pattern", "STORE")]
public sealed class DecoratorSnapshotStoreConformanceTests : SnapshotStoreConformanceTestKit, IDisposable
{
    private readonly Meter _meter = new("SnapshotDecoratorConformance");
    private readonly ActivitySource _activities = new("SnapshotDecoratorConformance");
    private int _decorator;

    protected override Task<ISnapshotStore> CreateStoreAsync()
    {
        ISnapshotStore inner = new InMemorySnapshotStore(Options.Create(new InMemorySnapshotOptions()),
            NullLogger<InMemorySnapshotStore>.Instance, new ConformanceAmbientTenantContext());
        ISnapshotStore store = _decorator switch
        {
            0 => new TelemetrySnapshotStore(inner, _meter, _activities, "inmemory"),
            1 => new CompressingSnapshotStore(inner, Options.Create(new SnapshotCompressionOptions
                { Algorithm = SnapshotCompressionAlgorithm.Brotli, MinimumSizeBytes = 0 })),
            2 => new CompressingSnapshotStore(inner, Options.Create(new SnapshotCompressionOptions
                { Algorithm = SnapshotCompressionAlgorithm.GZip, MinimumSizeBytes = 0 })),
            3 => new EncryptingSnapshotStore(inner, new AesSnapshotEncryptor()),
            _ => throw new InvalidOperationException("Unknown decorator test case."),
        };
        return Task.FromResult(store);
    }

    public void Dispose()
    {
        _activities.Dispose();
        _meter.Dispose();
    }

    // Authenticated encryption exercises transformed bytes, including empty payloads and concurrent calls.
    private sealed class AesSnapshotEncryptor : ISnapshotEncryptor
    {
        private readonly byte[] _key = RandomNumberGenerator.GetBytes(32);

        public ValueTask<byte[]> EncryptAsync(byte[] data, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var output = new byte[28 + data.Length];
            RandomNumberGenerator.Fill(output.AsSpan(0, 12));
            using var cipher = new AesGcm(_key, 16);
            cipher.Encrypt(output.AsSpan(0, 12), data, output.AsSpan(28), output.AsSpan(12, 16));
            return ValueTask.FromResult(output);
        }

        public ValueTask<byte[]> DecryptAsync(byte[] data, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var output = new byte[data.Length - 28];
            using var cipher = new AesGcm(_key, 16);
            cipher.Decrypt(data.AsSpan(0, 12), data.AsSpan(28), data.AsSpan(12, 16), output);
            return ValueTask.FromResult(output);
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public Task GetLatestSnapshotAsync_NoSnapshot_ShouldReturnNull_Test(int decorator)
    {
        _decorator = decorator;
        return GetLatestSnapshotAsync_NoSnapshot_ShouldReturnNull();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public Task SaveAndGetLatestSnapshot_ShouldRoundTrip_Test(int decorator)
    {
        _decorator = decorator;
        return SaveAndGetLatestSnapshot_ShouldRoundTrip();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public Task GetLatestSnapshot_MultipleVersions_ShouldReturnLatest_Test(int decorator)
    {
        _decorator = decorator;
        return GetLatestSnapshot_MultipleVersions_ShouldReturnLatest();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public Task SaveSnapshot_ShouldUpdateLatest_Test(int decorator)
    {
        _decorator = decorator;
        return SaveSnapshot_ShouldUpdateLatest();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public Task SaveSnapshot_StaleOrEqualVersion_ShouldBeASuccessfulNoOp_Test(int decorator)
    {
        _decorator = decorator;
        return SaveSnapshot_StaleOrEqualVersion_ShouldBeASuccessfulNoOp();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public Task DeleteSnapshots_ShouldRemoveAll_Test(int decorator)
    {
        _decorator = decorator;
        return DeleteSnapshots_ShouldRemoveAll();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public Task DeleteSnapshotsOlderThan_ShouldPreserveNewer_Test(int decorator)
    {
        _decorator = decorator;
        return DeleteSnapshotsOlderThan_ShouldPreserveNewer();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public Task DeleteSnapshots_NonExistent_ShouldNotThrow_Test(int decorator)
    {
        _decorator = decorator;
        return DeleteSnapshots_NonExistent_ShouldNotThrow();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public Task Snapshots_ShouldIsolateByAggregateType_Test(int decorator)
    {
        _decorator = decorator;
        return Snapshots_ShouldIsolateByAggregateType();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public Task Snapshots_ShouldIsolateByAggregateId_Test(int decorator)
    {
        _decorator = decorator;
        return Snapshots_ShouldIsolateByAggregateId();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public Task DeleteSnapshots_ShouldNotAffectOtherAggregates_Test(int decorator)
    {
        _decorator = decorator;
        return DeleteSnapshots_ShouldNotAffectOtherAggregates();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public Task SaveAndLoad_ShouldPreserveData_Test(int decorator)
    {
        _decorator = decorator;
        return SaveAndLoad_ShouldPreserveData();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public Task Snapshots_HigherVersionFromAnotherTenant_MustNotOverwrite_Test(int decorator)
    {
        _decorator = decorator;
        return Snapshots_HigherVersionFromAnotherTenant_MustNotOverwrite();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public Task Snapshots_SaveBehindAnotherTenantsVersion_MustNotBeDiscarded_Test(int decorator)
    {
        _decorator = decorator;
        return Snapshots_SaveBehindAnotherTenantsVersion_MustNotBeDiscarded();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public Task Snapshots_EachTenant_MustReadItsOwnForASharedAggregateId_Test(int decorator)
    {
        _decorator = decorator;
        return Snapshots_EachTenant_MustReadItsOwnForASharedAggregateId();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public Task SaveSnapshot_ConcurrentRisingVersions_ShouldLeaveTheHighestReadable_Test(int decorator)
    {
        _decorator = decorator;
        return SaveSnapshot_ConcurrentRisingVersions_ShouldLeaveTheHighestReadable();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public Task GetLatestSnapshot_ConcurrentReaders_ShouldAllObserveIt_Test(int decorator)
    {
        _decorator = decorator;
        return GetLatestSnapshot_ConcurrentReaders_ShouldAllObserveIt();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public Task Store_ShouldNotFaultWhenManyCallersArriveAtOnce_Test(int decorator)
    {
        _decorator = decorator;
        return Store_ShouldNotFaultWhenManyCallersArriveAtOnce();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public Task SaveAndLoad_EmptyPayload_ShouldRoundTripAsEmpty_Test(int decorator)
    {
        _decorator = decorator;
        return SaveAndLoad_EmptyPayload_ShouldRoundTripAsEmpty();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public Task SaveAndLoad_LargePayload_ShouldRoundTripByteForByte_Test(int decorator)
    {
        _decorator = decorator;
        return SaveAndLoad_LargePayload_ShouldRoundTripByteForByte();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public Task SaveAndLoad_AggregateIdWithSeparatorCharacters_ShouldRoundTrip_Test(int decorator)
    {
        _decorator = decorator;
        return SaveAndLoad_AggregateIdWithSeparatorCharacters_ShouldRoundTrip();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public Task ConformanceSuite_ShouldWireEveryArm_Test(int decorator)
    {
        _decorator = decorator;
        return ConformanceSuite_ShouldWireEveryArm();
    }

}
