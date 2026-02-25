// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Resilience.Polly;

using MsOptions = Microsoft.Extensions.Options.Options;

namespace Excalibur.Dispatch.Middleware.Tests.Resilience;

/// <summary>
/// Additional depth tests for <see cref="GracefulDegradationService"/> covering
/// DisposeAsync, metrics health, auto-adjustment, and fallback escalation.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Resilience")]
public sealed class GracefulDegradationDepthShould : IAsyncDisposable
{
	private GracefulDegradationService? _service;

	private GracefulDegradationService CreateService(GracefulDegradationOptions? options = null)
	{
		var opts = MsOptions.Create(options ?? new GracefulDegradationOptions
		{
			HealthCheckInterval = TimeSpan.FromHours(1), // Disable auto health checks during tests
			EnableAutoAdjustment = false,
		});
		var logger = A.Fake<ILogger<GracefulDegradationService>>();
		_service = new GracefulDegradationService(opts, logger);
		return _service;
	}

	public async ValueTask DisposeAsync()
	{
		if (_service != null)
		{
			await _service.DisposeAsync().ConfigureAwait(false);
		}
	}

	[Fact]
	public async Task DisposeAsync_CanBeCalledMultipleTimes()
	{
		// Arrange
		_service = CreateService();

		// Act & Assert — should not throw
		await _service.DisposeAsync().ConfigureAwait(false);
		await _service.DisposeAsync().ConfigureAwait(false);

		_service = null;
	}

	[Fact]
	public async Task DisposeAsync_AfterDispose_DoesNotThrow()
	{
		// Arrange
		_service = CreateService();
		_service.Dispose();

		// Act — DisposeAsync after Dispose
		await _service.DisposeAsync().ConfigureAwait(false);
		_service = null;
	}

	[Fact]
	public async Task ExecuteWithDegradationAsync_TracksMultipleOperations()
	{
		// Arrange
		_service = CreateService();
		var contextA = new DegradationContext<int>
		{
			PrimaryOperation = () => Task.FromResult(1),
			OperationName = "OpA",
		};
		var contextB = new DegradationContext<int>
		{
			PrimaryOperation = () => Task.FromResult(2),
			OperationName = "OpB",
		};

		// Act
		await _service.ExecuteWithDegradationAsync(contextA, CancellationToken.None).ConfigureAwait(false);
		await _service.ExecuteWithDegradationAsync(contextB, CancellationToken.None).ConfigureAwait(false);
		await _service.ExecuteWithDegradationAsync(contextA, CancellationToken.None).ConfigureAwait(false);

		// Assert
		var metrics = _service.GetMetrics();
		metrics.OperationStatistics.Count.ShouldBe(2);
		metrics.OperationStatistics["OpA"].TotalAttempts.ShouldBe(2);
		metrics.OperationStatistics["OpB"].TotalAttempts.ShouldBe(1);
		metrics.TotalOperations.ShouldBe(3);
	}

	[Fact]
	public async Task GetMetrics_AfterFallback_TracksCorrectly()
	{
		// Arrange
		_service = CreateService();
		_service.SetLevel(DegradationLevel.Moderate, "Test");

		var context = new DegradationContext<int>
		{
			PrimaryOperation = () => Task.FromResult(1),
			Fallbacks = new Dictionary<DegradationLevel, Func<Task<int>>>
			{
				[DegradationLevel.Moderate] = () => Task.FromResult(99),
			},
			OperationName = "FallbackOp",
			Priority = 100,
		};

		// Act
		var result = await _service.ExecuteWithDegradationAsync(context, CancellationToken.None).ConfigureAwait(false);

		// Assert
		result.ShouldBe(99);
		var metrics = _service.GetMetrics();
		metrics.TotalFallbacks.ShouldBe(1);
	}

	[Fact]
	public void GetMetrics_ReturnsHealthMetrics()
	{
		// Arrange
		_service = CreateService();

		// Act
		var metrics = _service.GetMetrics();

		// Assert
		metrics.HealthMetrics.ShouldNotBeNull();
	}

	[Fact]
	public void SetLevel_MultipleTimes_UpdatesMetrics()
	{
		// Arrange
		_service = CreateService();

		// Act
		_service.SetLevel(DegradationLevel.Minor, "First");
		_service.SetLevel(DegradationLevel.Moderate, "Second");
		_service.SetLevel(DegradationLevel.Major, "Third");

		// Assert
		var metrics = _service.GetMetrics();
		metrics.CurrentLevel.ShouldBe(DegradationLevel.Major);
		metrics.LastChangeReason.ShouldBe("Third");
	}

	[Fact]
	public async Task ExecuteWithDegradationAsync_FallbackEscalation_UsesNextHigherLevel()
	{
		// Arrange
		_service = CreateService();
		_service.SetLevel(DegradationLevel.Minor, "Test");

		var context = new DegradationContext<string>
		{
			PrimaryOperation = () => throw new InvalidOperationException("Fail"),
			Fallbacks = new Dictionary<DegradationLevel, Func<Task<string>>>
			{
				// No Minor fallback — should escalate to Moderate then Major
				[DegradationLevel.Major] = () => Task.FromResult("major-fallback"),
			},
			OperationName = "EscalationOp",
			IsCritical = false,
			Priority = 100,
		};

		// Act
		var result = await _service.ExecuteWithDegradationAsync(context, CancellationToken.None).ConfigureAwait(false);

		// Assert — should have found the Major fallback
		result.ShouldBe("major-fallback");
	}

	[Fact]
	public async Task ExecuteWithDegradationAsync_AtEmergency_CriticalStillRuns()
	{
		// Arrange
		_service = CreateService();
		_service.SetLevel(DegradationLevel.Emergency, "System critical");

		var context = new DegradationContext<int>
		{
			PrimaryOperation = () => Task.FromResult(1234),
			OperationName = "CriticalAtEmergency",
			IsCritical = true,
		};

		// Act
		var result = await _service.ExecuteWithDegradationAsync(context, CancellationToken.None).ConfigureAwait(false);

		// Assert
		result.ShouldBe(1234);
	}

	[Fact]
	public async Task ExecuteWithDegradationAsync_FailureTracking_RecordsFailuresCorrectly()
	{
		// Arrange
		_service = CreateService();
		var context = new DegradationContext<int>
		{
			PrimaryOperation = () => throw new InvalidOperationException("Boom"),
			OperationName = "FailOp",
		};

		// Act
		try
		{
			await _service.ExecuteWithDegradationAsync(context, CancellationToken.None).ConfigureAwait(false);
		}
		catch (InvalidOperationException)
		{
			// Expected
		}

		// Assert
		var metrics = _service.GetMetrics();
		metrics.OperationStatistics["FailOp"].Failures.ShouldBeGreaterThan(0);
		metrics.SuccessRate.ShouldBe(0.0);
	}

	[Fact]
	public async Task ExecuteWithDegradationAsync_MixedSuccessAndFailure_CalculatesRate()
	{
		// Arrange
		_service = CreateService();

		// 3 successes
		for (var i = 0; i < 3; i++)
		{
			await _service.ExecuteWithDegradationAsync(new DegradationContext<int>
			{
				PrimaryOperation = () => Task.FromResult(1),
				OperationName = "MixedOp",
			}, CancellationToken.None).ConfigureAwait(false);
		}

		// 1 failure
		try
		{
			await _service.ExecuteWithDegradationAsync(new DegradationContext<int>
			{
				PrimaryOperation = () => throw new InvalidOperationException("Fail"),
				OperationName = "MixedOp",
			}, CancellationToken.None).ConfigureAwait(false);
		}
		catch (InvalidOperationException)
		{
			// Expected
		}

		// Assert — 3 successes out of 4 total = 75% success rate
		var metrics = _service.GetMetrics();
		metrics.TotalOperations.ShouldBe(4);
		metrics.SuccessRate.ShouldBeInRange(0.74, 0.76);
	}

	[Fact]
	public void SetLevel_BackToNormal_ClearsLevel()
	{
		// Arrange
		_service = CreateService();
		_service.SetLevel(DegradationLevel.Major, "Crisis");

		// Act
		_service.SetLevel(DegradationLevel.Normal, "Recovered");

		// Assert
		_service.CurrentLevel.ShouldBe(DegradationLevel.Normal);
		_service.GetMetrics().LastChangeReason.ShouldBe("Recovered");
	}

	[Fact]
	public async Task ExecuteWithDegradationAsync_AtModerate_NoCritical_UsesCurrentFallback()
	{
		// Arrange
		_service = CreateService();
		_service.SetLevel(DegradationLevel.Moderate, "Test");

		var context = new DegradationContext<string>
		{
			PrimaryOperation = () => Task.FromResult("primary"),
			Fallbacks = new Dictionary<DegradationLevel, Func<Task<string>>>
			{
				[DegradationLevel.Moderate] = () => Task.FromResult("moderate-fallback"),
				[DegradationLevel.Major] = () => Task.FromResult("major-fallback"),
			},
			OperationName = "SelectionOp",
			Priority = 100,
		};

		// Act
		var result = await _service.ExecuteWithDegradationAsync(context, CancellationToken.None).ConfigureAwait(false);

		// Assert — should use the Moderate (current level) fallback
		result.ShouldBe("moderate-fallback");
	}

	[Fact]
	public void GetMetrics_EmptyStats_SuccessRateIsOne()
	{
		// Arrange
		_service = CreateService();

		// Act
		var metrics = _service.GetMetrics();

		// Assert — no operations: success rate defaults to 1.0
		metrics.SuccessRate.ShouldBe(1.0);
	}
}
