// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.ElasticSearch.MaterializedViews;
using Excalibur.Dispatch;
using Excalibur.EventSourcing.DependencyInjection;
using Excalibur.EventSourcing.Queries;
using Excalibur.EventSourcing.Views;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Excalibur.EventSourcing.Tests.Views;

/// <summary>
/// Binds the atomic view/checkpoint wiring gate through the REAL DI container. Constructing the processor
/// by hand with a mock store is unit-grade; the bar for a wiring seam is that the production registration
/// path resolves it end-to-end.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "EventSourcing")]
public sealed class MaterializedViewAtomicWiringShould
{
	/// <summary>
	/// A store that cannot persist a view and its checkpoint together must be rejected when the processor is
	/// resolved from the real container — not silently accepted and then relied upon to project exactly once.
	/// </summary>
	[Fact]
	public async Task RejectANonAtomicViewStore_WhenTheProcessorIsResolvedFromTheRealContainer()
	{
		var services = ServicesWithElasticSearchViewStore();

		await using var provider = services.BuildServiceProvider();

		// Registration alone must not be treated as verification: the container is built without complaint.
		// The rejection happens when the processor is actually constructed.
		var resolve = () => provider.GetRequiredService<IMaterializedViewProcessor>();

		var ex = Should.Throw<InvalidOperationException>(resolve);
		ex.Message.ShouldContain("cannot persist a view and its");
	}

	/// <summary>
	/// The rejection must reach the HOST, not merely the first component that happens to resolve the
	/// processor.
	/// </summary>
	/// <remarks>
	/// <para>
	/// Resolving the processor throws, but nothing at host start resolved it: the processor is a lazily
	/// resolved singleton whose only production resolve site sits inside the refresh background service,
	/// behind two <c>catch (Exception)</c> handlers that retry and then log-and-continue. The host booted
	/// clean and projected nothing, forever.
	/// </para>
	/// <para>
	/// So this test does not assert that the processor throws — the sibling above does that, and it passed
	/// while the hole was open. It asserts that <b>starting the hosted services</b> throws, which is the only
	/// moment an operator can still act on the misconfiguration.
	/// </para>
	/// </remarks>
	[Fact]
	public async Task RejectANonAtomicViewStore_WhenTheHostedServicesAreStarted()
	{
		var services = ServicesWithElasticSearchViewStore();
		await using var provider = services.BuildServiceProvider();

		var hostedServices = provider.GetServices<IHostedService>().ToList();
		hostedServices.ShouldNotBeEmpty(
			"a startup gate that is not registered as a hosted service can never fire at startup");

		var startAll = async () =>
		{
			foreach (var hostedService in hostedServices)
			{
				await hostedService.StartAsync(CancellationToken.None).ConfigureAwait(false);
			}
		};

		var ex = await Should.ThrowAsync<InvalidOperationException>(startAll).ConfigureAwait(false);
		ex.Message.ShouldContain("cannot persist a view and its");
	}

	/// <summary>
	/// Building the container is deliberately NOT the gate: a non-atomic store is a runtime configuration
	/// fault, and composition must stay side-effect free. The rejection belongs at host start, which the
	/// sibling above pins.
	/// </summary>
	[Fact]
	public async Task NotRejectANonAtomicViewStore_WhenTheContainerIsMerelyBuilt()
	{
		var services = ServicesWithElasticSearchViewStore();

		var provider = Should.NotThrow(services.BuildServiceProvider);
		await provider.DisposeAsync();
	}

	/// <summary>
	/// Registers the processor's non-store collaborators so that resolution reaches the atomic-store gate.
	/// Without these the container fails on an unrelated missing dependency and the gate is never exercised —
	/// a green test that proves nothing.
	/// </summary>
	private static ServiceCollection ServicesWithElasticSearchViewStore()
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddSingleton(A.Fake<IGlobalStreamQuery>());
		_ = services.AddSingleton(A.Fake<IEventSerializer>());
		_ = services.AddMaterializedViews(views =>
		{
			// A view builder, not just a store. The startup gate is triggered by the PRESENCE OF A PROJECTION,
			// never by the absence of a store: AddMaterializedViews() with no views declared is a legal call
			// that must start, and a non-atomic store nothing projects onto guarantees nothing. Without a
			// builder registered here the gate correctly stays silent, and this test would assert a throw the
			// design does not owe it.
			_ = views.AddBuilder<AtomicWiringTestView, AtomicWiringTestViewBuilder>();
			_ = views.UseElasticSearch(options => options.NodeUri = "http://localhost:9200");
		});
		return services;
	}

	/// <summary>A view with no behaviour; it exists so that a projection is declared.</summary>
	internal sealed class AtomicWiringTestView
	{
		public long Count { get; set; }
	}

	/// <summary>Declares the projection that makes the store's persistence guarantee load-bearing.</summary>
	internal sealed class AtomicWiringTestViewBuilder : IMaterializedViewBuilder<AtomicWiringTestView>
	{
		public string ViewName => "atomic-wiring-test-view";

		public IReadOnlyList<Type> HandledEventTypes => [];

		public string GetViewId(IDomainEvent @event) => "singleton";

		public AtomicWiringTestView Apply(AtomicWiringTestView view, IDomainEvent @event) => view;
	}
}
