// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch;
using Excalibur.EventSourcing.Projections;

using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.EventSourcing.Tests.Projections;

/// <summary>
/// Binds the aggregate identity reaching a context handler through the inline apply delegate.
/// </summary>
/// <remarks>
/// <para>
/// Domain events do not carry the aggregate identifier — the stored envelope is authoritative — so a
/// projection can only reach it through <see cref="ProjectionContext.AggregateId"/>. If that arrives
/// empty, a projection cannot stamp its own identity, and a client reading the projection back has
/// nothing to send to an update command. The read model is silently anonymous rather than wrong, which
/// is why this is asserted rather than left to inspection.
/// </para>
/// <para>
/// These arms drive <c>InlineApply</c> — the delegate that actually runs in production — rather than
/// inspecting the registration. A test that only checks a handler was registered passes whether or not
/// the context reaching it carries anything.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class ProjectionContextAggregateIdFlowShould
{
	private const string AggregateId = "customer-1234";

	private readonly InMemoryProjectionRegistry _registry = new();

	[Fact]
	public async Task PassTheAggregateIdToAContextHandler()
	{
		// Arrange
		string? observed = null;
		var builder = new ProjectionBuilder<OrderSummary>(_registry);
		builder.Async().When<TestOrderPlaced>((proj, e, ctx) =>
		{
			observed = ctx.AggregateId;
			proj.Total = e.Amount;
		});
		builder.Build();

		// Act
		await InvokeInlineApplyAsync(new TestOrderPlaced { Amount = 100m }).ConfigureAwait(true);

		// Assert -- the identity the handler needs to stamp onto the projection
		observed.ShouldBe(AggregateId);
	}

	[Fact]
	public async Task PassAnAggregateIdThatIsNeitherNullNorEmpty()
	{
		// The failure this guards is not an exception -- it is an empty string flowing into the read
		// model's own Id, which looks like a successfully projected document until a client tries to act
		// on it. Asserted separately from the equality arm so the diagnosis is unambiguous when it breaks.

		// Arrange
		string? observed = "sentinel";
		var builder = new ProjectionBuilder<OrderSummary>(_registry);
		builder.Async().When<TestOrderPlaced>((_, _, ctx) => observed = ctx.AggregateId);
		builder.Build();

		// Act
		await InvokeInlineApplyAsync(new TestOrderPlaced { Amount = 1m }).ConfigureAwait(true);

		// Assert
		observed.ShouldNotBeNullOrEmpty();
	}

	[Fact]
	public async Task StillInvokeTwoArgumentHandlersThatIgnoreTheContext()
	{
		// Liveness. Every arm above would also pass if the builder stopped invoking handlers entirely,
		// so one arm asserts the handler runs and does its ordinary work.

		// Arrange
		var builder = new ProjectionBuilder<OrderSummary>(_registry);
		builder.Async().When<TestOrderPlaced>((proj, e) => proj.Total = e.Amount);
		builder.Build();

		// Act
		var state = await InvokeInlineApplyAsync(new TestOrderPlaced { Amount = 42m }).ConfigureAwait(true);

		// Assert
		state.Total.ShouldBe(42m);
	}

	[Fact]
	public async Task PassTheAggregateIdOnTheAsyncCapablePathToo()
	{
		// The builder has TWO delegate paths and picks between them by whether any async handler is
		// registered. Every other arm here exercises the sync-only fast path, so without this one the
		// full path could revert to a context carrying no identity and the suite would stay green --
		// which is exactly how the original defect survived.

		// Arrange -- registering an async handler forces the full path. This arm uses its own
		// projection type: a handler for the shared OrderSummary would be discovered by the
		// assembly-scanning tests as a duplicate for an event they already cover.
		string? observed = null;
		var builder = new ProjectionBuilder<IsolatedProjection>(_registry);
		builder.Async()
			.When<TestOrderPlaced>((proj, e, ctx) =>
			{
				observed = ctx.AggregateId;
				proj.Total = e.Amount;
			})
			.WhenHandledBy<TestOrderShipped, NoOpShippedHandler>();
		builder.Build();

		// Act
		var registration = _registry.GetRegistration(typeof(IsolatedProjection));
		registration.ShouldNotBeNull();
		registration.InlineApply.ShouldNotBeNull();

		var services = new ServiceCollection();
		services.AddSingleton<IProjectionStore<IsolatedProjection>>(new InMemoryProjectionStore<IsolatedProjection>());
		services.AddTransient<NoOpShippedHandler>();
		using var provider = services.BuildServiceProvider();

		await registration.InlineApply(
			[new ProjectionEvent(new TestOrderPlaced { Amount = 5m }, AggregateId, GlobalPosition: 1)],
			new EventNotificationContext(AggregateId, "Customer", 1, DateTimeOffset.UnixEpoch),
			provider,
			CancellationToken.None).ConfigureAwait(true);

		// Assert
		observed.ShouldBe(AggregateId);
	}

	/// <summary>
	/// Registered only to force the builder onto its async-capable delegate; it is never invoked by
	/// these arms.
	/// </summary>
	private sealed class NoOpShippedHandler : IProjectionEventHandler<IsolatedProjection, TestOrderShipped>
	{
		public Task HandleAsync(
			IsolatedProjection projection,
			TestOrderShipped @event,
			ProjectionHandlerContext context,
			CancellationToken cancellationToken) => Task.CompletedTask;
	}

	/// <summary>Projection type used only by the async-path arm, to keep it out of shared fixtures.</summary>
	private sealed class IsolatedProjection
	{
		public decimal Total { get; set; }
	}

	/// <summary>
	/// Drives the registered inline apply delegate the way the framework does, and returns the
	/// projection state the store was left holding.
	/// </summary>
	private async Task<OrderSummary> InvokeInlineApplyAsync(
		IDomainEvent @event,
		Action<IServiceCollection>? configureServices = null)
	{
		var registration = _registry.GetRegistration(typeof(OrderSummary));
		registration.ShouldNotBeNull();
		registration.InlineApply.ShouldNotBeNull();

		var store = new TestProjectionStore<OrderSummary>();
		var services = new ServiceCollection();
		services.AddSingleton<IProjectionStore<OrderSummary>>(store);
		configureServices?.Invoke(services);

		using var provider = services.BuildServiceProvider();

		var context = new EventNotificationContext(
			AggregateId,
			"Customer",
			CommittedVersion: 1,
			Timestamp: DateTimeOffset.UnixEpoch);

		await registration.InlineApply(
			[new ProjectionEvent(@event, AggregateId, GlobalPosition: 1)],
			context,
			provider,
			CancellationToken.None).ConfigureAwait(true);

		return (await store.GetByIdAsync(AggregateId, CancellationToken.None))!;
	}

}
