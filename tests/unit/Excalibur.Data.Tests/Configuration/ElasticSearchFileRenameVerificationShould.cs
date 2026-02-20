// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Reflection;
using Excalibur.Data.ElasticSearch;
using Excalibur.Data.ElasticSearch.IndexManagement;
using Excalibur.Data.ElasticSearch.Monitoring;

namespace Excalibur.Data.Tests.Configuration;

/// <summary>
/// Verifies that ElasticSearch *Settings files were correctly renamed to *Options
/// and that no stale *Settings type references remain.
/// Sprint 564 S564.60: ElasticSearch file rename verification.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class ElasticSearchFileRenameVerificationShould
{
	private static readonly Assembly ElasticSearchAssembly = typeof(ProjectionOptions).Assembly;

	[Fact]
	public void HaveRenamedCircuitBreakerSettingsToOptions()
	{
		var type = ElasticSearchAssembly.GetType("Excalibur.Data.ElasticSearch.CircuitBreakerOptions");
		type.ShouldNotBeNull("CircuitBreakerOptions should exist after rename from CircuitBreakerSettings");
	}

	[Fact]
	public void HaveRenamedConsistencyTrackingSettingsToOptions()
	{
		var type = ElasticSearchAssembly.GetType("Excalibur.Data.ElasticSearch.ConsistencyTrackingOptions");
		type.ShouldNotBeNull("ConsistencyTrackingOptions should exist after rename from ConsistencyTrackingSettings");
	}

	[Fact]
	public void HaveRenamedTimeoutSettingsToOptions()
	{
		var type = ElasticSearchAssembly.GetType("Excalibur.Data.ElasticSearch.TimeoutOptions");
		type.ShouldNotBeNull("TimeoutOptions should exist after rename from TimeoutSettings");
	}

	[Fact]
	public void HaveRenamedEnvironmentSettingsToOptions()
	{
		var type = ElasticSearchAssembly.GetType("Excalibur.Data.ElasticSearch.IndexManagement.EnvironmentOptions");
		type.ShouldNotBeNull("EnvironmentOptions should exist after rename from EnvironmentSettings");
	}

	[Fact]
	public void HaveRenamedLifecycleManagementSettingsToOptions()
	{
		var type = ElasticSearchAssembly.GetType("Excalibur.Data.ElasticSearch.IndexManagement.LifecycleManagementOptions");
		type.ShouldNotBeNull("LifecycleManagementOptions should exist after rename from LifecycleManagementSettings");
	}

	[Fact]
	public void HaveRenamedOptimizationSettingsToOptions()
	{
		var type = ElasticSearchAssembly.GetType("Excalibur.Data.ElasticSearch.IndexManagement.OptimizationOptions");
		type.ShouldNotBeNull("OptimizationOptions should exist after rename from OptimizationSettings");
	}

	[Fact]
	public void HaveRenamedTracingSettingsToOptions()
	{
		var type = ElasticSearchAssembly.GetType("Excalibur.Data.ElasticSearch.Monitoring.TracingOptions");
		type.ShouldNotBeNull("TracingOptions should exist after rename from TracingSettings");
	}

	[Fact]
	public void HaveRenamedHealthMonitoringSettingsToOptions()
	{
		var type = ElasticSearchAssembly.GetType("Excalibur.Data.ElasticSearch.Monitoring.HealthMonitoringOptions");
		type.ShouldNotBeNull("HealthMonitoringOptions should exist after rename from HealthMonitoringSettings");
	}

	[Fact]
	public void HaveRenamedMetricsSettingsToOptions()
	{
		var type = ElasticSearchAssembly.GetType("Excalibur.Data.ElasticSearch.Monitoring.MetricsOptions");
		type.ShouldNotBeNull("MetricsOptions should exist after rename from MetricsSettings");
	}

	[Fact]
	public void NotContainAnyStaleSettingsTypes()
	{
		var staleSettingsNames = new[]
		{
			"CircuitBreakerSettings",
			"ConsistencyTrackingSettings",
			"TimeoutSettings",
			"EnvironmentSettings",
			"LifecycleManagementSettings",
			"OptimizationSettings",
			"TracingSettings",
			"HealthMonitoringSettings",
			"MetricsSettings",
		};

		var allTypes = ElasticSearchAssembly.GetTypes();
		foreach (var staleName in staleSettingsNames)
		{
			var staleType = allTypes.FirstOrDefault(t => t.Name == staleName);
			staleType.ShouldBeNull($"Stale type '{staleName}' should not exist after rename to *Options");
		}
	}

	[Fact]
	public void BeInstantiable_CircuitBreakerOptions()
	{
		var options = new CircuitBreakerOptions();
		options.ShouldNotBeNull();
	}

	[Fact]
	public void BeInstantiable_EnvironmentOptions()
	{
		var options = new EnvironmentOptions();
		options.ShouldNotBeNull();
	}

	[Fact]
	public void BeInstantiable_HealthMonitoringOptions()
	{
		var options = new HealthMonitoringOptions();
		options.ShouldNotBeNull();
	}

	[Fact]
	public void BeInstantiable_MetricsOptions()
	{
		var options = new MetricsOptions();
		options.ShouldNotBeNull();
	}

	[Fact]
	public void BeInstantiable_TracingOptions()
	{
		// Note: Using fully-qualified name to avoid collision with Dispatch TracingOptions
		var options = new TracingOptions();
		options.ShouldNotBeNull();
	}
}
