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
        using var pipeline = new UpcastingPipeline();
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
        using var pipeline = new UpcastingPipeline();
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

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Preserve_callback_errors_and_release_the_read_lock(bool reenterRegistration)
    {
        using var pipeline = new UpcastingPipeline();
        var next = new UserCreatedEventV2ToV3Upcaster();
        var expected = new InvalidOperationException("callback failure");
        var fail = true;
        pipeline.Register(new TestUpcaster<UserCreatedEventV1, UserCreatedEventV2>(1, 2, old =>
        {
            if (fail)
            {
                fail = false;
                if (reenterRegistration)
                {
                    pipeline.Register(next);
                }
                else
                {
                    throw expected;
                }
            }

            return new UserCreatedEventV2 { Id = old.Id, FirstName = "retry", LastName = "works" };
        }));
        var input = new UserCreatedEventV1 { Id = Guid.NewGuid(), Name = "original" };

        if (reenterRegistration)
        {
            _ = Should.Throw<LockRecursionException>(() => pipeline.UpcastTo(input, 2));
        }
        else
        {
            Should.Throw<InvalidOperationException>(() => pipeline.UpcastTo(input, 2)).ShouldBeSameAs(expected);
        }

        await Task.Run(() => pipeline.Register(next)).WaitAsync(TimeSpan.FromSeconds(10));
        var result = pipeline.UpcastTo(input, 3).ShouldBeOfType<UserCreatedEventV3>();
        result.Id.ShouldBe(input.Id);
        result.FirstName.ShouldBe("retry");
        _ = Should.Throw<InvalidOperationException>(() => pipeline.Register(next));
    }

    [Fact]
    public void Invalidate_negative_and_longer_cached_paths_after_registration()
    {
        using var pipeline = new UpcastingPipeline();
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

    private sealed class TestUpcaster<TFrom, TTo>(int from, int to, Func<TFrom, TTo> transform) : IMessageUpcaster<TFrom, TTo>
        where TFrom : IDispatchMessage, IVersionedMessage
        where TTo : IDispatchMessage, IVersionedMessage
    {
        public int FromVersion => from;
        public int ToVersion => to;
        public TTo Upcast(TFrom oldMessage) => transform(oldMessage);
    }
}
