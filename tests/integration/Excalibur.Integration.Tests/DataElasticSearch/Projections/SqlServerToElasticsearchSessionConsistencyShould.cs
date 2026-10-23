// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Globalization;
using System.Reflection;

using Excalibur.Dispatch;
using Excalibur.EventSourcing;
using Excalibur.EventSourcing.DependencyInjection;
using Excalibur.EventSourcing.Projections;
using Excalibur.EventSourcing.Queries;
using Excalibur.EventSourcing.SqlServer;
using Excalibur.Integration.Tests.Data.EventStore;

using Microsoft.Extensions.DependencyInjection;

using Shouldly;

using Tests.Shared.Fixtures;

namespace Excalibur.Integration.Tests.DataElasticSearch.Projections;

/// <summary>
/// The end-to-end pipeline a real deployment runs: a SQL Server event store, the projection host, an
/// Elasticsearch projection store, and a session-token read.
/// </summary>
/// <remarks>
/// <para>
/// <b>This configuration is the one that was never tested.</b> The append harnesses measure the event
/// store; the Elasticsearch conformance tests measure the projection store. Nothing measured them WIRED
/// TOGETHER, which is the only shape in which the defect a consumer actually hit can occur — the events
/// in SQL Server, the projection in Elasticsearch, and no transaction spanning the two.
/// </para>
/// <para>
/// <b>Both halves must be real.</b> The properties here are the stores' own: Elasticsearch's refresh
/// semantics decide when a written document becomes searchable, and SQL Server's allocation decides what
/// position the token carries. A fake returns what it was told and would certify all of this while the
/// real pair failed.
/// </para>
/// </remarks>
[Trait("Category", "Integration")]
[Trait("Component", "Projections")]
[Trait("Database", "SqlServer")]
public sealed class SqlServerToElasticsearchSessionConsistencyShould
	: IClassFixture<SqlServerEventStoreContainerFixture>, IClassFixture<ElasticsearchContainerFixture>
{
	// Past CheckpointInterval (100), so the durable checkpoint is written at least once during the run
	// and the read's max(in-process mark, durable checkpoint) crosses a REAL boundary rather than being
	// served by the in-process half alone.
	private const int EventsPastACheckpoint = 150;

	private readonly SqlServerEventStoreContainerFixture _sql;
	private readonly ElasticsearchContainerFixture _elastic;

	public SqlServerToElasticsearchSessionConsistencyShould(
		SqlServerEventStoreContainerFixture sql,
		ElasticsearchContainerFixture elastic)
	{
		_sql = sql;
		_elastic = elastic;
	}

	[Fact]
	public async Task ObserveAWriteThroughTheSessionTokenAfterTheProcessorRuns()
	{
		// THE REPLACEMENT FOR INLINE PROJECTIONS, end to end. The save returns a token; the processor
		// applies; the read carrying that token must see the write and say so. Asserting the data alone
		// would pass for a row that happened to exist from an earlier run -- Freshness is what binds the
		// read to THIS write.
		await using var host = await BuildAsync();

		// The aggregate id is retained, because the projection is keyed by it. Reading some other key --
		// the session token rendered as a string, as this arm used to -- requests a row that does not
		// exist, and Freshness follows the SUBSCRIPTION's progress rather than the row, so it reports
		// Fresh for a projection that was never there.
		var aggregateId = NewId();
		var token = await AppendAsync(host, aggregateId, count: 3);
		token.SessionToken.ShouldNotBeNull("SQL Server assigns global positions, so a token must exist");

		var processed = await host.Processor.ProcessPendingAsync(host.Cancellation);
		processed.Outcome.ShouldBe(ProjectionProcessingOutcome.CaughtUp);

		var read = await host.Reader.ReadAsync<LoadProjection>(aggregateId, token, host.Cancellation);

		read.Freshness.ShouldBe(
			ProjectionFreshness.Fresh,
			"the projection had applied this write's position before the read returned");

		// THE DATA, not just the progress. Freshness alone says the subscription reached the token; it
		// says nothing about whether this row exists or holds the fold. Asserting only progress is how a
		// read of an absent row passed as an end-to-end success.
		read.Projection.ShouldNotBeNull(
			"a Fresh read must return the projection, not merely report that the subscription caught up");
		read.Projection.Id.ShouldBe(
			aggregateId,
			"the id the handler folded into the body must survive the Elasticsearch round trip -- the "
			+ "store keys the document out of band, so a body that lost it reads back as a different row");
		read.Projection.Count.ShouldBe(3, "all three appended events must be folded into the row");
	}

	[Fact]
	public async Task ReportBoundExpiredRatherThanFreshWhenNothingHasApplied()
	{
		// The arm that keeps the one above honest. Without driving the processor, the projection has NOT
		// reached the token, and the read must say so rather than reporting Fresh for a row it never
		// confirmed. An implementation that always returned Fresh would pass every other arm here.
		await using var host = await BuildAsync(bound: TimeSpan.FromMilliseconds(250));

		var aggregateId = NewId();
		var token = await AppendAsync(host, aggregateId, count: 3);

		var read = await host.Reader.ReadAsync<LoadProjection>(aggregateId, token, host.Cancellation);

		read.Freshness.ShouldBe(
			ProjectionFreshness.BoundExpired,
			"nothing applied the write, so the read must not claim to have observed it");
	}

	[Fact]
	public async Task NotRequestConsistencyWhenNoTokenIsSupplied()
	{
		// A caller who does not ask pays nothing: no wait, and no claim about currency.
		await using var host = await BuildAsync();

		var read = await host.Reader.ReadAsync<LoadProjection>(
			NewId(), new SaveResult(0, SessionToken: null), host.Cancellation);

		read.Freshness.ShouldBe(ProjectionFreshness.NotRequested);
	}

	[Fact]
	public async Task CarryTheLastPositionAcrossACheckpointBoundary()
	{
		// 150 events is past CheckpointInterval, so the host persists a durable checkpoint during the
		// run. The token must be the LAST event's position -- the first would let a reader wait for a
		// projection that has seen only the beginning of its own write.
		await using var host = await BuildAsync();

		var aggregateId = NewId();
		var token = await AppendAsync(host, aggregateId, count: EventsPastACheckpoint);

		var processed = await host.Processor.ProcessPendingAsync(host.Cancellation);
		processed.EventsApplied.ShouldBeGreaterThanOrEqualTo(
			EventsPastACheckpoint, "every appended event must reach the projection host");

		var read = await host.Reader.ReadAsync<LoadProjection>(aggregateId, token, host.Cancellation);

		read.Freshness.ShouldBe(ProjectionFreshness.Fresh);

		// THE DATA. A span larger than a checkpoint interval is exactly where a partially applied fold
		// would hide behind a Fresh verdict, because the subscription's mark can pass the token while an
		// individual row lags.
		read.Projection.ShouldNotBeNull();
		read.Projection.Count.ShouldBe(
			EventsPastACheckpoint,
			"every event in a span past the checkpoint interval must be folded into the row");
	}

	[Fact]
	public async Task ReuseOneElasticsearchClientAcrossManySaves()
	{
		// REGRESSION GUARD for the defect that started this work. The projection store is scoped; a store
		// that built its own client built a new HTTP connection pool per scope, and under sustained load
		// the process ran out of ephemeral ports -- surfacing as a cancelled request with no HTTP
		// response. One client per cluster is the fix; this asserts it holds across many resolutions.
		await using var host = await BuildAsync();

		var clients = new HashSet<object>();
		for (var i = 0; i < 25; i++)
		{
			await using var scope = host.Provider.CreateAsyncScope();
			var store = scope.ServiceProvider.GetRequiredService<IProjectionStore<LoadProjection>>();
			var field = store.GetType().GetField("_client", BindingFlags.NonPublic | BindingFlags.Instance);
			field.ShouldNotBeNull("the store is expected to hold its client in a field named _client");
			var client = field!.GetValue(store);
			client.ShouldNotBeNull("the store must already hold a client, not create one lazily per use");
			_ = clients.Add(client!);
		}

		clients.Count.ShouldBe(1, "every scope must share one client -- a client per scope leaks a pool");
	}

	private static string NewId() => "agg-" + Guid.NewGuid().ToString("N");

	private async Task<SaveResult> AppendAsync(PipelineHost host, string aggregateId, int count)
	{
		var events = Enumerable.Range(0, count).Select(i => (IDomainEvent)new LoadEvent
		{
			EventId = Guid.NewGuid().ToString(),
			AggregateId = aggregateId,
			OccurredAt = DateTimeOffset.UtcNow,
			Version = i,
		});

		var result = await host.EventStore.AppendAsync(
			aggregateId, "LoadTest", events, -1, host.Cancellation);

		result.Success.ShouldBeTrue(result.ErrorMessage);
		return new SaveResult(result.NextExpectedVersion ?? 0, result.LastEventPosition);
	}

	private async Task<PipelineHost> BuildAsync(TimeSpan? bound = null)
	{
		await _sql.EnsureInitializedAsync();
		await _sql.CleanupTableAsync();
		_elastic.EnsureAvailable();

		var index = "sessions-" + Guid.NewGuid().ToString("N")[..12];

		var services = new ServiceCollection();
		_ = services.AddLogging();
		// The serializer refuses an unregistered event type rather than scanning assemblies by default,
		// and says so precisely when it halts. A real application registers its events the same way.
		_ = services.AddEventTypes<LoadEvent>();
		_ = services.AddElasticSearchProjections(
			_elastic.ConnectionString,
			registrar => registrar.Add<LoadProjection>(o => o.Index.IndexPrefix = index));

		_ = services.AddExcalibur(x => x.AddEventSourcing(es =>
		{
			_ = es.UseSqlServer(sql => sql.ConnectionString(_sql.ConnectionString));
			// THE HANDLER SETS THE KEY, because nothing else does and nothing else should. The apply path
			// creates the state with `new TProjection()` (ProjectionBuilder.cs:653) and writes the id
			// alongside it -- SqlServerProjectionStore stores it in its own Id column and reads back only
			// `Data`, so a key the handler did not put in the body does not come back in the body. That is
			// deliberate: the framework touches a projection's properties only through delegates the
			// consumer supplies (see WithSearchText, which takes BOTH a getter and a setter), because
			// reflecting on a property named Id would not survive trimming. Asserting Id below therefore
			// measures the store round-trip of a value the consumer owns, which is a real property; it is
			// not a claim that the framework populates identity.
			_ = es.AddProjection<LoadProjection>(p => p
				.Async()
				.When<LoadEvent>((proj, e) =>
				{
					proj.Id = e.AggregateId;
					proj.Count++;
				}));
			_ = es.EnableProjectionProcessing(
				opts => opts.SubscriptionScope = "sqlserver-to-elasticsearch-session-consistency");
		}));

		if (bound is { } configured)
		{
			_ = services.Configure<ProjectionSessionOptions>(o => o.Bound = configured);
		}

		var provider = services.BuildServiceProvider();
		return new PipelineHost(provider);
	}

	private sealed class PipelineHost(ServiceProvider provider) : IAsyncDisposable
	{
		public ServiceProvider Provider { get; } = provider;

		// RESOLVED FOR ITS CONSTRUCTOR SIDE EFFECT, which is a real wart worth naming rather than
		// working around silently. AddProjection defers its registry write to an IConfigureProjection
		// that runs in the EventNotificationBroker's CONSTRUCTOR, so until something resolves the broker
		// the registry is EMPTY and the projection host finds nothing to apply. A real application hits
		// this for free -- the repository resolves the broker on its first save -- but any host that
		// drives the processor without saving through a repository first gets a processor that
		// cheerfully reports CaughtUp having applied nothing.
		public IEventNotificationBroker Broker { get; } =
			provider.GetRequiredService<IEventNotificationBroker>();

		public IEventStore EventStore { get; } = provider.GetRequiredService<IEventStore>();

		public IProjectionProcessor Processor { get; } = provider.GetRequiredService<IProjectionProcessor>();

		public IProjectionSessionReader Reader { get; } =
			provider.GetRequiredService<IProjectionSessionReader>();

		// Real time, so a wait that never terminates FAILS rather than hanging the suite.
		public CancellationToken Cancellation { get; } = new CancellationTokenSource(TimeSpan.FromMinutes(2)).Token;

		public ValueTask DisposeAsync() => Provider.DisposeAsync();
	}

	private sealed class LoadProjection
	{
		public string Id { get; set; } = string.Empty;

		public int Count { get; set; }
	}

	[MessageName("Test.SqlServerToElasticsearchSessionConsistency.LoadEvent")]
	private sealed class LoadEvent : IDomainEvent
	{
		public required string EventId { get; init; }

		public required string AggregateId { get; init; }

		public required DateTimeOffset OccurredAt { get; init; }

		public required long Version { get; init; }

		public IDictionary<string, object>? Metadata { get; init; }
	}
}
