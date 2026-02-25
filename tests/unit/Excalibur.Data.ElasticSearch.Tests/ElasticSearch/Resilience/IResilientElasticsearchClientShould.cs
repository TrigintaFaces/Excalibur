// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Elastic.Clients.Elasticsearch;

using Excalibur.Data.ElasticSearch.Resilience;

namespace Excalibur.Data.Tests.ElasticSearch.Resilience;

/// <summary>
/// Unit tests for the <see cref="IResilientElasticsearchClient"/> interface definition.
/// </summary>
/// <remarks>
/// Sprint 514 (S514.2): Resilience unit tests.
/// Tests verify interface members and contract.
/// </remarks>
[Trait("Category", TestCategories.Unit)]
[Trait("Component", "Elasticsearch")]
[Trait("Feature", "Resilience")]
public sealed class IResilientElasticsearchClientShould
{
	#region Interface Definition Tests

	[Fact]
	public void DefineIsCircuitBreakerOpenProperty()
	{
		// Assert
		var property = typeof(IResilientElasticsearchClient).GetProperty("IsCircuitBreakerOpen");
		property.ShouldNotBeNull();
		property.PropertyType.ShouldBe(typeof(bool));
		property.CanRead.ShouldBeTrue();
	}

	[Fact]
	public void DefineSearchAsyncMethod()
	{
		// Assert
		var method = typeof(IResilientElasticsearchClient).GetMethod("SearchAsync");
		method.ShouldNotBeNull();
		method.IsGenericMethod.ShouldBeTrue();

		var parameters = method.GetParameters();
		parameters.Length.ShouldBe(2);
		parameters[0].ParameterType.ShouldBe(typeof(SearchRequest));
		parameters[1].ParameterType.ShouldBe(typeof(CancellationToken));
	}

	[Fact]
	public void DefineIndexAsyncMethod()
	{
		// Assert
		var method = typeof(IResilientElasticsearchClient).GetMethod("IndexAsync");
		method.ShouldNotBeNull();
		method.IsGenericMethod.ShouldBeTrue();

		var parameters = method.GetParameters();
		parameters.Length.ShouldBe(2);
		parameters[1].ParameterType.ShouldBe(typeof(CancellationToken));
	}

	[Fact]
	public void DefineUpdateAsyncMethod()
	{
		// Assert
		var method = typeof(IResilientElasticsearchClient).GetMethod("UpdateAsync");
		method.ShouldNotBeNull();
		method.IsGenericMethod.ShouldBeTrue();

		var parameters = method.GetParameters();
		parameters.Length.ShouldBe(2);
		parameters[1].ParameterType.ShouldBe(typeof(CancellationToken));
	}

	[Fact]
	public void DefineDeleteAsyncMethod()
	{
		// Assert
		var method = typeof(IResilientElasticsearchClient).GetMethod("DeleteAsync");
		method.ShouldNotBeNull();

		var parameters = method.GetParameters();
		parameters.Length.ShouldBe(2);
		parameters[0].ParameterType.ShouldBe(typeof(DeleteRequest));
		parameters[1].ParameterType.ShouldBe(typeof(CancellationToken));
	}

	[Fact]
	public void DefineBulkAsyncMethod()
	{
		// Assert
		var method = typeof(IResilientElasticsearchClient).GetMethod("BulkAsync");
		method.ShouldNotBeNull();

		var parameters = method.GetParameters();
		parameters.Length.ShouldBe(2);
		parameters[0].ParameterType.ShouldBe(typeof(BulkRequest));
		parameters[1].ParameterType.ShouldBe(typeof(CancellationToken));
	}

	[Fact]
	public void DefineGetAsyncMethod()
	{
		// Assert
		var method = typeof(IResilientElasticsearchClient).GetMethod("GetAsync");
		method.ShouldNotBeNull();
		method.IsGenericMethod.ShouldBeTrue();

		var parameters = method.GetParameters();
		parameters.Length.ShouldBe(2);
		parameters[0].ParameterType.ShouldBe(typeof(GetRequest));
		parameters[1].ParameterType.ShouldBe(typeof(CancellationToken));
	}

	[Fact]
	public void DefineIsHealthyAsyncMethod()
	{
		// Assert
		var method = typeof(IResilientElasticsearchClient).GetMethod("IsHealthyAsync");
		method.ShouldNotBeNull();
		method.ReturnType.ShouldBe(typeof(Task<bool>));

		var parameters = method.GetParameters();
		parameters.Length.ShouldBe(1);
		parameters[0].ParameterType.ShouldBe(typeof(CancellationToken));
	}

	#endregion

	#region Interface Contract Tests

	[Fact]
	public void BeImplementableInterface()
	{
		// Assert
		typeof(IResilientElasticsearchClient).IsInterface.ShouldBeTrue();
	}

	[Fact]
	public void HaveExpectedPublicMethods()
	{
		// Assert - Count public methods (excluding property getters)
		var methods = typeof(IResilientElasticsearchClient).GetMethods()
			.Where(m => m.DeclaringType == typeof(IResilientElasticsearchClient))
			.Where(m => !m.IsSpecialName) // Exclude property getters
			.ToList();

		// SearchAsync, IndexAsync, UpdateAsync, DeleteAsync, BulkAsync, GetAsync, IsHealthyAsync = 7 methods
		methods.Count.ShouldBe(7);
	}

	#endregion
}
