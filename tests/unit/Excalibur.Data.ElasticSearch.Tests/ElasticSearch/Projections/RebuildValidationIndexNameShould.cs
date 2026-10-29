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
using Excalibur.EventSourcing;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Excalibur.Data.Tests.ElasticSearch.Projections;

internal sealed class RebuildProjection
{
	public string Id { get; set; } = string.Empty;
}

/// <summary>A read model whose declared generation has been bumped.</summary>
[ProjectionVersion(2)]
internal sealed class VersionedRebuildProjection
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

		_ = await manager.ValidateRebuildAsync(typeof(RebuildProjection), CancellationToken.None)
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

		_ = await manager.ValidateRebuildAsync(typeof(RebuildProjection), CancellationToken.None)
			.ConfigureAwait(true);

		endpoint.Paths.ShouldContain("/projections-rebuildprojection");
	}

	/// <summary>
	/// SAFETY. After a generation bump the store writes to the new generation's index, so validation
	/// must probe THAT index. Asserting the path on the wire rather than a successful return is the
	/// whole point: the defect was a confident, successful answer about the previous generation's
	/// index, which no boolean assertion can distinguish from a correct one.
	/// </summary>
	[Fact]
	public async Task AskAboutTheNewGenerationsIndexAfterAVersionBump()
	{
		using var endpoint = new PathRecordingEndpoint();

		var manager = CreateManager(endpoint.Uri, static _ => { });

		_ = await manager.ValidateRebuildAsync(typeof(VersionedRebuildProjection), CancellationToken.None)
			.ConfigureAwait(true);

		endpoint.Paths.ShouldContain(
			"/projections-versionedrebuildprojection-v2",
			"validation must probe the generation the projection type declares -- the one the store "
			+ "writes to -- not the first generation's index");

		// The discriminator. The version-blind lookup probes exactly this, and it is the index the
		// rebuild is NOT targeting.
		endpoint.Paths.ShouldNotContain("/projections-versionedrebuildprojection");
	}

	/// <summary>
	/// SAFETY. The store registers a projection's settings under its FULL type name. Validation must ask
	/// for THAT bucket; asking for the simple name names a bucket nothing configured, so a namespaced
	/// projection -- which is every projection -- was validated against the defaults and reported on an
	/// index the store does not write to.
	/// </summary>
	/// <remarks>
	/// The options monitor here is NAME-SENSITIVE, which is the whole oracle: a monitor that answers the
	/// same object to every name cannot tell a correct bucket lookup from a wrong one, and the arms above
	/// use one. The assertion is the path on the wire, pinned as a literal, because the defect returns a
	/// confident, successful answer about the wrong index -- no boolean can separate the two.
	/// </remarks>
	[Fact]
	public async Task AskAboutTheBucketTheStoreRegisteredForANamespacedProjection()
	{
		using var endpoint = new PathRecordingEndpoint();

		var manager = CreateManager(endpoint.Uri, index =>
		{
			index.IndexPrefix = "readmodels";
			index.IndexName = "customers";
		},
		configuredFor: typeof(RebuildProjection));

		_ = await manager.ValidateRebuildAsync(typeof(RebuildProjection), CancellationToken.None)
			.ConfigureAwait(true);

		endpoint.Paths.ShouldContain(
			"/readmodels-customers",
			"validation must read the options bucket keyed on the projection's FULL type name -- the one "
			+ "the store registered -- not the simple name, which is an unconfigured default bucket");

		// The discriminator. The simple-name lookup resolves exactly this, from defaults the consumer
		// never set, and it is the index the rebuild is NOT targeting.
		endpoint.Paths.ShouldNotContain("/projections-rebuildprojection");
	}

	/// <summary>
	/// SAFETY. The bucket fix and the generation fix compose: after a version bump the index is the
	/// CONFIGURED name qualified by the declared generation, which needs both derivations to be right.
	/// </summary>
	[Fact]
	public async Task AskAboutTheConfiguredBucketsNewGenerationAfterAVersionBump()
	{
		using var endpoint = new PathRecordingEndpoint();

		var manager = CreateManager(endpoint.Uri, index =>
		{
			index.IndexPrefix = "readmodels";
			index.IndexName = "customers";
		},
		configuredFor: typeof(VersionedRebuildProjection));

		_ = await manager.ValidateRebuildAsync(typeof(VersionedRebuildProjection), CancellationToken.None)
			.ConfigureAwait(true);

		endpoint.Paths.ShouldContain(
			"/readmodels-customers-v2",
			"the configured index name and the declared generation must BOTH participate -- a correct "
			+ "generation applied to a default bucket is still the wrong index");

		endpoint.Paths.ShouldNotContain("/projections-versionedrebuildprojection-v2");
	}

	/// <summary>
	/// LIVENESS. Against the same name-sensitive monitor, a projection whose bucket holds nothing
	/// configured still resolves the convention's default index and is still probed. Without this arm the
	/// two SAFETY arms above are satisfied by a manager that has stopped probing, or by a monitor that
	/// answers nothing to anybody.
	/// </summary>
	[Fact]
	public async Task StillProbeTheConventionalIndexWhenTheRegisteredBucketConfiguresNothing()
	{
		using var endpoint = new PathRecordingEndpoint();

		var manager = CreateManager(endpoint.Uri, static _ => { }, configuredFor: typeof(RebuildProjection));

		_ = await manager.ValidateRebuildAsync(typeof(RebuildProjection), CancellationToken.None)
			.ConfigureAwait(true);

		endpoint.Paths.ShouldContain("/projections-rebuildprojection");
	}

	private static ProjectionRebuildManager CreateManager(
		Uri endpoint,
		Action<ElasticSearchProjectionIndexOptions> configureIndex,
		Type configuredFor)
	{
		var configured = new ElasticSearchProjectionStoreOptions { NodeUri = endpoint.ToString() };
		configureIndex(configured.Index);

		var optionsName = ElasticSearchProjectionIndexConvention.GetOptionsName(configuredFor);

		// Answers the CONFIGURED options for the bucket the store registered, and an untouched default for
		// every other name -- which is what IOptionsMonitor.Get really does for a name nobody configured.
		var monitor = A.Fake<IOptionsMonitor<ElasticSearchProjectionStoreOptions>>();
		A.CallTo(() => monitor.Get(A<string>._))
			.ReturnsLazily(call => string.Equals((string?)call.Arguments[0], optionsName, StringComparison.Ordinal)
				? configured
				: new ElasticSearchProjectionStoreOptions { NodeUri = endpoint.ToString() });

		return CreateManager(endpoint, monitor);
	}

	private static ProjectionRebuildManager CreateManager(
		Uri endpoint,
		Action<ElasticSearchProjectionIndexOptions> configureIndex)
	{
		var storeOptions = new ElasticSearchProjectionStoreOptions { NodeUri = endpoint.ToString() };
		configureIndex(storeOptions.Index);

		var monitor = A.Fake<IOptionsMonitor<ElasticSearchProjectionStoreOptions>>();
		A.CallTo(() => monitor.Get(A<string>._)).Returns(storeOptions);

		return CreateManager(endpoint, monitor);
	}

	private static ProjectionRebuildManager CreateManager(
		Uri endpoint,
		IOptionsMonitor<ElasticSearchProjectionStoreOptions> monitor)
	{
		return new ProjectionRebuildManager(
			new ElasticsearchClient(new ElasticsearchClientSettings(endpoint)),
			A.Fake<IIndexAliasManager>(),
			Options.Create(new ElasticSearchProjectionManagementOptions()),
			monitor,
			NullLogger<ProjectionRebuildManager>.Instance);
	}
}
