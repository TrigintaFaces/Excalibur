// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Data;
using Excalibur.Data;
using Excalibur.Data.SqlServer;
using Excalibur.Data.SqlServer.Persistence;
using Excalibur.Data.Resilience;
using Excalibur.Dispatch;
using Excalibur.Dispatch.Messaging;
using Excalibur.Dispatch.Middleware.Resilience;
using Excalibur.Dispatch.Options.Resilience;
using Excalibur.Dispatch.Telemetry;
using FakeItEasy;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace Excalibur.Data.Tests.SqlServer;

[Trait("Category", "Unit")]
[Trait("Component", "Data.SqlServer")]
[Trait("Pattern", "Regression")]
public sealed class SqlServerMiddlewareRetryCompositionShould
{
    [Theory]
    [InlineData(0, 2)]
    [InlineData(1, 1)]
    [InlineData(2, 3)]
    public async Task GiveTheOwningMiddlewareOneDataRequestInvocationPerOuterAttempt(int outerRetries, int innerRetries)
    {
        var invocations = 0;
        var handlerCalls = 0;
        var connections = 0;
        var fault = new TimeoutException("controlled SQL request timeout");
        var policy = new SqlServerRetryPolicy(innerRetries, TimeSpan.Zero, NullLogger.Instance, delegateRetriesToMessagePipeline: true);
        var request = new ProbeRequest(() => { invocations++; return Task.FromException<int>(fault); });
        var middleware = Middleware(outerRetries);
        var caught = await Should.ThrowAsync<TimeoutException>(() => middleware.InvokeAsync(
            new ProbeAction(), new MessageContext(), async (_, _, ct) =>
            {
                handlerCalls++;
                _ = await policy.ResolveAsync(request, () =>
                {
                    connections++;
                    return Task.FromResult(A.Fake<IDbConnection>());
                }, ct);
                return MessageResult.Success();
            }, TestContext.Current.CancellationToken).AsTask());
        caught.ShouldBeSameAs(fault);
        handlerCalls.ShouldBe(outerRetries + 1);
        invocations.ShouldBe(outerRetries + 1, "one retry owner must not multiply the inner and outer budgets for the same failure");
        connections.ShouldBe(invocations);
        policy.MaxRetryAttempts.ShouldBe(innerRetries, "ownership must not mutate the configured policy");
    }

    [Fact]
    public async Task PreserveTheSqlPolicyBudgetOutsideAnOwningMessageFlow()
    {
        var invocations = 0;
        var fault = new TimeoutException("unowned SQL request timeout");
        var policy = new SqlServerRetryPolicy(2, TimeSpan.Zero, NullLogger.Instance);
        var request = new ProbeRequest(() => { invocations++; return Task.FromException<int>(fault); });
        var caught = await Should.ThrowAsync<TimeoutException>(() => policy.ResolveAsync(
            request, () => Task.FromResult(A.Fake<IDbConnection>()), TestContext.Current.CancellationToken));
        caught.ShouldBeSameAs(fault);
        invocations.ShouldBe(3);
    }

    [Fact]
    public async Task PreserveInnerRetriesWhenTheOuterPolicyDeclinesThatFailureClass()
    {
        var invocations = 0;
        var handlerCalls = 0;
        var fault = new TimeoutException("SQL timeout excluded by outer owner");
        var options = Options(3);
        options.NonRetryableExceptions.Add(typeof(TimeoutException));
        var middleware = Middleware(options);
        var policy = new SqlServerRetryPolicy(2, TimeSpan.Zero, NullLogger.Instance, delegateRetriesToMessagePipeline: true);
        var request = new ProbeRequest(() => { invocations++; return Task.FromException<int>(fault); });
        var caught = await Should.ThrowAsync<TimeoutException>(() => middleware.InvokeAsync(
            new ProbeAction(), new MessageContext(), async (_, _, ct) =>
            {
                handlerCalls++;
                _ = await policy.ResolveAsync(request, () => Task.FromResult(A.Fake<IDbConnection>()), ct);
                return MessageResult.Success();
            }, TestContext.Current.CancellationToken).AsTask());
        caught.ShouldBeSameAs(fault);
        handlerCalls.ShouldBe(1);
        invocations.ShouldBe(3, "an unrelated active scope must not suppress the actual failure owner");
    }

    [Fact]
    public async Task CompleteAHealthyOwnedDataRequestOnce()
    {
        var invocations = 0;
        var policy = new SqlServerRetryPolicy(3, TimeSpan.Zero, NullLogger.Instance, delegateRetriesToMessagePipeline: true);
        var request = new ProbeRequest(() => { invocations++; return Task.FromResult(42); });
        var result = await Middleware(3).InvokeAsync(new ProbeAction(), new MessageContext(), async (_, _, ct) =>
        {
            (await policy.ResolveAsync(request, () => Task.FromResult(A.Fake<IDbConnection>()), ct)).ShouldBe(42);
            return MessageResult.Success();
        }, TestContext.Current.CancellationToken);
        result.Succeeded.ShouldBeTrue();
        invocations.ShouldBe(1);
    }

    [Fact]
    public async Task IsolateConcurrentOwnedAndUnownedCallsUsingTheSamePolicy()
    {
        var ownedCalls = 0;
        var unownedCalls = 0;
        var ownedEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var unownedEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var fault = new TimeoutException("concurrent SQL timeout");
        var policy = new SqlServerRetryPolicy(2, TimeSpan.Zero, NullLogger.Instance, delegateRetriesToMessagePipeline: true);
        var ct = TestContext.Current.CancellationToken;
        var owned = Task.Run(async () =>
        {
            var request = new ProbeRequest(async () =>
            {
                Interlocked.Increment(ref ownedCalls);
                ownedEntered.TrySetResult();
                await release.Task.WaitAsync(ct);
                throw fault;
            });
            return await Should.ThrowAsync<TimeoutException>(() => Middleware(0).InvokeAsync(
                new ProbeAction(), new MessageContext(), async (_, _, token) =>
                {
                    _ = await policy.ResolveAsync(request, () => Task.FromResult(A.Fake<IDbConnection>()), token);
                    return MessageResult.Success();
                }, ct).AsTask());
        }, ct);
        var unowned = Task.Run(async () =>
        {
            var request = new ProbeRequest(async () =>
            {
                Interlocked.Increment(ref unownedCalls);
                unownedEntered.TrySetResult();
                await release.Task.WaitAsync(ct);
                throw fault;
            });
            return await Should.ThrowAsync<TimeoutException>(() => policy.ResolveAsync(
                request, () => Task.FromResult(A.Fake<IDbConnection>()), ct));
        }, ct);
        try
        {
            await Task.WhenAll(ownedEntered.Task, unownedEntered.Task).WaitAsync(TimeSpan.FromSeconds(10), ct);
        }
        finally { release.TrySetResult(); }
        foreach (var caught in await Task.WhenAll(owned, unowned)) { caught.ShouldBeSameAs(fault); }
        ownedCalls.ShouldBe(1);
        unownedCalls.ShouldBe(3);
        policy.ShouldRetry(fault).ShouldBeTrue("classification remains independent of ambient execution ownership");
    }

    [Fact]
    public async Task TreatAnInactiveCapturedChildAsABoundaryEvenWhileItsParentIsActive()
    {
        var calls = 0;
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var fault = new TimeoutException("captured inactive child timeout");
        var policy = new SqlServerRetryPolicy(2, TimeSpan.Zero, NullLogger.Instance, delegateRetriesToMessagePipeline: true);
        var ct = TestContext.Current.CancellationToken;
        Task<TimeoutException>? captured = null;
        var result = await Middleware(0).InvokeAsync(new ProbeAction(), new MessageContext(), async (_, _, token) =>
        {
            var childResult = await Middleware(0).InvokeAsync(new ProbeAction(), new MessageContext(), (_, _, childToken) =>
            {
                captured = Task.Run(async () =>
                {
                    await release.Task.WaitAsync(childToken);
                    var request = new ProbeRequest(() => { calls++; return Task.FromException<int>(fault); });
                    return await Should.ThrowAsync<TimeoutException>(() => policy.ResolveAsync(
                        request, () => Task.FromResult(A.Fake<IDbConnection>()), childToken));
                }, childToken);
                return ValueTask.FromResult<IMessageResult>(MessageResult.Success());
            }, token);
            childResult.Succeeded.ShouldBeTrue();
            release.TrySetResult();
            (await captured.ShouldNotBeNull()).ShouldBeSameAs(fault);
            return MessageResult.Success();
        }, ct);
        result.Succeeded.ShouldBeTrue();
        calls.ShouldBe(3, "the disposed captured nearest scope must not fall through to the active parent");
    }

    [Fact]
    public async Task RespectTheNearestOwnerDecliningAClassInsteadOfSearchingItsParent()
    {
        var calls = 0;
        var fault = new TimeoutException("nearest owner declines timeout");
        var policy = new SqlServerRetryPolicy(2, TimeSpan.Zero, NullLogger.Instance, delegateRetriesToMessagePipeline: true);
        var nearest = Options(3);
        nearest.NonRetryableExceptions.Add(typeof(TimeoutException));
        var result = await Middleware(0).InvokeAsync(new ProbeAction(), new MessageContext(), async (_, _, ct) =>
        {
            var request = new ProbeRequest(() => { calls++; return Task.FromException<int>(fault); });
            var caught = await Should.ThrowAsync<TimeoutException>(() => Middleware(nearest).InvokeAsync(
                new ProbeAction(), new MessageContext(), async (_, _, token) =>
                {
                    _ = await policy.ResolveAsync(request, () => Task.FromResult(A.Fake<IDbConnection>()), token);
                    return MessageResult.Success();
                }, ct).AsTask());
            caught.ShouldBeSameAs(fault);
            return MessageResult.Success();
        }, TestContext.Current.CancellationToken);
        result.Succeeded.ShouldBeTrue();
        calls.ShouldBe(3);
    }

    [Fact]
    public async Task PropagateCallerCancellationWithoutEitherOwnerRetrying()
    {
        var calls = 0;
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var fault = new OperationCanceledException(cancellation.Token);
        var policy = new SqlServerRetryPolicy(2, TimeSpan.Zero, NullLogger.Instance, delegateRetriesToMessagePipeline: true);
        var request = new ProbeRequest(() =>
        {
            calls++;
            cancellation.Cancel();
            return Task.FromException<int>(fault);
        });
        var caught = await Should.ThrowAsync<OperationCanceledException>(() => Middleware(2).InvokeAsync(
            new ProbeAction(), new MessageContext(), async (_, _, ct) =>
            {
                _ = await policy.ResolveAsync(request, () => Task.FromResult(A.Fake<IDbConnection>()), ct);
                return MessageResult.Success();
            }, cancellation.Token).AsTask());
        caught.CancellationToken.ShouldBe(cancellation.Token);
        cancellation.IsCancellationRequested.ShouldBeTrue();
        calls.ShouldBe(1);
        policy.ShouldRetry(caught).ShouldBeFalse();
    }

    [Theory]
    [InlineData(false, 2, 42, false)]
    [InlineData(true, 1, -1, true)]
    public async Task MakeCaughtTimeoutFallbackAnExplicitProviderOptIn(bool delegateRetries, int expectedCalls, int expectedValue, bool expectedFallback)
    {
        new SqlServerPersistenceOptions().Resiliency.DelegateRetriesToMessagePipeline.ShouldBeFalse();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSqlServerPersistence(options =>
        {
            options.ConnectionString = "Server=localhost;Database=unused;Integrated Security=true;TrustServerCertificate=true";
            options.Resiliency.MaxRetryAttempts = 1;
            options.Resiliency.RetryDelayMilliseconds = 0;
            options.Resiliency.DelegateRetriesToMessagePipeline = delegateRetries;
        });
        using var provider = services.BuildServiceProvider();
        var policy = provider.GetRequiredService<IDataRequestRetryPolicy>().ShouldBeAssignableTo<IRelationalDataRequestRetryPolicy>();
        var fault = new TimeoutException("first timeout would recover on the local retry");
        var calls = 0;
        var value = 0;
        var fallback = false;
        var request = new ProbeRequest(() => ++calls == 1 ? Task.FromException<int>(fault) : Task.FromResult(42));
        var result = await Middleware(2).InvokeAsync(new ProbeAction(), new MessageContext(), async (_, _, ct) =>
        {
            try { value = await policy.ResolveAsync(request, () => Task.FromResult(A.Fake<IDbConnection>()), ct); }
            catch (TimeoutException caught)
            {
                caught.ShouldBeSameAs(fault);
                fallback = true;
                value = -1;
            }
            return MessageResult.Success();
        }, TestContext.Current.CancellationToken);
        result.Succeeded.ShouldBeTrue();
        calls.ShouldBe(expectedCalls);
        value.ShouldBe(expectedValue);
        fallback.ShouldBe(expectedFallback);
        policy.ShouldRetry(fault).ShouldBeTrue();
    }

    [Fact]
    public async Task PreserveThreeArgumentConstructorLocalRetriesInsideAnOwningFlowByDefault()
    {
        var policy = new SqlServerRetryPolicy(1, TimeSpan.Zero, NullLogger.Instance);
        var calls = 0;
        var fault = new TimeoutException("default constructor retains local availability");
        var request = new ProbeRequest(() => ++calls == 1 ? Task.FromException<int>(fault) : Task.FromResult(42));
        var result = await Middleware(0).InvokeAsync(new ProbeAction(), new MessageContext(), async (_, _, ct) =>
        {
            (await policy.ResolveAsync(request, () => Task.FromResult(A.Fake<IDbConnection>()), ct)).ShouldBe(42);
            return MessageResult.Success();
        }, TestContext.Current.CancellationToken);
        result.Succeeded.ShouldBeTrue();
        calls.ShouldBe(2);
    }

    private static RetryOptions Options(int retries) => new()
    {
        MaxRetryAttempts = retries, BaseDelay = TimeSpan.Zero, MaxDelay = TimeSpan.Zero, UseJitter = false,
    };
    private static RetryMiddleware Middleware(int retries) => Middleware(Options(retries));
    private static RetryMiddleware Middleware(RetryOptions options) => new(
        Microsoft.Extensions.Options.Options.Create(options), A.Fake<ITelemetrySanitizer>(), NullLogger<RetryMiddleware>.Instance);

    private sealed record ProbeAction : IDispatchAction;
    private sealed class ProbeRequest : DataRequest<int>
    {
        internal ProbeRequest(Func<Task<int>> resolve)
        {
            Command = CreateCommand("SELECT 1");
            ResolveAsync = _ => resolve();
        }
    }
}
