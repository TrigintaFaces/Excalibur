// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Excalibur.Dispatch.Hosting.Serverless.Tests;

/// <summary>
/// Depth tests for <see cref="ColdStartOptimizerBase"/> covering exception handling,
/// singleton warmup failure paths, JIT warmup, and PlatformName.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Platform")]
public sealed class ColdStartOptimizerBaseDepthShould : UnitTestBase
{
	#region PlatformName

	[Fact]
	public void PlatformName_ReturnsSubclassValue()
	{
		// Arrange
		var optimizer = CreateOptimizer();

		// Assert
		optimizer.ExposedPlatformName.ShouldBe("TestPlatform");
	}

	#endregion

	#region WarmupAsync — Singleton Warmup Exception Path

	[Fact]
	public async Task WarmupAsync_WhenScopeFactoryThrows_DoesNotPropagateException()
	{
		// Arrange — ServiceProvider whose IServiceScopeFactory.CreateScope() throws
		var sp = A.Fake<IServiceProvider>();
		var scopeFactory = A.Fake<IServiceScopeFactory>();
		A.CallTo(() => sp.GetService(typeof(IServiceScopeFactory))).Returns(scopeFactory);
		A.CallTo(() => scopeFactory.CreateScope()).Throws(new InvalidOperationException("scope failure"));

		var optimizer = CreateOptimizer(enabled: true, serviceProvider: sp);

		// Act — should not throw; the exception is caught internally
		await optimizer.WarmupAsync();

		// Assert — warmup still completes (platform SDK was called)
		optimizer.WarmupPlatformSdkCallCount.ShouldBe(1);
	}

	#endregion

	#region OptimizeAsync — Full Lifecycle

	[Fact]
	public async Task OptimizeAsync_WhenEnabled_ExecutesFullLifecycle()
	{
		// Arrange
		var sp = new ServiceCollection()
			.AddLogging()
			.BuildServiceProvider();
		var optimizer = CreateOptimizer(enabled: true, serviceProvider: sp);

		// Act
		await optimizer.OptimizeAsync();

		// Assert — full lifecycle: singleton warmup + platform SDK + JIT warmup
		optimizer.WarmupPlatformSdkCallCount.ShouldBe(1);
	}

	[Fact]
	public async Task OptimizeAsync_WhenDisabled_SkipsEntireWarmup()
	{
		// Arrange
		var optimizer = CreateOptimizer(enabled: false);

		// Act
		await optimizer.OptimizeAsync();

		// Assert
		optimizer.WarmupPlatformSdkCallCount.ShouldBe(0);
	}

	#endregion

	#region WarmupAsync — WithScopeFactory vs WithoutScopeFactory

	[Fact]
	public async Task WarmupAsync_WithScopeFactory_RetrievesLoggerFactory()
	{
		// Arrange — real DI container with IServiceScopeFactory
		var sp = new ServiceCollection()
			.AddLogging()
			.BuildServiceProvider();
		var optimizer = CreateOptimizer(enabled: true, serviceProvider: sp);

		// Act — this path uses scope.ServiceProvider.GetService<ILoggerFactory>()
		await optimizer.WarmupAsync();

		// Assert
		optimizer.WarmupPlatformSdkCallCount.ShouldBe(1);
	}

	[Fact]
	public async Task WarmupAsync_WithoutScopeFactory_UsesDirectServiceProvider()
	{
		// Arrange — mock provider without IServiceScopeFactory
		var sp = A.Fake<IServiceProvider>();
		A.CallTo(() => sp.GetService(typeof(IServiceScopeFactory))).Returns(null);
		A.CallTo(() => sp.GetService(typeof(ILoggerFactory)))
			.Returns(Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory.Instance);

		var optimizer = CreateOptimizer(enabled: true, serviceProvider: sp);

		// Act
		await optimizer.WarmupAsync();

		// Assert — direct fallback path exercised
		A.CallTo(() => sp.GetService(typeof(ILoggerFactory))).MustHaveHappenedOnceExactly();
		optimizer.WarmupPlatformSdkCallCount.ShouldBe(1);
	}

	#endregion

	#region WarmupAsync — Platform SDK Failure

	[Fact]
	public async Task WarmupAsync_WhenPlatformSdkThrows_PropagatesException()
	{
		// Arrange
		var sp = new ServiceCollection().BuildServiceProvider();
		var optimizer = CreateOptimizer(enabled: true, serviceProvider: sp, throwOnPlatformSdk: true);

		// Act & Assert — platform SDK exceptions are NOT caught (unlike singleton/JIT warmup)
		await Should.ThrowAsync<InvalidOperationException>(optimizer.WarmupAsync);
	}

	#endregion

	#region Multiple Calls

	[Fact]
	public async Task OptimizeAsync_CalledMultipleTimes_ExecutesEachTime()
	{
		// Arrange
		var sp = new ServiceCollection().AddLogging().BuildServiceProvider();
		var optimizer = CreateOptimizer(enabled: true, serviceProvider: sp);

		// Act
		await optimizer.OptimizeAsync();
		await optimizer.OptimizeAsync();

		// Assert
		optimizer.WarmupPlatformSdkCallCount.ShouldBe(2);
	}

	#endregion

	#region Helpers

	private static TestColdStartOptimizer CreateOptimizer(
		bool enabled = true,
		IServiceProvider? serviceProvider = null,
		bool throwOnPlatformSdk = false)
	{
		var sp = serviceProvider ?? new ServiceCollection().BuildServiceProvider();
		return new TestColdStartOptimizer(sp, EnabledTestLogger.Create(), enabled, throwOnPlatformSdk);
	}

	/// <summary>
	/// Concrete test implementation of <see cref="ColdStartOptimizerBase"/>.
	/// </summary>
	private sealed class TestColdStartOptimizer : ColdStartOptimizerBase
	{
		private readonly bool _enabled;
		private readonly bool _throwOnPlatformSdk;

		public TestColdStartOptimizer(
			IServiceProvider serviceProvider,
			ILogger logger,
			bool enabled = true,
			bool throwOnPlatformSdk = false)
			: base(serviceProvider, logger)
		{
			_enabled = enabled;
			_throwOnPlatformSdk = throwOnPlatformSdk;
		}

		public override bool IsEnabled => _enabled;

		protected override string PlatformName => "TestPlatform";

		public string ExposedPlatformName => PlatformName;

		public int WarmupPlatformSdkCallCount { get; private set; }

		protected override Task WarmupPlatformSdkAsync()
		{
			if (_throwOnPlatformSdk)
			{
				throw new InvalidOperationException("Platform SDK warmup failed");
			}

			WarmupPlatformSdkCallCount++;
			return Task.CompletedTask;
		}
	}

	#endregion
}
