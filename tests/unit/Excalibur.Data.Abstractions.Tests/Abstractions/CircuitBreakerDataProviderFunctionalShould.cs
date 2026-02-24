// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.Abstractions.Persistence;
using Excalibur.Data.Abstractions.Resilience;

using System.Diagnostics;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Excalibur.Data.Tests.Abstractions;

[Trait("Category", "Unit")]
public class CircuitBreakerDataProviderFunctionalShould
{
	private static async Task WaitForStateAsync(
		CircuitBreakerDataProvider provider,
		DataProviderCircuitState expectedState,
		TimeSpan timeout)
	{
		var stopwatch = Stopwatch.StartNew();
		while (stopwatch.Elapsed < timeout)
		{
			if (provider.State == expectedState)
			{
				return;
			}

			await global::Tests.Shared.Infrastructure.TestTiming.DelayAsync(TimeSpan.FromMilliseconds(10)).ConfigureAwait(false);
		}

		provider.State.ShouldBe(expectedState);
	}

	private static CircuitBreakerDataProvider CreateProvider(
		IPersistenceProvider inner,
		DataProviderCircuitBreakerOptions? options = null)
	{
		options ??= new DataProviderCircuitBreakerOptions
		{
			FailureThreshold = 3,
			BreakDuration = TimeSpan.FromSeconds(5),
			SamplingWindow = TimeSpan.FromSeconds(60)
		};

		return new CircuitBreakerDataProvider(
			inner,
			Options.Create(options),
			NullLogger<CircuitBreakerDataProvider>.Instance);
	}

	[Fact]
	public void InitialState_ShouldBeClosed()
	{
		var inner = A.Fake<IPersistenceProvider>();
		var provider = CreateProvider(inner);

		provider.State.ShouldBe(DataProviderCircuitState.Closed);
	}

	[Fact]
	public async Task ExecuteAsync_WhenClosed_ShouldDelegateToInner()
	{
		var inner = A.Fake<IPersistenceProvider>();
		var request = A.Fake<IDataRequest<IDbConnection, string>>();
		A.CallTo(() => inner.ExecuteAsync(request, A<CancellationToken>._))
			.Returns(Task.FromResult("result"));

		var provider = CreateProvider(inner);

		var result = await provider.ExecuteAsync(request, CancellationToken.None).ConfigureAwait(false);

		result.ShouldBe("result");
		provider.State.ShouldBe(DataProviderCircuitState.Closed);
	}

	[Fact]
	public async Task ExecuteAsync_TransientFailuresBelowThreshold_ShouldStayClosed()
	{
		var inner = A.Fake<IPersistenceProvider>();
		var request = A.Fake<IDataRequest<IDbConnection, string>>();
		A.CallTo(() => inner.ExecuteAsync(request, A<CancellationToken>._))
			.Throws(new TimeoutException("timeout"));

		var provider = CreateProvider(inner, new DataProviderCircuitBreakerOptions
		{
			FailureThreshold = 3,
			BreakDuration = TimeSpan.FromSeconds(5),
			SamplingWindow = TimeSpan.FromSeconds(60)
		});

		// Fail twice (below threshold of 3)
		for (var i = 0; i < 2; i++)
		{
			await Should.ThrowAsync<TimeoutException>(
				() => provider.ExecuteAsync(request, CancellationToken.None)).ConfigureAwait(false);
		}

		provider.State.ShouldBe(DataProviderCircuitState.Closed);
	}

	[Fact]
	public async Task ExecuteAsync_TransientFailuresAtThreshold_ShouldOpenCircuit()
	{
		var inner = A.Fake<IPersistenceProvider>();
		var request = A.Fake<IDataRequest<IDbConnection, string>>();
		A.CallTo(() => inner.ExecuteAsync(request, A<CancellationToken>._))
			.Throws(new TimeoutException("timeout"));

		var provider = CreateProvider(inner, new DataProviderCircuitBreakerOptions
		{
			FailureThreshold = 3,
			BreakDuration = TimeSpan.FromMinutes(5), // long so it stays open
			SamplingWindow = TimeSpan.FromSeconds(60)
		});

		// Fail 3 times to hit threshold
		for (var i = 0; i < 3; i++)
		{
			await Should.ThrowAsync<TimeoutException>(
				() => provider.ExecuteAsync(request, CancellationToken.None)).ConfigureAwait(false);
		}

		provider.State.ShouldBe(DataProviderCircuitState.Open);
	}

	[Fact]
	public async Task ExecuteAsync_WhenOpen_ShouldThrowCircuitBreakerOpenException()
	{
		var inner = A.Fake<IPersistenceProvider>();
		var request = A.Fake<IDataRequest<IDbConnection, string>>();
		A.CallTo(() => inner.ExecuteAsync(request, A<CancellationToken>._))
			.Throws(new TimeoutException("timeout"));

		var provider = CreateProvider(inner, new DataProviderCircuitBreakerOptions
		{
			FailureThreshold = 2,
			BreakDuration = TimeSpan.FromMinutes(5),
			SamplingWindow = TimeSpan.FromSeconds(60)
		});

		// Trip the circuit
		for (var i = 0; i < 2; i++)
		{
			await Should.ThrowAsync<TimeoutException>(
				() => provider.ExecuteAsync(request, CancellationToken.None)).ConfigureAwait(false);
		}

		provider.State.ShouldBe(DataProviderCircuitState.Open);

		// Next call should throw CircuitBreakerOpenException without calling inner
		var ex = await Should.ThrowAsync<CircuitBreakerOpenException>(
			() => provider.ExecuteAsync(request, CancellationToken.None)).ConfigureAwait(false);

		ex.RetryAfter.ShouldNotBeNull();
	}

	[Fact]
	public async Task ExecuteAsync_AfterBreakDuration_ShouldTransitionToHalfOpen()
	{
		var inner = A.Fake<IPersistenceProvider>();
		var request = A.Fake<IDataRequest<IDbConnection, string>>();
		A.CallTo(() => inner.ExecuteAsync(request, A<CancellationToken>._))
			.Throws(new TimeoutException("timeout"));

		var provider = CreateProvider(inner, new DataProviderCircuitBreakerOptions
		{
			FailureThreshold = 2,
			BreakDuration = TimeSpan.FromMilliseconds(50),
			SamplingWindow = TimeSpan.FromSeconds(60)
		});

		// Trip the circuit
		for (var i = 0; i < 2; i++)
		{
			await Should.ThrowAsync<TimeoutException>(
				() => provider.ExecuteAsync(request, CancellationToken.None)).ConfigureAwait(false);
		}

		// Depending on scheduler timing this read may observe either Open or HalfOpen.
		provider.State.ShouldNotBe(DataProviderCircuitState.Closed);
		await WaitForStateAsync(provider, DataProviderCircuitState.HalfOpen, TimeSpan.FromSeconds(2)).ConfigureAwait(false);
	}

	[Fact]
	public async Task ExecuteAsync_HalfOpenSuccess_ShouldCloseClosed()
	{
		var inner = A.Fake<IPersistenceProvider>();
		var request = A.Fake<IDataRequest<IDbConnection, string>>();

		var failCount = 0;
		A.CallTo(() => inner.ExecuteAsync(request, A<CancellationToken>._))
			.ReturnsLazily(() =>
			{
				failCount++;
				if (failCount <= 2)
				{
					throw new TimeoutException("timeout");
				}

				return Task.FromResult("success");
			});

		var provider = CreateProvider(inner, new DataProviderCircuitBreakerOptions
		{
			FailureThreshold = 2,
			BreakDuration = TimeSpan.FromMilliseconds(50),
			SamplingWindow = TimeSpan.FromSeconds(60)
		});

		// Trip the circuit
		for (var i = 0; i < 2; i++)
		{
			await Should.ThrowAsync<TimeoutException>(
				() => provider.ExecuteAsync(request, CancellationToken.None)).ConfigureAwait(false);
		}

		await WaitForStateAsync(provider, DataProviderCircuitState.HalfOpen, TimeSpan.FromSeconds(2)).ConfigureAwait(false);

		// Successful call should close the circuit
		var result = await provider.ExecuteAsync(request, CancellationToken.None).ConfigureAwait(false);
		result.ShouldBe("success");
		provider.State.ShouldBe(DataProviderCircuitState.Closed);
	}

	[Fact]
	public async Task ExecuteAsync_HalfOpenFailure_ShouldReopenCircuit()
	{
		var inner = A.Fake<IPersistenceProvider>();
		var request = A.Fake<IDataRequest<IDbConnection, string>>();
		A.CallTo(() => inner.ExecuteAsync(request, A<CancellationToken>._))
			.Throws(new TimeoutException("timeout"));

		var provider = CreateProvider(inner, new DataProviderCircuitBreakerOptions
		{
			FailureThreshold = 2,
			BreakDuration = TimeSpan.FromMilliseconds(50),
			SamplingWindow = TimeSpan.FromSeconds(60)
		});

		// Trip the circuit
		for (var i = 0; i < 2; i++)
		{
			await Should.ThrowAsync<TimeoutException>(
				() => provider.ExecuteAsync(request, CancellationToken.None)).ConfigureAwait(false);
		}

		await WaitForStateAsync(provider, DataProviderCircuitState.HalfOpen, TimeSpan.FromSeconds(2)).ConfigureAwait(false);

		// Failure in half-open should reopen
		await Should.ThrowAsync<TimeoutException>(
			() => provider.ExecuteAsync(request, CancellationToken.None)).ConfigureAwait(false);

		provider.State.ShouldBe(DataProviderCircuitState.Open);
	}

	[Fact]
	public async Task ExecuteAsync_NonTransientException_ShouldNotCountAsFailure()
	{
		var inner = A.Fake<IPersistenceProvider>();
		var request = A.Fake<IDataRequest<IDbConnection, string>>();
		A.CallTo(() => inner.ExecuteAsync(request, A<CancellationToken>._))
			.Throws(new InvalidOperationException("not transient"));

		var provider = CreateProvider(inner, new DataProviderCircuitBreakerOptions
		{
			FailureThreshold = 2,
			BreakDuration = TimeSpan.FromMinutes(5),
			SamplingWindow = TimeSpan.FromSeconds(60)
		});

		// Non-transient failures should not trip the circuit
		for (var i = 0; i < 5; i++)
		{
			await Should.ThrowAsync<InvalidOperationException>(
				() => provider.ExecuteAsync(request, CancellationToken.None)).ConfigureAwait(false);
		}

		provider.State.ShouldBe(DataProviderCircuitState.Closed);
	}

	[Fact]
	public async Task InitializeAsync_WhenOpen_ShouldThrowCircuitBreakerOpenException()
	{
		var inner = A.Fake<IPersistenceProvider>();
		var request = A.Fake<IDataRequest<IDbConnection, string>>();
		A.CallTo(() => inner.ExecuteAsync(request, A<CancellationToken>._))
			.Throws(new TimeoutException("timeout"));

		var provider = CreateProvider(inner, new DataProviderCircuitBreakerOptions
		{
			FailureThreshold = 2,
			BreakDuration = TimeSpan.FromMinutes(5),
			SamplingWindow = TimeSpan.FromSeconds(60)
		});

		// Trip circuit via ExecuteAsync
		for (var i = 0; i < 2; i++)
		{
			await Should.ThrowAsync<TimeoutException>(
				() => provider.ExecuteAsync(request, CancellationToken.None)).ConfigureAwait(false);
		}

		// InitializeAsync should also be blocked
		var options = A.Fake<IPersistenceOptions>();
		await Should.ThrowAsync<CircuitBreakerOpenException>(
			() => provider.InitializeAsync(options, CancellationToken.None)).ConfigureAwait(false);
	}

	[Fact]
	public void GetService_ForCircuitBreaker_ShouldReturnSelf()
	{
		var inner = A.Fake<IPersistenceProvider>();
		var provider = CreateProvider(inner);

		var result = provider.GetService(typeof(IDataProviderCircuitBreaker));

		result.ShouldBeSameAs(provider);
	}

	[Fact]
	public void GetService_ForOtherType_ShouldDelegateToInner()
	{
		var inner = A.Fake<IPersistenceProvider>();
		var expected = new object();
		A.CallTo(() => inner.GetService(typeof(object))).Returns(expected);

		var provider = CreateProvider(inner);

		provider.GetService(typeof(object)).ShouldBe(expected);
	}

	[Fact]
	public async Task IoException_ShouldBeTransient()
	{
		var inner = A.Fake<IPersistenceProvider>();
		var request = A.Fake<IDataRequest<IDbConnection, string>>();
		A.CallTo(() => inner.ExecuteAsync(request, A<CancellationToken>._))
			.Throws(new System.IO.IOException("IO error"));

		var provider = CreateProvider(inner, new DataProviderCircuitBreakerOptions
		{
			FailureThreshold = 2,
			BreakDuration = TimeSpan.FromMinutes(5),
			SamplingWindow = TimeSpan.FromSeconds(60)
		});

		for (var i = 0; i < 2; i++)
		{
			await Should.ThrowAsync<System.IO.IOException>(
				() => provider.ExecuteAsync(request, CancellationToken.None)).ConfigureAwait(false);
		}

		provider.State.ShouldBe(DataProviderCircuitState.Open);
	}
}
