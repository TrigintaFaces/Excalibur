// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Net;
using System.Net.Http.Headers;

using EsNodeFailoverHandler = Excalibur.AuditLogging.Elasticsearch.NodeFailoverHandler;
using OsNodeFailoverHandler = Excalibur.AuditLogging.OpenSearch.NodeFailoverHandler;

namespace Excalibur.AuditLogging.Elasticsearch.Tests;

/// <summary>
/// Tests for the Elasticsearch and OpenSearch <c>NodeFailoverHandler</c> delegating handlers, which
/// round-robin each outgoing request across the configured cluster nodes (rewriting only the request
/// authority) while preserving the request path, query, and headers. This is the seam that preserves
/// per-attempt node failover after retry moved into the standard resilience pipeline.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class NodeFailoverHandlerShould
{
	private const string Template = "https://template.example.com:9200/_bulk?refresh=false";

	[Fact]
	public async Task RoundRobinAcrossNodesOnSuccessiveSends_Elasticsearch()
	{
		Uri[] nodes =
		[
			new("https://node1.example.com:9200"),
			new("https://node2.example.com:9200"),
			new("https://node3.example.com:9200"),
		];
		using var inner = new FakeHttpMessageHandler();
		inner.SetResponse(HttpStatusCode.OK);
		using var handler = new EsNodeFailoverHandler(nodes) { InnerHandler = inner };
		using var invoker = new HttpMessageInvoker(handler, disposeHandler: false);

		var hosts = await SendNAsync(invoker, inner, 6).ConfigureAwait(false);

		// Liveness: every node is reached, cycling in order across successive sends.
		hosts.ShouldBe(
		[
			"node1.example.com", "node2.example.com", "node3.example.com",
			"node1.example.com", "node2.example.com", "node3.example.com",
		]);
	}

	[Fact]
	public async Task RoundRobinAcrossNodesOnSuccessiveSends_OpenSearch()
	{
		Uri[] nodes =
		[
			new("https://node1.example.com:9200"),
			new("https://node2.example.com:9200"),
			new("https://node3.example.com:9200"),
		];
		using var inner = new FakeHttpMessageHandler();
		inner.SetResponse(HttpStatusCode.OK);
		using var handler = new OsNodeFailoverHandler(nodes) { InnerHandler = inner };
		using var invoker = new HttpMessageInvoker(handler, disposeHandler: false);

		var hosts = await SendNAsync(invoker, inner, 6).ConfigureAwait(false);

		hosts.ShouldBe(
		[
			"node1.example.com", "node2.example.com", "node3.example.com",
			"node1.example.com", "node2.example.com", "node3.example.com",
		]);
	}

	[Fact]
	public async Task AlwaysTargetTheSingleNode_Elasticsearch()
	{
		Uri[] nodes = [new("https://only-node.example.com:9200")];
		using var inner = new FakeHttpMessageHandler();
		inner.SetResponse(HttpStatusCode.OK);
		using var handler = new EsNodeFailoverHandler(nodes) { InnerHandler = inner };
		using var invoker = new HttpMessageInvoker(handler, disposeHandler: false);

		var hosts = await SendNAsync(invoker, inner, 3).ConfigureAwait(false);

		hosts.ShouldAllBe(host => host == "only-node.example.com");
	}

	[Fact]
	public async Task AlwaysTargetTheSingleNode_OpenSearch()
	{
		Uri[] nodes = [new("https://only-node.example.com:9200")];
		using var inner = new FakeHttpMessageHandler();
		inner.SetResponse(HttpStatusCode.OK);
		using var handler = new OsNodeFailoverHandler(nodes) { InnerHandler = inner };
		using var invoker = new HttpMessageInvoker(handler, disposeHandler: false);

		var hosts = await SendNAsync(invoker, inner, 3).ConfigureAwait(false);

		hosts.ShouldAllBe(host => host == "only-node.example.com");
	}

	[Fact]
	public async Task PreservePathQueryAndAuthHeaderWhileSwappingAuthority_Elasticsearch()
	{
		Uri[] nodes = [new("https://node1.example.com:9200")];
		using var inner = new FakeHttpMessageHandler();
		inner.SetResponse(HttpStatusCode.OK);
		using var handler = new EsNodeFailoverHandler(nodes) { InnerHandler = inner };
		using var invoker = new HttpMessageInvoker(handler, disposeHandler: false);

		using var request = new HttpRequestMessage(HttpMethod.Post, "https://ignored.example.com:1/_bulk?refresh=wait_for")
		{
			Content = new StringContent("payload"),
		};
		request.Headers.Authorization = new AuthenticationHeaderValue("ApiKey", "secret-key");

		_ = await invoker.SendAsync(request, CancellationToken.None).ConfigureAwait(false);

		var sent = inner.LastRequest!.RequestUri!;
		sent.Host.ShouldBe("node1.example.com");
		sent.Port.ShouldBe(9200);
		sent.AbsolutePath.ShouldBe("/_bulk");
		sent.Query.ShouldContain("refresh=wait_for");
		inner.LastRequest!.Headers.Authorization!.Scheme.ShouldBe("ApiKey");
		inner.LastRequest!.Headers.Authorization!.Parameter.ShouldBe("secret-key");
	}

	[Fact]
	public async Task PreservePathQueryAndAuthHeaderWhileSwappingAuthority_OpenSearch()
	{
		Uri[] nodes = [new("https://node1.example.com:9200")];
		using var inner = new FakeHttpMessageHandler();
		inner.SetResponse(HttpStatusCode.OK);
		using var handler = new OsNodeFailoverHandler(nodes) { InnerHandler = inner };
		using var invoker = new HttpMessageInvoker(handler, disposeHandler: false);

		using var request = new HttpRequestMessage(HttpMethod.Post, "https://ignored.example.com:1/_bulk?refresh=wait_for")
		{
			Content = new StringContent("payload"),
		};
		request.Headers.Authorization = new AuthenticationHeaderValue("ApiKey", "secret-key");

		_ = await invoker.SendAsync(request, CancellationToken.None).ConfigureAwait(false);

		var sent = inner.LastRequest!.RequestUri!;
		sent.Host.ShouldBe("node1.example.com");
		sent.Port.ShouldBe(9200);
		sent.AbsolutePath.ShouldBe("/_bulk");
		sent.Query.ShouldContain("refresh=wait_for");
		inner.LastRequest!.Headers.Authorization!.Scheme.ShouldBe("ApiKey");
		inner.LastRequest!.Headers.Authorization!.Parameter.ShouldBe("secret-key");
	}

	[Fact]
	public void ThrowForNullNodes_Elasticsearch() =>
		Should.Throw<ArgumentNullException>(() => new EsNodeFailoverHandler(null!));

	[Fact]
	public void ThrowForEmptyNodes_Elasticsearch() =>
		Should.Throw<ArgumentException>(() => new EsNodeFailoverHandler([]));

	[Fact]
	public void ThrowForNullNodes_OpenSearch() =>
		Should.Throw<ArgumentNullException>(() => new OsNodeFailoverHandler(null!));

	[Fact]
	public void ThrowForEmptyNodes_OpenSearch() =>
		Should.Throw<ArgumentException>(() => new OsNodeFailoverHandler([]));

	private static async Task<List<string>> SendNAsync(HttpMessageInvoker invoker, FakeHttpMessageHandler inner, int count)
	{
		var hosts = new List<string>(count);
		for (var i = 0; i < count; i++)
		{
			using var request = new HttpRequestMessage(HttpMethod.Post, Template)
			{
				Content = new StringContent("payload"),
			};
			_ = await invoker.SendAsync(request, CancellationToken.None).ConfigureAwait(false);
			hosts.Add(inner.LastRequest!.RequestUri!.Host);
		}

		return hosts;
	}
}
