// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

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
