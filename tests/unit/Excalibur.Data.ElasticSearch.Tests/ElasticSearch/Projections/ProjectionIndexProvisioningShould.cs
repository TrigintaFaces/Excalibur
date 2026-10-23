// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

using Elastic.Clients.Elasticsearch;

using Excalibur.Data.ElasticSearch.Projections;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Excalibur.Data.Tests.ElasticSearch.Projections;

internal sealed class ProvisioningProjection
{
	public string Id { get; set; } = string.Empty;

	public string Name { get; set; } = string.Empty;
}

/// <summary>
/// An HTTP endpoint that behaves like Elasticsearch for an index that does not exist: it reports the index
/// absent, answers the create with whatever status and body it was given, and answers a count with zero.
/// </summary>
/// <remarks>
/// It sits BELOW the SDK. The store talks to a real, unconnected <see cref="ElasticsearchClient"/>, which
/// parses these responses exactly as it parses a server's, so the store sees the SDK's real response shape
/// and real error classification rather than a stand-in. Nothing here fakes an SDK type.
/// </remarks>
internal sealed class IndexCreationEndpoint : IDisposable
{
	private readonly HttpListener _listener = new();
	private readonly int _createStatus;
	private readonly string _createBody;
	private int _creates;
	private int _mappingReads;

	public IndexCreationEndpoint(int createStatus, string createBody)
	{
		_createStatus = createStatus;
		_createBody = createBody;
		Uri = new Uri($"http://127.0.0.1:{FreePort()}/");
		_listener.Prefixes.Add(Uri.ToString());
		_listener.Start();
		_ = Task.Run(ServeAsync);
	}

	public Uri Uri { get; }

	/// <summary>Create-index requests the store issued.</summary>
	public int Creates => Volatile.Read(ref _creates);

	public int MappingReads => Volatile.Read(ref _mappingReads);

	public void Dispose() => _listener.Close();

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

			var request = context.Request;
			var path = request.Url?.AbsolutePath ?? "/";

			string body;
			int status;
			if (request.HttpMethod == "HEAD")
			{
				// The index does not exist, which is what makes the create path reachable.
				status = 404;
				body = string.Empty;
			}
			else if (request.HttpMethod == "PUT")
			{
				_ = Interlocked.Increment(ref _creates);
				status = _createStatus;
				body = _createBody;
			}
			else if (request.HttpMethod == "GET" && path.EndsWith("/_mapping", StringComparison.Ordinal))
			{
				_ = Interlocked.Increment(ref _mappingReads);
				status = 200;
				// GET _mapping is keyed by the concrete index, with mappings.properties beneath it.
				// These are the independently specified fields of ProvisioningProjection plus the
				// framework's reserved positioned-write metadata, not an empty success response.
				var index = path.Split('/', StringSplitOptions.RemoveEmptyEntries)[0];
				body = JsonSerializer.Serialize(new Dictionary<string, object>
				{
					[index] = new
					{
						mappings = new
						{
							properties = new
							{
								id = new { type = "keyword" }, name = new { type = "keyword" },
								excaliburProjectionPosition = new { type = "long" }
							}
						}
					}
				});
			}
			else if (path.EndsWith("_count", StringComparison.Ordinal))
			{
				status = 200;
				body = """{"count":0,"_shards":{"total":1,"successful":1,"skipped":0,"failed":0}}""";
			}
			else
			{
				status = 200;
				body = "{}";
			}

			var bytes = Encoding.UTF8.GetBytes(body);
			context.Response.StatusCode = status;
			context.Response.ContentType = "application/json";

			// The v8 client refuses to talk to a server that does not identify itself as Elasticsearch.
			context.Response.Headers["X-Elastic-Product"] = "Elasticsearch";
			context.Response.ContentLength64 = bytes.Length;
			if (bytes.Length > 0)
			{
				await context.Response.OutputStream.WriteAsync(bytes).ConfigureAwait(false);
			}

			context.Response.Close();
		}
	}
}

/// <summary>
/// What the projection store does when provisioning its index fails.
/// </summary>
/// <remarks>
/// <para>
/// A projection index carries the position field, the configured shard and replica counts, the refresh
/// interval and the declared mapping. If the create fails and the store serves anyway, the first write
/// auto-creates the index with a dynamically inferred mapping: every one of those settings is silently
/// discarded and the system reports healthy. Provisioning is not a hot path, so it raises -- which is what
/// every first-party provisioning API does.
/// </para>
/// <para>
/// A create refused because the index already exists is not a failure: two instances starting together
/// both see it absent and both attempt it, and the loser must carry on.
/// </para>
/// </remarks>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Data)]
public sealed class ProjectionIndexProvisioningShould
{
	private const string Rejected =
		"""{"error":{"root_cause":[{"type":"illegal_argument_exception","reason":"unknown setting"}],"type":"illegal_argument_exception","reason":"unknown setting"},"status":400}""";

	private const string AlreadyExists =
		"""{"error":{"root_cause":[{"type":"resource_already_exists_exception","reason":"index already exists"}],"type":"resource_already_exists_exception","reason":"index already exists"},"status":400}""";

	private const string Acknowledged =
		"""{"acknowledged":true,"shards_acknowledged":true,"index":"provisioning"}""";

	[Fact]
	public async Task RaiseWhenTheIndexCannotBeCreated()
	{
		using var endpoint = new IndexCreationEndpoint(createStatus: 400, Rejected);
		var store = CreateStore(endpoint.Uri);

		_ = await Should.ThrowAsync<InvalidOperationException>(
			async () => await store.CountAsync(filters: null, CancellationToken.None).ConfigureAwait(true))
			.ConfigureAwait(true);

		endpoint.Creates.ShouldBe(1, "the store must have attempted the create it then reported as failed");
	}

	/// <summary>
	/// LIVENESS, and the arm that keeps the one above from being satisfied by a store that throws on every
	/// start-up: a create the server refuses because the index is already there must not stop the store.
	/// </summary>
	[Fact]
	public async Task StartWhenTheIndexAlreadyExists()
	{
		using var endpoint = new IndexCreationEndpoint(createStatus: 400, AlreadyExists);
		var store = CreateStore(endpoint.Uri);

		var count = await store.CountAsync(filters: null, CancellationToken.None).ConfigureAwait(true);

		count.ShouldBe(0);
		endpoint.MappingReads.ShouldBe(1, "the store must validate the concrete mapping before serving queries");
	}

	/// <summary>
	/// CONTROL for the fixture: an accepted create reaches the same place. Without it, the liveness arm
	/// above could be green because the endpoint never behaved like a server at all.
	/// </summary>
	[Fact]
	public async Task StartWhenTheCreateIsAccepted()
	{
		using var endpoint = new IndexCreationEndpoint(createStatus: 200, Acknowledged);
		var store = CreateStore(endpoint.Uri);

		var count = await store.CountAsync(filters: null, CancellationToken.None).ConfigureAwait(true);

		count.ShouldBe(0);
		endpoint.MappingReads.ShouldBe(1, "the store must validate the concrete mapping before serving queries");
		endpoint.Creates.ShouldBe(1);
	}

	private static ElasticSearchProjectionStore<ProvisioningProjection> CreateStore(Uri endpoint)
	{
		var options = new ElasticSearchProjectionStoreOptions { NodeUri = endpoint.ToString() };

		var monitor = A.Fake<IOptionsMonitor<ElasticSearchProjectionStoreOptions>>();
		A.CallTo(() => monitor.Get(A<string>._)).Returns(options);

		return new ElasticSearchProjectionStore<ProvisioningProjection>(
			new ElasticsearchClient(new ElasticsearchClientSettings(endpoint)),
			monitor,
			NullLogger<ElasticSearchProjectionStore<ProvisioningProjection>>.Instance);
	}
}
