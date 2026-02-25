// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Resilience;
using Excalibur.Dispatch.Options.Resilience;

using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Dispatch.Tests.Messaging.Resilience;

[Trait("Category", "Unit")]
public sealed class CircuitBreakerPolicyShould
{
    private static CircuitBreakerPolicy CreateSut(CircuitBreakerOptions? options = null) =>
        new(
            options ?? new CircuitBreakerOptions
            {
                FailureThreshold = 3,
                OpenDuration = TimeSpan.FromSeconds(30)
            },
            name: "test-circuit",
            logger: NullLogger<CircuitBreakerPolicy>.Instance);

    [Fact]
    public void StartInClosedState()
    {
        var sut = CreateSut();

        sut.State.ShouldBe(CircuitState.Closed);
    }

    [Fact]
    public async Task AllowExecutionWhenClosed()
    {
        var sut = CreateSut();
        var executed = false;

        await sut.ExecuteAsync<bool>(async ct =>
        {
            executed = true;
            await Task.CompletedTask.ConfigureAwait(false);
            return true;
        }, CancellationToken.None);

        executed.ShouldBeTrue();
    }

    [Fact]
    public async Task OpenAfterThresholdExceeded()
    {
        var sut = CreateSut(new CircuitBreakerOptions
        {
            FailureThreshold = 2,
            OpenDuration = TimeSpan.FromMinutes(5)
        });

        // Record failures to exceed threshold
        for (var i = 0; i < 3; i++)
        {
            try
            {
                await sut.ExecuteAsync<bool>(_ => throw new InvalidOperationException("fail"), CancellationToken.None);
            }
            catch (Exception) when (i < 3)
            {
                // Expected: InvalidOperationException on first attempts,
                // CircuitBreakerOpenException once circuit opens
            }
        }

        sut.State.ShouldBe(CircuitState.Open);
    }

    [Fact]
    public async Task ThrowCircuitBreakerOpenExceptionWhenOpen()
    {
        var sut = CreateSut(new CircuitBreakerOptions
        {
            FailureThreshold = 1,
            OpenDuration = TimeSpan.FromMinutes(5)
        });

        // Trip the circuit
        try
        {
            await sut.ExecuteAsync<bool>(_ => throw new InvalidOperationException("fail"), CancellationToken.None);
        }
        catch (InvalidOperationException)
        {
            // Expected
        }

        // Should now be open
        await Should.ThrowAsync<CircuitBreakerOpenException>(
            () => sut.ExecuteAsync<bool>(_ => Task.FromResult(true), CancellationToken.None));
    }

    [Fact]
    public async Task ResetOnSuccess()
    {
        var sut = CreateSut(new CircuitBreakerOptions
        {
            FailureThreshold = 3,
            OpenDuration = TimeSpan.FromSeconds(30)
        });

        // Record some failures (not enough to open)
        try
        {
            await sut.ExecuteAsync<bool>(_ => throw new InvalidOperationException("fail"), CancellationToken.None);
        }
        catch (InvalidOperationException)
        {
            // Expected
        }

        sut.ConsecutiveFailures.ShouldBe(1);

        // Success should reset counter
        await sut.ExecuteAsync<bool>(_ => Task.FromResult(true), CancellationToken.None);

        sut.ConsecutiveFailures.ShouldBe(0);
    }

    [Fact]
    public void TrackConsecutiveFailures()
    {
        var sut = CreateSut(new CircuitBreakerOptions
        {
            FailureThreshold = 10,
            OpenDuration = TimeSpan.FromSeconds(30)
        });

        sut.RecordFailure(new InvalidOperationException("fail1"));
        sut.RecordFailure(new InvalidOperationException("fail2"));

        sut.ConsecutiveFailures.ShouldBe(2);
    }

    [Fact]
    public void ResetToClosedState()
    {
        var sut = CreateSut(new CircuitBreakerOptions
        {
            FailureThreshold = 1,
            OpenDuration = TimeSpan.FromSeconds(30)
        });

        sut.RecordFailure(new InvalidOperationException("fail"));
        sut.State.ShouldBe(CircuitState.Open);

        sut.Reset();

        sut.State.ShouldBe(CircuitState.Closed);
        sut.ConsecutiveFailures.ShouldBe(0);
    }

    [Fact]
    public void ThrowOnNullOptions()
    {
        Should.Throw<ArgumentNullException>(() => new CircuitBreakerPolicy(null!));
    }

    [Fact]
    public async Task ReturnResultFromExecuteWithResult()
    {
        var sut = CreateSut();

        var result = await sut.ExecuteAsync(async _ =>
        {
            await Task.CompletedTask.ConfigureAwait(false);
            return 42;
        }, CancellationToken.None);

        result.ShouldBe(42);
    }

    [Fact]
    public async Task HandleShouldHandlePredicate()
    {
        var sut = new CircuitBreakerPolicy(
            new CircuitBreakerOptions
            {
                FailureThreshold = 1,
                OpenDuration = TimeSpan.FromSeconds(30)
            },
            name: "filtered",
            shouldHandle: ex => ex is TimeoutException);

        // This should be filtered out (not a TimeoutException) - must use ExecuteAsync
        // since RecordFailure bypasses the shouldHandle predicate
        try
        {
            await sut.ExecuteAsync<bool>(_ => throw new InvalidOperationException("not handled"), CancellationToken.None);
        }
        catch (InvalidOperationException)
        {
            // Expected - rethrown because not handled by circuit breaker
        }

        sut.State.ShouldBe(CircuitState.Closed);

        // This should be counted (is a TimeoutException)
        try
        {
            await sut.ExecuteAsync<bool>(_ => throw new TimeoutException("handled"), CancellationToken.None);
        }
        catch (TimeoutException)
        {
            // Expected - handled by circuit breaker and rethrown
        }

        sut.State.ShouldBe(CircuitState.Open);
    }
}
