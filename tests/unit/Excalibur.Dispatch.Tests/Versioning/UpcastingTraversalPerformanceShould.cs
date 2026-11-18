// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch.Tests.Versioning.TestData;
using Excalibur.Dispatch.Versioning;

namespace Excalibur.Dispatch.Tests.Versioning;

[Trait("Category", "Unit")]
[Trait("Component", "Dispatch.Core")]
[Trait("Pattern", "Concurrency")]
public sealed class UpcastingTraversalPerformanceShould
{
    private static readonly int[] ExpectedVersionSequence = [2, 3, 4];

    [Fact]
    public void Select_the_requested_edge_among_other_outgoing_versions()
    {
        var pipeline = new UpcastingPipeline();
        var calls = new List<int>();
        pipeline.Register(new TestUpcaster<UserCreatedEventV1, UserCreatedEventV3>(1, 3, old =>
        {
            calls.Add(3);
            return new UserCreatedEventV3 { Id = old.Id, FirstName = "third" };
        }));
        pipeline.Register(new TestUpcaster<UserCreatedEventV1, UserCreatedEventV2>(1, 2, old =>
        {
            calls.Add(2);
            return new UserCreatedEventV2 { Id = old.Id, FirstName = "second" };
        }));
        pipeline.Register(new TestUpcaster<UserCreatedEventV1, UserCreatedEventV4>(1, 4, old =>
        {
            calls.Add(4);
            return new UserCreatedEventV4 { Id = old.Id, FirstName = "fourth" };
        }));
        var input = new UserCreatedEventV1 { Id = Guid.NewGuid(), Name = "original" };

        pipeline.UpcastTo(input, 2).ShouldBeOfType<UserCreatedEventV2>().FirstName.ShouldBe("second");
        pipeline.UpcastTo(input, 3).ShouldBeOfType<UserCreatedEventV3>().FirstName.ShouldBe("third");
        pipeline.UpcastTo(input, 4).ShouldBeOfType<UserCreatedEventV4>().FirstName.ShouldBe("fourth");
        calls.ShouldBe(ExpectedVersionSequence);
        input.Name.ShouldBe("original");
    }

    [Fact]
    public void Preserve_callback_order_and_intermediate_identity_after_path_warmup()
    {
        var pipeline = new UpcastingPipeline();
        var input = new UserCreatedEventV1 { Id = Guid.NewGuid(), Name = "original" };
        var calls = new List<int>();
        UserCreatedEventV2? intermediate2 = null;
        UserCreatedEventV3? intermediate3 = null;
        pipeline.Register(new TestUpcaster<UserCreatedEventV1, UserCreatedEventV2>(1, 2, old =>
        {
            old.ShouldBeSameAs(input);
            calls.Add(2);
            return intermediate2 = new UserCreatedEventV2 { Id = old.Id, FirstName = "first", LastName = "last" };
        }));
        pipeline.Register(new TestUpcaster<UserCreatedEventV2, UserCreatedEventV3>(2, 3, old =>
        {
            old.ShouldBeSameAs(intermediate2);
            calls.Add(3);
            return intermediate3 = new UserCreatedEventV3 { Id = old.Id, FirstName = old.FirstName + "-second", LastName = old.LastName, Email = "fixed@example.com" };
        }));
        pipeline.Register(new TestUpcaster<UserCreatedEventV3, UserCreatedEventV4>(3, 4, old =>
        {
            old.ShouldBeSameAs(intermediate3);
            calls.Add(4);
            return new UserCreatedEventV4 { Id = old.Id, FirstName = old.FirstName + "-third", LastName = old.LastName, Email = old.Email, CreatedAt = DateTimeOffset.UnixEpoch };
        }));

        UserCreatedEventV4? previous = null;
        for (var repeat = 0; repeat < 3; repeat++)
        {
            calls.Clear();
            var result = pipeline.Upcast(input).ShouldBeOfType<UserCreatedEventV4>();
            result.Id.ShouldBe(input.Id);
            result.FirstName.ShouldBe("first-second-third");
            result.LastName.ShouldBe("last");
            result.Email.ShouldBe("fixed@example.com");
            result.CreatedAt.ShouldBe(DateTimeOffset.UnixEpoch);
            result.ShouldNotBeSameAs(previous);
            calls.ShouldBe(ExpectedVersionSequence);
            previous = result;
        }
    }

    /// <summary>
    /// A throwing upcaster must surface its own exception and leave the pipeline usable, including for
    /// further registration. Previously this also asserted the read lock was released; there is no
    /// longer a lock to release, but the observable property it protected is unchanged and still bound.
    /// </summary>
    [Fact]
    public async Task Preserve_callback_errors_and_stay_usable_afterwards()
    {
        var pipeline = new UpcastingPipeline();
        var next = new UserCreatedEventV2ToV3Upcaster();
        var expected = new InvalidOperationException("callback failure");
        var fail = true;
        pipeline.Register(new TestUpcaster<UserCreatedEventV1, UserCreatedEventV2>(1, 2, old =>
        {
            if (fail)
            {
                fail = false;
                throw expected;
            }

            return new UserCreatedEventV2 { Id = old.Id, FirstName = "retry", LastName = "works" };
        }));
        var input = new UserCreatedEventV1 { Id = Guid.NewGuid(), Name = "original" };

        Should.Throw<InvalidOperationException>(() => pipeline.UpcastTo(input, 2)).ShouldBeSameAs(expected);

        await Task.Run(() => pipeline.Register(next)).WaitAsync(TimeSpan.FromSeconds(10));
        var result = pipeline.UpcastTo(input, 3).ShouldBeOfType<UserCreatedEventV3>();
        result.Id.ShouldBe(input.Id);
        result.FirstName.ShouldBe("retry");
        _ = Should.Throw<InvalidOperationException>(() => pipeline.Register(next));
    }

    /// <summary>
    /// A callback that registers an upcaster mid-traversal must succeed, and the traversal in flight must
    /// finish against the graph it began with.
    /// </summary>
    /// <remarks>
    /// This arm replaces one that asserted <c>LockRecursionException</c>. That exception was not a
    /// guarantee; it was a reader-writer lock held across a consumer-supplied delegate leaking as a
    /// failure, and a test naming a lock primitive had made the mechanism the published contract.
    ///
    /// Be precise about what passes here, because the obvious reading is wrong: this is NOT exercising
    /// lock re-entrancy. The stack is Upcast, then the traversal which holds nothing, then the consumer
    /// upcaster, then Register — there is no enclosing Register frame, so the registration lock is
    /// acquired for the FIRST time. The arm passes because the pipeline holds no registration lock across the callback. Reinstating a
    /// lock around the traversal turns it red, which is the point of it.
    /// </remarks>
    [Fact]
    public async Task Allow_a_callback_to_register_mid_traversal_and_finish_on_the_starting_graph()
    {
        var pipeline = new UpcastingPipeline();
        var next = new UserCreatedEventV2ToV3Upcaster();
        var registered = false;
        pipeline.Register(new TestUpcaster<UserCreatedEventV1, UserCreatedEventV2>(1, 2, old =>
        {
            if (!registered)
            {
                registered = true;
                pipeline.Register(next);
            }

            return new UserCreatedEventV2 { Id = old.Id, FirstName = "retry", LastName = "works" };
        }));
        var input = new UserCreatedEventV1 { Id = Guid.NewGuid(), Name = "original" };

        // No deadlock, no recursion failure: the callback registers and the traversal completes.
        var intermediate = await Task.Run(() => pipeline.UpcastTo(input, 2))
            .WaitAsync(TimeSpan.FromSeconds(10));

        registered.ShouldBeTrue();
        _ = intermediate.ShouldBeOfType<UserCreatedEventV2>();

        // The registration the callback performed is visible to the NEXT call.
        pipeline.GetLatestVersion("UserCreatedEvent").ShouldBe(3);
        var result = pipeline.UpcastTo(input, 3).ShouldBeOfType<UserCreatedEventV3>();
        result.Id.ShouldBe(input.Id);

        // And it really was registered once, so a second attempt is refused as a duplicate.
        _ = Should.Throw<InvalidOperationException>(() => pipeline.Register(next));
    }

    [Fact]
    public void Invalidate_negative_and_longer_cached_paths_after_registration()
    {
        var pipeline = new UpcastingPipeline();
        pipeline.Register(new UserCreatedEventV1ToV2Upcaster());
        pipeline.CanUpcast("UserCreatedEvent", 1, 3).ShouldBeFalse();
        pipeline.Register(new UserCreatedEventV2ToV3Upcaster());
        pipeline.CanUpcast("UserCreatedEvent", 1, 3).ShouldBeTrue();
        pipeline.Register(new UserCreatedEventV3ToV4Upcaster());
        var input = new UserCreatedEventV1 { Id = Guid.NewGuid(), Name = "original" };
        _ = pipeline.Upcast(input).ShouldBeOfType<UserCreatedEventV4>();
        pipeline.Register(new TestUpcaster<UserCreatedEventV1, UserCreatedEventV4>(1, 4,
            old => new UserCreatedEventV4 { Id = old.Id, FirstName = "shortcut" }));

        var result = pipeline.Upcast(input).ShouldBeOfType<UserCreatedEventV4>();
        result.Id.ShouldBe(input.Id);
        result.FirstName.ShouldBe("shortcut");
        pipeline.GetLatestVersion("UserCreatedEvent").ShouldBe(4);
    }

    /// <summary>
    /// A registration on one thread must not wait on an upcaster callback running on another.
    /// </summary>
    /// <remarks>
    /// This is the liveness arm for the pipeline holding no registration lock across a callback. Under the
    /// reader-writer-lock design this replaced, the read lock was held across the consumer-supplied
    /// upcast delegate, so a registration could not proceed until every in-flight traversal finished --
    /// and a slow or blocked upcaster stalled registration for the whole process. The safety half is
    /// asserted alongside it: the traversal already in flight still completes and still returns the
    /// right message, rather than being disturbed by the concurrent registration.
    ///
    /// Reinstating any lock around traversal makes this arm time out, which is the point of it.
    /// </remarks>
    [Fact]
    public async Task Admit_a_registration_while_an_upcaster_callback_is_still_running()
    {
        var pipeline = new UpcastingPipeline();
        using var callbackEntered = new ManualResetEventSlim(false);
        using var releaseCallback = new ManualResetEventSlim(false);

        pipeline.Register(new TestUpcaster<UserCreatedEventV1, UserCreatedEventV2>(1, 2, old =>
        {
            callbackEntered.Set();

            // Hold the traversal open. A lock-based read path would still be holding its lock here.
            _ = releaseCallback.Wait(TimeSpan.FromSeconds(30));

            return new UserCreatedEventV2 { Id = old.Id, FirstName = "first", LastName = "last" };
        }));

        var input = new UserCreatedEventV1 { Id = Guid.NewGuid(), Name = "original" };
        var traversal = Task.Run(() => pipeline.UpcastTo(input, 2));

        callbackEntered.Wait(TimeSpan.FromSeconds(10)).ShouldBeTrue(
            "the upcaster callback never started, so this arm would not be measuring contention");

        // LIVENESS: registration completes while the callback is still inside the traversal.
        await Task.Run(() => pipeline.Register(new UserCreatedEventV2ToV3Upcaster()))
            .WaitAsync(TimeSpan.FromSeconds(5));

        pipeline.GetLatestVersion("UserCreatedEvent").ShouldBe(3);

        // SAFETY: the traversal that was already running still finishes, and still finishes correctly.
        releaseCallback.Set();
        var result = await traversal.WaitAsync(TimeSpan.FromSeconds(10));
        var typed = result.ShouldBeOfType<UserCreatedEventV2>();
        typed.Id.ShouldBe(input.Id);
        typed.FirstName.ShouldBe("first");
    }

    private sealed class TestUpcaster<TFrom, TTo>(int from, int to, Func<TFrom, TTo> transform) : IMessageUpcaster<TFrom, TTo>
        where TFrom : IDispatchMessage, IVersionedMessage
        where TTo : IDispatchMessage, IVersionedMessage
    {
        public int FromVersion => from;
        public int ToVersion => to;
        public TTo Upcast(TFrom oldMessage) => transform(oldMessage);
    }
}
