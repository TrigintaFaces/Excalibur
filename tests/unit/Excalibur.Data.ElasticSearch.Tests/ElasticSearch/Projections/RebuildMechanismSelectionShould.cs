// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;

using Elastic.Clients.Elasticsearch;

using Excalibur.Data.ElasticSearch;
using Excalibur.Data.ElasticSearch.IndexManagement;
using Excalibur.Data.ElasticSearch.Projections;
using Excalibur.EventSourcing.Projections;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SalesProjection = Excalibur.Data.ElasticSearch.Tests.OptionsIdentity.Sales.EmployeeProjection;
using SupportProjection = Excalibur.Data.ElasticSearch.Tests.OptionsIdentity.Support.EmployeeProjection;

namespace Excalibur.Data.Tests.ElasticSearch.Projections;

/// <summary>
/// A projection whose current generation carries a field no stored document has ever had.
/// </summary>
internal sealed class RefoldOrder
{
	public string Id { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the field the shape change ADDS. No source document carries it.
	/// </summary>
	/// <value> The field the shape change adds. </value>
	public string Status { get; set; } = string.Empty;
}

/// <summary>
/// Stands in for an ElasticSearch cluster, and models the ONE property that separates the two rebuild
/// mechanisms: a copy's destination is a function of the source documents, and nothing else.
/// </summary>
/// <remarks>
/// The source documents here carry no <c> status </c> field, so when this endpoint serves a reindex it
/// copies exactly that -- documents without the field. That is not a limitation of the fixture; it is the
/// defect the re-fold path exists to fix, reproduced at the only place a copy can get its content from.
/// </remarks>
internal sealed class RebuildClusterEndpoint : IDisposable
{
	private const string SourceDocumentWithoutTheNewField = """{"id":"o1"}""";

	private readonly HttpListener _listener = new();

	public RebuildClusterEndpoint()
	{
		Uri = new Uri($"http://127.0.0.1:{FreePort()}/");
		_listener.Prefixes.Add(Uri.ToString());
		_listener.Start();
		_ = Task.Run(ServeAsync);
	}

	public Uri Uri { get; }

	/// <summary>
	/// Gets every path the client asked for, so "did a reindex happen" is read off the wire.
	/// </summary>
	/// <value> Every path the client asked for. </value>
	public ConcurrentQueue<string> Paths { get; } = new();

	/// <summary>
	/// Gets the documents that ended up in the destination, however they got there.
	/// </summary>
	/// <value> The documents that ended up in the destination. </value>
	public ConcurrentQueue<string> Destination { get; } = new();

	/// <summary>
	/// Gets the bodies written to the rebuild-operations index, newest last.
	/// </summary>
	/// <value> The bodies written to the rebuild-operations index. </value>
	public ConcurrentQueue<string> OperationWrites { get; } = new();

    public ConcurrentQueue<string> SearchBodies { get; } = new();

	/// <summary>
	/// Gets anything that went wrong inside the fixture, so a fixture fault is reported rather than
	/// mistaken for the behaviour under test.
	/// </summary>
	/// <value> Anything that went wrong inside the fixture. </value>
	public ConcurrentQueue<string> Failures { get; } = new();

	public void Dispose() => _listener.Close();

	/// <summary>
	/// Waits for a condition the background rebuild produces, rather than for a duration.
	/// </summary>
	public async Task WaitForAsync(Func<bool> condition, string because)
	{
		for (var attempt = 0; attempt < 400; attempt++)
		{
			if (condition())
			{
				return;
			}

			await Task.Delay(25).ConfigureAwait(false);
		}

		var fixtureFaults = Failures.IsEmpty ? "none" : string.Join("; ", Failures);
		throw new TimeoutException(
			$"The rebuild never reached the expected state: {because}. Fixture faults: {fixtureFaults}. "
			+ $"Paths: {string.Join(" | ", Paths)}. Operation writes: {string.Join(" || ", OperationWrites)}");
	}

	private static int FreePort()
	{
		using var probe = new TcpListener(IPAddress.Loopback, 0);
		probe.Start();
		return ((IPEndPoint)probe.LocalEndpoint).Port;
	}

	private async Task ServeAsync()
	{
		while (_listener.IsListening)
		{
			HttpListenerContext context;
			try
			{
				context = await _listener.GetContextAsync().ConfigureAwait(false);
			}
			catch (Exception ex) when (ex is HttpListenerException or ObjectDisposedException)
			{
				return;
			}

			// NEVER let one request kill the loop. A fixture that stops serving does not fail a test --
			// the client waits for a response that will never come, and the whole run hangs with no
			// output at all, which is a far more expensive way to learn the fixture is wrong.
			try
			{
				await HandleAsync(context).ConfigureAwait(false);
			}
			catch (Exception ex) when (ex is not (HttpListenerException or ObjectDisposedException))
			{
				Failures.Enqueue(ex.ToString());
				try
				{
					context.Response.Abort();
				}
				catch (Exception abortFailure) when (abortFailure is not OutOfMemoryException)
				{
					// Nothing useful remains to do with a response the client has already abandoned.
				}
			}
		}
	}

	private async Task HandleAsync(HttpListenerContext context)
	{
		var path = context.Request.Url?.AbsolutePath ?? "/";
		Paths.Enqueue(path);

		// The client gzips what it sends, so reading the raw stream yields bytes rather than the document.
		var requestStream = string.Equals(
			context.Request.Headers["Content-Encoding"],
			"gzip",
			StringComparison.OrdinalIgnoreCase)
			? new System.IO.Compression.GZipStream(
				context.Request.InputStream,
				System.IO.Compression.CompressionMode.Decompress)
			: context.Request.InputStream;

		var body = await new StreamReader(requestStream, Encoding.UTF8)
			.ReadToEndAsync()
			.ConfigureAwait(false);

		if (path.Contains("rebuild-operations", StringComparison.Ordinal)
			&& context.Request.HttpMethod is "PUT" or "POST"
			&& body.Length > 0)
		{
			OperationWrites.Enqueue(body);
		}

        if (path.EndsWith("_search", StringComparison.Ordinal)) { SearchBodies.Enqueue(body); }

		// A COPY CAN ONLY MOVE WHAT THE SOURCE HOLDS.
		if (path.Contains("_reindex", StringComparison.Ordinal))
		{
			Destination.Enqueue(SourceDocumentWithoutTheNewField);
		}

		context.Response.StatusCode = 200;
		context.Response.ContentType = "application/json";
		context.Response.Headers["X-Elastic-Product"] = "Elasticsearch";

		// A HEAD carries no body, and writing one to it throws -- which is how an index-exists probe
		// took the whole fixture down.
		if (string.Equals(context.Request.HttpMethod, "HEAD", StringComparison.Ordinal))
		{
			context.Response.ContentLength64 = 0;
			context.Response.Close();
			return;
		}

		var payload = path.EndsWith("_count", StringComparison.Ordinal)
			? """{"count":1,"_shards":{"total":1,"successful":1,"skipped":0,"failed":0}}"""
			: "{}";

		var bytes = Encoding.UTF8.GetBytes(payload);
		context.Response.ContentLength64 = bytes.Length;
		await context.Response.OutputStream.WriteAsync(bytes).ConfigureAwait(false);
		context.Response.Close();
	}
}

/// <summary>
/// Stands in for the event re-fold, and writes the shape a copy cannot produce.
/// </summary>
internal sealed class RecordingRefoldService : IProjectionRebuildService
{
	private const string RefoldedDocumentWithTheNewField = """{"id":"o1","status":"Shipped"}""";

	private readonly ConcurrentQueue<string> _destination;
	private readonly ProjectionRebuildState _finishesIn;

	public RecordingRefoldService(ConcurrentQueue<string> destination, ProjectionRebuildState finishesIn)
	{
		_destination = destination;
		_finishesIn = finishesIn;
	}

	public int RebuildCalls { get; private set; }

	public Task RebuildAsync<TProjection>(CancellationToken cancellationToken)
		where TProjection : class, new()
	{
		RebuildCalls++;

		// The fold produces the current generation's shape, which is where the new field comes from.
		if (_finishesIn == ProjectionRebuildState.Completed)
		{
			_destination.Enqueue(RefoldedDocumentWithTheNewField);
		}

		return Task.CompletedTask;
	}

	public Task<EventSourcing.Projections.ProjectionRebuildStatus> GetStatusAsync<TProjection>(
		CancellationToken cancellationToken)
		where TProjection : class =>
		Task.FromResult(new EventSourcing.Projections.ProjectionRebuildStatus(
			typeof(TProjection).Name,
			_finishesIn,
			Progress: 100,
			LastRebuiltAt: DateTimeOffset.UtcNow));

	public Task<IReadOnlyList<EventSourcing.Projections.ProjectionRebuildStatus>> GetAllStatusesAsync(
		CancellationToken cancellationToken) =>
		Task.FromResult<IReadOnlyList<EventSourcing.Projections.ProjectionRebuildStatus>>([]);
}

/// <summary>
/// A rebuild must take the mechanism that CAN serve the change being made, and the API must leave the
/// consumer no way to pick the one that cannot.
/// </summary>
/// <remarks>
/// <para>
/// The rebuild counted documents in a source index and copied them, which is the pattern a managed
/// search service offers because a search service has nothing else to read. It therefore cannot populate
/// a field that was never in the source documents -- exactly the property-rename a consumer hits -- and
/// it reported success while doing so.
/// </para>
/// <para>
/// Event sourcing can do better: re-fold from the stream, where a new generation may contain fields no
/// previous document carried. Both mechanisms are kept, because a copy remains much cheaper and is
/// correct for a move that does not change shape.
/// </para>
/// </remarks>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Data)]
[Trait(TraitNames.Pattern, TestPatterns.Service)]
public sealed class RebuildMechanismSelectionShould
{
    [Fact]
    [Trait("Pattern", "Regression")]
    public async Task DistinguishSameNamedProjectionRecordsAndFilterByTheirFullTypeName()
    {
        using var cluster = new RebuildClusterEndpoint();
        var manager = CreateManager(cluster, new RecordingRefoldService(cluster.Destination, ProjectionRebuildState.Completed));
        typeof(SalesProjection).Name.ShouldBe(typeof(SupportProjection).Name);
        var requests = new[]
        {
            ProjectionRebuildRequest.ForEventRefold<SalesProjection>(),
            ProjectionRebuildRequest.ForEventRefold<SupportProjection>(),
        };
        foreach (var request in requests)
        {
            (await manager.StartRebuildAsync(request, CancellationToken.None)).Started.ShouldBeTrue();
        }
        foreach (var request in requests)
        {
            var name = request.ProjectionType.FullName!;
            await cluster.WaitForAsync(() => cluster.OperationWrites.Any(body =>
            {
                using var document = System.Text.Json.JsonDocument.Parse(body);
                return document.RootElement.TryGetProperty("projectionType", out var type) && type.GetString() == name;
            }), "a rebuild record did not identify its full projection type");
            _ = await manager.ListRebuildOperationsAsync(DateTime.UtcNow.AddDays(-1), DateTime.UtcNow,
                name, CancellationToken.None);
            cluster.SearchBodies.ShouldContain(body => body.Contains(name, StringComparison.Ordinal));
        }
    }

	/// <summary>
	/// SAFETY. The whole point of the second mechanism: a field that is in NO source document.
	/// </summary>
	[Fact]
	public async Task PopulateAFieldNoSourceDocumentCarries()
	{
		using var cluster = new RebuildClusterEndpoint();
		var refold = new RecordingRefoldService(cluster.Destination, ProjectionRebuildState.Completed);
		var manager = CreateManager(cluster, refold);

		var result = await manager
			.StartRebuildAsync(ProjectionRebuildRequest.ForEventRefold<RefoldOrder>(), CancellationToken.None)
			.ConfigureAwait(true);

		result.Started.ShouldBeTrue();
		await cluster.WaitForAsync(
				() => !cluster.Destination.IsEmpty,
				"the re-fold never wrote a document")
			.ConfigureAwait(true);

		cluster.Destination.ShouldContain(
			document => document.Contains("\"status\"", StringComparison.Ordinal),
			"a re-fold reads the event stream, so the documents it writes carry the current generation's "
			+ "fields -- including one no stored document has ever had");

		cluster.Paths.ShouldNotContain(
			path => path.Contains("_reindex", StringComparison.Ordinal),
			"a shape change must not be served by copying the documents that lack the new field");
	}

	/// <summary>
	/// SAFETY. The impossible selection is refused: a copy with nothing to copy from used to create an
	/// empty index, swap the alias onto it and report success.
	/// </summary>
	[Fact]
	public async Task RefuseACopyThatHasNoSourceToCopyFrom()
	{
		using var cluster = new RebuildClusterEndpoint();
		var manager = CreateManager(cluster, new RecordingRefoldService(
			cluster.Destination,
			ProjectionRebuildState.Completed));

		// The only request shape a consumer can hand-build that selects a mechanism able to populate
		// nothing. It is well formed, so a signature cannot reject it -- the call does.
		var result = await manager
			.StartRebuildAsync(
				new ProjectionRebuildRequest { ProjectionType = typeof(RefoldOrder), TargetIndexName = "orders-v2" },
				CancellationToken.None)
			.ConfigureAwait(true);

		result.Started.ShouldBeFalse("a copy with no source index can populate nothing");
		result.Message.ShouldNotBeNull();
		result.Message.ShouldContain(nameof(ProjectionRebuildRequest.ForEventRefold));
		cluster.Destination.ShouldBeEmpty();
	}

	/// <summary>
	/// SAFETY. The re-fold is reachable ONLY through the factory that states a shape change, and that
	/// factory has no argument in which to name a source index -- so "copy an index, but for a shape
	/// change" has no expression.
	/// </summary>
	[Fact]
	public void LeaveTheShapeChangeFactoryTheOnlyWayToReachAReFold()
	{
		ProjectionRebuildRequest.ForEventRefold<RefoldOrder>().Mechanism
			.ShouldBe(ProjectionRebuildMechanism.EventRefold);

		ProjectionRebuildRequest.ForEventRefold<RefoldOrder>().SourceIndexName
			.ShouldBeNull("a re-fold reads no documents, so there is nowhere to put a source index");

		ProjectionRebuildRequest.ForIndexMove(typeof(RefoldOrder), "orders-v1", "orders-v2").Mechanism
			.ShouldBe(
				ProjectionRebuildMechanism.IndexCopy,
				"CONTROL: the move factory resolves to the other mechanism, so the arm above is detecting "
				+ "the factory rather than one hardcoded answer");

		// The binding that selects the re-fold is not public, so no publicly constructible request can
		// pair it with a source index. This reddens the moment someone widens it.
		typeof(ProjectionRebuildRequest)
			.GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)
			.ShouldNotContain(
				property => property.PropertyType.IsGenericType
					&& property.PropertyType.GetGenericTypeDefinition() == typeof(Func<,,>),
				"the re-fold binding must stay internal, or the impossible combination becomes expressible");

		typeof(ProjectionRebuildRequest).GetProperty(nameof(ProjectionRebuildRequest.Mechanism))!
			.SetMethod
			.ShouldBeNull("the mechanism is derived from the data, never set beside it");
	}

	/// <summary>
	/// LIVENESS. The shipped copy path is unchanged and still correct for a like-for-like move, which is
	/// the far cheaper mechanism and the right answer whenever the documents do not change shape.
	/// </summary>
	[Fact]
	public async Task CopyDocumentsForALikeForLikeMove()
	{
		using var cluster = new RebuildClusterEndpoint();
		var manager = CreateManager(cluster, new RecordingRefoldService(
			cluster.Destination,
			ProjectionRebuildState.Completed));

		var result = await manager
			.StartRebuildAsync(
				ProjectionRebuildRequest.ForIndexMove(typeof(RefoldOrder), "orders-v1", "orders-v2"),
				CancellationToken.None)
			.ConfigureAwait(true);

		result.Started.ShouldBeTrue();
		await cluster.WaitForAsync(
				() => cluster.Paths.Any(path => path.Contains("_reindex", StringComparison.Ordinal)),
				"the copy never issued a reindex")
			.ConfigureAwait(true);

		cluster.Destination.ShouldNotBeEmpty();
		cluster.Destination.ShouldNotContain(
			document => document.Contains("\"status\"", StringComparison.Ordinal),
			"a copy's destination is a function of the source documents, which carry no such field -- "
			+ "this is what makes the copy path wrong for a shape change and right for a move");
	}

	/// <summary>
	/// LIVENESS. A re-fold that did not complete is never reported as one that did, so the cursor and the
	/// documents cannot be described as rebuilt together when they were not.
	/// </summary>
	/// <remarks>
	/// The re-fold releases this projection's subscription member BEFORE replaying, so the documents and
	/// the cursor are rebuilt together; that release is the rebuild service's own property and is covered
	/// where it lives. What the manager owns is the honest report: the replay can decline without
	/// throwing, so its finishing state is read back rather than assumed.
	/// </remarks>
	[Fact]
	public async Task NotReportAReFoldThatDeclinedAsOneThatCompleted()
	{
		using var cluster = new RebuildClusterEndpoint();
		var declines = new RecordingRefoldService(cluster.Destination, ProjectionRebuildState.Failed);
		var manager = CreateManager(cluster, declines);

		var result = await manager
			.StartRebuildAsync(ProjectionRebuildRequest.ForEventRefold<RefoldOrder>(), CancellationToken.None)
			.ConfigureAwait(true);

		result.Started.ShouldBeTrue();
		await cluster.WaitForAsync(
				() => cluster.OperationWrites.Any(
					body => body.Contains("finished in state", StringComparison.Ordinal)),
				"the declined re-fold was never recorded as a failure")
			.ConfigureAwait(true);

		declines.RebuildCalls.ShouldBe(1);
		cluster.Destination.ShouldBeEmpty("nothing was folded, so nothing should have been written");
	}

	/// <summary>
	/// LIVENESS. The real container resolves the manager in BOTH supported shapes, because the re-fold
	/// service is registered separately and a host that only moves indexes never registers it.
	/// </summary>
	/// <remarks>
	/// Container-selected construction cannot express an optional dependency -- it picks the greediest
	/// constructor whose parameters it can all resolve, and throws otherwise -- so the registration builds
	/// the manager by hand. This arm is what makes that load-bearing rather than incidental.
	/// </remarks>
	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public void ResolveFromRealDependencyInjectionWhetherOrNotAReFoldServiceIsRegistered(
		bool registerRefoldService)
	{
		var services = new Microsoft.Extensions.DependencyInjection.ServiceCollection();
#pragma warning disable CA2000 // Owned by the container for the lifetime of this arm.
		services.AddSingleton(new ElasticsearchClient(
			new ElasticsearchClientSettings(new Uri("http://localhost:9200"))));
#pragma warning restore CA2000
		_ = services.AddLogging();
		_ = services.AddMetrics();

		if (registerRefoldService)
		{
			services.AddSingleton<IProjectionRebuildService>(
				new RecordingRefoldService(new ConcurrentQueue<string>(), ProjectionRebuildState.Completed));
		}

		var configuration = new Microsoft.Extensions.Configuration.ConfigurationBuilder().Build();
		_ = services.AddElasticsearchIndexManagement(configuration);
		_ = services.AddElasticSearchProjectionManagement(configuration);

		using var provider = services.BuildServiceProvider();

		provider.GetService<IProjectionRebuildManager>().ShouldNotBeNull(
			"the manager must resolve whether or not the optional re-fold service is present");
	}

	private static ProjectionRebuildManager CreateManager(
		RebuildClusterEndpoint cluster,
		IProjectionRebuildService refoldService)
	{
		var storeOptions = new ElasticSearchProjectionStoreOptions { NodeUri = cluster.Uri.ToString() };

		var monitor = A.Fake<IOptionsMonitor<ElasticSearchProjectionStoreOptions>>();
		A.CallTo(() => monitor.Get(A<string>._)).Returns(storeOptions);

		return new ProjectionRebuildManager(
			new ElasticsearchClient(new ElasticsearchClientSettings(cluster.Uri)
				// Bounded, so a fixture that stops answering fails the arm instead of hanging the run.
				.RequestTimeout(TimeSpan.FromSeconds(5))
				.MaximumRetries(0)),
			A.Fake<IIndexAliasManager>(),
			Options.Create(new ElasticSearchProjectionManagementOptions()),
			monitor,
			NullLogger<ProjectionRebuildManager>.Instance,
			refoldService);
	}
}
