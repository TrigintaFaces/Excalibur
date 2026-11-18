// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Buffers;
using System.Reflection;

using Excalibur.Dispatch.Benchmarks.Diagnostics;
using Excalibur.Dispatch.Serialization;
using Excalibur.Dispatch.Transport;

using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Dispatch.Tests.Benchmarks;

[Trait("Category", "Unit")]
[Trait("Component", "Dispatch.Serialization")]
[Trait("Pattern", "Regression")]
public sealed class TransportAdapterBenchmarkCleanupShould
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CloseOwnedResourcesAndContinueAfterSerializerCleanupFailure(bool injectFailure)
    {
        var fixture = new TransportAdapterPhaseBenchmarks { PayloadSizeBytes = 256, Concurrency = 1 };
        fixture.GlobalSetup();
        var serializer = Get<DispatchJsonSerializer>(fixture, "_serializer");
        var pool = Get<IUtf8JsonWriterPool>(serializer, "_writerPool");
        var adapter = Get<InMemoryTransportAdapter>(fixture, "_adapter");
        var processing = Get<Task>(adapter, "_processingTask");
        // Replace only the fixture-owned provider to observe its disposal independently.
        Get<ServiceProvider>(fixture, "_provider").Dispose();
        var services = new ServiceCollection();
        services.AddSingleton<DisposalMarker>();
        var provider = services.BuildServiceProvider();
        var marker = provider.GetRequiredService<DisposalMarker>();
        typeof(TransportAdapterPhaseBenchmarks).GetField("_provider", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(fixture, provider);
        if (injectFailure)
        {
            Get<ThreadLocal<ArrayBufferWriter<byte>>>(serializer, "_threadLocalBufferWriter").Dispose();
            var error = Should.Throw<AggregateException>(fixture.GlobalCleanup);
            error.Flatten().InnerExceptions.ShouldContain(exception => exception is ObjectDisposedException);
        }
        else
        {
            fixture.GlobalCleanup();
        }

        marker.Disposed.ShouldBeTrue();
        adapter.IsRunning.ShouldBeFalse();
        processing.IsCompletedSuccessfully.ShouldBeTrue();
        Should.Throw<ObjectDisposedException>(() => pool.Rent(new ArrayBufferWriter<byte>()));
    }

    [Fact]
    public void TolerateCleanupBeforeSetup() =>
        Should.NotThrow(new TransportAdapterPhaseBenchmarks().GlobalCleanup);

    private static T Get<T>(object instance, string name) =>
        (T)instance.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(instance)!;

    private sealed class DisposalMarker : IDisposable
    {
        public bool Disposed { get; private set; }

        public void Dispose() => Disposed = true;
    }
}
