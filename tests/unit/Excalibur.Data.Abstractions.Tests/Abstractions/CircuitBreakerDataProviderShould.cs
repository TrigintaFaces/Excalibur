// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.Abstractions.Persistence;
using Excalibur.Data.Abstractions.Resilience;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Excalibur.Data.Tests.Abstractions;

[Trait("Category", "Unit")]
[Trait("Component", "Data.Abstractions")]
public sealed class CircuitBreakerDataProviderShould
{
	private readonly IPersistenceProvider _inner;
	private readonly IOptions<DataProviderCircuitBreakerOptions> _options;

	public CircuitBreakerDataProviderShould()
	{
		_inner = A.Fake<IPersistenceProvider>();
		A.CallTo(() => _inner.Name).Returns("TestProvider");
		_options = Options.Create(new DataProviderCircuitBreakerOptions
		{
			FailureThreshold = 3,
			BreakDuration = TimeSpan.FromSeconds(30),
			SamplingWindow = TimeSpan.FromMinutes(1),
		});
	}

	[Fact]
	public void StartInClosedState()
	{
		// Arrange
		var sut = CreateSut();

		// Act & Assert
		sut.State.ShouldBe(DataProviderCircuitState.Closed);
	}

	[Fact]
	public async Task DelegateExecuteAsyncWhenClosed()
	{
		// Arrange
		var request = A.Fake<IDataRequest<IDisposable, string>>();
		A.CallTo(() => _inner.ExecuteAsync(request, A<CancellationToken>._))
			.Returns(Task.FromResult("result"));
		var sut = CreateSut();

		// Act
		var result = await sut.ExecuteAsync(request, CancellationToken.None);

		// Assert
		result.ShouldBe("result");
	}

	[Fact]
	public async Task OpenCircuitAfterFailureThreshold()
	{
		// Arrange
		var request = A.Fake<IDataRequest<IDisposable, string>>();
		A.CallTo(() => _inner.ExecuteAsync(request, A<CancellationToken>._))
			.ThrowsAsync(new TimeoutException("timeout"));
		var sut = CreateSut();

		// Act - trigger failures up to threshold
		for (var i = 0; i < 3; i++)
		{
			await Should.ThrowAsync<TimeoutException>(
				() => sut.ExecuteAsync(request, CancellationToken.None));
		}

		// Assert
		sut.State.ShouldBe(DataProviderCircuitState.Open);
	}

	[Fact]
	public async Task ThrowCircuitBreakerOpenExceptionWhenOpen()
	{
		// Arrange
		var request = A.Fake<IDataRequest<IDisposable, string>>();
		A.CallTo(() => _inner.ExecuteAsync(request, A<CancellationToken>._))
			.ThrowsAsync(new TimeoutException("timeout"));
		var sut = CreateSut();

		// Trip the circuit
		for (var i = 0; i < 3; i++)
		{
			await Should.ThrowAsync<TimeoutException>(
				() => sut.ExecuteAsync(request, CancellationToken.None));
		}

		// Act & Assert - should throw CircuitBreakerOpenException
		var ex = await Should.ThrowAsync<CircuitBreakerOpenException>(
			() => sut.ExecuteAsync(request, CancellationToken.None));
		ex.Message.ShouldContain("open");
	}

	[Fact]
	public async Task ResetFailureCountAfterSuccess()
	{
		// Arrange
		var request = A.Fake<IDataRequest<IDisposable, string>>();
		var callCount = 0;
		A.CallTo(() => _inner.ExecuteAsync(request, A<CancellationToken>._))
			.ReturnsLazily(() =>
			{
				callCount++;
				if (callCount <= 2) throw new TimeoutException("timeout");
				return Task.FromResult("success");
			});
		var sut = CreateSut();

		// Act - 2 failures then success
		await Should.ThrowAsync<TimeoutException>(
			() => sut.ExecuteAsync(request, CancellationToken.None));
		await Should.ThrowAsync<TimeoutException>(
			() => sut.ExecuteAsync(request, CancellationToken.None));
		await sut.ExecuteAsync(request, CancellationToken.None);

		// Assert - should still be closed (failure count was reset)
		sut.State.ShouldBe(DataProviderCircuitState.Closed);
	}

	[Fact]
	public void ExposeCircuitBreakerViaGetService()
	{
		// Arrange
		var sut = CreateSut();

		// Act
		var service = sut.GetService(typeof(IDataProviderCircuitBreaker));

		// Assert
		service.ShouldBeSameAs(sut);
	}

	[Fact]
	public void DelegateUnknownServiceToInnerProvider()
	{
		// Arrange
		var expected = new object();
		A.CallTo(() => _inner.GetService(typeof(string))).Returns(expected);
		var sut = CreateSut();

		// Act
		var service = sut.GetService(typeof(string));

		// Assert
		service.ShouldBeSameAs(expected);
	}

	[Fact]
	public async Task NotOpenCircuitForNonTransientFailures()
	{
		// Arrange
		var request = A.Fake<IDataRequest<IDisposable, string>>();
		A.CallTo(() => _inner.ExecuteAsync(request, A<CancellationToken>._))
			.ThrowsAsync(new InvalidOperationException("not transient"));
		var sut = CreateSut();

		// Act - non-transient exceptions should not trip the circuit
		for (var i = 0; i < 5; i++)
		{
			try
			{
				await sut.ExecuteAsync(request, CancellationToken.None);
			}
			catch (InvalidOperationException)
			{
				// Expected - non-transient failures are not caught by the circuit breaker filter
			}
		}

		// Assert - circuit should remain closed since non-transient exceptions
		// propagate without triggering the circuit breaker's failure counting
		sut.State.ShouldBe(DataProviderCircuitState.Closed);
	}

	[Fact]
	public async Task ThrowCircuitBreakerOpenExceptionForInitializeWhenOpen()
	{
		// Arrange
		var request = A.Fake<IDataRequest<IDisposable, string>>();
		A.CallTo(() => _inner.ExecuteAsync(request, A<CancellationToken>._))
			.ThrowsAsync(new TimeoutException("timeout"));
		var options = A.Fake<IPersistenceOptions>();
		var sut = CreateSut();

		// Trip the circuit via execute
		for (var i = 0; i < 3; i++)
		{
			await Should.ThrowAsync<TimeoutException>(
				() => sut.ExecuteAsync(request, CancellationToken.None));
		}

		// Act & Assert - InitializeAsync should also throw when circuit is open
		await Should.ThrowAsync<CircuitBreakerOpenException>(
			() => sut.InitializeAsync(options, CancellationToken.None));
	}

	private CircuitBreakerDataProvider CreateSut() =>
		new(_inner, _options, NullLogger<CircuitBreakerDataProvider>.Instance);
}
