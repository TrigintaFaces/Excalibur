// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;
using Excalibur.EventSourcing.DependencyInjection;
using Excalibur.EventSourcing.Queries;
using Excalibur.EventSourcing.Views;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Excalibur.EventSourcing.Tests.Views;

/// <summary>
/// Binds the startup validator's predicate across every arm of its decision.
/// </summary>
/// <remarks>
/// <para>
/// The validator's trigger is the <b>presence of a projection</b>, not the absence of a store. Four
/// engineers proposed four predicates for this seam and each was a proxy for the next: the store's
/// absence, a registration marker, the processor's identity. Each excused a broken configuration or
/// rejected a legitimate one.
/// </para>
/// <para>
/// A single throw-test cannot distinguish those predicates — they all throw on the headline case. Only
/// the arms that must <em>not</em> throw separate them, and those are the arms a regression silently
/// takes away. Each test below pins one edge.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "EventSourcing")]
public sealed class MaterializedViewStartupValidatorShould
{
	/// <summary>ARM 1 — a declared view, the built-in processor, a non-atomic store: reject at startup.</summary>
	/// <remarks>
	/// The non-atomic store is a test-local fake, deliberately not a shipped provider. Naming a real store
	/// here would make this lock depend on that store's <em>defect</em>: the day Elasticsearch learns to write
	/// a view and its checkpoint together, the lock stops testing what its name says, and the pressure would
	/// be to swap in whichever provider is still broken. The property under test is "a store that cannot write
	/// atomically is rejected", and it is stated here as a store that cannot write atomically.
	/// </remarks>
	[Fact]
	public async Task Reject_WhenAViewIsDeclaredAndTheBuiltInProcessorHasANonAtomicStore()
	{
		var services = BaseServices();
		_ = services.AddMaterializedViews(views => views
			.AddBuilder<TestView, TestViewBuilder>()
			.UseStore(_ => new NonAtomicStore()));

		var ex = await StartAsyncAndCaptureAsync(services).ConfigureAwait(false);

		_ = ex.ShouldNotBeNull("a non-atomic store cannot back an exactly-once projection");
		ex.Message.ShouldContain("cannot persist a view and its");
	}

	/// <summary>ARM 2 — a declared view, the built-in processor, no store at all: reject at startup.</summary>
	[Fact]
	public async Task Reject_WhenAViewIsDeclaredAndTheBuiltInProcessorHasNoStore()
	{
		var services = BaseServices();
		_ = services.AddMaterializedViews(views => views.AddBuilder<TestView, TestViewBuilder>());

		var ex = await StartAsyncAndCaptureAsync(services).ConfigureAwait(false);

		_ = ex.ShouldNotBeNull("a declared view with nothing to persist it must not boot");
		ex.Message.ShouldContain("no view store is configured");
	}

	/// <summary>
	/// ARM 3 — a consumer-supplied processor owns its own persistence contract, so no store is required.
	/// This is the arm a blanket no-store throw silently removes: the host stops booting for a consumer who
	/// did nothing wrong.
	/// </summary>
	[Fact]
	public async Task NotReject_WhenTheConsumerSuppliedTheirOwnProcessor_AndNoStoreIsRegistered()
	{
		var services = BaseServices();
		_ = services.AddMaterializedViews(views => views
			.AddBuilder<TestView, TestViewBuilder>()
			.UseProcessor<CustomProcessor>());

		var ex = await StartAsyncAndCaptureAsync(services).ConfigureAwait(false);

		ex.ShouldBeNull(
			"a processor supplied through UseProcessor<T>() owns its persistence contract; the framework "
			+ "must not speak for it");
	}

	/// <summary>
	/// ARM 4 — <c>AddMaterializedViews()</c> ahead of any view is a legal call. Nothing is being projected,
	/// so there is no guarantee to hold and no store to demand.
	/// </summary>
	[Fact]
	public async Task NotReject_WhenNoViewIsDeclared_EvenWithNoStore()
	{
		var services = BaseServices();
		_ = services.AddMaterializedViews();

		var ex = await StartAsyncAndCaptureAsync(services).ConfigureAwait(false);

		ex.ShouldBeNull("registering the subsystem without declaring a view projects nothing");
	}

	/// <summary>ARM 5 — the happy path: a declared view, the built-in processor, an atomic store. Boots.</summary>
	[Fact]
	public async Task NotReject_WhenAViewIsDeclaredAndTheStoreWritesAtomically()
	{
		var services = BaseServices();
		_ = services.AddSingleton<IMaterializedViewStore>(new AtomicStore());
		_ = services.AddMaterializedViews(views => views.AddBuilder<TestView, TestViewBuilder>());

		var ex = await StartAsyncAndCaptureAsync(services).ConfigureAwait(false);

		ex.ShouldBeNull("an atomic store satisfies the projection's exactly-once contract");
	}

	/// <summary>
	/// ARM 6 — a store that <em>implements</em> the atomic interface but reports the capability off. This is
	/// the third and worst shape: the type says it can, the configuration says it cannot.
	/// </summary>
	/// <remarks>
	/// The type check and the capability check are two different questions, and a guard that asks only the
	/// first accepts a store which will not honor the contract it advertises. A shipped provider is in exactly
	/// this state by default, which is why the arm exists — but the arm is stated against a fake, so it keeps
	/// testing the guard after that provider's default changes.
	/// </remarks>
	[Fact]
	public async Task Reject_WhenTheStoreImplementsTheAtomicInterfaceButTheCapabilityIsDisabled()
	{
		var services = BaseServices();
		_ = services.AddMaterializedViews(views => views
			.AddBuilder<TestView, TestViewBuilder>()
			.UseStore(_ => new AtomicCapableButDisabledStore()));

		var ex = await StartAsyncAndCaptureAsync(services).ConfigureAwait(false);

		_ = ex.ShouldNotBeNull(
			"implementing IAtomicMaterializedViewStore is not the same as being configured to write atomically");
		ex.Message.ShouldContain("disabled by its current configuration");
	}

	/// <summary>
	/// Starts every registered hosted service and returns the exception, or <see langword="null"/>.
	/// </summary>
	/// <remarks>
	/// Asserts a hosted service exists at all: a startup gate that is not registered can never fire, and a
	/// test that merely observed "nothing threw" would pass for that reason.
	/// </remarks>
	private static async Task<InvalidOperationException?> StartAsyncAndCaptureAsync(IServiceCollection services)
	{
		await using var provider = services.BuildServiceProvider();

		var hostedServices = provider.GetServices<IHostedService>().ToList();
		hostedServices.ShouldNotBeEmpty("a startup gate that is never registered can never fire");

		try
		{
			foreach (var hostedService in hostedServices)
			{
				await hostedService.StartAsync(CancellationToken.None).ConfigureAwait(false);
			}

			return null;
		}
		catch (InvalidOperationException ex)
		{
			return ex;
		}
	}

	private static ServiceCollection BaseServices()
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddSingleton(A.Fake<IGlobalStreamQuery>());
		_ = services.AddSingleton(A.Fake<IEventSerializer>());
		return services;
	}

	private sealed class TestView;

	private sealed class TestViewBuilder : IMaterializedViewBuilder<TestView>
	{
		public string ViewName => "TestView";

		public IReadOnlyList<Type> HandledEventTypes => [];

		public string? GetViewId(IDomainEvent @event) => null;

		public TestView Apply(TestView view, IDomainEvent @event) => view;
	}

	/// <summary>A store that CAN persist a view and its checkpoint as one unit.</summary>
	private sealed class AtomicStore : IAtomicMaterializedViewStore
	{
		public bool SupportsAtomicWrites => true;

		public ValueTask<TView?> GetAsync<TView>(string viewName, string viewId, CancellationToken ct)
			where TView : class => new((TView?)null);

		public ValueTask SaveAsync<TView>(string viewName, string viewId, TView view, CancellationToken ct)
			where TView : class => default;

		public ValueTask DeleteAsync(string viewName, string viewId, CancellationToken ct) => default;

		public ValueTask<long?> GetPositionAsync(string viewName, CancellationToken ct) => new((long?)null);

		public ValueTask SavePositionAsync(string viewName, long position, CancellationToken ct) => default;

		public ValueTask SaveViewAndPositionAsync<TView>(
			string viewName, string viewId, TView view, long position, CancellationToken ct)
			where TView : class => default;
	}

	/// <summary>
	/// A store that implements the atomic contract but reports the capability disabled — the shape a shipped
	/// provider takes when its transactions option is left at its default.
	/// </summary>
	private sealed class AtomicCapableButDisabledStore : IAtomicMaterializedViewStore
	{
		public bool SupportsAtomicWrites => false;

		public ValueTask<TView?> GetAsync<TView>(string viewName, string viewId, CancellationToken ct)
			where TView : class => new((TView?)null);

		public ValueTask SaveAsync<TView>(string viewName, string viewId, TView view, CancellationToken ct)
			where TView : class => default;

		public ValueTask DeleteAsync(string viewName, string viewId, CancellationToken ct) => default;

		public ValueTask<long?> GetPositionAsync(string viewName, CancellationToken ct) => new((long?)null);

		public ValueTask SavePositionAsync(string viewName, long position, CancellationToken ct) => default;

		public ValueTask SaveViewAndPositionAsync<TView>(
			string viewName, string viewId, TView view, long position, CancellationToken ct)
			where TView : class => default;
	}

	/// <summary>
	/// A store that CANNOT persist a view and its checkpoint as one unit — it does not implement
	/// <see cref="IAtomicMaterializedViewStore"/> at all, which is exactly what the guard rejects.
	/// </summary>
	private sealed class NonAtomicStore : IMaterializedViewStore
	{
		public ValueTask<TView?> GetAsync<TView>(string viewName, string viewId, CancellationToken ct)
			where TView : class => new((TView?)null);

		public ValueTask SaveAsync<TView>(string viewName, string viewId, TView view, CancellationToken ct)
			where TView : class => default;

		public ValueTask DeleteAsync(string viewName, string viewId, CancellationToken ct) => default;

		public ValueTask<long?> GetPositionAsync(string viewName, CancellationToken ct) => new((long?)null);

		public ValueTask SavePositionAsync(string viewName, long position, CancellationToken ct) => default;
	}

	/// <summary>A consumer-supplied processor. Persists views however it likes; needs no framework store.</summary>
	private sealed class CustomProcessor : IMaterializedViewProcessor
	{
		public Task ProcessEventAsync(IDomainEvent @event, long position, CancellationToken ct) => Task.CompletedTask;

		public Task ProcessEventsAsync(
			IEnumerable<(IDomainEvent Event, long Position)> events, CancellationToken ct) => Task.CompletedTask;

		public Task RebuildAsync(CancellationToken ct) => Task.CompletedTask;

		public Task CatchUpAsync(string viewName, CancellationToken ct) => Task.CompletedTask;
	}
}
