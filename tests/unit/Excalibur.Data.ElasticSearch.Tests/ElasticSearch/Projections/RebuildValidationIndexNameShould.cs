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

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Excalibur.Data.Tests.ElasticSearch.Projections;

internal sealed class RebuildProjection
{
	public string Id { get; set; } = string.Empty;
}

/// <summary>
/// Records the paths a client asks for, and reports every index absent.
/// </summary>
/// <remarks>
/// Below the SDK, so the index name under test is the one the client really put on the wire rather than
/// a string read back out of the object that composed it.
/// </remarks>
internal sealed class PathRecordingEndpoint : IDisposable
{
	private readonly HttpListener _listener = new();

	public PathRecordingEndpoint()
	{
		Uri = new Uri($"http://127.0.0.1:{FreePort()}/");
		_listener.Prefixes.Add(Uri.ToString());
		_listener.Start();
		_ = Task.Run(ServeAsync);
	}

	public Uri Uri { get; }

	public ConcurrentQueue<string> Paths { get; } = new();

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

			Paths.Enqueue(context.Request.Url?.AbsolutePath ?? "/");

			// Absent, so validation takes the does-not-exist branch and never counts documents.
			var bytes = Encoding.UTF8.GetBytes(string.Empty);
			context.Response.StatusCode = 404;
			context.Response.ContentType = "application/json";
			context.Response.Headers["X-Elastic-Product"] = "Elasticsearch";
			context.Response.ContentLength64 = bytes.Length;
			context.Response.Close();
		}
	}
}

/// <summary>
/// Rebuild validation must ask about the index the store actually writes to.
/// </summary>
/// <remarks>
/// <para>
/// The store composes a projection's index name from <c>ElasticSearchProjectionStoreOptions.Index</c> —
/// its prefix, and its <c>IndexName</c> when one is set — through
/// <see cref="ElasticSearchProjectionIndexConvention"/>. Rebuild validation composed its own name from a
/// DIFFERENT settings object, the <c>ElasticSearchProjectionManagementOptions.IndexPrefix</c> that names the consistency
/// tracker's and schema handler's sidecar indexes, and ignored <c>IndexName</c> entirely.
/// </para>
/// <para>
/// Both prefixes default to <c>projections</c>, so the two agreed out of the box and diverged the moment
/// either was set — including by the framework's own documented example. The consequence was a
/// confident, reassuring, wrong answer about a consumer's read model: "Index X does not exist. A rebuild
/// will create a new index", with no exception and no error log.
/// </para>
/// </remarks>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Data)]
public sealed class RebuildValidationIndexNameShould
{
	[Fact]
	public async Task AskAboutTheIndexTheStoreWritesTo()
	{
		using var endpoint = new PathRecordingEndpoint();

		var manager = CreateManager(endpoint.Uri, index =>
		{
			index.IndexPrefix = "readmodels";
			index.IndexName = "customers";
		});

		_ = await manager.ValidateRebuildAsync(nameof(RebuildProjection), CancellationToken.None)
			.ConfigureAwait(true);

		endpoint.Paths.ShouldContain(
			"/readmodels-customers",
			"validation must probe the name the store composes from the projection's own store options -- "
			+ "prefix plus IndexName -- not a name built from the consistency tracker's prefix and the "
			+ "type name");
	}

	/// <summary>
	/// CONTROL: with nothing configured, the name is the convention's default, so the arm above is
	/// detecting the configured values rather than any change of source.
	/// </summary>
	[Fact]
	public async Task AskAboutTheConventionalNameWhenNothingIsConfigured()
	{
		using var endpoint = new PathRecordingEndpoint();

		var manager = CreateManager(endpoint.Uri, static _ => { });

		_ = await manager.ValidateRebuildAsync(nameof(RebuildProjection), CancellationToken.None)
			.ConfigureAwait(true);

		endpoint.Paths.ShouldContain("/projections-rebuildprojection");
	}

	private static ProjectionRebuildManager CreateManager(
		Uri endpoint,
		Action<ElasticSearchProjectionIndexOptions> configureIndex)
	{
		var storeOptions = new ElasticSearchProjectionStoreOptions { NodeUri = endpoint.ToString() };
		configureIndex(storeOptions.Index);

		var monitor = A.Fake<IOptionsMonitor<ElasticSearchProjectionStoreOptions>>();
		A.CallTo(() => monitor.Get(A<string>._)).Returns(storeOptions);

		return new ProjectionRebuildManager(
			new ElasticsearchClient(new ElasticsearchClientSettings(endpoint)),
			A.Fake<IIndexAliasManager>(),
			Options.Create(new ElasticSearchProjectionManagementOptions()),
			monitor,
			NullLogger<ProjectionRebuildManager>.Instance);
	}
}
