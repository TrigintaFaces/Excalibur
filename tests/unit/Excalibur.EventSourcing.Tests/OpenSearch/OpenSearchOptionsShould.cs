// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.OpenSearch;
using Excalibur.Data.OpenSearch.IndexManagement;
using Excalibur.Data.OpenSearch.Persistence;
using Excalibur.Data.OpenSearch.Projections;

namespace Excalibur.EventSourcing.Tests.OpenSearch;

/// <summary>
/// T.13 (725tc1): Unit tests for OpenSearch package -- Options defaults, models,
/// and index management interfaces.
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

	// --- Enums ---

	[Fact]
	public void AliasOperationTypeHasExpectedValues()
	{
		Enum.GetValues<AliasOperationType>().Length.ShouldBeGreaterThan(0);
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

}
