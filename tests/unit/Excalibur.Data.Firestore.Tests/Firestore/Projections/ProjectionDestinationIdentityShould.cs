// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Data.Firestore.Projections;
using Excalibur.EventSourcing;

using Google.Api.Gax;
using Google.Cloud.Firestore;
using Google.Cloud.Firestore.V1;

using SalesProjection = Excalibur.Data.Tests.OptionsIdentity.Sales.EmployeeProjection;
using SupportProjection = Excalibur.Data.Tests.OptionsIdentity.Support.EmployeeProjection;

namespace Excalibur.Data.Tests.OptionsIdentity.Sales
{
	/// <summary>A projection type whose simple name is shared with its support-side namesake.</summary>
	public sealed class EmployeeProjection
	{
		/// <summary>Gets or sets the employee identifier.</summary>
		public string Id { get; set; } = string.Empty;
	}
}

namespace Excalibur.Data.Tests.OptionsIdentity.Support
{
	/// <summary>A projection type whose simple name is shared with its sales-side namesake.</summary>
	public sealed class EmployeeProjection
	{
		/// <summary>Gets or sets the employee identifier.</summary>
		public string Id { get; set; } = string.Empty;
	}
}

namespace Excalibur.Data.Tests.Firestore.Projections
{
	/// <summary>
	/// Locks the destination identity the Firestore projection store advertises through its own explicit
	/// <see cref="IServiceProvider.GetService(System.Type)"/>.
	/// </summary>
	/// <remarks>
	/// <para>
	/// The identity is a Firestore RESOURCE NAME, which is why it is longer than every other provider's: it
	/// carries the project id and the database id as well as the collection path. A collection name alone
	/// repeats across projects, databases and parent documents, so dropping either segment merges two
	/// physically distinct destinations into one fingerprint.
	/// </para>
	/// <para>
	/// The identity carries no endpoint. The emulator is selected by the channel the client is built on, never
	/// by the resource path, so the same logical target yields the identical string against the emulator and
	/// against production — which is what lets a binding made in one environment be honoured in the other.
	/// </para>
	/// <para>
	/// The projection segment is the FULLY-QUALIFIED type name. The simple name is not unique in a
	/// solution, and two projection classes sharing one made the identity -- and the subcollection it is
	/// taken from -- identical for two distinct read models, which then read and wrote each other's
	/// documents wherever their ids coincided. The arms below register such a pair in ONE container, which
	/// is also only expressible because the store's settings are now named per projection type rather than
	/// written to the single unnamed options instance.
	/// </para>
	/// <para>
	/// Every arm resolves the store through a real container built by the production registration, so a
	/// registration that failed to wire the capability would fail here rather than pass against a
	/// hand-constructed store.
	/// </para>
	/// </remarks>
	[Trait("Category", "Unit")]
	[Trait("Component", "Projections")]
	public sealed class ProjectionDestinationIdentityShould
	{
		private const string DefaultDatabase = "(default)";

		// Composed from literals, deliberately: an oracle that asked production for the segment would
		// agree with production about anything, including a segment that collides.
		private const string SalesSegment = "Excalibur.Data.Tests.OptionsIdentity.Sales.EmployeeProjection";
		private const string SupportSegment = "Excalibur.Data.Tests.OptionsIdentity.Support.EmployeeProjection";

		[Theory]
		[InlineData("sales-projections", "support-projections")]
		[InlineData("projections", "projections-v2")]
		[InlineData("people", "people-archive")]
		public async Task ScopeTheConfiguredCollectionNamePerProjectionTypeInOneContainer(
			string salesCollection, string supportCollection)
		{
			typeof(SalesProjection).Name.ShouldBe(typeof(SupportProjection).Name);

			const string Project = "identity-project";

			// ONE container holding BOTH registrations. The settings live in a named options instance keyed
			// on the projection type, so the second registration scopes its collection name instead of
			// overwriting the first's -- which an unnamed instance did, leaving both stores addressing
			// whichever collection name was configured last.
			var (salesIdentity, supportIdentity) = await ResolveBothIdentitiesAsync(
				CreateDb(Project), salesCollection, supportCollection).ConfigureAwait(false);

			salesIdentity.Identity.ShouldBe(Expected(Project, DefaultDatabase, salesCollection, SalesSegment));
			supportIdentity.Identity.ShouldBe(Expected(Project, DefaultDatabase, supportCollection, SupportSegment));
			salesIdentity.Identity.ShouldNotBe(supportIdentity.Identity);

			// The capability came from the store's own explicit GetService, not from the default interface
			// implementation on IProjectionStore<TProjection>, which answers `this` for any assignable type.
			(salesIdentity is IProjectionStore<SalesProjection>).ShouldBeFalse();
			(supportIdentity is IProjectionStore<SupportProjection>).ShouldBeFalse();
		}

		[Fact]
		public async Task AddressDistinctSubcollectionsForSameNamedProjectionTypesSharingACollection()
		{
			typeof(SalesProjection).Name.ShouldBe(typeof(SupportProjection).Name);

			const string Project = "identity-project";
			const string Shared = "projections";

			// The defect this arm exists for: ONE collection name, two projection classes whose SIMPLE
			// names are identical. Keyed on the simple name both stores resolved the same subcollection,
			// so every write by one was visible to -- and overwritable by -- the other at any shared
			// document id. Nothing errored and nothing was logged, so no reader of either read model could
			// learn it was wrong.
			var (salesIdentity, supportIdentity) = await ResolveBothIdentitiesAsync(
				CreateDb(Project), Shared, Shared).ConfigureAwait(false);

			salesIdentity.Identity.ShouldBe(Expected(Project, DefaultDatabase, Shared, SalesSegment));
			supportIdentity.Identity.ShouldBe(Expected(Project, DefaultDatabase, Shared, SupportSegment));

			salesIdentity.Identity.ShouldNotBe(
				supportIdentity.Identity,
				"two read models sharing a simple name and a collection are distinct destinations; an "
				+ "identity keyed on the simple name merges them");

			// The identity is composed from the resolved collection path, so a differing identity is only
			// evidence of a differing PATH if the segment is what differs. Assert the segment directly:
			// the subcollection is where the corruption happened, and the identity merely inherited it.
			salesIdentity.Identity.ShouldEndWith($"/{Shared}/{SalesSegment}/items");
			supportIdentity.Identity.ShouldEndWith($"/{Shared}/{SupportSegment}/items");
		}

		[Fact]
		public async Task SeparateTheSameCollectionAcrossDifferentProjectsAndDatabases()
		{
			const string Collection = "projections";

			var inProjectA = await ResolveIdentityAsync<SalesProjection>(
				CreateDb("identity-project-a"), Collection).ConfigureAwait(false);
			var inProjectB = await ResolveIdentityAsync<SalesProjection>(
				CreateDb("identity-project-b"), Collection).ConfigureAwait(false);
			var inSecondDatabase = await ResolveIdentityAsync<SalesProjection>(
				CreateDb("identity-project-a", "identity-database-two"), Collection).ConfigureAwait(false);

			inProjectA.Identity.ShouldBe(Expected("identity-project-a", DefaultDatabase, Collection, SalesSegment));
			inProjectB.Identity.ShouldBe(Expected("identity-project-b", DefaultDatabase, Collection, SalesSegment));
			inSecondDatabase.Identity.ShouldBe(
				Expected("identity-project-a", "identity-database-two", Collection, SalesSegment));

			inProjectA.Identity.ShouldNotBe(
				inProjectB.Identity,
				"two GCP projects holding an identically named collection are different destinations; an " +
				"identity that omits the project id merges them");
			inProjectA.Identity.ShouldNotBe(
				inSecondDatabase.Identity,
				"two Firestore databases in one project holding an identically named collection are " +
				"different destinations; an identity that omits the database id merges them");
		}

		[Fact]
		public async Task HoldTheSameIdentityAgainstTheEmulatorAsAgainstProduction()
		{
			const string Project = "identity-project";
			const string Collection = "projections";

			var againstProduction = await ResolveIdentityAsync<SalesProjection>(
				CreateDb(Project), Collection).ConfigureAwait(false);
			var againstEmulator = await ResolveIdentityAsync<SalesProjection>(
				CreateEmulatorDb(Project), Collection).ConfigureAwait(false);

			againstEmulator.Identity.ShouldBe(
				againstProduction.Identity,
				"the emulator is selected by the channel the client is built on and never appears in a " +
				"Firestore resource path, so the same logical target must fingerprint identically in both " +
				"environments. An identity that baked in the endpoint would satisfy every safety arm above " +
				"and then refuse every binding made in another environment");
		}

		private static string Expected(string project, string database, string collection, string segment) =>
			$"firestore/projects/{project}/databases/{database}/documents/{collection}/{segment}/items";

		/// <summary>
		/// Registers BOTH same-named projections in ONE container and resolves each store's identity.
		/// </summary>
		private static async Task<(IProjectionDestinationIdentity Sales, IProjectionDestinationIdentity Support)>
			ResolveBothIdentitiesAsync(FirestoreDb db, string salesCollection, string supportCollection)
		{
			var services = new ServiceCollection();
			services.AddLogging();
			services.AddSingleton(db);
			services.AddFirestoreProjectionStore<SalesProjection>(o => o.CollectionName = salesCollection);
			services.AddFirestoreProjectionStore<SupportProjection>(o => o.CollectionName = supportCollection);

			await using var provider = services.BuildServiceProvider();
			await using var scope = provider.CreateAsyncScope();

			return (
				Identity(scope.ServiceProvider.GetRequiredService<IProjectionStore<SalesProjection>>()),
				Identity(scope.ServiceProvider.GetRequiredService<IProjectionStore<SupportProjection>>()));

			static IProjectionDestinationIdentity Identity<TProjection>(IProjectionStore<TProjection> store)
				where TProjection : class =>
				(IProjectionDestinationIdentity)store.GetService(typeof(IProjectionDestinationIdentity))!;
		}

		private static async Task<IProjectionDestinationIdentity> ResolveIdentityAsync<TProjection>(
			FirestoreDb db, string collectionName)
			where TProjection : class
		{
			var services = new ServiceCollection();
			services.AddLogging();
			services.AddSingleton(db);
			services.AddFirestoreProjectionStore<TProjection>(options => options.CollectionName = collectionName);

			await using var provider = services.BuildServiceProvider();
			await using var scope = provider.CreateAsyncScope();
			var store = scope.ServiceProvider.GetRequiredService<IProjectionStore<TProjection>>();

			return (IProjectionDestinationIdentity)store.GetService(typeof(IProjectionDestinationIdentity))!;
		}

		/// <summary>
		/// Builds a client against a faked gRPC stub. No server is contacted: a resource path is pure string
		/// composition over the project and database ids, and the store's identity performs no RPC.
		/// </summary>
		private static FirestoreDb CreateDb(string projectId) =>
			FirestoreDb.Create(projectId, A.Fake<FirestoreClient>());

		/// <summary>
		/// Builds a client for a non-default database. The SDK keeps its client factory internal, so the
		/// database id can only be set through the builder, and the builder needs a channel target — the
		/// emulator host below supplies one. The address is never dialled.
		/// </summary>
		private static FirestoreDb CreateDb(string projectId, string databaseId) =>
			BuildWithEmulatorHost(new FirestoreDbBuilder
			{
				ProjectId = projectId,
				DatabaseId = databaseId,
				EmulatorDetection = EmulatorDetection.EmulatorOnly,
			});

		private static FirestoreDb CreateEmulatorDb(string projectId) =>
			BuildWithEmulatorHost(new FirestoreDbBuilder
			{
				ProjectId = projectId,
				EmulatorDetection = EmulatorDetection.EmulatorOnly,
			});

		private static FirestoreDb BuildWithEmulatorHost(FirestoreDbBuilder builder)
		{
			var previous = Environment.GetEnvironmentVariable("FIRESTORE_EMULATOR_HOST");
			Environment.SetEnvironmentVariable("FIRESTORE_EMULATOR_HOST", "127.0.0.1:65123");
			try
			{
				return builder.Build();
			}
			finally
			{
				Environment.SetEnvironmentVariable("FIRESTORE_EMULATOR_HOST", previous);
			}
		}
	}
}
