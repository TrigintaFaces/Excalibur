// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Excalibur.Dispatch.Hosting.Serverless.Tests;

/// <summary>
/// Unit tests for <see cref="ColdStartOptimizerBase"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Platform")]
public sealed class ColdStartOptimizerBaseShould : UnitTestBase
{
	#region Constructor Tests

	[Fact]
	public void Constructor_WithNullServiceProvider_ThrowsArgumentNullException()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(
			() => new TestColdStartOptimizer(null!, EnabledTestLogger.Create()));
	}

	[Fact]
	public void Constructor_WithNullLogger_ThrowsArgumentNullException()
	{
		// Arrange
		var sp = A.Fake<IServiceProvider>();

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(
			() => new TestColdStartOptimizer(sp, null!));
	}

	[Fact]
	public void Constructor_WithValidArgs_CreatesInstance()
	{
		// Arrange & Act
		var optimizer = CreateOptimizer();

		// Assert
		optimizer.ShouldNotBeNull();
	}

	#endregion

	#region OptimizeAsync Tests

	[Fact]
	public async Task OptimizeAsync_WhenDisabled_ReturnsWithoutWarming()
	{
		// Arrange
		var optimizer = CreateOptimizer(enabled: false);

		// Act
		await optimizer.OptimizeAsync();

		// Assert
		optimizer.WarmupPlatformSdkCallCount.ShouldBe(0);
	}

	[Fact]
	public async Task OptimizeAsync_WhenEnabled_PerformsWarmup()
	{
		// Arrange
		var sp = new ServiceCollection()
			.AddLogging()
			.BuildServiceProvider();
		var optimizer = CreateOptimizer(enabled: true, serviceProvider: sp);

		// Act
		await optimizer.OptimizeAsync();

		// Assert
		optimizer.WarmupPlatformSdkCallCount.ShouldBe(1);
	}

	#endregion

	#region WarmupAsync Tests

	[Fact]
	public async Task WarmupAsync_WhenDisabled_SkipsAll()
	{
		// Arrange
		var optimizer = CreateOptimizer(enabled: false);

		// Act
		await optimizer.WarmupAsync();

		// Assert
		optimizer.WarmupPlatformSdkCallCount.ShouldBe(0);
	}

	[Fact]
	public async Task WarmupAsync_WhenEnabled_WarmsSingletonServicesAndPlatformSdk()
	{
		// Arrange
		var sp = new ServiceCollection()
			.AddLogging()
			.BuildServiceProvider();
		var optimizer = CreateOptimizer(enabled: true, serviceProvider: sp);

		// Act
		await optimizer.WarmupAsync();

		// Assert
		optimizer.WarmupPlatformSdkCallCount.ShouldBe(1);
	}

	[Fact]
	public async Task WarmupAsync_WhenEnabled_CallsPlatformSdk()
	{
		// Arrange
		var sp = new ServiceCollection().BuildServiceProvider();
		var optimizer = CreateOptimizer(enabled: true, serviceProvider: sp);

		// Act
		await optimizer.WarmupAsync();

		// Assert
		optimizer.WarmupPlatformSdkCallCount.ShouldBeGreaterThan(0);
	}

	[Fact]
	public async Task WarmupAsync_WithScopeFactory_UsesScopeForWarmup()
	{
		// Arrange
		var sp = new ServiceCollection()
			.AddLogging()
			.BuildServiceProvider();
		var optimizer = CreateOptimizer(enabled: true, serviceProvider: sp);

		// Act
		await optimizer.WarmupAsync();

		// Assert — no exception means scope-based warmup worked
		optimizer.WarmupPlatformSdkCallCount.ShouldBe(1);
	}

	[Fact]
	public async Task WarmupAsync_WithoutScopeFactory_FallsBackToDirectServiceProvider()
	{
		// Arrange — bare ServiceProvider without IServiceScopeFactory
		var sp = A.Fake<IServiceProvider>();
		A.CallTo(() => sp.GetService(A<Type>._)).Returns(null);
		var optimizer = CreateOptimizer(enabled: true, serviceProvider: sp);

		// Act
		await optimizer.WarmupAsync();

		// Assert — no exception, warmup still works
		optimizer.WarmupPlatformSdkCallCount.ShouldBe(1);
	}

	#endregion

	#region IsEnabled Tests

	[Fact]
	public void IsEnabled_WhenTrue_ReturnsTrue()
	{
		// Arrange
		var optimizer = CreateOptimizer(enabled: true);

		// Assert
		optimizer.IsEnabled.ShouldBeTrue();
	}

	[Fact]
	public void IsEnabled_WhenFalse_ReturnsFalse()
	{
		// Arrange
		var optimizer = CreateOptimizer(enabled: false);

		// Assert
		optimizer.IsEnabled.ShouldBeFalse();
	}

	#endregion

	#region Helpers

	private static TestColdStartOptimizer CreateOptimizer(
		bool enabled = true,
		IServiceProvider? serviceProvider = null)
	{
		var sp = serviceProvider ?? new ServiceCollection().BuildServiceProvider();
		return new TestColdStartOptimizer(sp, EnabledTestLogger.Create(), enabled);
	}

	/// <summary>
	/// Concrete test implementation of <see cref="ColdStartOptimizerBase"/>.
	/// </summary>
	private sealed class TestColdStartOptimizer : ColdStartOptimizerBase
	{
		private readonly bool _enabled;

		public TestColdStartOptimizer(IServiceProvider serviceProvider, ILogger logger, bool enabled = true)
			: base(serviceProvider, logger)
		{
			_enabled = enabled;
		}

		public override bool IsEnabled => _enabled;

		protected override string PlatformName => "TestPlatform";

		public int WarmupPlatformSdkCallCount { get; private set; }

		protected override Task WarmupPlatformSdkAsync()
		{
			WarmupPlatformSdkCallCount++;
			return Task.CompletedTask;
		}
	}

	#endregion
}
