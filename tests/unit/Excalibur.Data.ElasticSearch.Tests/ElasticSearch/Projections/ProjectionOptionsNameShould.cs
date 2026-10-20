// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Data.ElasticSearch.Projections;

using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Data.Tests.ElasticSearch.Projections;

/// <summary>Two projection types whose UNQUALIFIED names are identical.</summary>
internal static class Sales
{
	internal sealed class Summary
	{
		public string Id { get; set; } = string.Empty;
	}
}

/// <summary>The same unqualified name, a different type. This is the collision.</summary>
internal static class Support
{
	internal sealed class Summary
	{
		public string Id { get; set; } = string.Empty;
	}
}

/// <summary>
/// A projection's options — and therefore its index — are keyed on its unqualified type name.
/// </summary>
/// <remarks>
/// <para>
/// <c>Sales.Summary</c> and <c>Support.Summary</c> are different read models that would share one named
/// options instance and one index, so each would read the other's documents and overwrite them on the
/// same id. Nothing reported it: the registrations look correct, the types are plainly distinct, and the
/// collision lives in a string neither of them mentions.
/// </para>
/// <para>
/// A consumer in this very repository has two <c>EmployeeProjection</c> types, one registered and one
/// not, and had to write the hazard into their own source as a comment to protect the next reader.
/// </para>
/// </remarks>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Data)]
public sealed class ProjectionOptionsNameShould
{
	[Fact]
	public void RefuseTwoProjectionTypesThatWouldShareOneIndex()
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();

		_ = services.AddElasticSearchProjectionStore<Sales.Summary>(o => o.NodeUri = "http://localhost:9200");

		var error = Should.Throw<InvalidOperationException>(
			() => services.AddElasticSearchProjectionStore<Support.Summary>(
				o => o.NodeUri = "http://localhost:9200"));

		// Both full names, because the unqualified one is the same for both and so identifies neither.
		error.Message.ShouldContain(typeof(Sales.Summary).FullName!, Case.Sensitive);
		error.Message.ShouldContain(typeof(Support.Summary).FullName!, Case.Sensitive);
	}

	/// <summary>
	/// LIVENESS. Without this the refusal above is satisfied by a check that rejects the second
	/// registration of anything.
	/// </summary>
	[Fact]
	public void RegisterTwoProjectionsWithDifferentNames()
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();

		_ = services.AddElasticSearchProjectionStore<Sales.Summary>(o => o.NodeUri = "http://localhost:9200");
		_ = services.AddElasticSearchProjectionStore<CombinedProjection>(o => o.NodeUri = "http://localhost:9200");
	}

	/// <summary>
	/// Registering the SAME projection twice is not a collision — the second call replaces the first, as
	/// every other registration in the framework does.
	/// </summary>
	[Fact]
	public void AllowTheSameProjectionToBeRegisteredTwice()
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();

		_ = services.AddElasticSearchProjectionStore<Sales.Summary>(o => o.NodeUri = "http://localhost:9200");
		_ = services.AddElasticSearchProjectionStore<Sales.Summary>(o => o.NodeUri = "http://localhost:9201");
	}

	/// <summary>
	/// The options name is produced by ONE function, which the store, the registration and a consumer's
	/// own repository all call — it was derived independently in three places.
	/// </summary>
	[Fact]
	public void BeProducedByOneSharedFunction() =>
		ElasticSearchProjectionIndexConvention.GetOptionsName<Sales.Summary>()
			.ShouldBe(nameof(Sales.Summary));
}
