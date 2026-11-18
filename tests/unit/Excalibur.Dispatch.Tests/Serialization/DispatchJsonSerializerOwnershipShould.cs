// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Buffers;
using System.Reflection;

using Excalibur.Dispatch.Serialization;

namespace Excalibur.Dispatch.Tests.Serialization;

[Trait("Category", "Unit")]
[Trait("Component", "Dispatch.Serialization")]
[Trait("Pattern", "Regression")]
public sealed class DispatchJsonSerializerOwnershipShould
{
    [Fact]
    public void DisposeTheInternallyCreatedWriterPool()
    {
        var serializer = new DispatchJsonSerializer();
        var pool = GetPool(serializer);
        serializer.Dispose();
        serializer.Dispose();
        Should.Throw<ObjectDisposedException>(() => pool.Rent(new ArrayBufferWriter<byte>()));
    }

    [Fact]
    public void LeaveTheBorrowedWriterPoolUsable()
    {
        using var pool = new Utf8JsonWriterPool(enableAdaptiveSizing: false);
        var serializer = new DispatchJsonSerializer(configure: null, jsonContext: null, writerPool: pool, bufferManager: null);
        serializer.Dispose();
        var writer = pool.Rent(new ArrayBufferWriter<byte>());
        writer.WriteStringValue("still owned by caller");
        pool.ReturnToPool(writer);
    }

    [Fact]
    public void ClearBuffersCreatedOnlyOnAnotherThread()
    {
        var serializer = new DispatchJsonSerializer();
        var pool = GetPool(serializer);
        var buffers = GetBuffers(serializer);
        ArrayBufferWriter<byte>? workerBuffer = null;
        Exception? failure = null;
        var worker = new Thread(() =>
        {
            try
            {
                serializer.SerializeToUtf8Bytes("worker", typeof(string));
                workerBuffer = buffers.Value;
            }
            catch (Exception exception)
            {
                failure = exception;
            }
        });
        worker.Start();
        worker.Join(TimeSpan.FromSeconds(10)).ShouldBeTrue();
        failure.ShouldBeNull();
        buffers.IsValueCreated.ShouldBeFalse();
        workerBuffer.ShouldNotBeNull().WrittenCount.ShouldBeGreaterThan(0);
        serializer.Dispose();
        workerBuffer.WrittenCount.ShouldBe(0);
        Should.Throw<ObjectDisposedException>(() => pool.Rent(new ArrayBufferWriter<byte>()));
    }

    [Fact]
    public async Task CompleteMultipleDisposalRequestsAfterOperationsHaveCompleted()
    {
        var serializer = new DispatchJsonSerializer();
        var pool = GetPool(serializer);
        await Task.WhenAll(Enumerable.Range(0, 32).Select(_ => Task.Run(serializer.Dispose)));
        Should.Throw<ObjectDisposedException>(() => pool.Rent(new ArrayBufferWriter<byte>()));
    }

    [Fact]
    public void CloseOwnedPoolEvenWhenBufferCleanupFails()
    {
        var serializer = new DispatchJsonSerializer();
        var pool = GetPool(serializer);
        // Inject a deterministic failure in Values enumeration, independently of the pool.
        GetBuffers(serializer).Dispose();
        var failure = Should.Throw<AggregateException>(serializer.Dispose);
        failure.InnerExceptions.ShouldContain(exception => exception is ObjectDisposedException);
        Should.Throw<ObjectDisposedException>(() => pool.Rent(new ArrayBufferWriter<byte>()));
        Should.NotThrow(serializer.Dispose);
    }

    private static ThreadLocal<ArrayBufferWriter<byte>> GetBuffers(DispatchJsonSerializer serializer) =>
        (ThreadLocal<ArrayBufferWriter<byte>>)typeof(DispatchJsonSerializer)
            .GetField("_threadLocalBufferWriter", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(serializer)!;

    private static IUtf8JsonWriterPool GetPool(DispatchJsonSerializer serializer) =>
        (IUtf8JsonWriterPool)typeof(DispatchJsonSerializer)
            .GetField("_writerPool", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(serializer)!;
}
