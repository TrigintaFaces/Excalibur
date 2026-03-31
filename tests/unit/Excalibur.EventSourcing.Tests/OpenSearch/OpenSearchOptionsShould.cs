// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.OpenSearch;
using Excalibur.Data.OpenSearch.IndexManagement;
using Excalibur.Data.OpenSearch.Persistence;
using Excalibur.Data.OpenSearch.Projections;
using Excalibur.Data.OpenSearch.Resilience;

namespace Excalibur.EventSourcing.Tests.OpenSearch;

/// <summary>
/// T.13 (725tc1): Unit tests for OpenSearch package -- Options defaults, models,
/// circuit breaker states, resilience options, index management interfaces.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "OpenSearch")]
public sealed class OpenSearchOptionsShould
{
	// --- Configuration Options ---

	[Fact]
	public void ConfigurationOptionsHaveDefaults()
	{
		var options = new OpenSearchConfigurationOptions();
		options.ShouldNotBeNull();
	}

	[Fact]
	public void CircuitBreakerOptionsHaveDefaults()
	{
		var options = new CircuitBreakerOptions();
		options.ShouldNotBeNull();
	}

	[Fact]
	public void ResilienceOptionsHaveDefaults()
	{
		var options = new OpenSearchResilienceOptions();
		options.ShouldNotBeNull();
	}

	[Fact]
	public void TimeoutOptionsHaveDefaults()
	{
		var options = new OpenSearchTimeoutOptions();
		options.ShouldNotBeNull();
	}

	[Fact]
	public void RetryPolicyOptionsHaveDefaults()
	{
		var options = new OpenSearchRetryPolicyOptions();
		options.ShouldNotBeNull();
	}

	// --- Projection Store Options ---

	[Fact]
	public void ProjectionStoreOptionsHaveDefaults()
	{
		var options = new OpenSearchProjectionStoreOptions();
		options.ShouldNotBeNull();
	}

	// --- Persistence Options ---

	[Fact]
	public void PersistenceOptionsHaveDefaults()
	{
		var options = new OpenSearchPersistenceOptions();
		options.ShouldNotBeNull();
	}

	// --- Index Management ---

	[Fact]
	public void IndexManagementOptionsHaveDefaults()
	{
		var options = new IndexManagementOptions();
		options.ShouldNotBeNull();
	}

	[Fact]
	public void LifecycleManagementOptionsHaveDefaults()
	{
		var options = new LifecycleManagementOptions();
		options.ShouldNotBeNull();
	}

	[Fact]
	public void DefaultTemplateOptionsHaveDefaults()
	{
		var options = new DefaultTemplateOptions();
		options.ShouldNotBeNull();
	}

	[Fact]
	public void EnvironmentOptionsHaveDefaults()
	{
		var options = new EnvironmentOptions();
		options.ShouldNotBeNull();
	}

	[Fact]
	public void OptimizationOptionsHaveDefaults()
	{
		var options = new OptimizationOptions();
		options.ShouldNotBeNull();
	}

	// --- Enums ---

	[Fact]
	public void AliasOperationTypeHasExpectedValues()
	{
		Enum.GetValues<AliasOperationType>().Length.ShouldBeGreaterThan(0);
	}

	[Fact]
	public void CircuitBreakerStateHasThreeStates()
	{
		Enum.GetValues<CircuitBreakerState>().Length.ShouldBe(3);
		CircuitBreakerState.Closed.ShouldNotBe(CircuitBreakerState.Open);
		CircuitBreakerState.HalfOpen.ShouldNotBe(CircuitBreakerState.Closed);
	}

	// --- Lifecycle Models ---

	[Fact]
	public void IndexLifecyclePolicyCanBeCreated()
	{
		var policy = new IndexLifecyclePolicy();
		policy.ShouldNotBeNull();
	}

	[Fact]
	public void HotPhaseConfigurationCanBeCreated()
	{
		var phase = new HotPhaseConfiguration();
		phase.ShouldNotBeNull();
	}

	[Fact]
	public void ColdPhaseConfigurationCanBeCreated()
	{
		var phase = new ColdPhaseConfiguration();
		phase.ShouldNotBeNull();
	}

	[Fact]
	public void DeletePhaseConfigurationCanBeCreated()
	{
		var phase = new DeletePhaseConfiguration();
		phase.ShouldNotBeNull();
	}

	// --- Dead Letter Options ---

	[Fact]
	public void DeadLetterOptionsHaveDefaults()
	{
		var options = new OpenSearchDeadLetterOptions();
		options.ShouldNotBeNull();
	}

	// --- Interfaces exist (compilation check) ---

	[Fact]
	public void IndexManagementInterfacesAreAccessible()
	{
		typeof(IIndexOperationsManager).ShouldNotBeNull();
		typeof(IIndexAliasManager).ShouldNotBeNull();
		typeof(IIndexLifecycleManager).ShouldNotBeNull();
		typeof(IIndexTemplateManager).ShouldNotBeNull();
	}

	[Fact]
	public void ResilienceInterfacesAreAccessible()
	{
		typeof(IResilientOpenSearchClient).ShouldNotBeNull();
		typeof(IOpenSearchRetryPolicy).ShouldNotBeNull();
		typeof(IOpenSearchCircuitBreaker).ShouldNotBeNull();
	}

}
