// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0
using Excalibur.Dispatch.Tests.Versioning.TestData;
using Excalibur.Dispatch.Versioning;

namespace Excalibur.Dispatch.Tests.Versioning;

[Trait("Category", "Unit")]
[Trait("Component", "Dispatch.Core")]
[Trait("Pattern", "Concurrency")]
public sealed class UpcastingRegistryLifetimeShould
{
    [Fact]
    public void Read_registration_versions_once_including_duplicate_diagnostics()
    {
        var pipeline = new UpcastingPipeline();
        var first = new CountingUpcaster();
        pipeline.Register(first);
        first.FromReads.ShouldBe(1);
        first.ToReads.ShouldBe(1);
        var duplicate = new CountingUpcaster();
        Should.Throw<InvalidOperationException>(() => pipeline.Register(duplicate));
        duplicate.FromReads.ShouldBe(1);
        duplicate.ToReads.ShouldBe(1);
    }

    [Fact]
    public void Keep_duplicate_metadata_getters_outside_registration_lock()
    {
        var pipeline = new UpcastingPipeline();
        pipeline.Register(new UserCreatedEventV1ToV2Upcaster());
        var field = typeof(UpcastingPipeline).GetField("_registrationLock", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("Registration lock diagnostic field missing");
        var registrationLock = (Lock)field.GetValue(pipeline)!;
        var candidate = new CountingUpcaster(() => registrationLock.IsHeldByCurrentThread.ShouldBeFalse());
        Should.Throw<InvalidOperationException>(() => pipeline.Register(candidate));
    }

    [Fact]
    public void Preserve_public_disposal_and_reject_registry_work_after_disposal()
    {
        var pipeline = new UpcastingPipeline();
        pipeline.Register(new UserCreatedEventV1ToV2Upcaster());
        pipeline.CanUpcast("UserCreatedEvent", 1, 2).ShouldBeTrue();
        var lifetime = ((object)pipeline).ShouldBeAssignableTo<IDisposable>();
        lifetime.Dispose();
        lifetime.Dispose();
        Should.Throw<ObjectDisposedException>(() => pipeline.GetLatestVersion("UserCreatedEvent"));
        Should.Throw<ObjectDisposedException>(() => pipeline.Register(new UserCreatedEventV2ToV3Upcaster()));
        Should.Throw<ObjectDisposedException>(() => pipeline.Upcast(new UserCreatedEventV2()));
        Should.Throw<ObjectDisposedException>(() => pipeline.UpcastTo(new UserCreatedEventV1(), 2));
        Should.Throw<ObjectDisposedException>(() => pipeline.CanUpcast("UserCreatedEvent", 1, 2));
        pipeline.CanUpcast("UserCreatedEvent", 2, 2).ShouldBeTrue();
        pipeline.CanUpcast("UserCreatedEvent", 2, 1).ShouldBeFalse();
        var same = new UserCreatedEventV1();
        pipeline.UpcastTo(same, 1).ShouldBeSameAs(same);
        Should.Throw<ArgumentNullException>(() => pipeline.GetLatestVersion(null!));
    }

    [Fact]
    public async Task Reject_registration_admitted_to_metadata_before_disposal()
    {
        var pipeline = new UpcastingPipeline();
        var lifetime = ((object)pipeline).ShouldBeAssignableTo<IDisposable>();
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        var candidate = new CountingUpcaster(() =>
        {
            entered.Set();
            if (!release.Wait(TimeSpan.FromSeconds(10))) throw new TimeoutException("Metadata gate");
        });
        var registration = Task.Run(() => Record.Exception(() => pipeline.Register(candidate)));
        try
        {
            entered.Wait(TimeSpan.FromSeconds(10)).ShouldBeTrue();
            lifetime.Dispose();
        }
        finally
        {
            release.Set();
        }
        (await registration.WaitAsync(TimeSpan.FromSeconds(10))).ShouldBeOfType<ObjectDisposedException>();
        Should.Throw<ObjectDisposedException>(() => pipeline.GetLatestVersion("UserCreatedEvent"));
    }

    [Fact]
    public void Refuse_registration_when_its_metadata_getter_disposes_the_pipeline()
    {
        var pipeline = new UpcastingPipeline();
        var candidate = new CountingUpcaster(pipeline.Dispose);
        Should.Throw<ObjectDisposedException>(() => pipeline.Register(candidate));
        Should.Throw<ObjectDisposedException>(() => pipeline.GetLatestVersion("UserCreatedEvent"));
    }

    [Fact]
    public async Task Finish_an_admitted_traversal_when_disposal_races_its_callback()
    {
        var pipeline = new UpcastingPipeline();
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        pipeline.Register(new CountingUpcaster(transform: old =>
        {
            entered.Set();
            if (!release.Wait(TimeSpan.FromSeconds(10))) throw new TimeoutException("Callback gate");
            return new UserCreatedEventV2 { Id = old.Id };
        }));
        var input = new UserCreatedEventV1 { Id = Guid.NewGuid() };
        var traversal = Task.Run(() => pipeline.UpcastTo(input, 2));
        try
        {
            entered.Wait(TimeSpan.FromSeconds(10)).ShouldBeTrue();
            Parallel.For(0, 8, _ => pipeline.Dispose());
        }
        finally
        {
            release.Set();
        }
        (await traversal.WaitAsync(TimeSpan.FromSeconds(10))).ShouldBeOfType<UserCreatedEventV2>().Id.ShouldBe(input.Id);
        Should.Throw<ObjectDisposedException>(() => pipeline.Upcast(input));
    }

    [Fact]
    public void Permit_disposal_inside_an_admitted_upcaster_callback()
    {
        var pipeline = new UpcastingPipeline();
        pipeline.Register(new CountingUpcaster(transform: old =>
        {
            pipeline.Dispose();
            return new UserCreatedEventV2 { Id = old.Id };
        }));
        pipeline.UpcastTo(new UserCreatedEventV1(), 2).ShouldBeOfType<UserCreatedEventV2>();
        Should.Throw<ObjectDisposedException>(() => pipeline.GetLatestVersion("UserCreatedEvent"));
    }

    private sealed class CountingUpcaster(Action? onFrom = null, Func<UserCreatedEventV1, UserCreatedEventV2>? transform = null) : IMessageUpcaster<UserCreatedEventV1, UserCreatedEventV2>
    {
        public int FromReads { get; private set; }
        public int ToReads { get; private set; }
        public int FromVersion { get { FromReads++; onFrom?.Invoke(); return 1; } }
        public int ToVersion { get { ToReads++; return 2; } }
        public UserCreatedEventV2 Upcast(UserCreatedEventV1 oldMessage) => transform is null ? new() { Id = oldMessage.Id } : transform(oldMessage);
    }
}
