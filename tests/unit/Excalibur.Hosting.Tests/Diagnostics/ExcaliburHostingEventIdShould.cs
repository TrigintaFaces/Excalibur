// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Hosting.Diagnostics;

namespace Excalibur.Hosting.Tests.Diagnostics;

/// <summary>
/// Unit tests for <see cref="ExcaliburHostingEventId"/> constants.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Hosting")]
[Trait("Feature", "Diagnostics")]
public sealed class ExcaliburHostingEventIdShould : UnitTestBase
{
	// ========================================
	// Hosting Core (160000-160099)
	// ========================================

	[Fact]
	public void HaveHostBuilderCreatedAt160000()
	{
		// Assert
		ExcaliburHostingEventId.HostBuilderCreated.ShouldBe(160000);
	}

	[Fact]
	public void HaveHostStartingAt160001()
	{
		// Assert
		ExcaliburHostingEventId.HostStarting.ShouldBe(160001);
	}

	[Fact]
	public void HaveHostStartedAt160002()
	{
		// Assert
		ExcaliburHostingEventId.HostStarted.ShouldBe(160002);
	}

	[Fact]
	public void HaveHostStoppingAt160003()
	{
		// Assert
		ExcaliburHostingEventId.HostStopping.ShouldBe(160003);
	}

	[Fact]
	public void HaveHostStoppedAt160004()
	{
		// Assert
		ExcaliburHostingEventId.HostStopped.ShouldBe(160004);
	}

	[Fact]
	public void HaveHostErrorAt160005()
	{
		// Assert
		ExcaliburHostingEventId.HostError.ShouldBe(160005);
	}

	// ========================================
	// Service Registration (160100-160199)
	// ========================================

	[Fact]
	public void HaveServicesConfiguredAt160100()
	{
		// Assert
		ExcaliburHostingEventId.ServicesConfigured.ShouldBe(160100);
	}

	[Fact]
	public void HaveServiceRegisteredAt160101()
	{
		// Assert
		ExcaliburHostingEventId.ServiceRegistered.ShouldBe(160101);
	}

	[Fact]
	public void HaveServiceResolvedAt160102()
	{
		// Assert
		ExcaliburHostingEventId.ServiceResolved.ShouldBe(160102);
	}

	[Fact]
	public void HaveServiceDisposedAt160103()
	{
		// Assert
		ExcaliburHostingEventId.ServiceDisposed.ShouldBe(160103);
	}

	// ========================================
	// Worker Services (160500-160599)
	// ========================================

	[Fact]
	public void HaveWorkerServiceCreatedAt160500()
	{
		// Assert
		ExcaliburHostingEventId.WorkerServiceCreated.ShouldBe(160500);
	}

	[Fact]
	public void HaveWorkerServiceStartedAt160501()
	{
		// Assert
		ExcaliburHostingEventId.WorkerServiceStarted.ShouldBe(160501);
	}

	[Fact]
	public void HaveWorkerServiceStoppedAt160502()
	{
		// Assert
		ExcaliburHostingEventId.WorkerServiceStopped.ShouldBe(160502);
	}

	[Fact]
	public void HaveWorkerIterationCompletedAt160503()
	{
		// Assert
		ExcaliburHostingEventId.WorkerIterationCompleted.ShouldBe(160503);
	}

	[Fact]
	public void HaveWorkerErrorAt160504()
	{
		// Assert
		ExcaliburHostingEventId.WorkerError.ShouldBe(160504);
	}

	// ========================================
	// Background Tasks (160600-160699)
	// ========================================

	[Fact]
	public void HaveBackgroundTaskStartedAt160600()
	{
		// Assert
		ExcaliburHostingEventId.BackgroundTaskStarted.ShouldBe(160600);
	}

	[Fact]
	public void HaveBackgroundTaskCompletedAt160601()
	{
		// Assert
		ExcaliburHostingEventId.BackgroundTaskCompleted.ShouldBe(160601);
	}

	[Fact]
	public void HaveBackgroundTaskFailedAt160602()
	{
		// Assert
		ExcaliburHostingEventId.BackgroundTaskFailed.ShouldBe(160602);
	}

	[Fact]
	public void HaveBackgroundTaskCancelledAt160603()
	{
		// Assert
		ExcaliburHostingEventId.BackgroundTaskCancelled.ShouldBe(160603);
	}

	// ========================================
	// Health Checks Core (161000-161099)
	// ========================================

	[Fact]
	public void HaveHealthCheckServiceCreatedAt161000()
	{
		// Assert
		ExcaliburHostingEventId.HealthCheckServiceCreated.ShouldBe(161000);
	}

	[Fact]
	public void HaveHealthCheckExecutedAt161001()
	{
		// Assert
		ExcaliburHostingEventId.HealthCheckExecuted.ShouldBe(161001);
	}

	[Fact]
	public void HaveHealthCheckPassedAt161002()
	{
		// Assert
		ExcaliburHostingEventId.HealthCheckPassed.ShouldBe(161002);
	}

	[Fact]
	public void HaveHealthCheckFailedAt161003()
	{
		// Assert
		ExcaliburHostingEventId.HealthCheckFailed.ShouldBe(161003);
	}

	[Fact]
	public void HaveHealthStatusChangedAt161004()
	{
		// Assert
		ExcaliburHostingEventId.HealthStatusChanged.ShouldBe(161004);
	}

	// ========================================
	// Health Check Types (161100-161199)
	// ========================================

	[Fact]
	public void HaveLivenessCheckExecutedAt161100()
	{
		// Assert
		ExcaliburHostingEventId.LivenessCheckExecuted.ShouldBe(161100);
	}

	[Fact]
	public void HaveReadinessCheckExecutedAt161101()
	{
		// Assert
		ExcaliburHostingEventId.ReadinessCheckExecuted.ShouldBe(161101);
	}

	[Fact]
	public void HaveStartupCheckExecutedAt161102()
	{
		// Assert
		ExcaliburHostingEventId.StartupCheckExecuted.ShouldBe(161102);
	}

	[Fact]
	public void HaveDependencyCheckExecutedAt161103()
	{
		// Assert
		ExcaliburHostingEventId.DependencyCheckExecuted.ShouldBe(161103);
	}

	// ========================================
	// Configuration Core (161500-161599)
	// ========================================

	[Fact]
	public void HaveConfigurationLoadedAt161500()
	{
		// Assert
		ExcaliburHostingEventId.ConfigurationLoaded.ShouldBe(161500);
	}

	[Fact]
	public void HaveConfigurationValidatedAt161501()
	{
		// Assert
		ExcaliburHostingEventId.ConfigurationValidated.ShouldBe(161501);
	}

	[Fact]
	public void HaveConfigurationChangedAt161502()
	{
		// Assert
		ExcaliburHostingEventId.ConfigurationChanged.ShouldBe(161502);
	}

	[Fact]
	public void HaveConfigurationErrorAt161503()
	{
		// Assert
		ExcaliburHostingEventId.ConfigurationError.ShouldBe(161503);
	}

	[Fact]
	public void HaveEnvironmentDetectedAt161504()
	{
		// Assert
		ExcaliburHostingEventId.EnvironmentDetected.ShouldBe(161504);
	}

	// ========================================
	// Leader Election Core (162000-162099)
	// ========================================

	[Fact]
	public void HaveLeaderElectionServiceCreatedAt162000()
	{
		// Assert
		ExcaliburHostingEventId.LeaderElectionServiceCreated.ShouldBe(162000);
	}

	[Fact]
	public void HaveLeaderElectionStartedAt162001()
	{
		// Assert
		ExcaliburHostingEventId.LeaderElectionStarted.ShouldBe(162001);
	}

	[Fact]
	public void HaveLeadershipAcquiredAt162002()
	{
		// Assert
		ExcaliburHostingEventId.LeadershipAcquired.ShouldBe(162002);
	}

	[Fact]
	public void HaveLeadershipLostAt162003()
	{
		// Assert
		ExcaliburHostingEventId.LeadershipLost.ShouldBe(162003);
	}

	[Fact]
	public void HaveLeadershipRenewedAt162004()
	{
		// Assert
		ExcaliburHostingEventId.LeadershipRenewed.ShouldBe(162004);
	}

	// ========================================
	// Web Hosting (162500-162599)
	// ========================================

	[Fact]
	public void HaveWebHostCreatedAt162500()
	{
		// Assert
		ExcaliburHostingEventId.WebHostCreated.ShouldBe(162500);
	}

	[Fact]
	public void HaveWebHostStartedAt162501()
	{
		// Assert
		ExcaliburHostingEventId.WebHostStarted.ShouldBe(162501);
	}

	[Fact]
	public void HaveWebHostStoppedAt162502()
	{
		// Assert
		ExcaliburHostingEventId.WebHostStopped.ShouldBe(162502);
	}

	[Fact]
	public void HaveRequestPipelineConfiguredAt162503()
	{
		// Assert
		ExcaliburHostingEventId.RequestPipelineConfigured.ShouldBe(162503);
	}

	[Fact]
	public void HaveEndpointsMappedAt162504()
	{
		// Assert
		ExcaliburHostingEventId.EndpointsMapped.ShouldBe(162504);
	}

	// ========================================
	// Configuration Validation (161510-161549)
	// ========================================

	[Fact]
	public void HaveConfigValidationDisabledAt161510()
	{
		// Assert
		ExcaliburHostingEventId.ConfigValidationDisabled.ShouldBe(161510);
	}

	[Fact]
	public void HaveConfigValidationStartingAt161511()
	{
		// Assert
		ExcaliburHostingEventId.ConfigValidationStarting.ShouldBe(161511);
	}

	[Fact]
	public void HaveConfigValidatorRunningAt161512()
	{
		// Assert
		ExcaliburHostingEventId.ConfigValidatorRunning.ShouldBe(161512);
	}

	[Fact]
	public void HaveGlobalExceptionOccurredAt162510()
	{
		// Assert
		ExcaliburHostingEventId.GlobalExceptionOccurred.ShouldBe(162510);
	}

	// ========================================
	// Event ID Uniqueness
	// ========================================

	[Fact]
	public void HaveUniqueEventIds()
	{
		// Arrange
		var fieldInfos = typeof(ExcaliburHostingEventId)
			.GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
			.Where(f => f.FieldType == typeof(int));

		var eventIds = fieldInfos.Select(f => (int)f.GetValue(null)!).ToList();

		// Assert
		var distinctCount = eventIds.Distinct().Count();
		distinctCount.ShouldBe(eventIds.Count, "Some event IDs are duplicated");
	}

	[Fact]
	public void HaveEventIdsInHostingRange()
	{
		// Arrange
		var fieldInfos = typeof(ExcaliburHostingEventId)
			.GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
			.Where(f => f.FieldType == typeof(int));

		// Assert - all event IDs should be in the 160000-163000 range
		foreach (var field in fieldInfos)
		{
			var id = (int)field.GetValue(null)!;
			id.ShouldBeGreaterThanOrEqualTo(160000, $"Event ID {field.Name} ({id}) is below hosting range");
			id.ShouldBeLessThan(163000, $"Event ID {field.Name} ({id}) is above hosting range");
		}
	}
}
