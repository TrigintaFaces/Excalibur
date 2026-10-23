// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Data.ElasticSearch.Projections;
using Excalibur.EventSourcing;
using Excalibur.EventSourcing.DependencyInjection;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Excalibur.Data.Tests.ElasticSearch.Projections;

internal sealed class CombinedProjection
{
	public string Id { get; set; } = string.Empty;
}

/// <summary>
/// One call that registers what a projection folds AND where it is stored.
/// </summary>
/// <remarks>
/// <para>
/// A stored projection took two registrations in two places — <c>AddProjection&lt;T&gt;</c> inside
/// <c>AddEventSourcing</c>, and a store registration elsewhere — and nothing connected them. Omitting
/// the second gave a projection that folds into nothing, which is now a start-up refusal rather than a
/// failure on first save; this method makes the pair unforgettable instead, because it cannot be half
/// made.
/// </para>
/// <para>
/// The store configuration is REQUIRED rather than optional. A one-call API whose cluster silently
/// defaulted to <c>localhost:9200</c> would be a worse trap than the two calls it replaces: it would
/// start, and fail only where the cluster is somewhere else.
/// </para>
/// </remarks>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Data)]
public sealed class AddElasticSearchProjectionShould
{
	/// <remarks>
	/// Async disposal throughout: the Elasticsearch store implements only <see cref="IAsyncDisposable"/>,
	/// so a scope holding one refuses to be disposed synchronously -- which is also why the start-up
	/// probe asks the container whether a store is registered rather than resolving one.
	/// </remarks>
	[Fact]
	public async Task RegisterBothHalvesFromOneCall()
	{
		await using var provider = Build();

		// WHERE it is stored.
		await using var scope = provider.CreateAsyncScope();
		scope.ServiceProvider.GetService<IProjectionStore<CombinedProjection>>().ShouldNotBeNull();

		// The store options the call configured, in this projection's own named instance.
		var options = provider.GetRequiredService<IOptionsMonitor<ElasticSearchProjectionStoreOptions>>()
			.Get(typeof(CombinedProjection).FullName!);
		options.Index.IndexName.ShouldBe("combined");
	}

	/// <summary>
	/// The arm that proves the pair is complete: the start-up requirement that refuses a projection with
	/// no store is satisfied by this one call. Asserting the options alone would pass for a call that
	/// configured a store it never registered.
	/// </summary>
	[Fact]
	public void SatisfyTheStartupStoreRequirement()
	{
		using var provider = Build();

		foreach (var validator in provider.GetServices<IStartupPrerequisiteValidator>())
		{
			validator.Validate();
		}
	}

	/// <summary>
	/// CONTROL: the fold half alone leaves the requirement unsatisfied, so the arm above is detecting
	/// the store registration rather than a check that passes for everything.
	/// </summary>
	[Fact]
	public void LeaveTheRequirementUnsatisfiedWhenOnlyTheFoldIsRegistered()
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = new ExcaliburEventSourcingBuilder(services).AddProjection<CombinedProjection>(p => p.Async());

		using var provider = services.BuildServiceProvider();

		_ = Should.Throw<InvalidOperationException>(() =>
		{
			foreach (var validator in provider.GetServices<IStartupPrerequisiteValidator>())
			{
				validator.Validate();
			}
		});
	}

	private static ServiceProvider Build()
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();

		_ = new ExcaliburEventSourcingBuilder(services)
			.AddElasticSearchProjection<CombinedProjection>(
				p => p.Async(),
				o =>
				{
					o.NodeUri = "http://localhost:9200";
					o.Index.IndexName = "combined";
				});

		return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
	}
}
