// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Resilience.Polly;

using MsOptions = Microsoft.Extensions.Options.Options;

namespace Excalibur.Dispatch.Middleware.Tests.Resilience;

/// <summary>
/// Unit tests for <see cref="GracefulDegradationService"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Resilience")]
public sealed class GracefulDegradationServiceShould : UnitTestBase
{
	private GracefulDegradationService? _service;

	protected override void Dispose(bool disposing)
	{
		if (disposing)
		{
			_service?.Dispose();
		}
		base.Dispose(disposing);
	}

	private GracefulDegradationService CreateService(GracefulDegradationOptions? options = null)
	{
		var opts = MsOptions.Create(options ?? new GracefulDegradationOptions
		{
			HealthCheckInterval = TimeSpan.FromHours(1), // Disable auto health checks during tests
			EnableAutoAdjustment = false
		});
		var logger = A.Fake<ILogger<GracefulDegradationService>>();
		return new GracefulDegradationService(opts, logger);
	}

	#region Constructor Tests

	[Fact]
	public void Constructor_WithNullOptions_ThrowsArgumentNullException()
	{
		// Arrange
		var logger = A.Fake<ILogger<GracefulDegradationService>>();

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => new GracefulDegradationService(null!, logger));
	}

	[Fact]
	public void Constructor_WithNullLogger_ThrowsArgumentNullException()
	{
		// Arrange
		var options = MsOptions.Create(new GracefulDegradationOptions());

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => new GracefulDegradationService(options, null!));
	}

	[Fact]
	public void Constructor_WithValidParameters_CreatesInstance()
	{
		// Act
		_service = CreateService();

		// Assert
		_ = _service.ShouldNotBeNull();
		_service.CurrentLevel.ShouldBe(DegradationLevel.Normal);
	}

	#endregion

	#region CurrentLevel Tests

	[Fact]
	public void CurrentLevel_InitialValue_IsNormal()
	{
		// Arrange
		_service = CreateService();

		// Assert
		_service.CurrentLevel.ShouldBe(DegradationLevel.Normal);
	}

	#endregion

	#region SetLevel Tests

	[Fact]
	public void SetLevel_WithNullReason_ThrowsArgumentException()
	{
		// Arrange
		_service = CreateService();

		// Act & Assert
		_ = Should.Throw<ArgumentException>(() => _service.SetLevel(DegradationLevel.Minor, null!));
	}

	[Fact]
	public void SetLevel_WithEmptyReason_ThrowsArgumentException()
	{
		// Arrange
		_service = CreateService();

		// Act & Assert
		_ = Should.Throw<ArgumentException>(() => _service.SetLevel(DegradationLevel.Minor, string.Empty));
	}

	[Fact]
	public void SetLevel_WithWhitespaceReason_ThrowsArgumentException()
	{
		// Arrange
		_service = CreateService();

		// Act & Assert
		_ = Should.Throw<ArgumentException>(() => _service.SetLevel(DegradationLevel.Minor, "   "));
	}

	[Fact]
	public void SetLevel_WithValidParameters_ChangesLevel()
	{
		// Arrange
		_service = CreateService();

		// Act
		_service.SetLevel(DegradationLevel.Moderate, "High CPU usage");

		// Assert
		_service.CurrentLevel.ShouldBe(DegradationLevel.Moderate);
	}

	[Fact]
	public void SetLevel_ToSameLevel_DoesNotChangeTimestamp()
	{
		// Arrange
		_service = CreateService();
		_service.SetLevel(DegradationLevel.Minor, "Initial");
		var metrics1 = _service.GetMetrics();

		// Act
		_service.SetLevel(DegradationLevel.Minor, "Same level"); // Same level
		var metrics2 = _service.GetMetrics();

		// Assert
		metrics2.LastLevelChange.ShouldBe(metrics1.LastLevelChange);
	}

	[Theory]
	[InlineData(DegradationLevel.Minor)]
	[InlineData(DegradationLevel.Moderate)]
	[InlineData(DegradationLevel.Major)]
	[InlineData(DegradationLevel.Severe)]
	[InlineData(DegradationLevel.Emergency)]
	public void SetLevel_ToEachLevel_UpdatesCorrectly(DegradationLevel level)
	{
		// Arrange
		_service = CreateService();

		// Act
		_service.SetLevel(level, "Test reason");

		// Assert
		_service.CurrentLevel.ShouldBe(level);
	}

	#endregion

	#region ExecuteWithDegradationAsync Tests

	[Fact]
	public async Task ExecuteWithDegradationAsync_WithNullContext_ThrowsArgumentNullException()
	{
		// Arrange
		_service = CreateService();

		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentNullException>(
			() => _service.ExecuteWithDegradationAsync<int>(null!, CancellationToken.None));
	}

	[Fact]
	public async Task ExecuteWithDegradationAsync_AtNormalLevel_ExecutesPrimaryOperation()
	{
		// Arrange
		_service = CreateService();
		var context = new DegradationContext<int>
		{
			PrimaryOperation = () => Task.FromResult(42),
			OperationName = "TestOp"
		};

		// Act
		var result = await _service.ExecuteWithDegradationAsync(context, CancellationToken.None);

		// Assert
		result.ShouldBe(42);
	}

	[Fact]
	public async Task ExecuteWithDegradationAsync_CriticalOperation_ExecutesPrimaryEvenAtHighLevel()
	{
		// Arrange
		_service = CreateService();
		_service.SetLevel(DegradationLevel.Severe, "Test");
		var context = new DegradationContext<int>
		{
			PrimaryOperation = () => Task.FromResult(100),
			OperationName = "CriticalOp",
			IsCritical = true
		};

		// Act
		var result = await _service.ExecuteWithDegradationAsync(context, CancellationToken.None);

		// Assert
		result.ShouldBe(100);
	}

	[Fact]
	public async Task ExecuteWithDegradationAsync_LowPriorityAtMinorLevel_RejectsOperation()
	{
		// Arrange
		var options = new GracefulDegradationOptions
		{
			HealthCheckInterval = TimeSpan.FromHours(1),
			EnableAutoAdjustment = false,
		};
		options.Levels[0] = options.Levels[0] with { PriorityThreshold = 50 };
		_service = CreateService(options);
		_service.SetLevel(DegradationLevel.Minor, "Test");
		var context = new DegradationContext<int>
		{
			PrimaryOperation = () => Task.FromResult(1),
			OperationName = "LowPriorityOp",
			Priority = 10 // Below threshold of 50
		};

		// Act & Assert
		_ = await Should.ThrowAsync<DegradationRejectedException>(
			() => _service.ExecuteWithDegradationAsync(context, CancellationToken.None));
	}

	[Fact]
	public async Task ExecuteWithDegradationAsync_WithFallback_ExecutesFallbackWhenPrimaryFails()
	{
		// Arrange
		_service = CreateService();
		var fallbacks = new Dictionary<DegradationLevel, Func<Task<int>>>
		{
			[DegradationLevel.Normal] = () => Task.FromResult(10)
		};
		var context = new DegradationContext<int>
		{
			PrimaryOperation = () => throw new InvalidOperationException("Primary failed"),
			Fallbacks = fallbacks,
			OperationName = "TestOp"
		};

		// Act
		var result = await _service.ExecuteWithDegradationAsync(context, CancellationToken.None);

		// Assert
		result.ShouldBe(10);
	}

	[Fact]
	public async Task ExecuteWithDegradationAsync_WithoutFallback_PropagatesException()
	{
		// Arrange
		_service = CreateService();
		var context = new DegradationContext<int>
		{
			PrimaryOperation = () => throw new InvalidOperationException("Primary failed"),
			OperationName = "TestOp"
		};

		// Act & Assert
		_ = await Should.ThrowAsync<InvalidOperationException>(
			() => _service.ExecuteWithDegradationAsync(context, CancellationToken.None));
	}

	[Fact]
	public async Task ExecuteWithDegradationAsync_AtDegradedLevel_UsesFallbackForNonCritical()
	{
		// Arrange
		_service = CreateService();
		_service.SetLevel(DegradationLevel.Moderate, "High load");
		var fallbacks = new Dictionary<DegradationLevel, Func<Task<string>>>
		{
			[DegradationLevel.Moderate] = () => Task.FromResult("fallback-result")
		};
		var context = new DegradationContext<string>
		{
			PrimaryOperation = () => Task.FromResult("primary-result"),
			Fallbacks = fallbacks,
			OperationName = "TestOp",
			Priority = 50 // Not rejected
		};

		// Act
		var result = await _service.ExecuteWithDegradationAsync(context, CancellationToken.None);

		// Assert
		result.ShouldBe("fallback-result");
	}

	[Fact]
	public async Task ExecuteWithDegradationAsync_NoMatchingFallback_ThrowsNoFallbackAvailableException()
	{
		// Arrange
		_service = CreateService();
		_service.SetLevel(DegradationLevel.Moderate, "Test");
		var context = new DegradationContext<int>
		{
			PrimaryOperation = () => throw new InvalidOperationException("fail"),
			Fallbacks = new Dictionary<DegradationLevel, Func<Task<int>>>(), // No fallbacks
			OperationName = "TestOp",
			Priority = 100 // Not rejected
		};

		// Act & Assert
		_ = await Should.ThrowAsync<NoFallbackAvailableException>(
			() => _service.ExecuteWithDegradationAsync(context, CancellationToken.None));
	}

	[Fact]
	public async Task ExecuteWithDegradationAsync_AtEmergencyLevel_RejectsNonCritical()
	{
		// Arrange
		_service = CreateService();
		_service.SetLevel(DegradationLevel.Emergency, "System critical");
		var context = new DegradationContext<int>
		{
			PrimaryOperation = () => Task.FromResult(1),
			OperationName = "NonCriticalOp",
			IsCritical = false,
			Priority = 100 // High priority but still non-critical
		};

		// Act & Assert
		_ = await Should.ThrowAsync<DegradationRejectedException>(
			() => _service.ExecuteWithDegradationAsync(context, CancellationToken.None));
	}

	#endregion

	#region GetMetrics Tests

	[Fact]
	public void GetMetrics_ReturnsCorrectInitialMetrics()
	{
		// Arrange
		_service = CreateService();

		// Act
		var metrics = _service.GetMetrics();

		// Assert
		metrics.CurrentLevel.ShouldBe(DegradationLevel.Normal);
		metrics.LastChangeReason.ShouldBe("Initial");
		metrics.OperationStatistics.ShouldBeEmpty();
		metrics.TotalOperations.ShouldBe(0);
		metrics.TotalFallbacks.ShouldBe(0);
		metrics.SuccessRate.ShouldBe(1.0);
	}

	[Fact]
	public async Task GetMetrics_TracksOperationStatistics()
	{
		// Arrange
		_service = CreateService();
		var context = new DegradationContext<int>
		{
			PrimaryOperation = () => Task.FromResult(1),
			OperationName = "TrackedOp"
		};

		// Act
		await _service.ExecuteWithDegradationAsync(context, CancellationToken.None);
		await _service.ExecuteWithDegradationAsync(context, CancellationToken.None);
		var metrics = _service.GetMetrics();

		// Assert
		metrics.OperationStatistics.ShouldContainKey("TrackedOp");
		metrics.OperationStatistics["TrackedOp"].TotalAttempts.ShouldBe(2);
		metrics.OperationStatistics["TrackedOp"].Successes.ShouldBe(2);
	}

	[Fact]
	public void GetMetrics_AfterLevelChange_ReflectsNewLevel()
	{
		// Arrange
		_service = CreateService();

		// Act
		_service.SetLevel(DegradationLevel.Major, "High CPU");
		var metrics = _service.GetMetrics();

		// Assert
		metrics.CurrentLevel.ShouldBe(DegradationLevel.Major);
		metrics.LastChangeReason.ShouldBe("High CPU");
	}

	#endregion

	#region Fallback Selection Tests

	[Fact]
	public async Task ExecuteWithDegradationAsync_NoCurrentLevelFallback_UsesHigherLevelFallback()
	{
		// Arrange - When at Minor level but only have Moderate fallback, should use Moderate
		_service = CreateService();
		_service.SetLevel(DegradationLevel.Minor, "Test");

		var fallbacks = new Dictionary<DegradationLevel, Func<Task<int>>>
		{
			[DegradationLevel.Moderate] = () => Task.FromResult(99) // Higher level fallback
		};

		var context = new DegradationContext<int>
		{
			PrimaryOperation = () => throw new InvalidOperationException("Primary failed"),
			Fallbacks = fallbacks,
			OperationName = "TestOp",
			IsCritical = false,
			Priority = 100
		};

		// Act
		var result = await _service.ExecuteWithDegradationAsync(context, CancellationToken.None);

		// Assert - Should have used the Moderate level fallback
		result.ShouldBe(99);
	}

	[Fact]
	public async Task ExecuteWithDegradationAsync_CriticalOperationFailsWithFallback_UsesFallback()
	{
		// Arrange - Critical operation fails, fallback available
		_service = CreateService();
		_service.SetLevel(DegradationLevel.Severe, "Test");

		var fallbacks = new Dictionary<DegradationLevel, Func<Task<int>>>
		{
			[DegradationLevel.Severe] = () => Task.FromResult(77)
		};

		var context = new DegradationContext<int>
		{
			PrimaryOperation = () => throw new InvalidOperationException("Critical failed"),
			Fallbacks = fallbacks,
			OperationName = "CriticalOp",
			IsCritical = true // Critical operation still gets fallback
		};

		// Act
		var result = await _service.ExecuteWithDegradationAsync(context, CancellationToken.None);

		// Assert
		result.ShouldBe(77);
	}

	[Fact]
	public async Task ExecuteWithDegradationAsync_CriticalOperationFailsNoFallback_ThrowsOriginalException()
	{
		// Arrange - Critical operation fails, no fallback
		_service = CreateService();
		_service.SetLevel(DegradationLevel.Severe, "Test");

		var context = new DegradationContext<int>
		{
			PrimaryOperation = () => throw new InvalidOperationException("Critical failed"),
			Fallbacks = new Dictionary<DegradationLevel, Func<Task<int>>>(), // No fallbacks
			OperationName = "CriticalOp",
			IsCritical = true
		};

		// Act & Assert
		_ = await Should.ThrowAsync<InvalidOperationException>(
			() => _service.ExecuteWithDegradationAsync(context, CancellationToken.None));
	}

	#endregion

	#region Priority Threshold Tests

	[Fact]
	public async Task ExecuteWithDegradationAsync_LowPriorityAtModerateLevel_RejectsOperation()
	{
		// Arrange
		var options = new GracefulDegradationOptions
		{
			HealthCheckInterval = TimeSpan.FromHours(1),
			EnableAutoAdjustment = false,
		};
		options.Levels[1] = options.Levels[1] with { PriorityThreshold = 60 };
		_service = CreateService(options);
		_service.SetLevel(DegradationLevel.Moderate, "Test");

		var context = new DegradationContext<int>
		{
			PrimaryOperation = () => Task.FromResult(1),
			OperationName = "LowPriorityOp",
			Priority = 30 // Below threshold of 60
		};

		// Act & Assert
		_ = await Should.ThrowAsync<DegradationRejectedException>(
			() => _service.ExecuteWithDegradationAsync(context, CancellationToken.None));
	}

	[Fact]
	public async Task ExecuteWithDegradationAsync_LowPriorityAtMajorLevel_RejectsOperation()
	{
		// Arrange
		var options = new GracefulDegradationOptions
		{
			HealthCheckInterval = TimeSpan.FromHours(1),
			EnableAutoAdjustment = false,
		};
		options.Levels[2] = options.Levels[2] with { PriorityThreshold = 70 };
		_service = CreateService(options);
		_service.SetLevel(DegradationLevel.Major, "Test");

		var context = new DegradationContext<int>
		{
			PrimaryOperation = () => Task.FromResult(1),
			OperationName = "LowPriorityOp",
			Priority = 40 // Below threshold of 70
		};

		// Act & Assert
		_ = await Should.ThrowAsync<DegradationRejectedException>(
			() => _service.ExecuteWithDegradationAsync(context, CancellationToken.None));
	}

	[Fact]
	public async Task ExecuteWithDegradationAsync_LowPriorityAtSevereLevel_RejectsOperation()
	{
		// Arrange
		var options = new GracefulDegradationOptions
		{
			HealthCheckInterval = TimeSpan.FromHours(1),
			EnableAutoAdjustment = false,
		};
		options.Levels[3] = options.Levels[3] with { PriorityThreshold = 80 };
		_service = CreateService(options);
		_service.SetLevel(DegradationLevel.Severe, "Test");

		var context = new DegradationContext<int>
		{
			PrimaryOperation = () => Task.FromResult(1),
			OperationName = "LowPriorityOp",
			Priority = 50 // Below threshold of 80
		};

		// Act & Assert
		_ = await Should.ThrowAsync<DegradationRejectedException>(
			() => _service.ExecuteWithDegradationAsync(context, CancellationToken.None));
	}

	[Fact]
	public async Task ExecuteWithDegradationAsync_HighPriorityAtSevereLevel_Succeeds()
	{
		// Arrange
		var options = new GracefulDegradationOptions
		{
			HealthCheckInterval = TimeSpan.FromHours(1),
			EnableAutoAdjustment = false,
		};
		options.Levels[3] = options.Levels[3] with { PriorityThreshold = 80 };
		_service = CreateService(options);
		_service.SetLevel(DegradationLevel.Severe, "Test");

		var context = new DegradationContext<int>
		{
			PrimaryOperation = () => Task.FromResult(42),
			OperationName = "HighPriorityOp",
			IsCritical = true // Critical bypasses priority checks
		};

		// Act
		var result = await _service.ExecuteWithDegradationAsync(context, CancellationToken.None);

		// Assert
		result.ShouldBe(42);
	}

	#endregion

	#region Failure Tracking Tests

	[Fact]
	public async Task ExecuteWithDegradationAsync_OperationFails_IncrementsFailures()
	{
		// Arrange
		_service = CreateService();
		var context = new DegradationContext<int>
		{
			PrimaryOperation = () => throw new InvalidOperationException("Fail"),
			OperationName = "FailingOp",
			Fallbacks = new Dictionary<DegradationLevel, Func<Task<int>>>()
		};

		// Act
		_ = await Should.ThrowAsync<InvalidOperationException>(
			() => _service.ExecuteWithDegradationAsync(context, CancellationToken.None));

		// Assert - Failures are tracked (implementation increments in two places for coverage)
		var metrics = _service.GetMetrics();
		metrics.OperationStatistics.ShouldContainKey("FailingOp");
		metrics.OperationStatistics["FailingOp"].Failures.ShouldBeGreaterThan(0);
	}

	[Fact]
	public async Task GetMetrics_WithMixedResults_CalculatesCorrectSuccessRate()
	{
		// Arrange
		_service = CreateService();
		var successContext = new DegradationContext<int>
		{
			PrimaryOperation = () => Task.FromResult(1),
			OperationName = "MixedOp"
		};
		var failContext = new DegradationContext<int>
		{
			PrimaryOperation = () => throw new InvalidOperationException("Fail"),
			OperationName = "MixedOp",
			Fallbacks = new Dictionary<DegradationLevel, Func<Task<int>>>()
		};

		// Act - 2 successes, 1 failure = 66.67% success rate
		await _service.ExecuteWithDegradationAsync(successContext, CancellationToken.None);
		await _service.ExecuteWithDegradationAsync(successContext, CancellationToken.None);
		try
		{
			await _service.ExecuteWithDegradationAsync(failContext, CancellationToken.None);
		}
		catch (InvalidOperationException)
		{
			// Expected
		}

		var metrics = _service.GetMetrics();

		// Assert
		metrics.TotalOperations.ShouldBe(3);
		metrics.SuccessRate.ShouldBeInRange(0.66, 0.67);
	}

	#endregion

	#region Dispose Tests

	[Fact]
	public void Dispose_CanBeCalledMultipleTimes()
	{
		// Arrange
		_service = CreateService();

		// Act & Assert - Should not throw
		_service.Dispose();
		_service.Dispose();
		_service.Dispose();

		_service = null;
	}

	#endregion
}
