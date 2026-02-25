// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

#pragma warning disable CA2213 // Disposable fields should be disposed -- FakeItEasy fakes do not require disposal

using System.Collections.Concurrent;

using Excalibur.Dispatch.Resilience.Polly;

using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;

using MsOptions = Microsoft.Extensions.Options.Options;

namespace Excalibur.Dispatch.Middleware.Tests.Resilience;

/// <summary>
/// Unit tests for <see cref="DistributedCircuitBreakerFactory"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Resilience")]
public sealed class DistributedCircuitBreakerFactoryShould : UnitTestBase
{
	private IDistributedCache _cache = null!;
	private IOptions<DistributedCircuitBreakerOptions> _options = null!;
	private ILoggerFactory _loggerFactory = null!;

	public DistributedCircuitBreakerFactoryShould()
	{
		_cache = A.Fake<IDistributedCache>();
		_options = MsOptions.Create(new DistributedCircuitBreakerOptions());
		_loggerFactory = A.Fake<ILoggerFactory>();
		A.CallTo(() => _loggerFactory.CreateLogger(A<string>._))
			.Returns(A.Fake<ILogger>());
	}

	#region Constructor Tests

	[Fact]
	public void Constructor_WithNullCache_ThrowsArgumentNullException()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			new DistributedCircuitBreakerFactory(null!, _options, _loggerFactory));
	}

	[Fact]
	public void Constructor_WithNullOptions_ThrowsArgumentNullException()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			new DistributedCircuitBreakerFactory(_cache, null!, _loggerFactory));
	}

	[Fact]
	public void Constructor_WithNullLoggerFactory_ThrowsArgumentNullException()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			new DistributedCircuitBreakerFactory(_cache, _options, null!));
	}

	[Fact]
	public void Constructor_WithValidArguments_CreatesInstance()
	{
		// Act
		var factory = new DistributedCircuitBreakerFactory(_cache, _options, _loggerFactory);

		// Assert
		factory.ShouldNotBeNull();
	}

	#endregion

	#region GetOrCreate Tests

	[Fact]
	public void GetOrCreate_WithNullName_ThrowsArgumentException()
	{
		// Arrange
		var factory = new DistributedCircuitBreakerFactory(_cache, _options, _loggerFactory);

		// Act & Assert
		_ = Should.Throw<ArgumentException>(() => factory.GetOrCreate(null!));
	}

	[Fact]
	public void GetOrCreate_WithEmptyName_ThrowsArgumentException()
	{
		// Arrange
		var factory = new DistributedCircuitBreakerFactory(_cache, _options, _loggerFactory);

		// Act & Assert
		_ = Should.Throw<ArgumentException>(() => factory.GetOrCreate(string.Empty));
	}

	[Fact]
	public void GetOrCreate_WithWhitespaceName_ThrowsArgumentException()
	{
		// Arrange
		var factory = new DistributedCircuitBreakerFactory(_cache, _options, _loggerFactory);

		// Act & Assert
		_ = Should.Throw<ArgumentException>(() => factory.GetOrCreate("   "));
	}

	[Fact]
	public void GetOrCreate_WithValidName_ReturnsCircuitBreaker()
	{
		// Arrange
		var factory = new DistributedCircuitBreakerFactory(_cache, _options, _loggerFactory);

		// Act
		var breaker = factory.GetOrCreate("test-breaker");

		// Assert
		breaker.ShouldNotBeNull();
		breaker.ShouldBeAssignableTo<IDistributedCircuitBreaker>();
	}

	[Fact]
	public void GetOrCreate_WithSameName_ReturnsSameInstance()
	{
		// Arrange
		var factory = new DistributedCircuitBreakerFactory(_cache, _options, _loggerFactory);

		// Act
		var breaker1 = factory.GetOrCreate("test-breaker");
		var breaker2 = factory.GetOrCreate("test-breaker");

		// Assert
		breaker1.ShouldBeSameAs(breaker2);
	}

	[Fact]
	public void GetOrCreate_WithDifferentNames_ReturnsDifferentInstances()
	{
		// Arrange
		var factory = new DistributedCircuitBreakerFactory(_cache, _options, _loggerFactory);

		// Act
		var breaker1 = factory.GetOrCreate("breaker-1");
		var breaker2 = factory.GetOrCreate("breaker-2");

		// Assert
		breaker1.ShouldNotBeSameAs(breaker2);
	}

	[Fact]
	public void GetOrCreate_ReturnsCircuitBreakerWithCorrectName()
	{
		// Arrange
		var factory = new DistributedCircuitBreakerFactory(_cache, _options, _loggerFactory);

		// Act
		var breaker = factory.GetOrCreate("my-custom-circuit");

		// Assert
		breaker.ShouldBeOfType<DistributedCircuitBreaker>()
			.Name.ShouldBe("my-custom-circuit");
	}

	[Fact]
	public void GetOrCreate_MultipleCallsFromDifferentThreads_AreThreadSafe()
	{
		// Arrange
		var factory = new DistributedCircuitBreakerFactory(_cache, _options, _loggerFactory);
		var breakers = new ConcurrentBag<IDistributedCircuitBreaker>();

		// Act
		System.Threading.Tasks.Parallel.For(0, 100, _ =>
		{
			var breaker = factory.GetOrCreate("concurrent-breaker");
			breakers.Add(breaker);
		});

		// Assert - all should be the same instance
		var distinctBreakers = breakers.Distinct().ToList();
		distinctBreakers.Count.ShouldBe(1);
	}

	[Fact]
	public void GetOrCreate_WithCaseSensitiveNames_TreatsAsDistinct()
	{
		// Arrange
		var factory = new DistributedCircuitBreakerFactory(_cache, _options, _loggerFactory);

		// Act
		var breaker1 = factory.GetOrCreate("TestBreaker");
		var breaker2 = factory.GetOrCreate("testbreaker");

		// Assert - ordinal comparison means case-sensitive
		breaker1.ShouldNotBeSameAs(breaker2);
	}

	[Fact]
	public void GetOrCreate_CreatesLoggerForEachBreaker()
	{
		// Arrange
		var loggerFactory = A.Fake<ILoggerFactory>();
		A.CallTo(() => loggerFactory.CreateLogger(A<string>._))
			.Returns(A.Fake<ILogger>());
		var factory = new DistributedCircuitBreakerFactory(_cache, _options, loggerFactory);

		// Act
		_ = factory.GetOrCreate("breaker-with-logger");

		// Assert
		A.CallTo(() => loggerFactory.CreateLogger(A<string>.That.Contains("DistributedCircuitBreaker")))
			.MustHaveHappened();
	}

	#endregion

	#region Options Tests

	[Fact]
	public void Constructor_WithCustomOptions_UsesOptions()
	{
		// Arrange
		var customOptions = MsOptions.Create(new DistributedCircuitBreakerOptions
		{
			ConsecutiveFailureThreshold = 10,
			BreakDuration = TimeSpan.FromMinutes(5)
		});
		var factory = new DistributedCircuitBreakerFactory(_cache, customOptions, _loggerFactory);

		// Act
		var breaker = factory.GetOrCreate("custom-options-breaker");

		// Assert
		breaker.ShouldNotBeNull();
	}

	#endregion
}
