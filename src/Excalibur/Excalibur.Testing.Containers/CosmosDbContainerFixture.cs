// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Net;

using Testcontainers.CosmosDb;

namespace Excalibur.Testing.Containers;

/// <summary>
/// A reusable Azure Cosmos DB fixture backed by the TestContainers Cosmos DB <b>emulator</b>
/// (<see cref="CosmosDbContainer"/>). Inherit or use directly to test a Cosmos DB provider implementation
/// (event store, outbox fence, inbox) against a real emulator.
/// </summary>
/// <remarks>
/// <para>
/// The emulator advertises its own account endpoint as <c>http://127.0.0.1:8081/</c>. A <c>CosmosClient</c>
/// built from <see cref="ConnectionString"/> alone follows that advertised address for requests after the
/// initial account read, so it never reaches the port this container was actually mapped to. Set
/// <c>LimitToEndpoint</c> so the client keeps using the endpoint it was given:
/// </para>
/// <code>
/// var options = new CosmosClientOptions
/// {
///     LimitToEndpoint = true,
///     ConnectionMode = ConnectionMode.Gateway,
///     SerializerOptions = new CosmosSerializationOptions
///     {
///         PropertyNamingPolicy = CosmosPropertyNamingPolicy.CamelCase,
///     },
/// };
///
/// using var client = new CosmosClient(fixture.ConnectionString, options);
/// </code>
/// <para>
/// <c>SerializerOptions</c> is not optional. The Cosmos SDK's default serializer emits PascalCase
/// property names, so a client built without it writes <c>Id</c> where a later point-read looks for
/// <c>id</c>, and the read misses a document that is present.
/// </para>
/// <para>
/// That option was established by execution against the emulator, using client options alone and nothing
/// taken from this fixture. It addresses the advertised-endpoint obstacle; an individual environment may
/// impose others beyond it. This fixture owns only the container lifecycle and the connection string, and
/// the emulator can be slow to become ready — keep test timeouts generous.
/// </para>
/// </remarks>
public class CosmosDbContainerFixture : ContainerFixtureBase
{
	private CosmosDbContainer? _container;

	/// <summary>
	/// Gets the Cosmos DB emulator connection string for the started container.
	/// </summary>
	/// <exception cref="InvalidOperationException">Thrown when the container has not been initialized.</exception>
	public string ConnectionString =>
		_container?.GetConnectionString()
		?? throw new InvalidOperationException("The Cosmos DB emulator container has not been initialized.");

	private const string ImageEnvironmentVariable = "EXCALIBUR_COSMOS_EMULATOR_IMAGE";

	private const string DefaultImage = "mcr.microsoft.com/cosmosdb/linux/azure-cosmos-emulator:vnext-EN20260706";

	/// <summary>
	/// Gets the emulator image this fixture resolved, after applying any override. Read it to confirm which
	/// image a run actually used — an image chosen by environment variable is otherwise invisible in both the
	/// test code and its output.
	/// </summary>
	public string ResolvedImage => Image;

	/// <summary>
	/// Gets the Docker image used for the Cosmos DB emulator container. Set the
	/// <c>EXCALIBUR_COSMOS_EMULATOR_IMAGE</c> environment variable to use a different image — for example on
	/// an architecture the default does not serve, or to adopt a newer emulator on your own schedule.
	/// </summary>
	/// <remarks>
	/// <para>
	/// Setting the environment variable requires no change to your test code and no type of your own. This
	/// member remains overridable for a fixture that needs to choose its image in code, but a consumer is
	/// never obliged to derive from this class merely to change the image it runs.
	/// </para>
	/// <para>
	/// An override replaces this expression entirely, so a fixture that chooses its image in code takes
	/// precedence and the environment variable is not consulted for it. Whichever wins,
	/// <see cref="ResolvedImage"/> reports the image the run used.
	/// </para>
	/// <para>
	/// The default names a specific published emulator version, matching the other fixtures in this package.
	/// A versioned tag anchors the image while still allowing a consumer to receive upstream fixes for that
	/// version. The failure this default exists to avoid came from an <i>unversioned</i> tag rather than from
	/// tags in general: an unversioned tag can resolve to an entirely different image later, so a fixture
	/// following one can keep reporting healthy while no longer being able to create a database.
	/// </para>
	/// </remarks>
	protected virtual string Image =>
		Environment.GetEnvironmentVariable(ImageEnvironmentVariable) is { Length: > 0 } configured
			? configured
			: DefaultImage;

	/// <inheritdoc />
	protected override async Task InitializeContainerAsync(CancellationToken cancellationToken)
	{
		_container = new CosmosDbBuilder().WithImage(Image).Build();
		await _container.StartAsync(cancellationToken).ConfigureAwait(false);
		await WaitForDataPlaneAsync(_container, cancellationToken).ConfigureAwait(false);
	}

	/// <summary>
	/// Waits until the emulator will actually serve a request, not merely until its container is running.
	/// </summary>
	/// <remarks>
	/// <para>
	/// <b>Container-ready and data-plane-ready are different states.</b> <c>StartAsync</c> returns once the
	/// Testcontainers wait strategy is satisfied — the container is up and the gateway answers. The emulator
	/// then continues initialising internally, and until it finishes it rejects requests with
	/// <c>503 Service Unavailable</c>. Without this wait the fixture hands back a
	/// <see cref="ConnectionString"/> for an emulator that is not yet usable, so the FIRST call a consumer
	/// makes is the one that fails — and it surfaces as a database-client error rather than as a fixture
	/// problem, which sends them looking for a bug in their own code.
	/// </para>
	/// <para>
	/// <b>The probe is deliberately transport-level.</b> It reuses the container's own
	/// <see cref="HttpClient"/> rather than a database client, so this package does not acquire a database
	/// SDK dependency that every consumer would then carry for a readiness check. A non-503 response of any
	/// kind proves the data plane is answering; the probe deliberately does not care what that response says.
	/// </para>
	/// <para>
	/// <b>Only 503 and transport faults are retried.</b> Anything else is returned immediately, so a genuine
	/// misconfiguration fails fast with its own error instead of being masked for the whole timeout and then
	/// reported as a readiness timeout.
	/// </para>
	/// </remarks>
	private static async Task WaitForDataPlaneAsync(CosmosDbContainer container, CancellationToken cancellationToken)
	{
		var pollInterval = TimeSpan.FromSeconds(2);

		// NOT disposed: the HttpClient belongs to the container, which hands the same instance to callers.
		// Disposing it here would leave every later consumer of the fixture with a dead client.
		var probe = container.HttpClient;

		// An ABSOLUTE uri, because the container's client carries no BaseAddress — a relative uri throws
		// "either the request URI must be an absolute URI or BaseAddress must be set" and the readiness
		// wait would fail the fixture it exists to protect. The endpoint is taken from the connection
		// string rather than rebuilt from host and port so it always matches what the consumer is handed.
		var endpoint = ExtractAccountEndpoint(container.GetConnectionString());

		while (!cancellationToken.IsCancellationRequested)
		{
			try
			{
				using var response = await probe.GetAsync(endpoint, cancellationToken)
					.ConfigureAwait(false);

				if (response.StatusCode != HttpStatusCode.ServiceUnavailable)
				{
					return;
				}
			}
			catch (HttpRequestException)
			{
				// The listener can accept the port before it will serve on it; keep waiting.
			}

			try
			{
				await Task.Delay(pollInterval, cancellationToken).ConfigureAwait(false);
			}
			catch (OperationCanceledException)
			{
				break;
			}
		}

		throw new InvalidOperationException(
			"The Cosmos DB emulator container started but its data plane never began serving requests within "
			+ "the fixture's startup budget: it kept returning 503 Service Unavailable. The container itself is "
			+ "healthy, so this is the emulator's own initialisation exceeding the budget rather than a Docker "
			+ "failure. Raise the startup timeout, or give the container more memory.");
	}

	/// <summary>
	/// Reads the account endpoint out of an emulator connection string.
	/// </summary>
	/// <remarks>
	/// Falls back to the whole connection string only if the expected key is absent, so a future change to
	/// the emulator's connection-string format degrades to a failed probe rather than to a silently skipped
	/// readiness wait — a wait that quietly stops waiting is worse than one that fails loudly.
	/// </remarks>
	private static Uri ExtractAccountEndpoint(string connectionString)
	{
		foreach (var part in connectionString.Split(';', StringSplitOptions.RemoveEmptyEntries))
		{
			var trimmed = part.Trim();
			if (trimmed.StartsWith("AccountEndpoint=", StringComparison.OrdinalIgnoreCase)
				&& Uri.TryCreate(trimmed["AccountEndpoint=".Length..], UriKind.Absolute, out var endpoint))
			{
				return endpoint;
			}
		}

		throw new InvalidOperationException(
			"Could not read AccountEndpoint from the Cosmos DB emulator connection string, so the fixture "
			+ "cannot probe the emulator for readiness.");
	}

	/// <inheritdoc />
	protected override async Task DisposeContainerAsync(CancellationToken cancellationToken)
	{
		if (_container is not null)
		{
			await _container.DisposeAsync().ConfigureAwait(false);
		}
	}
}
