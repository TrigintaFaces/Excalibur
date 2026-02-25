// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.ElasticSearch.Resilience;

namespace Excalibur.Data.Tests.ElasticSearch.Resilience;

/// <summary>
/// Unit tests for the <see cref="IElasticsearchRetryPolicy"/> interface definition.
/// </summary>
/// <remarks>
/// Sprint 514 (S514.2): Resilience unit tests.
/// Tests verify interface members and contract.
/// </remarks>
[Trait("Category", TestCategories.Unit)]
[Trait("Component", "Elasticsearch")]
[Trait("Feature", "Resilience")]
public sealed class IElasticsearchRetryPolicyShould
{
	#region Interface Definition Tests

	[Fact]
	public void DefineMaxAttemptsProperty()
	{
		// Assert - Interface defines MaxAttempts property
		var property = typeof(IElasticsearchRetryPolicy).GetProperty("MaxAttempts");
		property.ShouldNotBeNull();
		property.PropertyType.ShouldBe(typeof(int));
		property.CanRead.ShouldBeTrue();
	}

	[Fact]
	public void DefineGetRetryDelayMethod()
	{
		// Assert - Interface defines GetRetryDelay method
		var method = typeof(IElasticsearchRetryPolicy).GetMethod("GetRetryDelay");
		method.ShouldNotBeNull();
		method.ReturnType.ShouldBe(typeof(TimeSpan));

		var parameters = method.GetParameters();
		parameters.Length.ShouldBe(1);
		parameters[0].ParameterType.ShouldBe(typeof(int));
		parameters[0].Name.ShouldBe("attemptNumber");
	}

	[Fact]
	public void DefineShouldRetryMethod()
	{
		// Assert - Interface defines ShouldRetry method
		var method = typeof(IElasticsearchRetryPolicy).GetMethod("ShouldRetry");
		method.ShouldNotBeNull();
		method.ReturnType.ShouldBe(typeof(bool));

		var parameters = method.GetParameters();
		parameters.Length.ShouldBe(2);
		parameters[0].ParameterType.ShouldBe(typeof(Exception));
		parameters[0].Name.ShouldBe("exception");
		parameters[1].ParameterType.ShouldBe(typeof(int));
		parameters[1].Name.ShouldBe("attemptNumber");
	}

	#endregion

	#region Interface Contract Tests

	[Fact]
	public void BeImplementableInterface()
	{
		// Assert - IElasticsearchRetryPolicy is an interface
		typeof(IElasticsearchRetryPolicy).IsInterface.ShouldBeTrue();
	}

	[Fact]
	public void HaveThreeMembers()
	{
		// Assert - Interface has 3 members: MaxAttempts (property), GetRetryDelay, ShouldRetry
		var members = typeof(IElasticsearchRetryPolicy).GetMembers()
			.Where(m => m.DeclaringType == typeof(IElasticsearchRetryPolicy))
			.ToList();

		// Property has getter method + property itself = 2, plus 2 methods = 4
		members.Count.ShouldBeGreaterThanOrEqualTo(3);
	}

	#endregion
}
