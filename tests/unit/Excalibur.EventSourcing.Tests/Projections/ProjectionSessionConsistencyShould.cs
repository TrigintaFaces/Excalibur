// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Globalization;
using System.Text;

using Excalibur.Dispatch;
using Excalibur.EventSourcing.DependencyInjection;
using Excalibur.EventSourcing.Projections;
using Excalibur.EventSourcing.Queries;
using Excalibur.EventSourcing.Subscriptions;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Excalibur.EventSourcing.Tests.Projections;

/// <summary>
/// Read-after-write against an ASYNC projection: the save hands back a session token, the projection is
/// applied by the one writer that owns it, and a read carrying the token observes that write.
/// </summary>
/// <remarks>
/// <para>
/// These arms used to assert that a projection was up to date the instant <c>SaveAsync</c> returned,
/// because the fold ran inside the save. That fold was a second uncoordinated writer -- two concurrent
/// saves both read a row, both folded, and the last write won while both callers were told they had
/// succeeded -- so it is gone, and the async host is the only writer.
/// </para>
/// <para>
/// What replaces it is strictly stronger evidence. Asserting that a row exists after a save says only
/// that something wrote something; asserting <see cref="ProjectionFreshness.Fresh"/> for a specific
/// <see cref="SaveResult"/> says the read observed THAT write.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class ProjectionSessionConsistencyShould
{
	/// <summary>
	/// AC-1.1 as it now reads: drive the projection, then prove the read saw this write.
	/// </summary>
	[Fact]
	public async Task ApplyTheFoldAndReportFreshForTheWriteThatProducedTheToken()
	{
		// Arrange
		using var safety = ProjectionSessionHarness.Safety();
		using var harness = new ProjectionSessionHarness();
		var projectionStore = new TestProjectionStore<OrderSummary>();
		_ = harness.Services.AddSingleton<IProjectionStore<OrderSummary>>(projectionStore);

		var builder = new ProjectionBuilder<OrderSummary>(harness.Registry);
		_ = builder.Async();
		_ = builder.When<TestOrderPlaced>((proj, e) =>
		{
			proj.Total = e.Amount;
			proj.EventCount++;
		});
		builder.Build();

		var saved = harness.Append(new TestOrderPlaced { Amount = 250m }, "order-1", version: 1);

		// Act -- the pull API a host with no background service drives, then the consistent read.
		var processed = await harness.DriveAsync(safety.Token);
		var read = await harness.ReadAsync<OrderSummary>("order-1", saved, safety.Token);

		// Assert
		processed.Outcome.ShouldBe(ProjectionProcessingOutcome.CaughtUp);
		processed.EventsApplied.ShouldBe(1);

		var projected = read.Projection.ShouldNotBeNull();
		projected.Total.ShouldBe(250m);
		projected.EventCount.ShouldBe(1);

		// The load-bearing half. Without it this arm would pass against a projection left over from any
		// earlier write, which is exactly what the old immediate-consistency assertion could not tell.
		read.Freshness.ShouldBe(
			ProjectionFreshness.Fresh,
			"the read must have observed the save that produced its token, not merely found a row");
	}

	[Fact]
	public async Task ResolveAScopedProjectionStoreUnderScopeValidation()
	{
		// Regression: IProjectionStore<T> is registered SCOPED (SQL Server / Mongo / ES), but the
		// projection host is a singleton. Resolving the store from the captured root provider threw
		// "Cannot resolve scoped service ... from root provider" under DI scope validation -- the default
		// in the Development host. The host must apply each projection inside a freshly created scope.
		using var safety = ProjectionSessionHarness.Safety();
		using var harness = new ProjectionSessionHarness { ValidateScopes = true };
		var projectionStore = new TestProjectionStore<OrderSummary>();
		_ = harness.Services.AddScoped<IProjectionStore<OrderSummary>>(_ => projectionStore);

		var builder = new ProjectionBuilder<OrderSummary>(harness.Registry);
		_ = builder.Async();
		_ = builder.When<TestOrderPlaced>((proj, e) =>
		{
			proj.Total = e.Amount;
			proj.EventCount++;
		});
		builder.Build();

		var saved = harness.Append(new TestOrderPlaced { Amount = 250m }, "order-1", version: 1);

		// Act
		var processed = await harness.DriveAsync(safety.Token);
		var read = await harness.ReadAsync<OrderSummary>("order-1", saved, safety.Token);

		// Assert -- a scope-validation failure surfaces as an apply fault, which HALTS the host without
		// advancing the checkpoint. So the outcome is the sensitive assertion here: a swallowed
		// InvalidOperationException would leave the fold undone and the read stale.
		processed.Outcome.ShouldBe(
			ProjectionProcessingOutcome.CaughtUp,
			"resolving the scoped store must not fault the apply under scope validation");
		read.Projection.ShouldNotBeNull().Total.ShouldBe(250m);
		read.Freshness.ShouldBe(ProjectionFreshness.Fresh);
	}

	/// <summary>
	/// AC-1.2: no broker registered = zero-overhead path, unchanged.
	/// </summary>
	[Fact]
	public void ResolveNullBrokerWhenNotRegistered()
	{
		// Arrange -- empty service collection, no UseEventNotification called
		var services = new ServiceCollection();
		var sp = services.BuildServiceProvider();

		// Act
		var broker = sp.GetService<IEventNotificationBroker>();

		// Assert -- null, zero overhead path
		broker.ShouldBeNull();
	}

	/// <summary>
	/// The post-commit phase still invokes notification handlers, and it no longer writes a projection.
	/// </summary>
	/// <remarks>
	/// This arm used to assert the ORDER of two phases -- projections, then handlers. One of those phases
	/// is gone, so the ordering property is gone with it. What replaced it is the property the removal
	/// actually turns on: the broker is no longer a writer of our read models. Asserting the apply was
	/// NOT invoked is a safety claim, so it is paired with the liveness claim that the handler still was;
	/// a broker that did nothing at all would otherwise satisfy it.
	/// </remarks>
	[Fact]
	public async Task InvokeNotificationHandlersWithoutApplyingAnyProjectionItself()
	{
		// Arrange
		var observed = new List<string>();
		var registry = new InMemoryProjectionRegistry();
		registry.Register(new ProjectionRegistration(
			typeof(OrderSummary),
			ProjectionMode.Async,
			new MultiStreamProjection<OrderSummary>(),
			inlineApply: (_, _, _, _) =>
			{
				observed.Add("projection-apply");
				return Task.CompletedTask;
			}));

		var handler = new OrderingTestHandler(observed);
		var services = new ServiceCollection()
			.AddSingleton<IProjectionStore<OrderSummary>>(new TestProjectionStore<OrderSummary>())
			.AddSingleton<IProjectionRegistry>(registry)
			.AddSingleton<IEventNotificationHandler<TestOrderPlaced>>(handler)
			.BuildServiceProvider();

		var broker = new EventNotificationBroker(
			services.GetRequiredService<IServiceScopeFactory>(),
			Options.Create(new EventNotificationOptions()),
			NullLogger<EventNotificationBroker>.Instance,
			Array.Empty<EventNotificationServiceCollectionExtensions.IConfigureProjection>());

		// Act
		await broker.NotifyAsync(
			new List<IDomainEvent> { new TestOrderPlaced() },
			new EventNotificationContext("order-1", "Order", 1, DateTimeOffset.UtcNow));

		// Assert -- the consumer callback ran (liveness) and the broker wrote no projection (safety).
		observed.ShouldBe(["notification-handler"]);
	}

	/// <summary>
	/// AC-1.7: two projections of different types, both folded from one batch.
	/// </summary>
	[Fact]
	public async Task UpdateEveryRegisteredProjectionTypeFromOneBatch()
	{
		// Arrange
		using var safety = ProjectionSessionHarness.Safety();
		using var harness = new ProjectionSessionHarness();
		var orderStore = new TestProjectionStore<OrderSummary>();
		var inventoryStore = new TestProjectionStore<InventoryView>();
		_ = harness.Services.AddSingleton<IProjectionStore<OrderSummary>>(orderStore);
		_ = harness.Services.AddSingleton<IProjectionStore<InventoryView>>(inventoryStore);

		var orderBuilder = new ProjectionBuilder<OrderSummary>(harness.Registry);
		_ = orderBuilder.Async();
		_ = orderBuilder.When<TestOrderPlaced>((proj, e) => proj.Total = e.Amount);
		orderBuilder.Build();

		var inventoryBuilder = new ProjectionBuilder<InventoryView>(harness.Registry);
		_ = inventoryBuilder.Async();
		_ = inventoryBuilder.When<TestOrderPlaced>((proj, _) => proj.Quantity--);
		inventoryBuilder.Build();

		var saved = harness.Append(new TestOrderPlaced { Amount = 500m }, "order-1", version: 1);

		// Act
		var processed = await harness.DriveAsync(safety.Token);

		// Assert -- both projections caught up to the same write.
		processed.Outcome.ShouldBe(ProjectionProcessingOutcome.CaughtUp);

		var order = await harness.ReadAsync<OrderSummary>("order-1", saved, safety.Token);
		order.Projection.ShouldNotBeNull().Total.ShouldBe(500m);
		order.Freshness.ShouldBe(ProjectionFreshness.Fresh);

		var inventory = await harness.ReadAsync<InventoryView>("order-1", saved, safety.Token);
		inventory.Projection.ShouldNotBeNull().Quantity.ShouldBe(-1); // decremented from default 0
		inventory.Freshness.ShouldBe(ProjectionFreshness.Fresh);
	}

	/// <summary>
	/// Full DI pipeline: UseEventNotification registers and resolves the post-commit infrastructure,
	/// including the reader that now serves read-after-write.
	/// </summary>
	[Fact]
	public void RegisterAndResolveEventNotificationInfrastructureViaDI()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddSingleton<IProjectionStore<OrderSummary>>(new TestProjectionStore<OrderSummary>());
		services.AddLogging();
		services.AddMetrics();

		// The session reader waits on a SUBSCRIPTION CHECKPOINT, which is a provider concern --
		// UseEventNotification does not register one, and an event-store provider or
		// EnableProjectionProcessing supplies it. Registering one here is what makes the assertion below
		// a resolution rather than a look at the descriptor list: a registration that cannot be
		// constructed is the advertised-but-unwired shape, and only resolving it catches that.
		services.AddSingleton<ISubscriptionCheckpointStore, InMemorySubscriptionCheckpointStore>();

		var fakeBuilder = A.Fake<IEventSourcingBuilder>();
		A.CallTo(() => fakeBuilder.Services).Returns(services);

		// Act
		fakeBuilder.UseEventNotification();

		var sp = services.BuildServiceProvider();

		// Assert -- all services resolve correctly. The inline processor assertion this arm used to carry
		// is replaced by the session reader: that is the seam read-after-write is served by now, so a
		// registration gap there is the same class of defect the old assertion guarded against.
		sp.GetService<IEventNotificationBroker>().ShouldNotBeNull();
		sp.GetService<IProjectionRegistry>().ShouldNotBeNull();
		sp.GetService<IProjectionSessionReader>().ShouldNotBeNull();
	}

	/// <summary>
	/// UseEventNotification is idempotent -- multiple calls register services once.
	/// </summary>
	[Fact]
	public void BeIdempotentForMultipleUseEventNotificationCalls()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddSingleton<IProjectionStore<OrderSummary>>(new TestProjectionStore<OrderSummary>());
		services.AddLogging();
		services.AddMetrics();

		var fakeBuilder = A.Fake<IEventSourcingBuilder>();
		A.CallTo(() => fakeBuilder.Services).Returns(services);

		// Act -- call twice
		fakeBuilder.UseEventNotification();
		fakeBuilder.UseEventNotification();

		var sp = services.BuildServiceProvider();

		// Assert -- only one instance registered (TryAddSingleton)
		var brokers = sp.GetServices<IEventNotificationBroker>().ToList();
		brokers.Count.ShouldBe(1);
	}

	/// <summary>
	/// Multiple events are applied in stream order, and the read observes the LAST of them.
	/// </summary>
	[Fact]
	public async Task ApplyMultipleEventsInOrder()
	{
		// Arrange
		using var safety = ProjectionSessionHarness.Safety();
		using var harness = new ProjectionSessionHarness();
		var store = new TestProjectionStore<OrderSummary>();
		_ = harness.Services.AddSingleton<IProjectionStore<OrderSummary>>(store);

		var builder = new ProjectionBuilder<OrderSummary>(harness.Registry);
		_ = builder.Async();
		_ = builder.When<TestOrderPlaced>((proj, e) =>
		{
			proj.Total = e.Amount;
			proj.EventCount++;
		});
		_ = builder.When<TestOrderShipped>((proj, e) =>
		{
			proj.ShippedAt = e.ShippedAt;
			proj.EventCount++;
		});
		builder.Build();

		var shippedAt = DateTimeOffset.UtcNow;
		_ = harness.Append(new TestOrderPlaced { Amount = 75m }, "order-1", version: 1);
		var saved = harness.Append(
			new TestOrderShipped { ShippedAt = shippedAt }, "order-1", version: 2);

		// Act
		var processed = await harness.DriveAsync(safety.Token);
		var read = await harness.ReadAsync<OrderSummary>("order-1", saved, safety.Token);

		// Assert -- both events applied, and the token of the SECOND one is what the read observed.
		processed.EventsApplied.ShouldBe(2);
		var projected = read.Projection.ShouldNotBeNull();
		projected.Total.ShouldBe(75m);
		projected.ShippedAt.ShouldBe(shippedAt);
		projected.EventCount.ShouldBe(2);
		read.Freshness.ShouldBe(ProjectionFreshness.Fresh);
	}

	// -- Test helpers --

	private sealed class OrderingTestHandler : IEventNotificationHandler<TestOrderPlaced>
	{
		private readonly List<string> _order;

		public OrderingTestHandler(List<string> order) => _order = order;

		public Task HandleAsync(
			TestOrderPlaced @event,
			EventNotificationContext context,
			CancellationToken cancellationToken)
		{
			_order.Add("notification-handler");
			return Task.CompletedTask;
		}
	}
}

/// <summary>
/// Drives async projections the way a host with no background service does: append the events a save
/// would have committed, pull them through <see cref="IProjectionProcessor.ProcessPendingAsync"/>, then
/// read a projection back through the <see cref="SaveResult"/> that save returned.
/// </summary>
/// <remarks>
/// Shared by the three session-consistency suites in this namespace. The host and the reader are the real
/// ones; only the event stream and its serializer are doubles, because there is no event store here to
/// append to.
/// </remarks>
internal sealed class ProjectionSessionHarness : IDisposable
{
	// Advances the durable checkpoint after every batch, which is what the session reader waits on. At
	// the shipped default of 100 a one-event test would apply the fold and never publish a mark, and
	// every consistent read would wait out its bound against a projection that was already caught up.
	private readonly GlobalStreamProjectionOptions _options = new() { SubscriptionScope = "session-test", CheckpointInterval = 1 };

	private readonly InMemorySubscriptionCheckpointStore _checkpoints = new();
	private readonly List<StoredEvent> _stream = [];
	private readonly Dictionary<string, IDomainEvent> _events = new(StringComparer.Ordinal);
	private readonly IEventSerializer _serializer;

	private ServiceProvider? _provider;
	private AsyncProjectionProcessingHost? _host;
	private long _position;

	public ProjectionSessionHarness()
	{
		_serializer = A.Fake<IEventSerializer>();
		A.CallTo(() => _serializer.ResolveType(A<string>._))
			.ReturnsLazily((string typeName) => _events[typeName].GetType());

		// The PAYLOAD carries the per-event identity, because DeserializeEvent is handed only the bytes
		// and the resolved type -- there is nowhere else for it to travel.
		A.CallTo(() => _serializer.DeserializeEvent(A<byte[]>._, A<Type>._))
			.ReturnsLazily((byte[] data, Type _) => _events[Encoding.UTF8.GetString(data)]);
	}

	public InMemoryProjectionRegistry Registry { get; } = new();

	public ServiceCollection Services { get; } = new();

	/// <summary>Gets or sets a value indicating whether the container validates scopes.</summary>
	public bool ValidateScopes { get; set; }

	/// <summary>
	/// A REAL-TIME bound on every arm that waits. The session reader polls a real clock, and a fake one
	/// would turn a never-satisfied wait into a spin that hangs the suite instead of failing it.
	/// </summary>
	public static CancellationTokenSource Safety() => new(TimeSpan.FromSeconds(30));

	/// <summary>
	/// Appends one event to the global stream and returns what <c>SaveAsync</c> would have returned for
	/// it: the committed version, and the global position a read must observe to see this write.
	/// </summary>
	/// <remarks>
	/// The aggregate identity and the version are parameters rather than properties read off the event,
	/// because <see cref="IDomainEvent"/> carries neither -- the stored envelope is authoritative for
	/// them, and the apply path reads them from there.
	/// </remarks>
	public SaveResult Append(
		IDomainEvent domainEvent,
		string aggregateId,
		long version,
		string aggregateType = "Order")
	{
		ArgumentNullException.ThrowIfNull(domainEvent);

		var position = ++_position;
		var key = position.ToString(CultureInfo.InvariantCulture);
		_events[key] = domainEvent;
		_stream.Add(new StoredEvent(
			$"evt-{key}",
			aggregateId,
			aggregateType,
			key,
			Encoding.UTF8.GetBytes(key),
			null,
			version,
			domainEvent.OccurredAt)
		{
			GlobalPosition = position,
		});

		return new SaveResult(version, position);
	}

	/// <summary>Applies everything pending and returns, the way a serverless trigger does.</summary>
	public Task<ProjectionProcessingResult> DriveAsync(CancellationToken cancellationToken) =>
		Host.ProcessPendingAsync(cancellationToken);

	/// <summary>Reads a projection, waiting until it has applied the supplied save's token.</summary>
	public ValueTask<ProjectionRead<TProjection>> ReadAsync<TProjection>(
		string projectionId,
		SaveResult session,
		CancellationToken cancellationToken)
		where TProjection : class
	{
		var reader = new ProjectionSessionReader(
			Provider.GetRequiredService<IServiceScopeFactory>(),
			// Short, because a bound that expires is the FAILURE path here: every arm that asserts Fresh
			// has already driven the processor, so a projection that has not caught up is a real defect
			// and waiting five seconds for it only slows the suite down.
			Options.Create(new ProjectionSessionOptions { Bound = TimeSpan.FromSeconds(2) }),
			Options.Create(_options),
			_checkpoints);

		return reader.ReadAsync<TProjection>(projectionId, session, cancellationToken);
	}

	public void Dispose()
	{
		_host?.Dispose();
		_provider?.Dispose();
	}

	private ServiceProvider Provider =>
		_provider ??= Services
			.AddSingleton<IGlobalStreamQuery>(new ListStream(_stream))
			.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = ValidateScopes });

	private AsyncProjectionProcessingHost Host =>
		_host ??= new AsyncProjectionProcessingHost(
			Registry,
			_serializer,
			_checkpoints,
			Options.Create(_options),
			Provider,
			NullLogger<AsyncProjectionProcessingHost>.Instance);

	/// <summary>
	/// The appended events, served from an exclusive cursor the way a real global stream is.
	/// </summary>
	private sealed class ListStream : IGlobalStreamQuery
	{
		private readonly List<StoredEvent> _events;

		public ListStream(List<StoredEvent> events) => _events = events;

		public ValueTask<IReadOnlyList<StoredEvent>> ReadAllAsync(
			GlobalStreamPosition position, int maxCount, CancellationToken cancellationToken) =>
			ValueTask.FromResult<IReadOnlyList<StoredEvent>>(
				[.. _events.Where(e => e.GlobalPosition > position.Position).Take(maxCount)]);

		public ValueTask<IReadOnlyList<StoredEvent>> ReadByEventTypeAsync(
			string eventType,
			GlobalStreamPosition position,
			int maxCount,
			CancellationToken cancellationToken) =>
			ValueTask.FromResult<IReadOnlyList<StoredEvent>>(
				[.. _events
					.Where(e => e.GlobalPosition > position.Position
						&& string.Equals(e.EventType, eventType, StringComparison.Ordinal))
					.Take(maxCount)]);

		// Everything appended here is appended BEFORE the host first runs, so the whole stream is
		// history as far as the host is concerned and every event is delivered as a replay. That is the
		// honest answer for this harness and it keeps the arms independent of when they call Drive.
		public ValueTask<long> GetHeadPositionAsync(CancellationToken cancellationToken) =>
			ValueTask.FromResult(_events.Count == 0 ? 0L : _events[^1].GlobalPosition);
	}
}
