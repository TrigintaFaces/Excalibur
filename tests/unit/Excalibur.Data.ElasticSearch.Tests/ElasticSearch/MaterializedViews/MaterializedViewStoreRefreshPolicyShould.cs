// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Reflection;

using Excalibur.Data.ElasticSearch.MaterializedViews;
using Excalibur.Data.OpenSearch.MaterializedViews;

using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Data.Tests.ElasticSearch.MaterializedViews;

/// <summary>
/// Regression locks for the materialized-view stores' configurable refresh policy.
/// Pre-fix, both stores hardcoded <c>Refresh.True</c> on every per-document write (a per-write
/// segment flush that throttles projection rebuilds). The policy is now configurable and defaults
/// to <c>wait_for</c> — these tests fail to compile/run against the pre-fix code (no RefreshPolicy
/// option, no GetRefresh seam) and assert the default is NOT Refresh.True.
/// </summary>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Platform)]
public sealed class MaterializedViewStoreRefreshPolicyShould
{
	private static Refresh InvokeElasticGetRefresh(string refreshPolicy)
	{
		var options = Options.Create(new ElasticSearchMaterializedViewStoreOptions { RefreshPolicy = refreshPolicy });
		var store = new ElasticSearchMaterializedViewStore(options, NullLogger<ElasticSearchMaterializedViewStore>.Instance);
		var method = typeof(ElasticSearchMaterializedViewStore)
			.GetMethod("GetRefresh", BindingFlags.Instance | BindingFlags.NonPublic)
			?? throw new MissingMethodException("ElasticSearchMaterializedViewStore.GetRefresh not found.");
		return (Refresh)method.Invoke(store, null)!;
	}

	private static global::OpenSearch.Net.Refresh InvokeOpenSearchGetRefresh(string refreshPolicy)
	{
		var options = Options.Create(new OpenSearchMaterializedViewStoreOptions { RefreshPolicy = refreshPolicy });
		var store = new OpenSearchMaterializedViewStore(options, NullLogger<OpenSearchMaterializedViewStore>.Instance);
		var method = typeof(OpenSearchMaterializedViewStore)
			.GetMethod("GetRefresh", BindingFlags.Instance | BindingFlags.NonPublic)
			?? throw new MissingMethodException("OpenSearchMaterializedViewStore.GetRefresh not found.");
		return (global::OpenSearch.Net.Refresh)method.Invoke(store, null)!;
	}

	[Fact]
	public void ElasticDefaultToWaitForNotTrue()
	{
		var defaultPolicy = new ElasticSearchMaterializedViewStoreOptions().RefreshPolicy;

		defaultPolicy.ShouldBe("wait_for");
		InvokeElasticGetRefresh(defaultPolicy).ShouldBe(Refresh.WaitFor);
		InvokeElasticGetRefresh(defaultPolicy).ShouldNotBe(Refresh.True);
	}

	[Theory]
	[InlineData("wait_for")]
	[InlineData("false")]
	[InlineData("anything-unrecognized")]
	public void ElasticNeverForceTrueRefreshUnlessExplicitlyRequested(string policy)
		=> InvokeElasticGetRefresh(policy).ShouldNotBe(Refresh.True);

	[Theory]
	[InlineData("true")]
	[InlineData("false")]
	[InlineData("wait_for")]
	public void ElasticMapRefreshPolicyToConfiguredValue(string policy)
	{
		var expected = policy switch
		{
			"true" => Refresh.True,
			"false" => Refresh.False,
			_ => Refresh.WaitFor,
		};

		InvokeElasticGetRefresh(policy).ShouldBe(expected);
	}

	[Fact]
	public void OpenSearchDefaultToWaitForNotTrue()
	{
		var defaultPolicy = new OpenSearchMaterializedViewStoreOptions().RefreshPolicy;

		defaultPolicy.ShouldBe("wait_for");
		InvokeOpenSearchGetRefresh(defaultPolicy).ShouldBe(global::OpenSearch.Net.Refresh.WaitFor);
		InvokeOpenSearchGetRefresh(defaultPolicy).ShouldNotBe(global::OpenSearch.Net.Refresh.True);
	}

	[Theory]
	[InlineData("true")]
	[InlineData("false")]
	[InlineData("wait_for")]
	public void OpenSearchMapRefreshPolicyToConfiguredValue(string policy)
	{
		var expected = policy switch
		{
			"true" => global::OpenSearch.Net.Refresh.True,
			"false" => global::OpenSearch.Net.Refresh.False,
			_ => global::OpenSearch.Net.Refresh.WaitFor,
		};

		InvokeOpenSearchGetRefresh(policy).ShouldBe(expected);
	}
}
