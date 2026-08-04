// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;

using Excalibur.Integration.Tests.Infrastructure;

using OpenSearch.Client;

namespace Excalibur.Integration.Tests.OpenSearch;

/// <summary>
/// Owns a single OpenSearch container shared by every test in the
/// <see cref="OpenSearchTestCollection"/>.
/// </summary>
/// <remarks>
/// <para>
/// xUnit constructs a new instance of a test class for every fact, so a container started from a test
/// class's own <c>IAsyncLifetime.InitializeAsync</c> is started once per fact. A collection fixture is
/// constructed once for the whole collection, which is why container ownership belongs here and not on
/// the test class.
/// </para>
/// <para>
/// <b>Degradation is graceful and honest.</b> A failed start does not throw: it records
/// <see cref="Available"/> = <see langword="false"/> plus the cause, and each fact skips itself with
/// <c>Assert.SkipUnless</c>. Skips remain skips — a fact that did not execute is never reported passed.
/// </para>
/// <para>
/// Exactly one start is attempted. There is deliberately no retry loop: when the infrastructure is
/// absent, a retry buys nothing and costs the whole start timeout again, which is the wall-clock overrun
/// this fixture exists to remove.
/// </para>
/// </remarks>
public sealed class OpenSearchContainerFixture : IAsyncLifetime
{
	private const string Image = "opensearchproject/opensearch:2.16.0";

	private IContainer? _container;

	/// <summary>
	/// Gets a value indicating whether the OpenSearch container started and is reachable.
	/// </summary>
	public bool Available { get; private set; }

	/// <summary>
	/// Gets the cause of the failure when <see cref="Available"/> is <see langword="false"/>.
	/// </summary>
	public Exception? UnavailableCause { get; private set; }

	/// <summary>
	/// Gets the base address of the running container.
	/// </summary>
	public Uri Endpoint => _container is not null
		? new Uri($"http://localhost:{_container.GetMappedPublicPort(9200)}")
		: throw new InvalidOperationException("OpenSearch container is not available.");

	/// <summary>
	/// Gets the skip reason describing why OpenSearch is unavailable, including the original cause.
	/// </summary>
	public string SkipReason => ContainerAvailabilityGate.SkipReason("OpenSearch (Docker)", UnavailableCause);

	/// <inheritdoc/>
	public async ValueTask InitializeAsync()
	{
		// If a previous fixture in this process already discovered that OpenSearch cannot start, do not
		// pay the start timeout again — adopt the recorded cause and report unavailable immediately.
		if (ContainerAvailabilityGate.TryGetFailure(Image, out var priorFailure))
		{
			UnavailableCause = priorFailure;
			Available = false;
			return;
		}

		try
		{
			_container = new ContainerBuilder()
				.WithImage(Image)
				.WithPortBinding(9200, true)
				.WithEnvironment("discovery.type", "single-node")
				.WithEnvironment("DISABLE_SECURITY_PLUGIN", "true")
				.WithEnvironment("DISABLE_INSTALL_DEMO_CONFIG", "true")
				.WithEnvironment("OPENSEARCH_JAVA_OPTS", "-Xms256m -Xmx256m")
				.WithWaitStrategy(Wait.ForUnixContainer()
					.UntilHttpRequestIsSucceeded(static r => r.ForPort(9200).ForPath("/")))
				.Build();

			using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(2));
			await _container.StartAsync(cts.Token).ConfigureAwait(false);

			// Reachability is probed ONCE, here, and with a cluster ping.
			//
			// It must not be probed by counting documents: the projection index does not exist until a
			// test writes to it, so a count-based probe returns 404 on a perfectly healthy node. That is
			// exactly what used to happen — every fact reported "[infrastructure-unavailable]" while a
			// working OpenSearch container sat behind it, so these facts skipped on every machine and in
			// CI, and had never actually executed. A ping asks the question the skip claims to be asking:
			// is the node reachable.
			var probe = new OpenSearchClient(new ConnectionSettings(Endpoint));
			Available = await global::Tests.Shared.Infrastructure.WaitHelpers.WaitUntilAsync(
				async () =>
				{
					try
					{
						return (await probe.PingAsync().ConfigureAwait(false)).IsValid;
					}
					catch
					{
						return false;
					}
				},
				TimeSpan.FromSeconds(60)).ConfigureAwait(false);

			if (!Available)
			{
				UnavailableCause = new InvalidOperationException(
					"The OpenSearch container started but the node never answered a ping within 60s.");
			}
		}
		catch (Exception ex)
		{
			// Preserve the cause so the skip message can say WHY. Record it process-wide so no later
			// fixture repeats this wait.
			ContainerAvailabilityGate.RecordFailure(Image, ex);
			UnavailableCause = ex;
			Available = false;
		}
	}

	/// <inheritdoc/>
	public async ValueTask DisposeAsync()
	{
		try
		{
			if (_container is not null)
			{
				using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
				await _container.DisposeAsync().AsTask().WaitAsync(cts.Token).ConfigureAwait(false);
			}
		}
		catch (Exception)
		{
			// Suppress disposal errors and timeouts to prevent test host crash.
		}
	}
}

/// <summary>
/// Collection definition sharing one <see cref="OpenSearchContainerFixture"/> across every OpenSearch
/// integration test class.
/// </summary>
[CollectionDefinition(CollectionName, DisableParallelization = true)]
public sealed class OpenSearchTestCollection : ICollectionFixture<OpenSearchContainerFixture>
{
	/// <summary>
	/// The collection name used by test classes.
	/// </summary>
	public const string CollectionName = "OpenSearch Integration Tests";
}
