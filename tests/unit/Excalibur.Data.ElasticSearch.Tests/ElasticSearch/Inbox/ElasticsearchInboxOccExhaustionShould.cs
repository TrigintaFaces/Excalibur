// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Net;
using System.Net.Sockets;
using System.Text;

using Excalibur.Inbox.ElasticSearch;

using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Data.Tests.ElasticSearch.Inbox;

/// <summary>
/// An HTTP endpoint that behaves like Elasticsearch for exactly two requests: a document read, which it
/// answers with a document carrying a sequence number, and a conditional write, which it answers with
/// whatever status it was given -- a real <c>409</c> version conflict by default.
/// </summary>
/// <remarks>
/// It sits BELOW the SDK. The store talks to a real, unconnected <see cref="ElasticsearchClient"/>, which
/// parses these responses exactly as it parses a server's, so the store sees the SDK's real response shape
/// and real error classification rather than a stand-in. Nothing here fakes an SDK type.
/// </remarks>
internal sealed class ConflictingElasticsearchEndpoint : IDisposable
{
	private readonly HttpListener _listener = new();
	private readonly int _writeStatus;
	private int _conditionalWrites;

	public ConflictingElasticsearchEndpoint(int writeStatus)
	{
		_writeStatus = writeStatus;
		Uri = new Uri($"http://127.0.0.1:{FreePort()}/");
		_listener.Prefixes.Add(Uri.ToString());
		_listener.Start();
		_ = Task.Run(ServeAsync);
	}

	public Uri Uri { get; }

	/// <summary>Conditional writes the store attempted -- the "exactly N" the arm asserts.</summary>
	public int ConditionalWrites => Volatile.Read(ref _conditionalWrites);

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
			var isConditionalWrite = request.HttpMethod is "PUT" or "POST"
				&& request.QueryString["if_seq_no"] is not null;

			string body;
			int status;
			if (isConditionalWrite)
			{
				_ = Interlocked.Increment(ref _conditionalWrites);
				status = _writeStatus;
				body = status == 409
					? """{"error":{"root_cause":[{"type":"version_conflict_engine_exception","reason":"version conflict"}],"type":"version_conflict_engine_exception","reason":"version conflict"},"status":409}"""
					: """{"_index":"inbox","_id":"doc","_version":2,"result":"updated","_seq_no":8,"_primary_term":1,"_shards":{"total":1,"successful":1,"failed":0}}""";
			}
			else
			{
				status = 200;
				body = """{"_index":"inbox","_id":"doc","_version":1,"_seq_no":7,"_primary_term":1,"found":true,"_source":{"messageId":"m","handlerType":"h","messageType":"t","receivedAt":"2026-01-01T00:00:00+00:00","status":0,"retryCount":0}}""";
			}

			var bytes = Encoding.UTF8.GetBytes(body);
			context.Response.StatusCode = status;
			context.Response.ContentType = "application/json";

			// The v8 client refuses to talk to a server that does not identify itself as Elasticsearch.
			context.Response.Headers["X-Elastic-Product"] = "Elasticsearch";
			context.Response.ContentLength64 = bytes.Length;
			await context.Response.OutputStream.WriteAsync(bytes).ConfigureAwait(false);
			context.Response.Close();
		}
	}
}

/// <summary>
/// The Elasticsearch inbox store's handling of optimistic-concurrency EXHAUSTION on mark-failed.
/// </summary>
/// <remarks>
/// <para>
/// <b>What this proves, and what it does not.</b> It proves the STORE's loop: given N consecutive version
/// conflicts at a bound of N, the store reports <see cref="InboxMarkFailedOutcome.Undecided"/>, does not
/// throw, and attempted exactly N conditional writes. It proves NOTHING about whether a real server
/// produces those conflicts under contention -- the conflicts here are supplied, deterministically, by
/// the endpoint. That server behaviour is the real-infrastructure lost-race arms' job, and a green here
/// must not be read as discharging it.
/// </para>
/// <para>
/// "Exactly N attempts" is what keeps the arm honest: it reddens a loop that exits early, retries forever,
/// or never issues the write -- all of which would still end in some outcome.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class ElasticsearchInboxOccExhaustionShould
{
	private const int Bound = 3;

	[Fact]
	public async Task ReportUndecided_AfterExactlyTheBoundOfConflictingWrites_AndNotThrow()
	{
		using var endpoint = new ConflictingElasticsearchEndpoint(writeStatus: 409);
		var store = CreateStore(endpoint.Uri);

		var outcome = await store.MarkFailedAsync("m", "h", "transient failure", CancellationToken.None)
			.ConfigureAwait(false);

		outcome.ShouldBe(
			InboxMarkFailedOutcome.Undecided,
			"contention that outlasts the bound is reported as Undecided so the caller can re-drive it");
		endpoint.ConditionalWrites.ShouldBe(
			Bound,
			"the store must attempt the conditional write exactly once per permitted attempt -- fewer means "
			+ "it gave up early, more means the bound is not the bound");
	}

	/// <summary>
	/// CONTROL for the fixture: the same endpoint accepting the write yields Applied after ONE attempt. Without
	/// this, the arm above could be green because the endpoint never behaved like a server at all.
	/// </summary>
	[Fact]
	public async Task ReportApplied_AfterOneWrite_WhenTheWriteIsAccepted()
	{
		using var endpoint = new ConflictingElasticsearchEndpoint(writeStatus: 200);
		var store = CreateStore(endpoint.Uri);

		var outcome = await store.MarkFailedAsync("m", "h", "transient failure", CancellationToken.None)
			.ConfigureAwait(false);

		outcome.ShouldBe(InboxMarkFailedOutcome.Applied);
		endpoint.ConditionalWrites.ShouldBe(1, "an accepted write must not be retried");
	}

	private static ElasticsearchInboxStore CreateStore(Uri endpoint)
	{
		var client = new ElasticsearchClient(new ElasticsearchClientSettings(endpoint));
		var options = Options.Create(new ElasticsearchInboxOptions
		{
			IndexName = "inbox",
			MaxConcurrencyRetries = Bound,
		});

		var tenant = A.Fake<ITenantContext>();
		_ = A.CallTo(() => tenant.TenantId).Returns("tenant-1");
		_ = A.CallTo(() => tenant.HasTenant).Returns(true);

		return new ElasticsearchInboxStore(client, options, NullLogger<ElasticsearchInboxStore>.Instance, tenant);
	}
}
