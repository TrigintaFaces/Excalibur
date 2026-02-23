using Excalibur.Dispatch.Compliance;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Compliance.Tests.Soc2;

[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class ComplianceMonitoringServiceShould
{
	[Fact]
	public void Throw_for_null_scope_factory()
	{
		Should.Throw<ArgumentNullException>(() =>
			new ComplianceMonitoringService(
				null!,
				Microsoft.Extensions.Options.Options.Create(new Soc2Options()),
				NullLogger<ComplianceMonitoringService>.Instance));
	}

	[Fact]
	public void Throw_for_null_options()
	{
		var scopeFactory = A.Fake<IServiceScopeFactory>();

		Should.Throw<ArgumentNullException>(() =>
			new ComplianceMonitoringService(
				scopeFactory,
				null!,
				NullLogger<ComplianceMonitoringService>.Instance));
	}

	[Fact]
	public void Throw_for_null_logger()
	{
		var scopeFactory = A.Fake<IServiceScopeFactory>();

		Should.Throw<ArgumentNullException>(() =>
			new ComplianceMonitoringService(
				scopeFactory,
				Microsoft.Extensions.Options.Options.Create(new Soc2Options()),
				null!));
	}

	[Fact]
	public async Task Exit_immediately_when_disabled()
	{
		var scopeFactory = A.Fake<IServiceScopeFactory>();
		var options = new Soc2Options { EnableContinuousMonitoring = false };
		var sut = new ComplianceMonitoringService(
			scopeFactory,
			Microsoft.Extensions.Options.Options.Create(options),
			NullLogger<ComplianceMonitoringService>.Instance);

		using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
		await sut.StartAsync(cts.Token).ConfigureAwait(false);
		await sut.StopAsync(CancellationToken.None).ConfigureAwait(false);

		// No scope should be created when monitoring is disabled
		A.CallTo(() => scopeFactory.CreateScope()).MustNotHaveHappened();
	}

	[Fact]
	public async Task Run_monitoring_cycle()
	{
		var complianceService = A.Fake<ISoc2ComplianceService>();
		var cycleObserved = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		A.CallTo(() => complianceService.GetComplianceStatusAsync(A<string?>._, A<CancellationToken>._))
			.ReturnsLazily(() =>
			{
				cycleObserved.TrySetResult();
				return Task.FromResult(CreateCompliantStatus());
			});

		var (scopeFactory, _) = SetupScopeFactory(complianceService);

		var options = new Soc2Options
		{
			EnableContinuousMonitoring = true,
			MonitoringInterval = TimeSpan.FromMilliseconds(50),
			EnableAlerts = false
		};

		var sut = new ComplianceMonitoringService(
			scopeFactory,
			Microsoft.Extensions.Options.Options.Create(options),
			NullLogger<ComplianceMonitoringService>.Instance);

		using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
		await sut.StartAsync(cts.Token).ConfigureAwait(false);

		var completed = await Task.WhenAny(
			cycleObserved.Task,
			Task.Delay(TimeSpan.FromSeconds(2), CancellationToken.None)).ConfigureAwait(false);

		await cts.CancelAsync().ConfigureAwait(false);
		await sut.StopAsync(CancellationToken.None).ConfigureAwait(false);

		completed.ShouldBe(cycleObserved.Task);
		A.CallTo(() => complianceService.GetComplianceStatusAsync(A<string?>._, A<CancellationToken>._))
			.MustHaveHappened();
	}

	[Fact]
	public async Task Continue_after_status_fetch_error()
	{
		var complianceService = A.Fake<ISoc2ComplianceService>();
		var callCount = 0;
		var secondCycleObserved = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		A.CallTo(() => complianceService.GetComplianceStatusAsync(A<string?>._, A<CancellationToken>._))
			.ReturnsLazily(() =>
			{
				var currentCall = Interlocked.Increment(ref callCount);
				if (currentCall == 1)
				{
					throw new InvalidOperationException("First cycle fails");
				}

				secondCycleObserved.TrySetResult();
				return Task.FromResult(CreateCompliantStatus());
			});

		var (scopeFactory, _) = SetupScopeFactory(complianceService);

		var options = new Soc2Options
		{
			EnableContinuousMonitoring = true,
			MonitoringInterval = TimeSpan.FromMilliseconds(50),
			EnableAlerts = false
		};

		var sut = new ComplianceMonitoringService(
			scopeFactory,
			Microsoft.Extensions.Options.Options.Create(options),
			NullLogger<ComplianceMonitoringService>.Instance);

		using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
		await sut.StartAsync(cts.Token).ConfigureAwait(false);

		var completed = await Task.WhenAny(
			secondCycleObserved.Task,
			Task.Delay(TimeSpan.FromSeconds(2), CancellationToken.None)).ConfigureAwait(false);

		await cts.CancelAsync().ConfigureAwait(false);
		await sut.StopAsync(CancellationToken.None).ConfigureAwait(false);

		completed.ShouldBe(secondCycleObserved.Task);
		callCount.ShouldBeGreaterThanOrEqualTo(2);
	}

	[Fact]
	public async Task Process_compliance_gaps_with_alerts_enabled()
	{
		var complianceService = A.Fake<ISoc2ComplianceService>();
		var alertHandler = A.Fake<IComplianceAlertHandler>();
		var alertObserved = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

		A.CallTo(() => complianceService.GetComplianceStatusAsync(A<string?>._, A<CancellationToken>._))
			.Returns(CreateStatusWithGaps());

		A.CallTo(() => alertHandler.HandleComplianceGapAsync(A<ComplianceGapAlert>._, A<CancellationToken>._))
			.ReturnsLazily(() =>
			{
				alertObserved.TrySetResult();
				return Task.CompletedTask;
			});

		var (scopeFactory, _) = SetupScopeFactory(complianceService, alertHandler);

		var options = new Soc2Options
		{
			EnableContinuousMonitoring = true,
			MonitoringInterval = TimeSpan.FromMilliseconds(50),
			EnableAlerts = true,
			AlertThreshold = GapSeverity.Low
		};

		var sut = new ComplianceMonitoringService(
			scopeFactory,
			Microsoft.Extensions.Options.Options.Create(options),
			NullLogger<ComplianceMonitoringService>.Instance);

		using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
		await sut.StartAsync(cts.Token).ConfigureAwait(false);

		var completed = await Task.WhenAny(
			alertObserved.Task,
			Task.Delay(TimeSpan.FromSeconds(2), CancellationToken.None)).ConfigureAwait(false);

		await cts.CancelAsync().ConfigureAwait(false);
		await sut.StopAsync(CancellationToken.None).ConfigureAwait(false);

		completed.ShouldBe(alertObserved.Task);
		A.CallTo(() => alertHandler.HandleComplianceGapAsync(
				A<ComplianceGapAlert>._, A<CancellationToken>._))
			.MustHaveHappened();
	}

	[Fact]
	public async Task Skip_gap_alerts_below_threshold()
	{
		var complianceService = A.Fake<ISoc2ComplianceService>();
		var alertHandler = A.Fake<IComplianceAlertHandler>();
		var cycleObserved = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

		A.CallTo(() => complianceService.GetComplianceStatusAsync(A<string?>._, A<CancellationToken>._))
			.ReturnsLazily(() =>
			{
				cycleObserved.TrySetResult();
				return Task.FromResult(CreateStatusWithLowSeverityGap());
			});

		var (scopeFactory, _) = SetupScopeFactory(complianceService, alertHandler);

		var options = new Soc2Options
		{
			EnableContinuousMonitoring = true,
			MonitoringInterval = TimeSpan.FromMilliseconds(50),
			EnableAlerts = true,
			AlertThreshold = GapSeverity.High // High threshold, gap is Low
		};

		var sut = new ComplianceMonitoringService(
			scopeFactory,
			Microsoft.Extensions.Options.Options.Create(options),
			NullLogger<ComplianceMonitoringService>.Instance);

		using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
		await sut.StartAsync(cts.Token).ConfigureAwait(false);

		var completed = await Task.WhenAny(
			cycleObserved.Task,
			Task.Delay(TimeSpan.FromSeconds(2), CancellationToken.None)).ConfigureAwait(false);

		await cts.CancelAsync().ConfigureAwait(false);
		await sut.StopAsync(CancellationToken.None).ConfigureAwait(false);

		completed.ShouldBe(cycleObserved.Task);
		// Gap alert below threshold should not be sent
		A.CallTo(() => alertHandler.HandleComplianceGapAsync(
				A<ComplianceGapAlert>._, A<CancellationToken>._))
			.MustNotHaveHappened();
	}

	[Fact]
	public void Have_default_options()
	{
		var options = new Soc2Options();

		options.EnableContinuousMonitoring.ShouldBeTrue();
		options.MonitoringInterval.ShouldBe(TimeSpan.FromHours(1));
		options.EnableAlerts.ShouldBeTrue();
		options.AlertThreshold.ShouldBe(GapSeverity.Medium);
		options.EvidenceRetentionPeriod.ShouldBe(TimeSpan.FromDays(365 * 7));
		options.DefaultTestSampleSize.ShouldBe(25);
		options.MinimumTypeIIPeriodDays.ShouldBe(90);
		options.IncludeSubServiceOrganizations.ShouldBeFalse();
		options.EnabledCategories.ShouldHaveSingleItem();
		options.EnabledCategories[0].ShouldBe(TrustServicesCategory.Security);
	}

	private static ComplianceStatus CreateCompliantStatus() =>
		new()
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
					EvidenceCount = 5
				}
			},
			ActiveGaps = []
		};

	private static ComplianceStatus CreateStatusWithGaps() =>
		new()
		{
			OverallLevel = ComplianceLevel.PartiallyCompliant,
			CategoryStatuses = new Dictionary<TrustServicesCategory, CategoryStatus>
			{
				[TrustServicesCategory.Security] = new()
				{
					Category = TrustServicesCategory.Security,
					Level = ComplianceLevel.PartiallyCompliant,
					CompliancePercentage = 50,
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
					EffectivenessScore = 50,
					LastValidated = DateTimeOffset.UtcNow,
					EvidenceCount = 3
				}
			},
			ActiveGaps =
			[
				new ComplianceGap
				{
					GapId = "GAP-001",
					Criterion = TrustServicesCriterion.CC6_LogicalAccess,
					Description = "Encryption not configured",
					Severity = GapSeverity.High,
					Remediation = "Configure encryption provider",
					IdentifiedAt = DateTimeOffset.UtcNow
				}
			]
		};

	private static ComplianceStatus CreateStatusWithLowSeverityGap() =>
		new()
		{
			OverallLevel = ComplianceLevel.SubstantiallyCompliant,
			CategoryStatuses = new Dictionary<TrustServicesCategory, CategoryStatus>
			{
				[TrustServicesCategory.Security] = new()
				{
					Category = TrustServicesCategory.Security,
					Level = ComplianceLevel.SubstantiallyCompliant,
					CompliancePercentage = 90,
					ActiveControls = 5,
					ControlsWithIssues = 1
				}
			},
			CriterionStatuses = new Dictionary<TrustServicesCriterion, CriterionStatus>
			{
				[TrustServicesCriterion.CC6_LogicalAccess] = new()
				{
					Criterion = TrustServicesCriterion.CC6_LogicalAccess,
					IsMet = true,
					EffectivenessScore = 90,
					LastValidated = DateTimeOffset.UtcNow,
					EvidenceCount = 5
				}
			},
			ActiveGaps =
			[
				new ComplianceGap
				{
					GapId = "GAP-002",
					Criterion = TrustServicesCriterion.CC6_LogicalAccess,
					Description = "Minor documentation gap",
					Severity = GapSeverity.Low,
					Remediation = "Update documentation",
					IdentifiedAt = DateTimeOffset.UtcNow
				}
			]
		};

	private static (IServiceScopeFactory, IServiceScope) SetupScopeFactory(
		ISoc2ComplianceService complianceService,
		IComplianceAlertHandler? alertHandler = null)
	{
		var serviceProvider = A.Fake<IServiceProvider>();
		A.CallTo(() => serviceProvider.GetService(typeof(ISoc2ComplianceService)))
			.Returns(complianceService);

		if (alertHandler != null)
		{
			A.CallTo(() => serviceProvider.GetService(typeof(IEnumerable<IComplianceAlertHandler>)))
				.Returns(new[] { alertHandler });
		}
		else
		{
			A.CallTo(() => serviceProvider.GetService(typeof(IEnumerable<IComplianceAlertHandler>)))
				.Returns(Array.Empty<IComplianceAlertHandler>());
		}

		// CreateAsyncScope() is an extension method that calls CreateScope() internally,
		// so faking CreateScope() is sufficient.
		var scope = A.Fake<IServiceScope>();
		A.CallTo(() => scope.ServiceProvider).Returns(serviceProvider);

		var scopeFactory = A.Fake<IServiceScopeFactory>();
		A.CallTo(() => scopeFactory.CreateScope()).Returns(scope);

		return (scopeFactory, scope);
	}
}
