// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Testing.Containers;

using Microsoft.Azure.Cosmos;

using Shouldly;

namespace Excalibur.Integration.Tests.Testing.Containers;

/// <summary>
/// Consumer-shaped locks for <see cref="CosmosDbContainerFixture"/>. Every step below is one a consumer can
/// take against the fixture's public surface alone — it is resolved without subclassing and the client is
/// built from nothing but the documented <see cref="CosmosClientOptions"/> steps.
///
/// The <c>Excalibur.Testing.Containers</c> package is not published, so the audience for these guarantees is
/// a consumer building from source rather than one installing from a feed. The locks are written to the
/// public surface regardless, because that is what those consumers compile against.
/// </summary>
/// <remarks>
/// NON-VACUOUS by construction: the shipped default previously pinned a floating <c>:latest</c> emulator tag
/// whose image becomes ready and answers its readiness probe but cannot create a database, so
/// <see cref="CreateADatabaseThroughTheShippedDefaultImage"/> is RED against that default and GREEN against a
/// functional pin. Nothing here uses <c>InternalsVisibleTo</c> or any member that is not part of the
/// package's published API surface.
/// </remarks>
[Trait("Category", "Integration")]
[Trait("Infrastructure", "CosmosEmulator")]
[Trait("Database", "CosmosDb")]
[Trait("Component", "Testing")]
public sealed class CosmosDbContainerFixtureShould
{
	/// <summary>
	/// AC-1/AC-2 — a consumer takes the fixture exactly as shipped and performs the first operation any
	/// Cosmos consumer performs. The default image must be able to create a database, not merely start.
	/// </summary>
	[Fact]
	public async Task CreateADatabaseThroughTheShippedDefaultImage()
	{
		await using var fixture = new CosmosDbContainerFixture();

		await fixture.InitializeAsync();
		fixture.DockerAvailable.ShouldBeTrue(fixture.InitializationError);

		await CreateDatabaseAsync(fixture.ConnectionString);
	}

	/// <summary>
	/// AC-3 — a consumer can move off this package's pin WITHOUT subclassing. The criterion is explicit that
	/// an override point on the type is a consumer fork rather than an escape hatch, so this binds the
	/// property (the image is configurable externally) and not the mechanism (an override exists).
	/// </summary>
	/// <remarks>
	/// The environment variable names a DIFFERENT string from the shipped default while resolving to the same
	/// bytes this suite already exercises, so a pass proves the setting genuinely took effect without
	/// introducing an unverified image. RED before the environment variable was honored: the fixture then had
	/// no non-subclass configuration path at all.
	/// </remarks>
	[Fact]
	public async Task HonorAnImageSuppliedWithoutSubclassing()
	{
		var previous = Environment.GetEnvironmentVariable(ImageEnvironmentVariable);
		Environment.SetEnvironmentVariable(ImageEnvironmentVariable, PinnedByDigest);

		try
		{
			// The SHIPPED type, unsubclassed — exactly what a consumer resolves from the package.
			await using var fixture = new CosmosDbContainerFixture();

			await fixture.InitializeAsync();
			fixture.DockerAvailable.ShouldBeTrue(fixture.InitializationError);

			await CreateDatabaseAsync(fixture.ConnectionString);
		}
		finally
		{
			Environment.SetEnvironmentVariable(ImageEnvironmentVariable, previous);
		}
	}

	/// <summary>
	/// A document written through the documented client must be retrievable by its lowercase <c>id</c>.
	/// The SDK's default serializer emits PascalCase, so a client built without the serializer option
	/// stores <c>Id</c> and this point-read returns NotFound for a document that is present.
	/// </summary>
	/// <remarks>
	/// This asserts the EMITTED KEY rather than the presence of a setting. A test that checked
	/// <c>SerializerOptions is not null</c> would pass against any naming policy, including the one that
	/// causes the defect — the same vacuity that let a PascalCase checkpoint document ship once before.
	/// RED when <see cref="DocumentedClientOptions"/> omits the serializer; GREEN with it.
	/// </remarks>
	[Fact]
	public async Task StoreADocumentUnderTheLowercaseIdTheDocumentedRecipeImplies()
	{
		await using var fixture = new CosmosDbContainerFixture();

		await fixture.InitializeAsync();
		fixture.DockerAvailable.ShouldBeTrue(fixture.InitializationError);

		using var client = new CosmosClient(fixture.ConnectionString, DocumentedClientOptions());

		var database = await client
			.CreateDatabaseIfNotExistsAsync($"naming-{Guid.NewGuid():N}")
			.ConfigureAwait(false);
		var container = await database.Database
			.CreateContainerIfNotExistsAsync("items", "/id")
			.ConfigureAwait(false);

		var id = $"doc-{Guid.NewGuid():N}";
		_ = await container.Container
			.CreateItemAsync(new NamedDocument(id), new PartitionKey(id))
			.ConfigureAwait(false);

		// Point-read by the lowercase key. Under the SDK default (PascalCase) the stored key is "Id",
		// the document is unreachable by this read, and the SDK throws NotFound.
		var read = await container.Container
			.ReadItemAsync<NamedDocument>(id, new PartitionKey(id))
			.ConfigureAwait(false);

		read.Resource.Id.ShouldBe(id);
	}

	private sealed record NamedDocument(string Id);

	/// <summary>
	/// Deriving to choose the image in code must keep working — it is a supported option, just not the one
	/// AC-3 requires. Kept as its own arm so a regression in either path is attributable.
	/// </summary>
	[Fact]
	public async Task HonorAnImageSuppliedByAConsumerOverride()
	{
		await using var fixture = new ConsumerPinnedImageFixture();

		await fixture.InitializeAsync();
		fixture.DockerAvailable.ShouldBeTrue(fixture.InitializationError);

		await CreateDatabaseAsync(fixture.ConnectionString);
	}

	/// <summary>
	/// Builds a client using ONLY the steps the shipped documentation gives a consumer, then performs the
	/// operation the documentation promises will work.
	/// </summary>
	/// <remarks>
	/// The options here must stay identical to the recipe in the fixture's XML documentation and the
	/// package README. If this method configures the client in a way the documentation does not, the lock
	/// stops testing the consumer's path and can no longer detect a defect in the advice we ship.
	/// </remarks>
	private static CosmosClientOptions DocumentedClientOptions() =>
		new()
		{
			LimitToEndpoint = true,
			ConnectionMode = ConnectionMode.Gateway,
			SerializerOptions = new CosmosSerializationOptions
			{
				PropertyNamingPolicy = CosmosPropertyNamingPolicy.CamelCase,
			},
		};

	private static async Task CreateDatabaseAsync(string connectionString)
	{
		using var client = new CosmosClient(connectionString, DocumentedClientOptions());

		var response = await client
			.CreateDatabaseIfNotExistsAsync($"consumer-smoke-{Guid.NewGuid():N}")
			.ConfigureAwait(false);

		response.StatusCode.ShouldBeOneOf(System.Net.HttpStatusCode.Created, System.Net.HttpStatusCode.OK);
	}

	/// <summary>
	/// The environment variable a consumer sets, written as the literal a consumer actually types rather
	/// than borrowed from the implementation — so the lock binds the published contract and would still fail
	/// if the fixture silently started reading a different variable.
	/// </summary>
	private const string ImageEnvironmentVariable = "EXCALIBUR_COSMOS_EMULATOR_IMAGE";

	/// <summary>
	/// A DIFFERENT string from the shipped default that resolves to the same bytes this suite already
	/// exercises — so an override is proven to take effect without adding an unverified image to the run.
	/// </summary>
	private const string PinnedByDigest =
		"mcr.microsoft.com/cosmosdb/linux/azure-cosmos-emulator@sha256:a8b93e25520e999d867ed3949e7de7f4ff3ddab23ca95fa6f90230de5dd9729b";

	/// <summary>
	/// A consumer's own fixture, naming its emulator image in code through the override point. A consumer is
	/// free to name either a version or a digest; the shipped default names a version by convention.
	/// </summary>
	private sealed class ConsumerPinnedImageFixture : CosmosDbContainerFixture
	{
		protected override string Image => PinnedByDigest;
	}
}
