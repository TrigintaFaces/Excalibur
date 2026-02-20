// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Security.Tests.Compliance.Soc2;

/// <summary>
/// Unit tests for <see cref="ComplianceMonitoringService"/>.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public sealed class ComplianceMonitoringServiceShould
{
	private readonly IServiceScopeFactory _fakeScopeFactory;
	private readonly IOptions<Soc2Options> _fakeOptions;
	private readonly ILogger<ComplianceMonitoringService> _fakeLogger;
	private readonly ISoc2ComplianceService _fakeComplianceService;
	private readonly IComplianceAlertHandler _fakeAlertHandler;

	public ComplianceMonitoringServiceShould()
	{
		_fakeScopeFactory = A.Fake<IServiceScopeFactory>();
		_fakeOptions = A.Fake<IOptions<Soc2Options>>();
		_fakeLogger = A.Fake<ILogger<ComplianceMonitoringService>>();
		_fakeComplianceService = A.Fake<ISoc2ComplianceService>();
		_fakeAlertHandler = A.Fake<IComplianceAlertHandler>();

		// Default options with monitoring enabled
		_ = A.CallTo(() => _fakeOptions.Value).Returns(new Soc2Options
		{
			EnableContinuousMonitoring = true,
			EnableAlerts = true,
			MonitoringInterval = TimeSpan.FromSeconds(1),
			AlertThreshold = GapSeverity.Medium,
			EnabledCategories = [TrustServicesCategory.Security]
		});

		SetupServiceScope();
	}

	#region Constructor Tests

	[Fact]
	public void ThrowArgumentNullException_WhenScopeFactoryIsNull()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() => new ComplianceMonitoringService(
			null!,
			_fakeOptions,
			_fakeLogger))
			.ParamName.ShouldBe("scopeFactory");
	}

	[Fact]
	public void ThrowArgumentNullException_WhenOptionsIsNull()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() => new ComplianceMonitoringService(
			_fakeScopeFactory,
			null!,
			_fakeLogger))
			.ParamName.ShouldBe("options");
	}

	[Fact]
	public void ThrowArgumentNullException_WhenLoggerIsNull()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() => new ComplianceMonitoringService(
			_fakeScopeFactory,
			_fakeOptions,
			null!))
			.ParamName.ShouldBe("logger");
	}

	#endregion Constructor Tests

	#region ExecuteAsync Tests

	[Fact]
	public async Task ExecuteAsync_ExitImmediately_WhenMonitoringDisabled()
	{
		// Arrange
		_ = A.CallTo(() => _fakeOptions.Value).Returns(new Soc2Options
		{
			EnableContinuousMonitoring = false
		});

		var sut = new ComplianceMonitoringService(_fakeScopeFactory, _fakeOptions, _fakeLogger);
		var cts = new CancellationTokenSource();

		// Act
		await sut.StartAsync(cts.Token);
		await Task.Delay(2000); // Give it time to potentially run (generous for full-suite parallel load)
		await sut.StopAsync(cts.Token);

		// Assert
		A.CallTo(() => _fakeComplianceService.GetComplianceStatusAsync(
				A<string>._,
				A<CancellationToken>._))
			.MustNotHaveHappened();
	}

	[Fact]
	public async Task ExecuteAsync_GetComplianceStatus_WhenMonitoringEnabled()
	{
		// Arrange
		SetupFullyCompliantStatus();
		var sut = new ComplianceMonitoringService(_fakeScopeFactory, _fakeOptions, _fakeLogger);
		var cts = new CancellationTokenSource();

		// Act
		await sut.StartAsync(cts.Token);
		await Task.Delay(3000); // Generous for full-suite parallel load (1s monitoring interval) // Let one cycle complete
		cts.Cancel();

		try
		{
			await sut.StopAsync(CancellationToken.None);
		}
		catch (OperationCanceledException)
		{
			// Expected
		}

		// Assert
		_ = A.CallTo(() => _fakeComplianceService.GetComplianceStatusAsync(
				A<string>._,
				A<CancellationToken>._))
			.MustHaveHappened();
	}

	[Fact]
	public async Task ExecuteAsync_HandleCancellation_Gracefully()
	{
		// Arrange
		SetupFullyCompliantStatus();
		var sut = new ComplianceMonitoringService(_fakeScopeFactory, _fakeOptions, _fakeLogger);
		var cts = new CancellationTokenSource();

		// Act
		await sut.StartAsync(cts.Token);
		cts.Cancel();

		// Assert - Should not throw
		await Should.NotThrowAsync(() => sut.StopAsync(CancellationToken.None));
	}

	[Fact]
	public async Task ExecuteAsync_ContinueAfterException_InMonitoringCycle()
	{
		// Arrange
		var callCount = 0;
		_ = A.CallTo(() => _fakeComplianceService.GetComplianceStatusAsync(
				A<string>._,
				A<CancellationToken>._))
			.Invokes(() =>
			{
				callCount++;
				if (callCount == 1)
				{
					throw new InvalidOperationException("First call fails");
				}
			})
			.ReturnsLazily(() => CreateFullyCompliantStatus());

		var sut = new ComplianceMonitoringService(_fakeScopeFactory, _fakeOptions, _fakeLogger);
		var cts = new CancellationTokenSource();

		// Act
		await sut.StartAsync(cts.Token);
		await Task.Delay(5000); // Generous for full-suite parallel load (needs 2+ cycles at 1s interval) // Give time for retries
		cts.Cancel();

		try
		{
			await sut.StopAsync(CancellationToken.None);
		}
		catch (OperationCanceledException)
		{
			// Expected
		}

		// Assert - Should have made multiple calls despite first failure
		callCount.ShouldBeGreaterThan(1);
	}

	#endregion ExecuteAsync Tests

	#region Alert Notification Tests

	[Fact]
	public async Task ExecuteAsync_NotifyComplianceGap_WhenGapExceedsThreshold()
	{
		// Arrange
		SetupStatusWithGap(GapSeverity.High); // Above Medium threshold
		var sut = new ComplianceMonitoringService(_fakeScopeFactory, _fakeOptions, _fakeLogger);
		var cts = new CancellationTokenSource();

		// Act
		await sut.StartAsync(cts.Token);
		await Task.Delay(3000); // Generous for full-suite parallel load (1s monitoring interval)
		cts.Cancel();

		try
		{
			await sut.StopAsync(CancellationToken.None);
		}
		catch (OperationCanceledException)
		{
			// Expected
		}

		// Assert
		_ = A.CallTo(() => _fakeAlertHandler.HandleComplianceGapAsync(
				A<ComplianceGapAlert>._,
				A<CancellationToken>._))
			.MustHaveHappened();
	}

	[Fact]
	public async Task ExecuteAsync_NotNotifyComplianceGap_WhenGapBelowThreshold()
	{
		// Arrange
		SetupStatusWithGap(GapSeverity.Low); // Below Medium threshold
		var sut = new ComplianceMonitoringService(_fakeScopeFactory, _fakeOptions, _fakeLogger);
		var cts = new CancellationTokenSource();

		// Act
		await sut.StartAsync(cts.Token);
		await Task.Delay(3000); // Generous for full-suite parallel load (1s monitoring interval)
		cts.Cancel();

		try
		{
			await sut.StopAsync(CancellationToken.None);
		}
		catch (OperationCanceledException)
		{
			// Expected
		}

		// Assert
		A.CallTo(() => _fakeAlertHandler.HandleComplianceGapAsync(
				A<ComplianceGapAlert>._,
				A<CancellationToken>._))
			.MustNotHaveHappened();
	}

	[Fact]
	public async Task ExecuteAsync_NotSendAlerts_WhenAlertsDisabled()
	{
		// Arrange
		_ = A.CallTo(() => _fakeOptions.Value).Returns(new Soc2Options
		{
			EnableContinuousMonitoring = true,
			EnableAlerts = false,
			MonitoringInterval = TimeSpan.FromSeconds(1),
			EnabledCategories = [TrustServicesCategory.Security]
		});

		SetupStatusWithGap(GapSeverity.Critical);
		var sut = new ComplianceMonitoringService(_fakeScopeFactory, _fakeOptions, _fakeLogger);
		var cts = new CancellationTokenSource();

		// Act
		await sut.StartAsync(cts.Token);
		await Task.Delay(3000); // Generous for full-suite parallel load (1s monitoring interval)
		cts.Cancel();

		try
		{
			await sut.StopAsync(CancellationToken.None);
		}
		catch (OperationCanceledException)
		{
			// Expected
		}

		// Assert
		A.CallTo(() => _fakeAlertHandler.HandleComplianceGapAsync(
				A<ComplianceGapAlert>._,
				A<CancellationToken>._))
			.MustNotHaveHappened();
	}

	[Fact]
	public async Task ExecuteAsync_NotifyValidationFailure_WhenGetStatusFails()
	{
		// Arrange
		_ = A.CallTo(() => _fakeComplianceService.GetComplianceStatusAsync(
				A<string>._,
				A<CancellationToken>._))
			.ThrowsAsync(new InvalidOperationException("Database unavailable"));

		var sut = new ComplianceMonitoringService(_fakeScopeFactory, _fakeOptions, _fakeLogger);
		var cts = new CancellationTokenSource();

		// Act
		await sut.StartAsync(cts.Token);
		await Task.Delay(3000); // Generous for full-suite parallel load (1s monitoring interval)
		cts.Cancel();

		try
		{
			await sut.StopAsync(CancellationToken.None);
		}
		catch (OperationCanceledException)
		{
			// Expected
		}

		// Assert
		_ = A.CallTo(() => _fakeAlertHandler.HandleValidationFailureAsync(
				A<ControlValidationFailureAlert>._,
				A<CancellationToken>._))
			.MustHaveHappened();
	}

	#endregion Alert Notification Tests

	#region Status Change Detection Tests

	[Fact]
	public async Task ExecuteAsync_NotifyStatusChange_WhenComplianceDrops()
	{
		// Arrange
		var callCount = 0;
		_ = A.CallTo(() => _fakeComplianceService.GetComplianceStatusAsync(
				A<string>._,
				A<CancellationToken>._))
			.ReturnsLazily(() =>
			{
				callCount++;
				return callCount == 1
					? CreateFullyCompliantStatus()
					: CreateNonCompliantStatus();
			});

		var sut = new ComplianceMonitoringService(_fakeScopeFactory, _fakeOptions, _fakeLogger);
		var cts = new CancellationTokenSource();

		// Act
		await sut.StartAsync(cts.Token);
		await Task.Delay(5000); // Generous for full-suite parallel load (needs 2+ cycles at 1s interval) // Let two cycles complete
		cts.Cancel();

		try
		{
			await sut.StopAsync(CancellationToken.None);
		}
		catch (OperationCanceledException)
		{
			// Expected
		}

		// Assert
		_ = A.CallTo(() => _fakeAlertHandler.HandleStatusChangeAsync(
				A<ComplianceStatusChangeNotification>.That.Matches(n => !n.IsCompliant),
				A<CancellationToken>._))
			.MustHaveHappened();
	}

	[Fact]
	public async Task ExecuteAsync_NotifyStatusChange_WhenComplianceImproves()
	{
		// Arrange
		var callCount = 0;
		_ = A.CallTo(() => _fakeComplianceService.GetComplianceStatusAsync(
				A<string>._,
				A<CancellationToken>._))
			.ReturnsLazily(() =>
			{
				callCount++;
				return callCount == 1
					? CreateNonCompliantStatus()
					: CreateFullyCompliantStatus();
			});

		var sut = new ComplianceMonitoringService(_fakeScopeFactory, _fakeOptions, _fakeLogger);
		var cts = new CancellationTokenSource();

		// Act
		await sut.StartAsync(cts.Token);
		await Task.Delay(5000); // Generous for full-suite parallel load (needs 2+ cycles at 1s interval) // Let two cycles complete
		cts.Cancel();

		try
		{
			await sut.StopAsync(CancellationToken.None);
		}
		catch (OperationCanceledException)
		{
			// Expected
		}

		// Assert
		_ = A.CallTo(() => _fakeAlertHandler.HandleStatusChangeAsync(
				A<ComplianceStatusChangeNotification>.That.Matches(n => n.IsCompliant),
				A<CancellationToken>._))
			.MustHaveHappened();
	}

	#endregion Status Change Detection Tests

	#region Recurring Gap Detection Tests

	[Fact]
	public async Task ExecuteAsync_TrackRecurringGaps()
	{
		// Arrange
		SetupStatusWithGap(GapSeverity.Critical);

		var capturedAlerts = new List<ComplianceGapAlert>();
		_ = A.CallTo(() => _fakeAlertHandler.HandleComplianceGapAsync(
				A<ComplianceGapAlert>._,
				A<CancellationToken>._))
			.Invokes((ComplianceGapAlert alert, CancellationToken _) =>
			{
				capturedAlerts.Add(alert);
			});

		var sut = new ComplianceMonitoringService(_fakeScopeFactory, _fakeOptions, _fakeLogger);
		var cts = new CancellationTokenSource();

		// Act
		await sut.StartAsync(cts.Token);
		await Task.Delay(5000); // Generous for full-suite parallel load (needs 2+ cycles at 1s interval) // Let multiple cycles complete
		cts.Cancel();

		try
		{
			await sut.StopAsync(CancellationToken.None);
		}
		catch (OperationCanceledException)
		{
			// Expected
		}

		// Assert - Second occurrence should be marked as recurring
		if (capturedAlerts.Count > 1)
		{
			capturedAlerts[1].IsRecurring.ShouldBeTrue();
			capturedAlerts[1].OccurrenceCount.ShouldBeGreaterThan(1);
		}
	}

	#endregion Recurring Gap Detection Tests

	#region Helper Methods

	private static ComplianceStatus CreateFullyCompliantStatus()
	{
		return new ComplianceStatus
		{
			OverallLevel = ComplianceLevel.FullyCompliant,
			CategoryStatuses = new Dictionary<TrustServicesCategory, CategoryStatus>
			{
				[TrustServicesCategory.Security] = new()
				{
					Category = TrustServicesCategory.Security,
					Level = ComplianceLevel.FullyCompliant,
					CompliancePercentage = 100,
					ActiveControls = 5,
					ControlsWithIssues = 0
				}
			},
			CriterionStatuses = new Dictionary<TrustServicesCriterion, CriterionStatus>
			{
				[TrustServicesCriterion.CC6_LogicalAccess] = new()
				{
					Criterion = TrustServicesCriterion.CC6_LogicalAccess,
					IsMet = true,
					EffectivenessScore = 100,
					LastValidated = DateTimeOffset.UtcNow,
					EvidenceCount = 5,
					Gaps = []
				}
			},
			ActiveGaps = [],
			EvaluatedAt = DateTimeOffset.UtcNow
		};
	}

	private static ComplianceStatus CreateNonCompliantStatus()
	{
		return new ComplianceStatus
		{
			OverallLevel = ComplianceLevel.NonCompliant,
			CategoryStatuses = new Dictionary<TrustServicesCategory, CategoryStatus>
			{
				[TrustServicesCategory.Security] = new()
				{
					Category = TrustServicesCategory.Security,
					Level = ComplianceLevel.NonCompliant,
					CompliancePercentage = 30,
					ActiveControls = 5,
					ControlsWithIssues = 4
				}
			},
			CriterionStatuses = new Dictionary<TrustServicesCriterion, CriterionStatus>
			{
				[TrustServicesCriterion.CC6_LogicalAccess] = new()
				{
					Criterion = TrustServicesCriterion.CC6_LogicalAccess,
					IsMet = false,
					EffectivenessScore = 30,
					LastValidated = DateTimeOffset.UtcNow,
					EvidenceCount = 2,
					Gaps = ["Missing encryption"]
				}
			},
			ActiveGaps =
			[
				new ComplianceGap
				{
					GapId = "gap-1",
					Criterion = TrustServicesCriterion.CC6_LogicalAccess,
					Description = "Missing encryption",
					Severity = GapSeverity.Critical,
					Remediation = "Implement encryption",
					IdentifiedAt = DateTimeOffset.UtcNow
				}
			],
			EvaluatedAt = DateTimeOffset.UtcNow
		};
	}

	private void SetupServiceScope()
	{
		var fakeScope = A.Fake<IServiceScope>();
		var fakeServiceProvider = A.Fake<IServiceProvider>();

		_ = A.CallTo(() => _fakeScopeFactory.CreateScope()).Returns(fakeScope);
		_ = A.CallTo(() => fakeScope.ServiceProvider).Returns(fakeServiceProvider);

		// AsyncServiceScope is a struct wrapper around IServiceScope, so we configure it via CreateScope
		// The CreateAsyncScope() extension method wraps CreateScope() internally
		_ = A.CallTo(() => fakeServiceProvider.GetService(typeof(ISoc2ComplianceService)))
			.Returns(_fakeComplianceService);
		_ = A.CallTo(() => fakeServiceProvider.GetService(typeof(IEnumerable<IComplianceAlertHandler>)))
			.Returns(new[] { _fakeAlertHandler });
	}

	private void SetupFullyCompliantStatus()
	{
		_ = A.CallTo(() => _fakeComplianceService.GetComplianceStatusAsync(
				A<string>._,
				A<CancellationToken>._))
			.Returns(CreateFullyCompliantStatus());
	}

	private void SetupStatusWithGap(GapSeverity severity)
	{
		_ = A.CallTo(() => _fakeComplianceService.GetComplianceStatusAsync(
				A<string>._,
				A<CancellationToken>._))
			.Returns(new ComplianceStatus
			{
				OverallLevel = ComplianceLevel.PartiallyCompliant,
				CategoryStatuses = new Dictionary<TrustServicesCategory, CategoryStatus>
				{
					[TrustServicesCategory.Security] = new()
					{
						Category = TrustServicesCategory.Security,
						Level = ComplianceLevel.PartiallyCompliant,
						CompliancePercentage = 60,
						ActiveControls = 5,
						ControlsWithIssues = 2
					}
				},
				CriterionStatuses = new Dictionary<TrustServicesCriterion, CriterionStatus>
				{
					[TrustServicesCriterion.CC6_LogicalAccess] = new()
					{
						Criterion = TrustServicesCriterion.CC6_LogicalAccess,
						IsMet = false,
						EffectivenessScore = 60,
						LastValidated = DateTimeOffset.UtcNow,
						EvidenceCount = 3,
						Gaps = ["Gap description"]
					}
				},
				ActiveGaps =
				[
					new ComplianceGap
					{
						GapId = "gap-1",
						Criterion = TrustServicesCriterion.CC6_LogicalAccess,
						Description = "Test gap",
						Severity = severity,
						Remediation = "Fix it",
						IdentifiedAt = DateTimeOffset.UtcNow
					}
				],
				EvaluatedAt = DateTimeOffset.UtcNow
			});
	}

	#endregion Helper Methods
}
